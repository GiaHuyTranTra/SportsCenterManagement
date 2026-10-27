using APIViewModel.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.RevenueReportService;
using Services.ReportExport;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/reports/revenue")]
[Authorize(Roles = "CenterManager")]
public class RevenueReportController : ControllerBase
{
    private readonly IRevenueReportService _service;

    public RevenueReportController(IRevenueReportService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetRevenueReportAsync(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] string? groupBy = null, [FromQuery] string? paymentMethod = null)
    {
        RevenueReportAPIViewModel report = await _service.GetRevenueReportAsync(from, to, groupBy, paymentMethod);
        return Ok(report);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null, [FromQuery] string? groupBy = null, [FromQuery] string? paymentMethod = null)
    {
        RevenueReportAPIViewModel report = await _service.GetRevenueReportAsync(from, to, groupBy, paymentMethod);
        return File(ReportCsvWriter.Revenue(report), "text/csv; charset=utf-8", $"revenue-{report.From:yyyy-MM-dd}-{report.To:yyyy-MM-dd}.csv");
    }
}
