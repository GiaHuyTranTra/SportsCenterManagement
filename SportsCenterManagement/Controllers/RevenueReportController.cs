using APIViewModel.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.RevenueReportService;

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
}
