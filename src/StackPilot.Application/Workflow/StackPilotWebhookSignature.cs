using System.Security.Cryptography;
using System.Text;

namespace StackPilot.Application.Workflow;

public static class StackPilotWebhookSignature
{
    public const string Prefix = "sha256=";

    public static string Compute(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Prefix + Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }

    public static bool IsValid(string payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        if (!signatureHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var provided = signatureHeader[Prefix.Length..];
        var expected = Compute(payload, secret)[Prefix.Length..];

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided.ToLowerInvariant()));
    }
}
