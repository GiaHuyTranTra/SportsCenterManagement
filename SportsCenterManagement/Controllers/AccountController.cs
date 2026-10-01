using APIViewModel.AccountProfile;
using APIViewModel.Auth;
using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Common;
using APIViewModel.Member;
using APIViewModel.Receptionist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AccountService;
using Services.AuditLogService;
using Services.EmailVerificationService;
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
        private readonly IAuditLogService _auditLogService;

        public AccountController(
            IAccountService account,
            IEmailVerificationService emailVerification,
            IAuditLogService auditLogService)
        {
            _account = account;
            _emailVerification = emailVerification;
            _auditLogService = auditLogService;
        }

        [Authorize]
        [TypeFilter(typeof(AuthFilter))]
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
            return profile is null ? NotFound("Account profile not found.") : Ok(profile);
        }

        [Authorize]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfileAsync(
            UpdateAccountProfileAPIViewModel request)
        {
            string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return Unauthorized();
            }

            AccountProfileAPIViewModel? profile =
                await _account.UpdateProfileAsync(accountId, request);

            if (profile is not null)
            {
                await _auditLogService.RecordAsync(
                    accountId,
                    "UPDATE_PROFILE",
                    "USER",
                    accountId,
                    $"Cập nhật hồ sơ tài khoản {profile.FullName}.");
                return Ok(profile);
            }

            return Conflict("Account profile could not be updated.");
        }

        [Authorize(Roles = "CenterManager")]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPost("Create_center_manager")]
        public async Task<IActionResult> CreateCenterManagerAsync(CreateCenterManagerAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                bool isCreated = await _account.CreateCenterManagerAsync(info);

                if (isCreated)
                {
                    string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    await _auditLogService.RecordAsync(
                        currentUserId,
                        "CREATE",
                        "USER",
                        null,
                        $"Tạo mới tài khoản quản lý trung tâm {info.FullName}.");
                    return Ok("Create center manager successful");
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
            if (ModelState.IsValid)
            {
                bool isCreated = await _account.CreateCoachAsync(info);

                if (isCreated)
                {
                    string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    await _auditLogService.RecordAsync(
                        currentUserId,
                        "CREATE",
                        "COACH",
                        null,
                        $"Tạo mới tài khoản huấn luyện viên {info.FullName}.");
                    return Ok("Create coach successful");
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

        [Authorize(Roles = "CenterManager,Receptionist")]
        [TypeFilter(typeof(AuthFilter))]
        [HttpPost("Create_member")]
        public async Task<IActionResult> CreateMemberAsync(CreateMemberAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                RegisterMemberResponseAPIViewModel? createdMember =
                    await _account.CreateMemberAsync(info);

                if (createdMember is not null)
                {
                    string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    await _auditLogService.RecordAsync(
                        currentUserId,
                        "CREATE",
                        "MEMBER",
                        createdMember.AccountId,
                        $"Tạo mới tài khoản thành viên {info.FullName}.");
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
            if (ModelState.IsValid)
            {
                bool isCreated = await _account.CreateReceptionistAsync(info);

                if (isCreated)
                {
                    string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    await _auditLogService.RecordAsync(
                        currentUserId,
                        "CREATE",
                        "RECEPTIONIST",
                        null,
                        $"Tạo mới tài khoản nhân viên lễ tân {info.FullName}.");
                    return Ok("Create receptionist successful");
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
                    "Email already exists."));
            }

            (RequestEmailVerificationResult result, int retryAfterSeconds, string? demoCode) =
                await _emailVerification.RequestCodeAsync(normalizedEmail, "REGISTER");
            return result switch
            {
                RequestEmailVerificationResult.Success => Ok(
                    new EmailVerificationOtpResponseAPIViewModel
                    {
                        Message = "A verification code was sent to " + normalizedEmail + ".",
                        ExpiresInSeconds = 300,
                        CooldownSeconds = 60,
                        DemoCode = demoCode
                    }),
                RequestEmailVerificationResult.CooldownActive => StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    CreateError(
                        "OTP_COOLDOWN_ACTIVE",
                        "Please wait before requesting another verification code.",
                        new Dictionary<string, int>
                        {
                            ["retryAfterSeconds"] = retryAfterSeconds
                        })),
                RequestEmailVerificationResult.DeliveryFailed => StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateError(
                        "OTP_DELIVERY_FAILED",
                        "The verification code could not be delivered. Please try again.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
            };
        }

        [AllowAnonymous]
        [HttpPost("Register_member")]
        [HttpPost("register")]
        public async Task<IActionResult> RegisterMemberAsync(RegisterMemberRequestAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                if (await _account.IsEmailExistsAsync(info.Email))
                {
                    return Conflict(CreateError(
                        "EMAIL_ALREADY_EXISTS",
                        "Email already exists."));
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

        private IActionResult CreateEmailVerificationError(VerifyEmailCodeResult result)
        {
            return result switch
            {
                VerifyEmailCodeResult.CodeRequired => BadRequest(CreateError(
                    "EMAIL_VERIFICATION_REQUIRED",
                    "An email verification code is required.")),
                VerifyEmailCodeResult.CodeNotFound => BadRequest(CreateError(
                    "EMAIL_VERIFICATION_NOT_FOUND",
                    "Request a new email verification code before continuing.")),
                VerifyEmailCodeResult.CodeExpired => BadRequest(CreateError(
                    "EMAIL_VERIFICATION_EXPIRED",
                    "The email verification code has expired.")),
                VerifyEmailCodeResult.InvalidCode => BadRequest(CreateError(
                    "INVALID_EMAIL_VERIFICATION_CODE",
                    "The email verification code is incorrect.")),
                VerifyEmailCodeResult.AttemptsExceeded => StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    CreateError(
                        "EMAIL_VERIFICATION_ATTEMPTS_EXCEEDED",
                        "The email verification code is no longer valid.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
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
