using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Member;
using APIViewModel.Receptionist;
using Microsoft.AspNetCore.Mvc;
using Services.AccountService;

namespace SportsCenterManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _account;

        public AccountController(IAccountService account)
        {
            _account = account;
        }

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

        [HttpPost("Create_coach")]
        public async Task<IActionResult> CreateCoachAsync(CreateCoachAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                bool isCreated = await _account.CreateCoachAsync(info);

                if (isCreated)
                {
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

        [HttpPost("Create_receptionist")]
        public async Task<IActionResult> CreateReceptionistAsync(CreateReceptionistAPIViewModel info)
        {
            if (ModelState.IsValid)
            {
                bool isCreated = await _account.CreateReceptionistAsync(info);

                if (isCreated)
                {
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
    }
}
