using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;
using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.MembershipInvoiceService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Receptionist,CenterManager,Member")]
public class MembershipInvoiceController : ControllerBase
{
    private readonly IMembershipInvoiceService _invoiceService;

    public MembershipInvoiceController(IMembershipInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoicesAsync(
        [FromQuery] string? memberId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? paymentMethod = null)
    {
        string? staffAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(staffAccountId))
        {
            return Unauthorized();
        }

        List<MembershipReceiptAPIViewModel> invoices =
            await _invoiceService.GetInvoicesAsync(staffAccountId, memberId, status, search, paymentMethod);

        return Ok(invoices);
    }

    [HttpGet("{invoiceId:int}/bank-transfer")]
    public async Task<IActionResult> GetBankTransferAsync(int invoiceId)
    {
        string? callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(callerId)) return InvoiceQueryError(401, "UNAUTHORIZED", "Account could not be identified.");
        return Ok(await _invoiceService.GetBankTransferInstructionsAsync(invoiceId, callerId));
    }

    [HttpPost("{invoiceId:int}/confirm-bank-transfer")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> ConfirmBankTransferAsync(int invoiceId, [FromBody] ConfirmBankTransferAPIViewModel request)
    {
        string? callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(callerId)) return InvoiceQueryError(401, "UNAUTHORIZED", "Account could not be identified.");
        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) = await _invoiceService.ConfirmBankTransferAsync(invoiceId, callerId, request);
        return PaymentResponse(result, receipt, true);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchInvoicesAsync(
        [FromQuery] string? memberId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? paymentMethod = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        string? callerAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(callerAccountId))
        {
            return InvoiceQueryError(401, "UNAUTHORIZED", "The account could not be identified.");
        }

        string? normalizedStatus = status?.Trim().ToUpperInvariant();
        string? normalizedMethod = paymentMethod?.Trim().ToUpperInvariant();
        if (page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize ||
            (!string.IsNullOrEmpty(normalizedStatus) &&
             normalizedStatus != "PENDING_PAYMENT" && normalizedStatus != "PAID" && normalizedStatus != "CANCELED") ||
            (!string.IsNullOrEmpty(normalizedMethod) &&
             normalizedMethod != "CASH" && normalizedMethod != "CARD" && normalizedMethod != "BANK_TRANSFER") ||
            (fromDate.HasValue && toDate.HasValue && fromDate > toDate))
        {
            return InvoiceQueryError(400, "INVALID_INVOICE_FILTER", "Invalid invoice filters or pagination.");
        }

        PagedMembershipInvoiceAPIViewModel result = await _invoiceService.SearchInvoicesAsync(
            callerAccountId, memberId, normalizedStatus, search, normalizedMethod, page, pageSize,
            fromDate, toDate);
        return Ok(result);
    }

    private ObjectResult InvoiceQueryError(int statusCode, string code, string message)
    {
        return StatusCode(statusCode, new ApiErrorResponseAPIViewModel
        {
            Error = new ApiErrorAPIViewModel { Code = code, Message = message },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("{invoiceId:int}/pay")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    [ProducesResponseType(typeof(MembershipReceiptAPIViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponseAPIViewModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponseAPIViewModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponseAPIViewModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponseAPIViewModel), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponseAPIViewModel), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ApiErrorResponseAPIViewModel), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PayInvoiceAsync(
        [FromRoute] int invoiceId,
        [FromBody] PayMembershipInvoiceAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string? staffAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(staffAccountId))
        {
            return InvoiceQueryError(401, "UNAUTHORIZED", "The account could not be identified.");
        }

        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) =
            await _invoiceService.PayInvoiceAsync(invoiceId, staffAccountId, request);

        return PaymentResponse(result, receipt, false);
    }

    private IActionResult PaymentResponse(PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt, bool bankTransfer)
    {
        return result switch
        {
            PayInvoiceResult.Success when receipt is not null => Ok(receipt),
            PayInvoiceResult.InvalidPaymentMethod => InvoiceQueryError(400, bankTransfer ? "BANK_TRANSFER_ONLY" : "CASH_ONLY", "Invoice method must match the confirmation endpoint."),
            PayInvoiceResult.InvalidConfirmation => InvoiceQueryError(400, bankTransfer ? "INVALID_TRANSFER_CONFIRMATION" : "INVALID_CASH_CONFIRMATION", "Confirm receipt of funds and supply a positive whole-VND amount."),
            PayInvoiceResult.AmountMismatch => InvoiceQueryError(409, "PAYMENT_AMOUNT_MISMATCH", "The confirmed amount does not match the invoice. Reload the invoice."),
            PayInvoiceResult.PackageChanged => InvoiceQueryError(409, "PACKAGE_CHANGED", "The package price or duration changed. Cancel the pending order and create a new one."),
            PayInvoiceResult.QuoteExpired => InvoiceQueryError(409, "UPGRADE_QUOTE_EXPIRED", "The upgrade quote has expired. Create a new order."),
            PayInvoiceResult.UnsupportedOrderKind => InvoiceQueryError(409, "ORDER_KIND_NOT_SUPPORTED", "This order requires a verified upgrade or change-package quote before collection."),
            PayInvoiceResult.InvalidInvoiceState => InvoiceQueryError(409, "INVALID_INVOICE_STATE", "Invoice must be PENDING_PAYMENT."),
            PayInvoiceResult.InvalidSubscriptionState => InvoiceQueryError(409, "INVALID_SUBSCRIPTION_STATE", "The pending subscription is not eligible for confirmation."),
            PayInvoiceResult.InvoiceNotFound => InvoiceQueryError(404, "INVOICE_NOT_FOUND", "Membership invoice not found."),
            PayInvoiceResult.MemberNotFound => InvoiceQueryError(404, "MEMBER_NOT_FOUND", "Member account not found."),
            PayInvoiceResult.PackageNotFound => InvoiceQueryError(404, "PACKAGE_NOT_FOUND", "Membership package not found."),
            PayInvoiceResult.StaffNotFound => InvoiceQueryError(403, "FORBIDDEN", "Staff account could not be verified."),
            PayInvoiceResult.StaffRoleNotAllowed => InvoiceQueryError(403, "FORBIDDEN", "Staff account does not have required permissions."),
            PayInvoiceResult.StaffInactive or PayInvoiceResult.MemberInactive => InvoiceQueryError(403, "ACCOUNT_INACTIVE", "The account is inactive or deleted."),
            PayInvoiceResult.StaffLocked or PayInvoiceResult.MemberLocked => InvoiceQueryError(423, "ACCOUNT_LOCKED", "The account is locked."),
            PayInvoiceResult.PackageInactive => InvoiceQueryError(409, "PACKAGE_INACTIVE", "Membership package is currently inactive."),
            PayInvoiceResult.AlreadyPaid => InvoiceQueryError(409, "INVOICE_ALREADY_PAID", "Invoice has already been paid. Retrieve its receipt; do not collect cash again."),
            PayInvoiceResult.ConcurrencyConflict => InvoiceQueryError(409, "PAYMENT_CONCURRENCY_CONFLICT", "Collection conflicted with another transaction. Reload the invoice and receipt."),
            PayInvoiceResult.DataIntegrityViolation => InvoiceQueryError(500, "DATA_INTEGRITY_ERROR", "Invoice or subscription data is inconsistent."),
            _ => InvoiceQueryError(500, "INTERNAL_ERROR", "Payment could not be confirmed.")
        };
    }

    [HttpGet("{invoiceId:int}/receipt")]
    public async Task<IActionResult> GetReceiptAsync([FromRoute] int invoiceId)
    {
        string? staffAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(staffAccountId))
        {
            return Unauthorized();
        }

        (GetReceiptResult result, MembershipReceiptAPIViewModel? receipt) =
            await _invoiceService.GetReceiptAsync(invoiceId, staffAccountId);

        switch (result)
        {
            case GetReceiptResult.Success when receipt is not null:
                return Ok(receipt);
            case GetReceiptResult.ReceiptNotAvailable:
                return InvoiceQueryError(400, "RECEIPT_NOT_AVAILABLE", "Receipt is only available for PAID invoices.");
            case GetReceiptResult.InvoiceNotFound:
                return InvoiceQueryError(404, "INVOICE_NOT_FOUND", "Membership invoice not found.");
            case GetReceiptResult.StaffNotFound:
                return InvoiceQueryError(404, "ACCOUNT_NOT_FOUND", "Account not found.");
            case GetReceiptResult.StaffRoleNotAllowed:
                return InvoiceQueryError(403, "FORBIDDEN", "Account does not have required permissions.");
            case GetReceiptResult.StaffInactive:
                return InvoiceQueryError(403, "ACCOUNT_INACTIVE", "Account is inactive.");
            case GetReceiptResult.StaffLocked:
                return InvoiceQueryError(423, "ACCOUNT_LOCKED", "Account is locked.");
            case GetReceiptResult.DataIntegrityViolation:
                return InvoiceQueryError(500, "DATA_INTEGRITY_ERROR", "Receipt data is inconsistent.");
            default:
                return InvoiceQueryError(500, "INTERNAL_ERROR", "Receipt could not be retrieved.");
        }
    }

    [HttpPost("{invoiceId:int}/cancel")]
    public async Task<IActionResult> CancelInvoiceAsync([FromRoute] int invoiceId)
    {
        string? callerAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(callerAccountId))
        {
            return Unauthorized();
        }

        CancelInvoiceResult result =
            await _invoiceService.CancelPendingInvoiceAsync(invoiceId, callerAccountId);

        switch (result)
        {
            case CancelInvoiceResult.Success:
                return Ok(new { message = "Membership invoice and pending order have been canceled." });
            case CancelInvoiceResult.InvoiceNotFound:
                return NotFound("Membership invoice not found.");
            case CancelInvoiceResult.InvalidInvoiceState:
                return BadRequest("Only invoices in PENDING_PAYMENT state can be canceled.");
            case CancelInvoiceResult.ConfirmedSubscriptionCannotBeCanceled:
                return InvoiceQueryError(
                    StatusCodes.Status409Conflict,
                    "CONFIRMED_SUBSCRIPTION_CANNOT_BE_CANCELED",
                    "Gói đã thanh toán và xác nhận không thể hủy.");
            case CancelInvoiceResult.CallerNotFound:
                return NotFound("Account not found.");
            case CancelInvoiceResult.CallerRoleNotAllowed:
                return StatusCode(StatusCodes.Status403Forbidden, "Account does not have required permissions.");
            case CancelInvoiceResult.CallerInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Account is inactive.");
            case CancelInvoiceResult.CallerLocked:
                return StatusCode(StatusCodes.Status423Locked, "Account is locked.");
            case CancelInvoiceResult.ConcurrencyConflict:
                return Conflict("Data was modified by another transaction or deadlock occurred. Please retry.");
            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
