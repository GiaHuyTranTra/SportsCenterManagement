using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.MembershipInvoiceService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Receptionist,CenterManager,Member")]
[TypeFilter(typeof(AuthFilter))]
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

    [HttpPost("{invoiceId:int}/pay")]
    [Authorize(Roles = "Receptionist,CenterManager")]
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
            return Unauthorized();
        }

        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) =
            await _invoiceService.PayInvoiceAsync(invoiceId, staffAccountId, request);

        switch (result)
        {
            case PayInvoiceResult.Success when receipt is not null:
                return Ok(receipt);
            case PayInvoiceResult.InvalidPaymentMethod:
                return BadRequest("Invalid payment method. Allowed methods: CASH, BANK_TRANSFER, CARD.");
            case PayInvoiceResult.InvalidInvoiceState:
            case PayInvoiceResult.InvalidSubscriptionState:
                return BadRequest("Invoice or subscription is not in PENDING_PAYMENT state.");
            case PayInvoiceResult.InvoiceNotFound:
                return NotFound("Membership invoice not found.");
            case PayInvoiceResult.MemberNotFound:
                return NotFound("Member account not found.");
            case PayInvoiceResult.PackageNotFound:
                return NotFound("Membership package not found.");
            case PayInvoiceResult.StaffNotFound:
                return NotFound("Staff account not found.");
            case PayInvoiceResult.StaffRoleNotAllowed:
                return StatusCode(StatusCodes.Status403Forbidden, "Staff account does not have required permissions.");
            case PayInvoiceResult.StaffInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Staff account is inactive.");
            case PayInvoiceResult.MemberInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Member account is inactive.");
            case PayInvoiceResult.StaffLocked:
                return StatusCode(StatusCodes.Status423Locked, "Staff account is locked.");
            case PayInvoiceResult.MemberLocked:
                return StatusCode(StatusCodes.Status423Locked, "Member account is locked.");
            case PayInvoiceResult.PackageInactive:
                return Conflict("Membership package is currently inactive.");
            case PayInvoiceResult.AlreadyPaid:
                return Conflict("Invoice has already been paid.");
            case PayInvoiceResult.ConcurrencyConflict:
                return Conflict("Data was modified by another transaction or deadlock occurred. Please retry.");
            case PayInvoiceResult.DataIntegrityViolation:
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal data integrity error.");
            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
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
                return BadRequest("Receipt is only available for PAID invoices.");
            case GetReceiptResult.InvoiceNotFound:
                return NotFound("Membership invoice not found.");
            case GetReceiptResult.StaffNotFound:
                return NotFound("Staff account not found.");
            case GetReceiptResult.StaffRoleNotAllowed:
                return StatusCode(StatusCodes.Status403Forbidden, "Staff account does not have required permissions.");
            case GetReceiptResult.StaffInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Staff account is inactive.");
            case GetReceiptResult.StaffLocked:
                return StatusCode(StatusCodes.Status423Locked, "Staff account is locked.");
            case GetReceiptResult.DataIntegrityViolation:
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal data integrity error.");
            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
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
