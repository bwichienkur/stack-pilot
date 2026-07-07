using System.Security.Cryptography;
using System.Text;

namespace StackPilot.Application.Workflow;

public static class GitHubWebhookSignature
{
  public static bool IsValid(string payload, string? signatureHeader, string secret)
  {
    if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
      return false;

    const string prefix = "sha256=";
    if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      return false;

    var provided = signatureHeader[prefix.Length..];
    var keyBytes = Encoding.UTF8.GetBytes(secret);
    var payloadBytes = Encoding.UTF8.GetBytes(payload);
    using var hmac = new HMACSHA256(keyBytes);
    var hash = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    return CryptographicOperations.FixedTimeEquals(
      Encoding.UTF8.GetBytes(hash),
      Encoding.UTF8.GetBytes(provided.ToLowerInvariant()));
  }
}
