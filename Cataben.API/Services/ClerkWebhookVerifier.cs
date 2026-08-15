using System.Security.Cryptography;
using System.Text;

namespace Cataben.API.Services;

/// <summary>
/// Verifies Clerk/Svix webhook signatures using the documented HMAC-SHA256 scheme.
/// </summary>
internal static class ClerkWebhookVerifier
{
    /// <summary>The placeholder shipped in appsettings.json. Program.cs fail-fasts on it at
    /// startup (Production) or warns (Development) so a missing secret can never silently
    /// disable user provisioning — see the verifier note below.</summary>
    public const string PlaceholderSecret = "your_webhook_secret";

    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    public static bool TryVerify(
        HttpRequest request,
        string rawBody,
        string signingSecret,
        out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(signingSecret) ||
            signingSecret.Equals(PlaceholderSecret, StringComparison.OrdinalIgnoreCase))
        {
            error = "Clerk webhook secret is not configured.";
            return false;
        }

        if (!request.Headers.TryGetValue("svix-id", out var idValues) ||
            !request.Headers.TryGetValue("svix-timestamp", out var timestampValues) ||
            !request.Headers.TryGetValue("svix-signature", out var signatureValues))
        {
            error = "Missing Svix webhook signature headers.";
            return false;
        }

        var id = idValues.ToString();
        var timestamp = timestampValues.ToString();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(timestamp))
        {
            error = "Svix webhook signature headers are empty.";
            return false;
        }

        if (!long.TryParse(timestamp, out var timestampSeconds))
        {
            error = "Svix webhook timestamp is invalid.";
            return false;
        }

        var timestampUtc = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        if (Math.Abs((DateTimeOffset.UtcNow - timestampUtc).TotalSeconds) > TimestampTolerance.TotalSeconds)
        {
            error = "Svix webhook timestamp is outside the allowed tolerance.";
            return false;
        }

        byte[] key;
        try
        {
            var encodedKey = signingSecret.Trim();
            if (encodedKey.StartsWith("whsec_", StringComparison.OrdinalIgnoreCase))
                encodedKey = encodedKey["whsec_".Length..];
            key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException)
        {
            error = "Clerk webhook secret is not valid base64.";
            return false;
        }

        var signedContent = Encoding.UTF8.GetBytes($"{id}.{timestamp}.{rawBody}");
        byte[] expectedSignature;
        using (var hmac = new HMACSHA256(key))
        {
            expectedSignature = hmac.ComputeHash(signedContent);
        }

        foreach (var candidate in signatureValues.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = candidate.Split(',', 2);
            if (parts.Length != 2 ||
                !string.Equals(parts[0], "v1", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                continue;
            }

            try
            {
                var received = Convert.FromBase64String(parts[1]);
                if (CryptographicOperations.FixedTimeEquals(received, expectedSignature))
                    return true;
            }
            catch (FormatException)
            {
                // Ignore malformed candidates and continue checking the rest.
            }
        }

        error = "Svix webhook signature does not match.";
        return false;
    }
}
