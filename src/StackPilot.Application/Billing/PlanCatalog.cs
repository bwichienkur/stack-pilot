using StackPilot.Domain.Enums;

namespace StackPilot.Application.Billing;

public record PlanLimits(
    int IncludedSeats,
    int MaxSeats,
    int MaxWorkspaces,
    int MaxConnectors,
    long MonthlyAiTokenBudget,
    int AuditRetentionDays,
    bool SamlSso,
    bool PrioritySupport);

public record PlanPricing(
    OrganizationPlan Plan,
    string Name,
    string Tagline,
    decimal? MonthlyPriceUsd,
    decimal? AnnualPriceUsd,
    decimal AdditionalSeatPriceUsd,
    string BillingModel,
    PlanLimits Limits,
    string[] Highlights);

public static class PlanCatalog
{
    public const int TrialDays = 14;
    public const decimal DesignPartnerYearOneDiscount = 0.20m;
    public const string DesignPartnerCouponId = "designpartner20";
    public const string DesignPartnerPromotionCode = "DESIGNPARTNER20";

    public static IReadOnlyList<PlanPricing> All { get; } =
    [
        new(
            OrganizationPlan.Trial,
            "Trial",
            "Evaluate StackPilot with your team",
            0m,
            0m,
            0m,
            "free",
            new PlanLimits(5, 5, 1, 3, 100_000, 30, false, false),
            [
                "14-day full product access",
                "Up to 5 seats · 1 workspace · 3 connectors",
                "100K AI tokens / month",
                "All workflow modules included"
            ]),
        new(
            OrganizationPlan.Starter,
            "Starter",
            "For growing engineering teams standardizing change governance",
            349m,
            3_490m,
            29m,
            "flat_plus_seats",
            new PlanLimits(10, 25, 2, 8, 1_000_000, 90, false, false),
            [
                "Includes 10 seats (+$29 / additional seat)",
                "2 workspaces · 8 connectors",
                "1M AI tokens / month",
                "QA, UAT, and release calendar"
            ]),
        new(
            OrganizationPlan.Professional,
            "Professional",
            "For multi-team orgs with compliance and ITSM needs",
            899m,
            8_990m,
            35m,
            "flat_plus_seats",
            new PlanLimits(25, 100, 10, 50, 5_000_000, 365, true, false),
            [
                "Includes 25 seats (+$35 / additional seat)",
                "10 workspaces · 50 connectors",
                "5M AI tokens / month",
                "SAML SSO · 1-year audit retention"
            ]),
        new(
            OrganizationPlan.Enterprise,
            "Enterprise",
            "Custom deployment, security review, and dedicated support",
            null,
            null,
            0m,
            "custom",
            new PlanLimits(100, int.MaxValue, int.MaxValue, int.MaxValue, 50_000_000, 730, true, true),
            [
                "Unlimited seats & connectors (contract)",
                "Dedicated support & security review",
                "Custom AI token pool & data residency",
                "On-prem / VPC deployment option"
            ])
    ];

    public static PlanPricing Get(OrganizationPlan plan) =>
        All.First(p => p.Plan == plan);

    public static PlanLimits LimitsFor(OrganizationPlan plan) =>
        Get(plan).Limits;
}
