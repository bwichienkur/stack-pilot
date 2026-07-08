using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackPilot.Application.Billing;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class BillingService : IBillingService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IPlanLimitService _limits;
    private readonly ILogger<BillingService> _logger;

    public BillingService(AppDbContext db, IConfiguration config, IPlanLimitService limits, ILogger<BillingService> logger)
    {
        _db = db;
        _config = config;
        _limits = limits;
        _logger = logger;
    }

    public IReadOnlyList<PlanPricingDto> GetPlans() =>
        PlanCatalog.All.Select(ToDto).ToList();

    public async Task<OrganizationBillingDto> GetOrganizationBillingAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([organizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        var pricing = PlanCatalog.Get(org.Plan);
        var usage = await GetUsageAsync(organizationId, ct);
        var enforcement = await _limits.GetEnforcementStatusAsync(organizationId, ct);
        var trialDays = org.TrialEndsAt is null
            ? (int?)null
            : Math.Max(0, (int)Math.Ceiling((org.TrialEndsAt.Value - DateTime.UtcNow).TotalDays));

        return new OrganizationBillingDto(
            org.Plan.ToString(),
            org.SubscriptionStatus.ToString(),
            org.TrialEndsAt,
            trialDays,
            ToLimitsDto(pricing.Limits),
            usage,
            IsStripeConfigured(),
            org.StripeCustomerId,
            enforcement.IsWriteBlocked,
            enforcement.BlockReason,
            !string.IsNullOrEmpty(org.StripeCustomerId) || IsStripeConfigured());
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        Guid organizationId, Guid userId, CreateCheckoutSessionRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<OrganizationPlan>(request.Plan, true, out var targetPlan) || targetPlan == OrganizationPlan.Trial)
            throw new ArgumentException("Invalid plan for checkout");

        if (targetPlan == OrganizationPlan.Enterprise)
            throw new InvalidOperationException("Contact sales for Enterprise pricing");

        var org = await _db.Organizations.FindAsync([organizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found");

        if (!IsStripeConfigured())
        {
            _logger.LogInformation("Stripe not configured; returning mock checkout for org {OrgId} plan {Plan}", organizationId, targetPlan);
            org.Plan = targetPlan;
            org.SubscriptionStatus = SubscriptionStatus.Active;
            org.TrialEndsAt = null;
            await _db.SaveChangesAsync(ct);
            return new CheckoutSessionDto(
                $"mock_{Guid.NewGuid():N}",
                $"{request.SuccessUrl}?mock_checkout=true&plan={targetPlan}",
                true);
        }

        Stripe.StripeConfiguration.ApiKey = _config["Billing:Stripe:SecretKey"]!;

        if (string.IsNullOrEmpty(org.StripeCustomerId))
        {
            var customer = await new Stripe.CustomerService().CreateAsync(new Stripe.CustomerCreateOptions
            {
                Email = user.Email,
                Name = org.Name,
                Metadata = new Dictionary<string, string> { ["organization_id"] = organizationId.ToString() }
            }, cancellationToken: ct);
            org.StripeCustomerId = customer.Id;
            org.BillingEmail = user.Email;
            await _db.SaveChangesAsync(ct);
        }

        var priceId = ResolveStripePriceId(targetPlan, request.BillingInterval);
        if (string.IsNullOrEmpty(priceId))
            throw new InvalidOperationException($"Stripe price not configured for {targetPlan} ({request.BillingInterval})");

        var sessionOptions = new Stripe.Checkout.SessionCreateOptions
        {
            Mode = "subscription",
            Customer = org.StripeCustomerId,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            AllowPromotionCodes = string.IsNullOrWhiteSpace(request.PromotionCode),
            LineItems =
            [
                new Stripe.Checkout.SessionLineItemOptions { Price = priceId, Quantity = 1 }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["organization_id"] = organizationId.ToString(),
                ["plan"] = targetPlan.ToString()
            },
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["organization_id"] = organizationId.ToString(),
                    ["plan"] = targetPlan.ToString()
                }
            }
        };

        var promoCode = request.PromotionCode;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            var promotionId = await ResolvePromotionCodeIdAsync(promoCode, ct);
            if (promotionId is not null)
            {
                sessionOptions.Discounts =
                [
                    new Stripe.Checkout.SessionDiscountOptions { PromotionCode = promotionId }
                ];
                sessionOptions.AllowPromotionCodes = false;
            }
        }

        var session = await new Stripe.Checkout.SessionService().CreateAsync(sessionOptions, cancellationToken: ct);
        return new CheckoutSessionDto(session.Id, session.Url, false);
    }

    public async Task<PortalSessionDto> CreatePortalSessionAsync(
        Guid organizationId, CreatePortalSessionRequest request, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([organizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        if (!IsStripeConfigured() || string.IsNullOrEmpty(org.StripeCustomerId))
        {
            return new PortalSessionDto($"{request.ReturnUrl}?portal=mock", true);
        }

        Stripe.StripeConfiguration.ApiKey = _config["Billing:Stripe:SecretKey"]!;
        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = org.StripeCustomerId,
                ReturnUrl = request.ReturnUrl
            }, cancellationToken: ct);

        return new PortalSessionDto(session.Url, false);
    }

    public async Task EnsureStripeCouponsAsync(CancellationToken ct = default)
    {
        if (!IsStripeConfigured()) return;

        Stripe.StripeConfiguration.ApiKey = _config["Billing:Stripe:SecretKey"]!;
        var couponId = PlanCatalog.DesignPartnerCouponId;

        try
        {
            await new Stripe.CouponService().GetAsync(couponId, cancellationToken: ct);
        }
        catch (Stripe.StripeException ex) when (ex.StripeError?.Code == "resource_missing")
        {
            await new Stripe.CouponService().CreateAsync(new Stripe.CouponCreateOptions
            {
                Id = couponId,
                PercentOff = PlanCatalog.DesignPartnerYearOneDiscount * 100,
                Duration = "repeating",
                DurationInMonths = 12,
                Name = "Design partner year-one discount"
            }, cancellationToken: ct);
            _logger.LogInformation("Created Stripe coupon {CouponId}", couponId);
        }

        var promoCode = PlanCatalog.DesignPartnerPromotionCode;
        var promoList = await new Stripe.PromotionCodeService().ListAsync(
            new Stripe.PromotionCodeListOptions { Code = promoCode, Limit = 1 }, cancellationToken: ct);

        if (promoList.Data.Count == 0)
        {
            await new Stripe.PromotionCodeService().CreateAsync(new Stripe.PromotionCodeCreateOptions
            {
                Coupon = couponId,
                Code = promoCode,
                Active = true
            }, cancellationToken: ct);
            _logger.LogInformation("Created Stripe promotion code {PromoCode}", promoCode);
        }
    }

    public async Task HandleStripeWebhookAsync(string json, string signatureHeader, CancellationToken ct = default)
    {
        if (!IsStripeConfigured())
        {
            _logger.LogWarning("Stripe webhook received but Stripe is not configured");
            return;
        }

        Stripe.StripeConfiguration.ApiKey = _config["Billing:Stripe:SecretKey"]!;

        Stripe.Event stripeEvent;
        try
        {
            // Signature is verified at the API controller; parse the event payload here.
            stripeEvent = Stripe.EventUtility.ParseEvent(json, throwOnApiVersionMismatch: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring unparseable Stripe webhook payload");
            return;
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
                    await ApplyCheckoutCompletedAsync(session, ct);
                break;
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                if (stripeEvent.Data.Object is Stripe.Subscription subscription)
                    await ApplySubscriptionChangeAsync(subscription, ct);
                break;
        }
    }

    private async Task<string?> ResolvePromotionCodeIdAsync(string code, CancellationToken ct)
    {
        try
        {
            var list = await new Stripe.PromotionCodeService().ListAsync(
                new Stripe.PromotionCodeListOptions { Code = code, Active = true, Limit = 1 },
                cancellationToken: ct);
            return list.Data.FirstOrDefault()?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve promotion code {Code}", code);
            return null;
        }
    }

    private async Task ApplyCheckoutCompletedAsync(Stripe.Checkout.Session session, CancellationToken ct)
    {
        if (!session.Metadata.TryGetValue("organization_id", out var orgIdStr) || !Guid.TryParse(orgIdStr, out var orgId))
            return;

        var org = await _db.Organizations.FindAsync([orgId], ct);
        if (org is null) return;

        org.StripeSubscriptionId = session.SubscriptionId;
        org.SubscriptionStatus = SubscriptionStatus.Active;
        org.TrialEndsAt = null;
        if (session.Metadata.TryGetValue("plan", out var planStr) && Enum.TryParse<OrganizationPlan>(planStr, out var plan))
            org.Plan = plan;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Activated subscription for org {OrgId} plan {Plan}", orgId, org.Plan);
    }

    private async Task ApplySubscriptionChangeAsync(Stripe.Subscription subscription, CancellationToken ct)
    {
        if (!subscription.Metadata.TryGetValue("organization_id", out var orgIdStr) || !Guid.TryParse(orgIdStr, out var orgId))
            return;

        var org = await _db.Organizations.FindAsync([orgId], ct);
        if (org is null) return;

        org.StripeSubscriptionId = subscription.Id;
        org.SubscriptionStatus = subscription.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            "paused" => SubscriptionStatus.Paused,
            _ => org.SubscriptionStatus
        };

        if (subscription.Metadata.TryGetValue("plan", out var planStr) && Enum.TryParse<OrganizationPlan>(planStr, out var plan))
            org.Plan = plan;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<BillingUsageDto> GetUsageAsync(Guid organizationId, CancellationToken ct)
    {
        var seatCount = await _db.OrganizationMembers.CountAsync(m => m.OrganizationId == organizationId, ct);
        var workspaceCount = await _db.Workspaces.CountAsync(w => w.OrganizationId == organizationId, ct);
        var connectorCount = await _db.ConnectorInstances.CountAsync(c => c.OrganizationId == organizationId, ct);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var aiTokens = await _db.AiActions
            .Where(a => a.OrganizationId == organizationId && a.CreatedAt >= monthStart && a.TokensUsed != null)
            .SumAsync(a => a.TokensUsed ?? 0, ct);

        return new BillingUsageDto(seatCount, workspaceCount, connectorCount, aiTokens);
    }

    public async Task<AiUsageWithOverageDto> GetAiUsageWithOverageAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([organizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        var limits = PlanCatalog.LimitsFor(org.Plan);
        var usage = await GetUsageAsync(organizationId, ct);
        var budget = limits.MonthlyAiTokenBudget;
        var graceBudget = (long)(budget * 1.1);
        var overage = Math.Max(0, usage.AiTokensUsedThisMonth - budget);
        var withinGrace = usage.AiTokensUsedThisMonth > budget && usage.AiTokensUsedThisMonth <= graceBudget;

        if (withinGrace)
            _logger.LogWarning("Organization {OrgId} is within 10% AI token grace period ({Used}/{GraceBudget})", organizationId, usage.AiTokensUsedThisMonth, graceBudget);

        return new AiUsageWithOverageDto(usage.AiTokensUsedThisMonth, budget, overage, withinGrace, usage.AiTokensUsedThisMonth > budget);
    }

    private bool IsStripeConfigured() =>
        !string.IsNullOrWhiteSpace(_config["Billing:Stripe:SecretKey"]) &&
        !string.IsNullOrWhiteSpace(_config["Billing:Stripe:WebhookSecret"]);

    private string? ResolveStripePriceId(OrganizationPlan plan, string interval)
    {
        var key = interval.Equals("annual", StringComparison.OrdinalIgnoreCase) ? "AnnualPriceId" : "MonthlyPriceId";
        return _config[$"Billing:Stripe:Prices:{plan}:{key}"];
    }

    private static PlanPricingDto ToDto(PlanPricing p) => new(
        p.Plan.ToString(),
        p.Name,
        p.Tagline,
        p.MonthlyPriceUsd,
        p.AnnualPriceUsd,
        p.AdditionalSeatPriceUsd,
        p.BillingModel,
        ToLimitsDto(p.Limits),
        p.Highlights);

    private static PlanLimitsDto ToLimitsDto(PlanLimits l) => new(
        l.IncludedSeats, l.MaxSeats, l.MaxWorkspaces, l.MaxConnectors,
        l.MonthlyAiTokenBudget, l.AuditRetentionDays, l.SamlSso, l.PrioritySupport);
}
