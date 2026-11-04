using System.Security.Claims;
using APIViewModel.MembershipInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MembershipInvoiceService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/payments/demo")]
[Authorize(Roles = "Member,Receptionist,CenterManager")]
public class DemoPaymentController : ControllerBase
{
    private readonly IMembershipInvoiceService _service;

    public DemoPaymentController(IMembershipInvoiceService service) { _service = service; }

    [HttpGet("config")]
    public IActionResult Configuration() => Ok(new { enabled = _service.IsDemoPaymentEnabled() });

    [HttpPost("{invoiceId:int}/confirm")]
    public async Task<IActionResult> ConfirmAsync(int invoiceId, [FromBody] DemoPaymentConfirmationAPIViewModel request)
    {
        string? caller = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(caller)) return Unauthorized();
        return Ok(await _service.ConfirmDemoPaymentAsync(invoiceId, caller, request.ConfirmSimulation));
    }
}
