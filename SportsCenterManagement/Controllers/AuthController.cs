using APIViewModel.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Services.AccessTokenService;
using Services.AuthService;
using SportsCenterManagement.Filter;
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

    public AuthController(IAuthService authService, IAccessTokenService accesstoken,IMemoryCache cache)
    {
        _authService = authService;
        _accessToken = accesstoken;
        _cache = cache;
       
    }

    [Authorize]
    [TypeFilter(typeof(AuthFilter))]
    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        string? rawToken = await HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(rawToken))
        {
            _cache.Set("blacklist-" + rawToken, rawToken, TimeSpan.FromMinutes(1440));
        }
        return Ok();
    }


    [AllowAnonymous]
    [HttpPost("Login_center_manager")]
    public async Task<IActionResult> LoginCenterManager(LoginRequestAPIViewModel model)
    {
        if (ModelState.IsValid)
        {
            //check login
            LoginResponseAPIViewModel? result = await _authService.LoginCenterManagerAsync(model);
            if (result != null)
            {
                //gen toiken
                string token =  _accessToken.GenerateAccessToken(result);
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
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);

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


}
