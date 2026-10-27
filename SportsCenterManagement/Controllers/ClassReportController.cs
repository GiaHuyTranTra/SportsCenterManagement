using APIViewModel.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ClassReportService;
using Services.ReportExport;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/reports/classes")]
[Authorize(Roles = "CenterManager")]
public class ClassReportController : ControllerBase
{
    private readonly IClassReportService _service;

    public ClassReportController(IClassReportService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetClassReportAsync([FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null, [FromQuery] int? classId = null, [FromQuery] int? sportId = null,
        [FromQuery] string? registrationStatus = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        ClassReportAPIViewModel report = await _service.GetClassReportAsync(
            from, to, classId, sportId, registrationStatus, page, pageSize);
        return Ok(report);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null, [FromQuery] int? classId = null, [FromQuery] int? sportId = null,
        [FromQuery] string? registrationStatus = null)
    {
        ClassReportAPIViewModel report = await _service.GetClassReportForExportAsync(from, to, classId, sportId, registrationStatus);
        return File(ReportCsvWriter.Classes(report), "text/csv; charset=utf-8", $"classes-{report.From:yyyy-MM-dd}-{report.To:yyyy-MM-dd}.csv");
    }
}
