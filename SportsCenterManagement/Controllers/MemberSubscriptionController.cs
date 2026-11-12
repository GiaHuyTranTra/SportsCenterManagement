using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;
using APIViewModel.MemberSubscription;
using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.MemberSubscriptionService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemberSubscriptionController : ControllerBase
{
    private readonly IMemberSubscriptionService _subscriptionService;

    public MemberSubscriptionController(IMemberSubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
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
                return StatusCode(StatusCodes.Status201Created, data);
            case RegisterSubscriptionResult.InvalidPackageId:
                return BadRequest("Mã gói tập không hợp lệ.");
            case RegisterSubscriptionResult.InvalidPaymentMethod:
                return BadRequest("Phương thức thanh toán không hợp lệ. Chỉ hỗ trợ: CASH, BANK_TRANSFER, CARD.");
            case RegisterSubscriptionResult.MemberNotFound:
                return NotFound("Không tìm thấy tài khoản hội viên.");
            case RegisterSubscriptionResult.MemberInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Tài khoản hội viên đang ngừng hoạt động.");
            case RegisterSubscriptionResult.MemberLocked:
                return StatusCode(StatusCodes.Status423Locked, "Tài khoản hội viên đang bị khóa.");
            case RegisterSubscriptionResult.PackageNotFound:
                return NotFound("Không tìm thấy gói tập.");
            case RegisterSubscriptionResult.PackageInactive:
                return Conflict("Gói tập hiện đang ngừng kinh doanh.");
            case RegisterSubscriptionResult.PendingOrderExists:
                return Conflict("Bạn đang có một hóa đơn chờ thanh toán. Vui lòng hoàn tất hoặc hủy hóa đơn đó trước.");
            case RegisterSubscriptionResult.SubscriptionSuspended:
                return Conflict("Gói tập hiện tại của bạn đang tạm ngưng (bảo lưu). Vui lòng liên hệ quầy lễ tân mở lại gói trước khi gia hạn hoặc mua gói mới.");
            case RegisterSubscriptionResult.UpcomingSubscriptionExists:
                return Conflict("Bạn đã có sẵn một gói tập nối tiếp đang chờ kích hoạt trong tương lai, không thể mua chồng thêm gói mới.");
            case RegisterSubscriptionResult.TermsAcceptanceRequired:
                return TermsAcceptanceRequired();

            case RegisterSubscriptionResult.ConcurrencyConflict:
                return Conflict("Dữ liệu vừa được thay đổi bởi giao dịch khác. Vui lòng thử lại.");
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
                return StatusCode(StatusCodes.Status201Created, receipt);
            case CounterRegisterResult.InvalidPackageId:
            case CounterRegisterResult.InvalidMemberAccountId:
                return BadRequest("Dữ liệu yêu cầu không hợp lệ.");
            case CounterRegisterResult.InvalidPaymentMethod:
                return BadRequest("Phương thức thanh toán không hợp lệ. Chỉ hỗ trợ: CASH, BANK_TRANSFER, CARD.");
            case CounterRegisterResult.StaffNotFound:
                return NotFound("Không tìm thấy tài khoản nhân viên.");
            case CounterRegisterResult.MemberNotFound:
                return NotFound("Không tìm thấy tài khoản hội viên.");
            case CounterRegisterResult.PackageNotFound:
                return NotFound("Không tìm thấy gói tập.");
            case CounterRegisterResult.StaffRoleNotAllowed:
                return StatusCode(StatusCodes.Status403Forbidden, "Tài khoản nhân viên không có quyền thực hiện thao tác này.");
            case CounterRegisterResult.StaffInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Tài khoản nhân viên đang ngừng hoạt động.");
            case CounterRegisterResult.MemberInactive:
                return StatusCode(StatusCodes.Status403Forbidden, "Tài khoản hội viên đang ngừng hoạt động.");
            case CounterRegisterResult.StaffLocked:
                return StatusCode(StatusCodes.Status423Locked, "Tài khoản nhân viên đang bị khóa.");
            case CounterRegisterResult.MemberLocked:
                return StatusCode(StatusCodes.Status423Locked, "Tài khoản hội viên đang bị khóa.");
            case CounterRegisterResult.PackageInactive:
                return Conflict("Gói tập hiện đang ngừng kinh doanh.");
            case CounterRegisterResult.PendingOrderExists when pendingInfo is not null:
                return Conflict(pendingInfo);
            case CounterRegisterResult.PendingOrderExists:
                return Conflict("Hội viên đang có hóa đơn chờ thanh toán.");
            case CounterRegisterResult.SubscriptionSuspended:
                return Conflict("Gói tập hiện tại của hội viên đang tạm ngưng (bảo lưu). Vui lòng kích hoạt mở lại gói trước khi gia hạn.");
            case CounterRegisterResult.UpcomingSubscriptionExists:
                return Conflict("Hội viên đã có sẵn một gói tập nối tiếp đang chờ bắt đầu, không thể gia hạn chồng thêm kỳ mới.");
            case CounterRegisterResult.TermsAcceptanceRequired:
                return TermsAcceptanceRequired();

            case CounterRegisterResult.InvoiceNumberCollision:
                return Conflict("Xung đột mã hóa đơn khi xử lý đồng thời. Vui lòng thử lại.");
            case CounterRegisterResult.ConcurrencyConflict:
                return Conflict("Dữ liệu vừa được thay đổi bởi giao dịch khác. Vui lòng thử lại.");
            case CounterRegisterResult.DataIntegrityViolation:
                return StatusCode(StatusCodes.Status500InternalServerError, "Dữ liệu hóa đơn hoặc gói tập không đồng nhất.");
            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private ObjectResult TermsAcceptanceRequired()
    {
        return StatusCode(StatusCodes.Status409Conflict, new ApiErrorResponseAPIViewModel
        {
            Error = new ApiErrorAPIViewModel
            {
                Code = "TERMS_ACCEPTANCE_REQUIRED",
                Message = "Học viên phải tự đăng nhập, đọc và chấp nhận điều khoản hiện hành trước khi đăng ký gói."
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
