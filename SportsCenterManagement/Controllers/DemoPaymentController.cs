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

    [HttpPost("{invoiceId:int}/qr")]
    public async Task<IActionResult> CreateQrAsync(int invoiceId)
    {
        string? caller = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(caller)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await _service.CreateTransferQrAsync(invoiceId, caller));
    }

    [AllowAnonymous]
    [HttpPost("qr/status")]
    public async Task<IActionResult> QrStatusAsync([FromBody] TransferQrRequestAPIViewModel request)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _service.GetTransferQrStatusAsync(request.Token));
    }

    [AllowAnonymous]
    [HttpPost("qr/confirm")]
    public async Task<IActionResult> ConfirmQrAsync([FromBody] TransferQrRequestAPIViewModel request)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _service.ConfirmTransferQrAsync(request.Token, request.ConfirmSimulation));
    }

    [HttpPost("{invoiceId:int}/confirm")]
    public async Task<IActionResult> ConfirmAsync(int invoiceId, [FromBody] DemoPaymentConfirmationAPIViewModel request)
    {
        string? caller = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(caller)) return Unauthorized();
        return Ok(await _service.ConfirmDemoPaymentAsync(invoiceId, caller, request.ConfirmSimulation));
    }
}
