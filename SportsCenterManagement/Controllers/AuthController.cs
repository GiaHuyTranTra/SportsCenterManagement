using APIViewModel.Account;
using APIViewModel.Auth;
using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Services.AccessTokenService;
using Services.AccountService;
using Services.AuthService;
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
    private readonly IAccountService _accountService;
    private readonly IAccessTokenService _accessToken;
    private readonly IMemoryCache _cache;

    public AuthController(
        IAuthService authService,
        IAccountService accountService,
        IAccessTokenService accesstoken,
        IMemoryCache cache)
    {
        _authService = authService;
        _accountService = accountService;
        _accessToken = accesstoken;
        _cache = cache;
    }

    [Authorize]
    [TypeFilter(typeof(AuthFilter))]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserAsync()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized(CreateError(
                "INVALID_TOKEN_CLAIMS",
                "The access token does not contain the required claims."));
        }

        (GetCurrentUserProfileResult result, CurrentUserProfileAPIViewModel? profile) =
            await _accountService.GetCurrentUserProfileAsync(accountId);

        return result switch
        {
            GetCurrentUserProfileResult.Success when profile is not null => Ok(profile),
            GetCurrentUserProfileResult.AccountNotFound => Unauthorized(CreateError(
                "ACCOUNT_NOT_FOUND",
                "The authenticated account no longer exists.")),
            GetCurrentUserProfileResult.ProfileNotFound => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateError(
                    "PROFILE_DATA_INTEGRITY_ERROR",
                    "The account profile is not configured correctly.")),
            GetCurrentUserProfileResult.UnsupportedRole => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateError(
                    "UNSUPPORTED_ACCOUNT_ROLE",
                    "The account role is not supported.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
        };
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LoginRequestAPIViewModel request)
    {
        (LoginResult result, LoginResponseAPIViewModel? account) =
            await _authService.LoginAsync(request);

        if (result == LoginResult.Success && account is not null)
        {
            GeneratedAccessTokenAPIViewModel tokenMeta =
                _accessToken.GenerateAccessTokenWithMetadata(account);

            AuthSessionAPIViewModel session = new AuthSessionAPIViewModel
            {
                AccessToken = tokenMeta.AccessToken,
                TokenType = "Bearer",
                ExpiresAtUtc = tokenMeta.ExpiresAtUtc,
                AccountId = account.Id,
                Email = account.Email,
                Role = account.Role,
                FullName = account.FullName,
                CreatedAt = account.CreatedAt
            };

            return Ok(session);
        }

        return result switch
        {
            LoginResult.InvalidCredentials => Unauthorized(new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "INVALID_CREDENTIALS",
                    Message = "The email or password is incorrect.",
                    Details = null
                },
                TraceId = HttpContext.TraceIdentifier
            }),
            LoginResult.AccountLocked => StatusCode(423, new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "ACCOUNT_LOCKED",
                    Message = "This account has been locked due to too many failed login attempts.",
                    Details = null
                },
                TraceId = HttpContext.TraceIdentifier
            }),
            LoginResult.AccountInactive => StatusCode(403, new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "ACCOUNT_INACTIVE",
                    Message = "This account is not active.",
                    Details = null
                },
                TraceId = HttpContext.TraceIdentifier
            }),
            LoginResult.ConcurrentPasswordChange => Conflict(new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "CONCURRENT_PASSWORD_CHANGE",
                    Message = "A concurrent change was detected. Please try again.",
                    Details = null
                },
                TraceId = HttpContext.TraceIdentifier
            }),
            LoginResult.ConcurrencyConflict => Conflict(new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "CONCURRENCY_CONFLICT",
                    Message = "A concurrent request conflict was detected. Please try again.",
                    Details = null
                },
                TraceId = HttpContext.TraceIdentifier
            }),
            _ => StatusCode(500, new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "INTERNAL_SERVER_ERROR",
                    Message = "An unexpected error occurred.",
                    Details = null
                },
                TraceId = HttpContext.TraceIdentifier
            })
        };
    }

    [Authorize]
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
            ChangePasswordWithOtpResult.IncorrectCurrentPassword => Unauthorized(CreateError(
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
    public async Task<ActionResult<CheckTokenResponse>> CheckToken()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized(new { message = "The access token does not contain the required claims." });
        }

        LoginResponseAPIViewModel? account =
            await _authService.GetSessionAccountAsync(accountId);
        if (account is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateError(
                "ACCOUNT_INACTIVE",
                "This account is not active."));
        }

        return Ok(new CheckTokenResponse
        {
            AccountId = account.Id,
            Email = account.Email,
            Role = account.Role,
            FullName = account.FullName,
            CreatedAt = account.CreatedAt
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
