using System;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.Receptionist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AuditLogService;
using Services.ReceptionistService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(AuthFilter))]
public class ReceptionistController : ControllerBase
{
    private readonly IReceptionistService _receptionistService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistController(
        IReceptionistService receptionistService,
        IAuditLogService auditLogService)
    {
        _receptionistService = receptionistService;
        _auditLogService = auditLogService;
    }

    [Authorize(Roles = "CenterManager")]
    [HttpGet]
    public async Task<IActionResult> GetReceptionistsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            string trimmedStatus = status.Trim();
            if (!string.Equals(trimmedStatus, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(trimmedStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid status filter. Allowed values: Active, Inactive.");
            }
        }

        PagedReceptionistResultAPIViewModel result = await _receptionistService.GetReceptionistsAsync(page, pageSize, search, status);
        return Ok(result);
    }

    [Authorize(Roles = "CenterManager")]
    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetReceptionistByIdAsync([FromRoute] string accountId)
    {
        ReceptionistDetailAPIViewModel? result = await _receptionistService.GetReceptionistByIdAsync(accountId);
        if (result is null)
        {
            return NotFound("Receptionist not found.");
        }

        return Ok(result);
    }

    [Authorize(Roles = "CenterManager")]
    [HttpPatch("{accountId}")]
    public async Task<IActionResult> UpdateReceptionistAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateReceptionistAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            bool isUpdated = await _receptionistService.UpdateReceptionistAsync(accountId, request);
            if (!isUpdated)
            {
                return NotFound("Receptionist not found.");
            }

            string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _auditLogService.RecordAsync(
                currentUserId,
                "UPDATE",
                "RECEPTIONIST",
                accountId,
                $"Cập nhật hồ sơ nhân viên lễ tân {request.FullName}.");

            return Ok("Update receptionist successful.");
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [Authorize(Roles = "CenterManager")]
    [HttpPatch("{accountId}/status")]
    public async Task<IActionResult> UpdateReceptionistStatusAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateReceptionistStatusAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        bool isUpdated = await _receptionistService.UpdateReceptionistStatusAsync(accountId, request);
        if (!isUpdated)
        {
            return NotFound("Receptionist not found.");
        }

        string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _auditLogService.RecordAsync(
            currentUserId,
            request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "ACTIVATE" : "DEACTIVATE",
            "RECEPTIONIST",
            accountId,
            $"Cập nhật trạng thái nhân viên lễ tân sang {request.Status}.");

        return Ok("Update receptionist status successful.");
    }
}
