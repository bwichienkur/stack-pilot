using System.Security.Cryptography;
using System.Text;

namespace StackPilot.Application.Workflow;

public static class StripeWebhookSignature
{
    public static bool IsValid(string payload, string? signatureHeader, string secret, long toleranceSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        long? timestamp = null;
        var signatures = new List<string>();

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0) continue;

            var key = part[..separator];
            var value = part[(separator + 1)..];

            if (key == "t" && long.TryParse(value, out var ts))
                timestamp = ts;
            else if (key == "v1")
                signatures.Add(value);
        }

        if (timestamp is null || signatures.Count == 0)
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp.Value) > toleranceSeconds)
            return false;

        var signedPayload = $"{timestamp.Value}.{payload}";
        var keyMaterial = FormatSecret(secret);
        var keyBytes = Encoding.UTF8.GetBytes(keyMaterial);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
        using var hmac = new HMACSHA256(keyBytes);
        var expected = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();

        return signatures.Any(sig =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(sig.ToLowerInvariant())));
    }

    public static string Sign(string payload, string secret, long? timestamp = null)
    {
        timestamp ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp.Value}.{payload}";
        var keyMaterial = FormatSecret(secret);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keyMaterial));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
        return $"t={timestamp},v1={hash}";
    }

    private static string FormatSecret(string secret)
    {
        const string prefix = "whsec_";
        if (secret.StartsWith(prefix, StringComparison.Ordinal))
            return Encoding.UTF8.GetString(Convert.FromBase64String(secret[prefix.Length..]));
        return secret;
    }
}
