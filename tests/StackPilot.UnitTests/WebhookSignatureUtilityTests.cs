using StackPilot.Application.Workflow;

namespace StackPilot.UnitTests;

public class WebhookSignatureUtilityTests
{
    [Fact]
    public void StackPilotWebhookSignature_RoundTrip_IsValid()
    {
        const string payload = """{"eventType":"ticket.created","ticketId":"abc"}""";
        const string secret = "test-secret-key";

        var signature = StackPilotWebhookSignature.Compute(payload, secret);
        Assert.True(StackPilotWebhookSignature.IsValid(payload, signature, secret));
    }

    [Fact]
    public void StackPilotWebhookSignature_TamperedPayload_IsInvalid()
    {
        const string payload = """{"eventType":"ticket.created"}""";
        const string secret = "test-secret-key";
        var signature = StackPilotWebhookSignature.Compute(payload, secret);

        Assert.False(StackPilotWebhookSignature.IsValid("""{"eventType":"ticket.deleted"}""", signature, secret));
    }

    [Fact]
    public void StripeWebhookSignature_RoundTrip_IsValid()
    {
        const string payload = """{"type":"checkout.session.completed"}""";
        const string secret = "whsec_test";

        var signature = StripeWebhookSignature.Sign(payload, secret);
        Assert.True(StripeWebhookSignature.IsValid(payload, signature, secret));
    }

    [Fact]
    public void StripeWebhookSignature_MissingHeader_IsInvalid()
    {
        Assert.False(StripeWebhookSignature.IsValid("{}", null, "whsec_test"));
    }
}
