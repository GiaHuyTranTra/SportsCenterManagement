using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;
using APIViewModel.MemberSubscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.AuditLogService;
using Services.MemberSubscriptionService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(AuthFilter))]
public class MemberSubscriptionController : ControllerBase
{
    private readonly IMemberSubscriptionService _subscriptionService;
    private readonly IAuditLogService _auditLogService;

    public MemberSubscriptionController(
        IMemberSubscriptionService subscriptionService,
        IAuditLogService auditLogService)
    {
        _subscriptionService = subscriptionService;
        _auditLogService = auditLogService;
    }

    [HttpPost("register-or-renew")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> RegisterOrRenewAsync([FromBody] RegisterMemberSubscriptionAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized();
        }

        (RegisterSubscriptionResult result, MemberSubscriptionDetailAPIViewModel? data) =
            await _subscriptionService.RegisterOrRenewAsync(accountId, request);

        switch (result)
        {
            case RegisterSubscriptionResult.Success when data is not null:
                string action = data.Kind.Equals("REGISTER", System.StringComparison.OrdinalIgnoreCase) ? "CREATE" : "UPDATE";
                string kindLabel = data.Kind.Equals("REGISTER", System.StringComparison.OrdinalIgnoreCase) ? "Đăng ký" : "Gia hạn";
                await _auditLogService.RecordAsync(
                    accountId,
                    action,
                    "MEMBERSHIP_ORDER",
                    data.InvoiceId.ToString(),
                    $"{kindLabel} gói tập {data.PackageName} (#{data.InvoiceNumber}, {data.Amount:N0} VND).");
                return StatusCode(StatusCodes.Status201Created, data);
            case RegisterSubscriptionResult.InvalidPackageId:
                return BadRequest("Invalid membership package id.");
            case RegisterSubscriptionResult.InvalidPaymentMethod:
                return BadRequest("Invalid payment method. Allowed methods: CASH, BANK_TRANSFER, CARD.");
            case RegisterSubscriptionResult.MemberNotFound:
                return NotFound("Member account not found.");
            case RegisterSubscriptionResult.MemberInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Member account is inactive.");
            case RegisterSubscriptionResult.MemberLocked:
                return StatusCode(StatusCodes.Status423Locked, "Member account is locked.");
            case RegisterSubscriptionResult.PackageNotFound:
                return NotFound("Membership package not found.");
            case RegisterSubscriptionResult.PackageInactive:
                return Conflict("Membership package is currently inactive.");
            case RegisterSubscriptionResult.PendingOrderExists:
                return Conflict("You already have a pending order awaiting payment.");
            case RegisterSubscriptionResult.ConcurrencyConflict:
                return Conflict("Data was modified by another transaction or deadlock occurred. Please retry.");
            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("counter-register-or-renew")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> CounterRegisterOrRenewAsync([FromBody] CounterRegisterSubscriptionAPIViewModel request)
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

        (CounterRegisterResult result, MembershipReceiptAPIViewModel? receipt, PendingOrderConflictResponseAPIViewModel? pendingInfo) =
            await _subscriptionService.CounterRegisterOrRenewAsync(staffAccountId, request);

        switch (result)
        {
            case CounterRegisterResult.Success when receipt is not null:
                string counterAction = receipt.Kind.Equals("REGISTER", System.StringComparison.OrdinalIgnoreCase) ? "CREATE" : "UPDATE";
                string counterKindLabel = receipt.Kind.Equals("REGISTER", System.StringComparison.OrdinalIgnoreCase) ? "Đăng ký" : "Gia hạn";
                await _auditLogService.RecordAsync(
                    staffAccountId,
                    counterAction,
                    "MEMBERSHIP_ORDER",
                    receipt.InvoiceId.ToString(),
                    $"{counterKindLabel} gói tập {receipt.PackageName} tại quầy cho hội viên {receipt.MemberFullName ?? receipt.MemberCode} (#{receipt.InvoiceNumber}, {receipt.Amount:N0} VND).");
                await _auditLogService.RecordAsync(
                    staffAccountId,
                    "CONFIRM_PAYMENT",
                    "INVOICE",
                    receipt.InvoiceId.ToString(),
                    $"Xác nhận thanh toán tại quầy hóa đơn #{receipt.InvoiceNumber} ({receipt.Amount:N0} VND, {receipt.PaymentMethod}) cho gói {receipt.PackageName}.");
                return StatusCode(StatusCodes.Status201Created, receipt);
            case CounterRegisterResult.InvalidPackageId:
            case CounterRegisterResult.InvalidMemberAccountId:
                return BadRequest("Invalid request data.");
            case CounterRegisterResult.InvalidPaymentMethod:
                return BadRequest("Invalid payment method. Allowed methods: CASH, BANK_TRANSFER, CARD.");
            case CounterRegisterResult.StaffNotFound:
                return NotFound("Staff account not found.");
            case CounterRegisterResult.MemberNotFound:
                return NotFound("Member account not found.");
            case CounterRegisterResult.PackageNotFound:
                return NotFound("Membership package not found.");
            case CounterRegisterResult.StaffRoleNotAllowed:
                return StatusCode(StatusCodes.Status403Forbidden, "Staff account does not have required permissions.");
            case CounterRegisterResult.StaffInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Staff account is inactive.");
            case CounterRegisterResult.MemberInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Member account is inactive.");
            case CounterRegisterResult.StaffLocked:
                return StatusCode(StatusCodes.Status423Locked, "Staff account is locked.");
            case CounterRegisterResult.MemberLocked:
                return StatusCode(StatusCodes.Status423Locked, "Member account is locked.");
            case CounterRegisterResult.PackageInactive:
                return Conflict("Membership package is currently inactive.");
            case CounterRegisterResult.PendingOrderExists when pendingInfo is not null:
                return Conflict(pendingInfo);
            case CounterRegisterResult.PendingOrderExists:
                return Conflict("Member already has a pending order awaiting payment.");
            case CounterRegisterResult.InvoiceNumberCollision:
                return Conflict("Invoice number collision occurred. Please retry.");
            case CounterRegisterResult.ConcurrencyConflict:
                return Conflict("Data was modified by another transaction or deadlock occurred. Please retry.");
            case CounterRegisterResult.DataIntegrityViolation:
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal data integrity error.");
            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
