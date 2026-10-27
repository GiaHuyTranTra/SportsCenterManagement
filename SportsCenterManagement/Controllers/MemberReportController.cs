using APIViewModel.Common;
using APIViewModel.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MemberReportService;
using Services.ReportExport;

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
        return await BuildResponseAsync(from, to, false);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        return await BuildResponseAsync(from, to, true);
    }

    private async Task<IActionResult> BuildResponseAsync(DateOnly? from, DateOnly? to, bool export)
    {
        try
        {
            MemberReportAPIViewModel result = await _service.GetMemberReportAsync(from, to);
            if (export)
                return File(ReportCsvWriter.Members(result), "text/csv; charset=utf-8", $"members-{result.AsOfDate:yyyy-MM-dd}.csv");
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
