using APIViewModel.Common;
using APIViewModel.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MemberReportService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/reports/members")]
[Authorize(Roles = "CenterManager")]
public class MemberReportController : ControllerBase
{
    private readonly IMemberReportService _service;

    public MemberReportController(IMemberReportService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetMemberReportAsync([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        try
        {
            MemberReportAPIViewModel result = await _service.GetMemberReportAsync(from, to);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return BadRequest(new ApiErrorResponseAPIViewModel
            {
                Error = new ApiErrorAPIViewModel
                {
                    Code = "INVALID_REPORT_DATE_RANGE", Message = "The report date range is invalid."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }
}
