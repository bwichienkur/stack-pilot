using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackPilot.Api.Authorization;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;

    public BillingController(IBillingService billing) => _billing = billing;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("plans")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<IReadOnlyList<PlanPricingDto>>> Plans() =>
        Ok(ApiResponse<IReadOnlyList<PlanPricingDto>>.Ok(_billing.GetPlans()));

    [HttpGet("organizations/{organizationId:guid}")]
    [Authorize]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<OrganizationBillingDto>>> GetOrganizationBilling(Guid organizationId, CancellationToken ct) =>
        Ok(ApiResponse<OrganizationBillingDto>.Ok(await _billing.GetOrganizationBillingAsync(organizationId, ct)));

    [HttpPost("organizations/{organizationId:guid}/checkout")]
    [Authorize]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<CheckoutSessionDto>>> CreateCheckout(
        Guid organizationId, [FromBody] CreateCheckoutSessionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<CheckoutSessionDto>.Ok(await _billing.CreateCheckoutSessionAsync(organizationId, UserId, request, ct)));

    [HttpPost("webhooks/stripe")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();
        await _billing.HandleStripeWebhookAsync(json, signature, ct);
        return Ok();
    }
}
