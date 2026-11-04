using System.Security.Claims;
using APIViewModel.MembershipInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MembershipInvoiceService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/payments/vnpay-sandbox")]
public class VnpaySandboxController : ControllerBase
{
    private readonly IMembershipInvoiceService _service;
    public VnpaySandboxController(IMembershipInvoiceService service) { _service = service; }

    [Authorize(Roles = "Member,Receptionist,CenterManager")]
    [HttpPost("{invoiceId:int}/checkout")]
    public async Task<IActionResult> CheckoutAsync(int invoiceId)
    {
        string? caller = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(caller)) return Unauthorized();
        VnpayCheckoutAPIViewModel checkout = await _service.CreateVnpayCheckoutAsync(invoiceId, caller,
            HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1");
        return Ok(checkout);
    }

    [AllowAnonymous]
    [HttpGet("ipn")]
    public async Task<IActionResult> IpnAsync()
    {
        if (Request.Query.Any(item => item.Value.Count != 1))
            return Ok(new VnpayIpnAPIViewModel { RspCode = "97", Message = "Ambiguous query parameters" });
        Dictionary<string, string> values = Request.Query.ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.Ordinal);
        return Ok(await _service.ProcessVnpayIpnAsync(values));
    }
}
