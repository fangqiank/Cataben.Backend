using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cataben.Shared.Messaging;

public static class ExecutionResultSigner
{
    /// <summary>Shipped in appsettings.json so local dev works out of the box — but it is
    /// publicly known, so both hosts validate at startup that Production overrides it
    /// (user-secrets/env). The same value must be set on the Worker (signer) and the API
    /// (verifier), or every result is dropped by signature check.</summary>
    public const string PlaceholderKey = "dev_result_signing_key_change_me";

    public static string Sign(ExecutionResultMessage message, string key)
    {
        var originalSignature = message.Signature;
        message.Signature = null;

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(message);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            return Convert.ToBase64String(hmac.ComputeHash(payload));
        }
        finally
        {
            message.Signature = originalSignature;
        }
    }

    public static bool TryVerify(ExecutionResultMessage message, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var receivedSignature = message.Signature;
        if (string.IsNullOrWhiteSpace(receivedSignature))
            return false;

        var originalSignature = message.Signature;
        message.Signature = null;

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(message);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var expected = hmac.ComputeHash(payload);
            var received = Convert.FromBase64String(receivedSignature);
            return CryptographicOperations.FixedTimeEquals(expected, received);
        }
        catch (FormatException)
        {
            return false;
        }
        finally
        {
            message.Signature = originalSignature;
        }
    }
}
