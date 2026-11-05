using APIViewModel.Account;
using APIViewModel.AccountProfile;
using APIViewModel.Auth;
using Services.EmailVerificationService;
using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Common;
using APIViewModel.Member;
using APIViewModel.Receptionist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AccountService;
using Services.CoachService;
using Services.ReceptionistService;
using SportsCenterManagement.Filter;
using System.Security.Claims;

namespace SportsCenterManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _account;
        private readonly IEmailVerificationService _emailVerification;
        private readonly ICoachService _coachService;
        private readonly IReceptionistService _receptionistService;

        public AccountController(
            IAccountService account,
            ICoachService coachService,
            IReceptionistService receptionistService,
            IEmailVerificationService emailVerification)
        {
            _account = account;
            _emailVerification = emailVerification;
            _coachService = coachService;
            _receptionistService = receptionistService;
        }

        [Authorize(Roles = "CenterManager")]
        [HttpPost("Create_center_manager")]
        public async Task<IActionResult> CreateCenterManagerAsync(CreateCenterManagerAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                bool isCreated = await _account.CreateCenterManagerAsync(info);

                if (isCreated)
                {
                    return Ok("Tạo tài khoản quản lý trung tâm thành công.");
                }
                else
                {
                    return StatusCode(500);
                }
            }
            else
            {
                string allErrors = string.Join(
                    "\n",
                    ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                return BadRequest(allErrors);
            }
        }

        [Authorize(Roles = "CenterManager")]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPost("Create_coach")]
        public async Task<IActionResult> CreateCoachAsync(CreateCoachAPIViewModel info)
        {
            string? actorAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actorAccountId))
            {
                return Unauthorized();
            }

            CreateManagedCoachAPIViewModel request = new CreateManagedCoachAPIViewModel
            {
                Email = info.Email,
                Password = info.Password,
                FullName = info.FullName,
                Phone = info.Phone,
                WorkSchedule = info.WorkSchedule,
                DisciplineIds = info.DisciplineIds
            };
            (CreateCoachResult Result, CoachDetailAPIViewModel? Data) result =
                await _coachService.CreateCoachAsync(actorAccountId, request);
            return result.Result switch
            {
                CreateCoachResult.Success when result.Data is not null =>
                    StatusCode(StatusCodes.Status201Created, result.Data),
                CreateCoachResult.InvalidData => BadRequest("Dữ liệu huấn luyện viên không hợp lệ."),
                CreateCoachResult.DuplicateEmail => Conflict("Email already exists."),
                CreateCoachResult.DuplicatePhone => Conflict("Phone number already exists."),
                CreateCoachResult.DisciplineNotFoundOrInactive =>
                    BadRequest("A discipline was not found or is inactive."),
                CreateCoachResult.ConcurrencyConflict => Conflict(),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        [Authorize(Roles = "CenterManager,Receptionist")]
        [HttpPost("Create_member")]
        public async Task<IActionResult> CreateMemberAsync(CreateMemberAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                RegisterMemberResponseAPIViewModel? createdMember =
                    await _account.CreateMemberAsync(info);

                if (createdMember is not null)
                {
                    return Ok(createdMember);
                }
                else
                {
                    return StatusCode(500);
                }
            }
            else
            {
                string allErrors = string.Join(
                    "\n",
                    ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                return BadRequest(allErrors);
            }
        }

        [Authorize(Roles = "CenterManager")]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPost("Create_receptionist")]
        public async Task<IActionResult> CreateReceptionistAsync(CreateReceptionistAPIViewModel info)
        {
            string? actorAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actorAccountId))
            {
                return Unauthorized();
            }

            (CreateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data) result =
                await _receptionistService.CreateReceptionistAsync(actorAccountId, info);
            return result.Result switch
            {
                CreateReceptionistResult.Success when result.Data is not null =>
                    StatusCode(StatusCodes.Status201Created, result.Data),
                CreateReceptionistResult.InvalidData => BadRequest("Dữ liệu lễ tân không hợp lệ."),
                CreateReceptionistResult.DuplicateEmail => Conflict("Email already exists."),
                CreateReceptionistResult.DuplicatePhone => Conflict("Phone number already exists."),
                CreateReceptionistResult.ReceptionistRoleMissing =>
                    StatusCode(StatusCodes.Status500InternalServerError),
                CreateReceptionistResult.ConcurrencyConflict =>
                    Conflict("Xung đột dữ liệu khi xử lý đồng thời. Vui lòng thử lại."),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        [AllowAnonymous]
        [HttpPost("request-register-email-verification")]
        public async Task<IActionResult> RequestRegisterEmailVerificationAsync(
            RequestRegistrationEmailVerificationAPIViewModel request)
        {
            string normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (await _account.IsEmailExistsAsync(normalizedEmail))
            {
                return Conflict(CreateError(
                    "EMAIL_ALREADY_EXISTS",
                    "Email này đã được đăng ký."));
            }

            (RequestEmailVerificationResult result, int retryAfterSeconds, string? demoCode) =
                await _emailVerification.RequestCodeAsync(normalizedEmail, "REGISTER");
            return result switch
            {
                RequestEmailVerificationResult.Success => Ok(
                    new EmailVerificationOtpResponseAPIViewModel
                    {
                        Message = "Mã xác thực đã được gửi tới " + normalizedEmail + ".",
                        ExpiresInSeconds = 300,
                        CooldownSeconds = 60,
                        DemoCode = demoCode
                    }),
                RequestEmailVerificationResult.CooldownActive => StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    CreateError(
                        "OTP_COOLDOWN_ACTIVE",
                        "Vui lòng đợi hết thời gian chờ trước khi yêu cầu mã xác thực mới.",
                        new Dictionary<string, int>
                        {
                            ["retryAfterSeconds"] = retryAfterSeconds
                        })),
                RequestEmailVerificationResult.DeliveryFailed => StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateError(
                        "OTP_DELIVERY_FAILED",
                        "Không thể gửi mã xác thực qua email. Vui lòng thử lại.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "Đã xảy ra lỗi hệ thống ngoài dự kiến."))
            };
        }

        [AllowAnonymous]
        [HttpPost("Register_member")]
        public async Task<IActionResult> RegisterMemberAsync(RegisterMemberRequestAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                if (await _account.IsEmailExistsAsync(info.Email))
                {
                    return Conflict(CreateError(
                        "EMAIL_ALREADY_EXISTS",
                        "Email này đã được đăng ký."));
                }

                VerifyEmailCodeResult verification = _emailVerification.VerifyCode(
                    info.Email,
                    "REGISTER",
                    info.EmailVerificationCode,
                    consumeOnSuccess: false);
                if (verification != VerifyEmailCodeResult.Success)
                {
                    return CreateEmailVerificationError(verification);
                }

                RegisterMemberResponseAPIViewModel? result = await _account.RegisterMemberAsync(info);

                if (result is not null)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500);
                }
            }
            else
            {
                string allErrors = string.Join(
                    "\n",
                    ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                return BadRequest(allErrors);
            }
        }

        [Authorize]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfileAsync(
            [FromBody] UpdateProfileAPIViewModel request)
        {
            string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return Unauthorized(CreateError(
                    "INVALID_TOKEN_CLAIMS",
                    "Phiên đăng nhập không hợp lệ."));
            }

            UpdateCurrentProfileResult result =
                await _account.UpdateCurrentProfileAsync(accountId, request);

            return result switch
            {
                UpdateCurrentProfileResult.Success => Ok(),
                UpdateCurrentProfileResult.AccountNotFound => Unauthorized(CreateError(
                    "ACCOUNT_NOT_FOUND",
                    "Tài khoản đang đăng nhập không còn tồn tại.")),
                UpdateCurrentProfileResult.ProfileNotFound => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError(
                        "PROFILE_DATA_INTEGRITY_ERROR",
                        "Hồ sơ tài khoản chưa được cấu hình đúng.")),
                UpdateCurrentProfileResult.UnsupportedRole => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError(
                        "UNSUPPORTED_ACCOUNT_ROLE",
                        "Vai trò tài khoản không được hỗ trợ.")),
                UpdateCurrentProfileResult.InvalidData => BadRequest(CreateError(
                    "INVALID_PROFILE_DATA",
                    "Dữ liệu hồ sơ không hợp lệ.")),
                UpdateCurrentProfileResult.InvalidFieldForRole => BadRequest(CreateError(
                    "INVALID_FIELD_FOR_ROLE",
                    "Một hoặc nhiều trường thông tin không áp dụng cho vai trò tài khoản này.")),
                UpdateCurrentProfileResult.DuplicatePhone => Conflict(CreateError(
                    "DUPLICATE_PHONE",
                    "Số điện thoại này đã được sử dụng.")),
                UpdateCurrentProfileResult.NoChanges => BadRequest(CreateError(
                    "NO_CHANGES_PROVIDED",
                    "Không có thay đổi nào được cung cấp.")),
                UpdateCurrentProfileResult.ConcurrencyConflict => Conflict(CreateError(
                    "CONCURRENCY_CONFLICT",
                    "Phát hiện xung đột dữ liệu đồng thời. Vui lòng thử lại.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "Đã xảy ra lỗi hệ thống ngoài dự kiến."))
            };
        }

        [Authorize(Roles = "CenterManager")]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPost("{accountId}/unlock")]
        public async Task<IActionResult> UnlockAccountAsync([FromRoute] string accountId)
        {
            UnlockAccountResult result = await _account.UnlockAccountAsync(accountId);
            return result switch
            {
                UnlockAccountResult.Success => Ok(),
                UnlockAccountResult.NotFound => NotFound(CreateError(
                    "ACCOUNT_NOT_FOUND",
                    "Không tìm thấy tài khoản.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "Đã xảy ra lỗi hệ thống ngoài dự kiến."))
            };
        }

        [Authorize(Roles = "CenterManager")]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPatch("{accountId}/status")]
        public async Task<IActionResult> UpdateAccountStatusAsync(
            [FromRoute] string accountId,
            [FromBody] AccountStatusUpdateAPIViewModel request)
        {
            string? actorAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actorAccountId))
            {
                return Unauthorized(CreateError(
                    "INVALID_TOKEN_CLAIMS",
                    "Phiên đăng nhập không hợp lệ."));
            }

            UpdateAccountStatusResult result = await _account.UpdateAccountStatusAsync(
                actorAccountId,
                accountId,
                request.Status);

            return result switch
            {
                UpdateAccountStatusResult.Success => Ok(),
                UpdateAccountStatusResult.NotFound => NotFound(CreateError(
                    "ACCOUNT_NOT_FOUND",
                    "Không tìm thấy tài khoản.")),
                UpdateAccountStatusResult.InvalidStatus => BadRequest(CreateError(
                    "INVALID_ACCOUNT_STATUS",
                    "Trạng thái tài khoản chỉ được là Active hoặc Inactive.")),
                UpdateAccountStatusResult.SelfDeactivationNotAllowed => Conflict(CreateError(
                    "SELF_DEACTIVATION_NOT_ALLOWED",
                    "Quản lý trung tâm không thể tự ngừng hoạt động tài khoản của chính mình.")),
                UpdateAccountStatusResult.LastActiveCenterManager => Conflict(CreateError(
                    "LAST_ACTIVE_CENTER_MANAGER",
                    "Không thể ngừng hoạt động quản lý trung tâm đang hoạt động cuối cùng.")),
                UpdateAccountStatusResult.CoachHasUpcomingSessions => Conflict(CreateError(
                    "COACH_HAS_UPCOMING_SESSIONS",
                    "Không thể ngừng hoạt động tài khoản huấn luyện viên đang có lịch dạy trong tương lai.")),
                UpdateAccountStatusResult.ConcurrencyConflict => Conflict(CreateError(
                    "CONCURRENCY_CONFLICT",
                    "Phát hiện xung đột dữ liệu đồng thời. Vui lòng thử lại.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "Đã xảy ra lỗi hệ thống ngoài dự kiến."))
            };
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfileAsync()
        {
            string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return Unauthorized();
            }

            AccountProfileAPIViewModel? profile =
                await _account.GetProfileAsync(accountId);
            return profile is null ? NotFound("Không tìm thấy hồ sơ tài khoản.") : Ok(profile);
        }

        private IActionResult CreateEmailVerificationError(VerifyEmailCodeResult result)
        {
            return result switch
            {
                VerifyEmailCodeResult.CodeRequired => BadRequest(CreateError(
                    "EMAIL_VERIFICATION_REQUIRED",
                    "Vui lòng nhập mã xác thực email.")),
                VerifyEmailCodeResult.CodeNotFound => BadRequest(CreateError(
                    "EMAIL_VERIFICATION_NOT_FOUND",
                    "Chưa có mã xác thực email. Vui lòng yêu cầu gửi mã trước khi tiếp tục.")),
                VerifyEmailCodeResult.CodeExpired => BadRequest(CreateError(
                    "EMAIL_VERIFICATION_EXPIRED",
                    "Mã xác thực email đã hết hạn.")),
                VerifyEmailCodeResult.InvalidCode => BadRequest(CreateError(
                    "INVALID_EMAIL_VERIFICATION_CODE",
                    "Mã xác thực email không chính xác.")),
                VerifyEmailCodeResult.AttemptsExceeded => StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    CreateError(
                        "EMAIL_VERIFICATION_ATTEMPTS_EXCEEDED",
                        "Mã xác thực email không còn hiệu lực do nhập sai quá số lần cho phép.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "Đã xảy ra lỗi hệ thống ngoài dự kiến."))
            };
        }

        private ApiErrorResponseAPIViewModel CreateError(
            string code,
            string message,
            object? details = null)
        {
            return new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = code,
                    Message = message,
                    Details = details
                },
                TraceId = HttpContext.TraceIdentifier
            };
        }
    }
}
