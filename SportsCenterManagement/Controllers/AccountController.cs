using APIViewModel.Account;
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
        private readonly ICoachService _coachService;
        private readonly IReceptionistService _receptionistService;

        public AccountController(
            IAccountService account,
            ICoachService coachService,
            IReceptionistService receptionistService)
        {
            _account = account;
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
                CreateCoachResult.InvalidData => BadRequest("Invalid coach data."),
                CreateCoachResult.DuplicateEmail => Conflict("Email already exists."),
                CreateCoachResult.DuplicatePhone => Conflict("Phone number already exists."),
                CreateCoachResult.DisciplineNotFoundOrInactive =>
                    BadRequest("A discipline was not found or is inactive."),
                CreateCoachResult.ConcurrencyConflict => Conflict(),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        [Authorize(Roles = "CenterManager")]
        [HttpPost("Create_member")]
        public async Task<IActionResult> CreateMemberAsync(CreateMemberAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                bool isCreated = await _account.CreateMemberAsync(info);

                if (isCreated)
                {
                    return Ok("Create member successful");
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
                CreateReceptionistResult.InvalidData => BadRequest("Invalid receptionist data."),
                CreateReceptionistResult.DuplicateEmail => Conflict("Email already exists."),
                CreateReceptionistResult.DuplicatePhone => Conflict("Phone number already exists."),
                CreateReceptionistResult.ReceptionistRoleMissing =>
                    StatusCode(StatusCodes.Status500InternalServerError),
                CreateReceptionistResult.ConcurrencyConflict =>
                    Conflict("A concurrent request conflict occurred."),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
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
                    return Conflict("Email already exists");
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
                    "The access token does not contain the required claims."));
            }

            UpdateCurrentProfileResult result =
                await _account.UpdateCurrentProfileAsync(accountId, request);

            return result switch
            {
                UpdateCurrentProfileResult.Success => Ok(),
                UpdateCurrentProfileResult.AccountNotFound => Unauthorized(CreateError(
                    "ACCOUNT_NOT_FOUND",
                    "The authenticated account no longer exists.")),
                UpdateCurrentProfileResult.ProfileNotFound => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError(
                        "PROFILE_DATA_INTEGRITY_ERROR",
                        "The account profile is not configured correctly.")),
                UpdateCurrentProfileResult.UnsupportedRole => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError(
                        "UNSUPPORTED_ACCOUNT_ROLE",
                        "The account role is not supported.")),
                UpdateCurrentProfileResult.InvalidData => BadRequest(CreateError(
                    "INVALID_PROFILE_DATA",
                    "The profile data is invalid.")),
                UpdateCurrentProfileResult.InvalidFieldForRole => BadRequest(CreateError(
                    "INVALID_FIELD_FOR_ROLE",
                    "One or more profile fields are not available for this account role.")),
                UpdateCurrentProfileResult.DuplicatePhone => Conflict(CreateError(
                    "DUPLICATE_PHONE",
                    "The phone number is already in use.")),
                UpdateCurrentProfileResult.NoChanges => BadRequest(CreateError(
                    "NO_CHANGES_PROVIDED",
                    "No profile changes were provided.")),
                UpdateCurrentProfileResult.ConcurrencyConflict => Conflict(CreateError(
                    "CONCURRENCY_CONFLICT",
                    "A concurrent request conflict was detected. Please try again.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
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
                    "The account was not found.")),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateError("INTERNAL_SERVER_ERROR", "An unexpected error occurred."))
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
                    "The access token does not contain the required claims."));
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
                    "The account was not found.")),
                UpdateAccountStatusResult.InvalidStatus => BadRequest(CreateError(
                    "INVALID_ACCOUNT_STATUS",
                    "Status must be either Active or Inactive.")),
                UpdateAccountStatusResult.SelfDeactivationNotAllowed => Conflict(CreateError(
                    "SELF_DEACTIVATION_NOT_ALLOWED",
                    "A center manager cannot deactivate their own account.")),
                UpdateAccountStatusResult.LastActiveCenterManager => Conflict(CreateError(
                    "LAST_ACTIVE_CENTER_MANAGER",
                    "The last active center manager cannot be deactivated.")),
                UpdateAccountStatusResult.ConcurrencyConflict => Conflict(CreateError(
                    "CONCURRENCY_CONFLICT",
                    "A concurrent request conflict was detected. Please try again.")),
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
