using APIViewModel.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ClassReportService;

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
}
