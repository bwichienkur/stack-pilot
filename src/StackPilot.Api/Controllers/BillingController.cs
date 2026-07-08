using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using StackPilot.Api.Authorization;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Application.Workflow;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly IConfiguration _config;
    private readonly ILogger<BillingController> _logger;
    private readonly IHostEnvironment _env;

    public BillingController(
        IBillingService billing,
        IConfiguration config,
        ILogger<BillingController> logger,
        IHostEnvironment env)
    {
        _billing = billing;
        _config = config;
        _logger = logger;
        _env = env;
    }

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

    [HttpPost("organizations/{organizationId:guid}/portal")]
    [Authorize]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<PortalSessionDto>>> CreatePortal(
        Guid organizationId, [FromBody] CreatePortalSessionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<PortalSessionDto>.Ok(await _billing.CreatePortalSessionAsync(organizationId, request, ct)));

    [HttpGet("organizations/{organizationId:guid}/ai-usage")]
    [Authorize]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<AiUsageWithOverageDto>>> GetAiUsage(Guid organizationId, CancellationToken ct) =>
        Ok(ApiResponse<AiUsageWithOverageDto>.Ok(await _billing.GetAiUsageWithOverageAsync(organizationId, ct)));

    [HttpPost("webhooks/stripe")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        var webhookSecret = _config["Billing:Stripe:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            if (string.IsNullOrWhiteSpace(signature) ||
                !StripeWebhookSignature.IsValid(json, signature, webhookSecret))
                return Unauthorized();

            await _billing.HandleStripeWebhookAsync(json, signature, ct);
            return Ok();
        }

        if (!_env.IsDevelopment() && !_env.IsEnvironment("Testing"))
            return StatusCode(500);

        _logger.LogWarning("Stripe webhook secret not configured; accepting request without signature verification in dev/testing");
        await _billing.HandleStripeWebhookAsync(json, signature, ct);
        return Ok();
    }
}
