using APIViewModel.Auth;
using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Services.AccessTokenService;
using Services.AuthService;
using Services.EmailVerificationService;
using SportsCenterManagement.Filter;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAccessTokenService _accessToken;
    private readonly IMemoryCache _cache;
    private readonly IEmailVerificationService _emailVerification;

    public AuthController(
        IAuthService authService,
        IAccessTokenService accesstoken,
        IMemoryCache cache,
        IEmailVerificationService emailVerification)
    {
        _authService = authService;
        _accessToken = accesstoken;
        _cache = cache;
        _emailVerification = emailVerification;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LoginRequestAPIViewModel request)
    {
        (LoginResult result, LoginResponseAPIViewModel? account) =
            await _authService.LoginAsync(request);

        if (result == LoginResult.Success && account is not null)
        {
            VerifyEmailCodeResult verification = _emailVerification.VerifyCode(
                account.Email,
                "LOGIN",
                request.EmailVerificationCode);
            if (verification == VerifyEmailCodeResult.CodeNotFound)
            {
                verification = _emailVerification.VerifyCode(
                    account.Email,
                    "REGISTER",
                    request.EmailVerificationCode);
            }
            if (verification != VerifyEmailCodeResult.Success)
            {
                return CreateEmailVerificationError(verification);
            }

            GeneratedAccessTokenAPIViewModel tokenMeta =
                _accessToken.GenerateAccessTokenWithMetadata(account);

            AuthSessionAPIViewModel session = new AuthSessionAPIViewModel
            {
                AccessToken = tokenMeta.AccessToken,
                TokenType = "Bearer",
                ExpiresAtUtc = tokenMeta.ExpiresAtUtc,
                AccountId = account.Id,
                Email = account.Email,
                Role = account.Role
            };

            return Ok(session);
        }

        return CreateLoginError(result);
    }

    [AllowAnonymous]
    [HttpPost("request-login-email-verification")]
    public async Task<IActionResult> RequestLoginEmailVerificationAsync(
        RequestLoginEmailVerificationAPIViewModel request)
    {
        LoginRequestAPIViewModel loginRequest = new LoginRequestAPIViewModel
        {
            Email = request.Email,
            Password = request.Password
        };
        (LoginResult result, LoginResponseAPIViewModel? account) =
            await _authService.LoginAsync(loginRequest);
        if (result != LoginResult.Success || account is null)
        {
            return CreateLoginError(result);
        }

        (RequestEmailVerificationResult requestResult, int retryAfterSeconds, string? demoCode) =
            await _emailVerification.RequestCodeAsync(account.Email, "LOGIN");
        return CreateEmailVerificationRequestResponse(
            requestResult,
            retryAfterSeconds,
            demoCode,
            account.Email);
    }

    [Authorize]
    [TypeFilter(typeof(AuthFilter))]
    [HttpPost("Logout")]
    public IActionResult Logout()
    {
        IActionResult? blacklistError = BlacklistCurrentToken();
        if (blacklistError is not null)
        {
            return blacklistError;
        }

        return Ok();
    }

    [Authorize]
    [TypeFilter(typeof(AuthFilter))]
    [HttpPost("request-change-password-otp")]
    public async Task<IActionResult> RequestChangePasswordOtpAsync()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized(CreateError(
                "INVALID_TOKEN_CLAIMS",
                "The access token does not contain the required claims."));
        }

        (RequestPasswordChangeOtpResult result, int retryAfterSeconds) =
            await _authService.RequestChangePasswordOtpAsync(accountId);

        return result switch
        {
            RequestPasswordChangeOtpResult.Success => Ok(
                new PasswordChangeOtpResponseAPIViewModel
                {
                    Message = "A password change code was sent to your email address.",
                    ExpiresInSeconds = 300,
                    CooldownSeconds = 60
                }),
            RequestPasswordChangeOtpResult.AccountNotFound => Unauthorized(CreateError(
                "ACCOUNT_NOT_FOUND",
                "The authenticated account no longer exists.")),
            RequestPasswordChangeOtpResult.AccountInactive => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateError("ACCOUNT_INACTIVE", "This account is not active.")),
            RequestPasswordChangeOtpResult.AccountLocked => StatusCode(
                StatusCodes.Status423Locked,
                CreateError("ACCOUNT_LOCKED", "This account is locked.")),
            RequestPasswordChangeOtpResult.CooldownActive => StatusCode(
                StatusCodes.Status429TooManyRequests,
                CreateError(
                    "OTP_COOLDOWN_ACTIVE",
                    "Please wait before requesting another code.",
                    new Dictionary<string, int>
                    {
                        ["retryAfterSeconds"] = retryAfterSeconds
                    })),
            RequestPasswordChangeOtpResult.ConcurrentRequest => Conflict(CreateError(
                "OTP_CONCURRENT_REQUEST",
                "Another password change code request is already being processed.")),
            RequestPasswordChangeOtpResult.DeliveryFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateError(
                    "OTP_DELIVERY_FAILED",
                    "The password change code could not be delivered. Please try again.")),
            RequestPasswordChangeOtpResult.ConcurrencyConflict => Conflict(CreateError(
                "CONCURRENCY_CONFLICT",
                "A concurrent request conflict was detected. Please try again.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
        };
    }

    [Authorize]
    [TypeFilter(typeof(AuthFilter))]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePasswordWithOtpAsync(
        ChangePasswordWithOtpRequestAPIViewModel request)
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized(CreateError(
                "INVALID_TOKEN_CLAIMS",
                "The access token does not contain the required claims."));
        }

        ChangePasswordWithOtpResult result =
            await _authService.ChangePasswordWithOtpAsync(accountId, request);

        if (result == ChangePasswordWithOtpResult.Success)
        {
            IActionResult? blacklistError = BlacklistCurrentToken();
            if (blacklistError is not null)
            {
                return blacklistError;
            }

            return Ok("Password changed successfully. Please sign in again.");
        }

        return result switch
        {
            ChangePasswordWithOtpResult.AccountNotFound => Unauthorized(CreateError(
                "ACCOUNT_NOT_FOUND",
                "The authenticated account no longer exists.")),
            ChangePasswordWithOtpResult.AccountInactive => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateError("ACCOUNT_INACTIVE", "This account is not active.")),
            ChangePasswordWithOtpResult.AccountLocked => StatusCode(
                StatusCodes.Status423Locked,
                CreateError("ACCOUNT_LOCKED", "This account is locked.")),
            // 400, not 401: the caller's token is valid, only the submitted
            // current password is wrong. Returning 401 makes clients treat it
            // as an expired session and sign the user out mid-flow.
            ChangePasswordWithOtpResult.IncorrectCurrentPassword => BadRequest(CreateError(
                "INCORRECT_CURRENT_PASSWORD",
                "The current password is incorrect.")),
            ChangePasswordWithOtpResult.OtpNotFound => BadRequest(CreateError(
                "OTP_NOT_FOUND",
                "No active password change code was found.")),
            ChangePasswordWithOtpResult.OtpExpired => BadRequest(CreateError(
                "OTP_EXPIRED",
                "The password change code has expired.")),
            ChangePasswordWithOtpResult.InvalidOtp => BadRequest(CreateError(
                "INVALID_OTP",
                "The password change code is incorrect.")),
            ChangePasswordWithOtpResult.AttemptsExceeded => StatusCode(
                StatusCodes.Status429TooManyRequests,
                CreateError(
                    "OTP_ATTEMPTS_EXCEEDED",
                    "The password change code is no longer valid.")),
            ChangePasswordWithOtpResult.PasswordUnchanged => Conflict(CreateError(
                "PASSWORD_UNCHANGED",
                "The new password must be different from the current password.")),
            ChangePasswordWithOtpResult.ConcurrentPasswordChange => Conflict(CreateError(
                "CONCURRENT_PASSWORD_CHANGE",
                "The password changed during this request. Please try again.")),
            ChangePasswordWithOtpResult.ConcurrencyConflict => Conflict(CreateError(
                "CONCURRENCY_CONFLICT",
                "A concurrent request conflict was detected. Please try again.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
        };
    }

    [AllowAnonymous]
    [HttpPost("Login_center_manager")]
    public async Task<IActionResult> LoginCenterManager(LoginRequestAPIViewModel model)
    {
        if (ModelState.IsValid)
        {
            LoginResponseAPIViewModel? result = await _authService.LoginCenterManagerAsync(model);
            if (result != null)
            {
                string token = _accessToken.GenerateAccessToken(result);
                return Ok(token);
            }
            else return BadRequest("Email or password is not corrected");
        }
        else
        {
            return BadRequest();
        }
    }

    [AllowAnonymous]
    [HttpPost("Login_coach")]
    public async Task<IActionResult> LoginCoach(LoginRequestAPIViewModel model)
    {
        if (ModelState.IsValid)
        {
            LoginResponseAPIViewModel? result = await _authService.LoginCoachAsync(model);
            if (result != null)
            {
                string token = _accessToken.GenerateAccessToken(result);
                return Ok(token);
            }
            else
            {
                return BadRequest("Email or password is not corrected");
            }
        }
        else
        {
            return BadRequest();
        }
    }

    [AllowAnonymous]
    [HttpPost("Login_member")]
    public async Task<IActionResult> LoginMember(LoginRequestAPIViewModel model)
    {
        if (ModelState.IsValid)
        {
            LoginResponseAPIViewModel? result = await _authService.LoginMemberAsync(model);
            if (result != null)
            {
                string token = _accessToken.GenerateAccessToken(result);
                return Ok(token);
            }
            else
            {
                return BadRequest("Email or password is not corrected");
            }
        }
        else
        {
            return BadRequest();
        }
    }

    [AllowAnonymous]
    [HttpPost("Login_receptionist")]
    public async Task<IActionResult> LoginReceptionist(LoginRequestAPIViewModel model)
    {
        if (ModelState.IsValid)
        {
            LoginResponseAPIViewModel? result = await _authService.LoginReceptionistAsync(model);
            if (result != null)
            {
                string token = _accessToken.GenerateAccessToken(result);
                return Ok(token);
            }
            else
            {
                return BadRequest("Email or password is not corrected");
            }
        }
        else
        {
            return BadRequest();
        }
    }

    [HttpPost("check-token")]
    [Authorize]
    [TypeFilter(typeof(AuthFilter))]
    public ActionResult<CheckTokenResponse> CheckToken()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email = User.FindFirstValue(JwtRegisteredClaimNames.Email);
        string? role = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrWhiteSpace(accountId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized(new { message = "The access token does not contain the required claims." });
        }

        return Ok(new CheckTokenResponse
        {
            AccountId = accountId,
            Email = email,
            Role = role
        });
    }

    private IActionResult? BlacklistCurrentToken()
    {
        string? jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        string? expClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);

        if (string.IsNullOrWhiteSpace(jti) || string.IsNullOrWhiteSpace(expClaim))
        {
            return Unauthorized(CreateError(
                "INVALID_TOKEN_CLAIMS",
                "The access token does not contain the required claims."));
        }

        if (!long.TryParse(
                expClaim,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long expUnixSeconds))
        {
            return Unauthorized(CreateError(
                "INVALID_TOKEN_CLAIMS",
                "The access token does not contain the required claims."));
        }

        DateTimeOffset tokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds);
        if (tokenExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Unauthorized(CreateError(
                "INVALID_TOKEN_CLAIMS",
                "The access token has already expired."));
        }

        string cacheKey = "jwt:blacklist:" + jti;
        _cache.Set(cacheKey, true, tokenExpiresAt);
        return null;
    }

    private IActionResult CreateLoginError(LoginResult result)
    {
        return result switch
        {
            LoginResult.InvalidCredentials => Unauthorized(CreateError(
                "INVALID_CREDENTIALS",
                "The email or password is incorrect.")),
            LoginResult.AccountLocked => StatusCode(
                StatusCodes.Status423Locked,
                CreateError(
                    "ACCOUNT_LOCKED",
                    "This account has been locked due to too many failed login attempts.")),
            LoginResult.AccountInactive => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateError("ACCOUNT_INACTIVE", "This account is not active.")),
            LoginResult.ConcurrentPasswordChange => Conflict(CreateError(
                "CONCURRENT_PASSWORD_CHANGE",
                "A concurrent change was detected. Please try again.")),
            LoginResult.ConcurrencyConflict => Conflict(CreateError(
                "CONCURRENCY_CONFLICT",
                "A concurrent request conflict was detected. Please try again.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
        };
    }

    private IActionResult CreateEmailVerificationRequestResponse(
        RequestEmailVerificationResult result,
        int retryAfterSeconds,
        string? demoCode,
        string email)
    {
        return result switch
        {
            RequestEmailVerificationResult.Success => Ok(
                new EmailVerificationOtpResponseAPIViewModel
                {
                    Message = "A verification code was sent to " + email + ".",
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
