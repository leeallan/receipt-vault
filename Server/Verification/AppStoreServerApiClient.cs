using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ReceiptVault.Server.Verification;

// Fetches a signed transaction (JWS) from Apple's App Store Server API for a transaction id.
// Needed because Plugin.InAppBilling (StoreKit 1) can't hand the app a JWS — so the server
// retrieves the authoritative signed transaction from Apple itself, keyed by the id the app
// forwards. Authenticates with an ES256 JWT signed by the App Store Server API (.p8) key.
public class AppStoreServerApiClient(
    IHttpClientFactory httpFactory,
    IOptions<AppleOptions> options,
    ILogger<AppStoreServerApiClient> logger)
{
    private const string ProductionHost = "https://api.storekit.itunes.apple.com";
    private const string SandboxHost = "https://api.storekit-sandbox.itunes.apple.com";

    public async Task<string?> GetSignedTransactionAsync(string transactionId, CancellationToken ct)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.KeyId) || string.IsNullOrWhiteSpace(opt.IssuerId)
            || string.IsNullOrWhiteSpace(opt.PrivateKeyPath) || !File.Exists(opt.PrivateKeyPath))
        {
            logger.LogWarning("App Store Server API credentials are not configured.");
            return null;
        }

        string jwt;
        try { jwt = GenerateJwt(opt); }
        catch (Exception ex) { logger.LogError(ex, "Failed to build App Store Server API JWT."); return null; }

        // A sandbox transaction only resolves on the sandbox host and vice-versa; "Auto" tries
        // production first (404) then sandbox, per Apple's guidance.
        var hosts = opt.Environment?.ToLowerInvariant() switch
        {
            "production" => new[] { ProductionHost },
            "sandbox" => new[] { SandboxHost },
            _ => new[] { ProductionHost, SandboxHost },
        };

        var http = httpFactory.CreateClient();
        foreach (var host in hosts)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"{host}/inApps/v1/transactions/{transactionId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

                using var resp = await http.SendAsync(req, ct);
                if (resp.StatusCode == HttpStatusCode.NotFound) continue; // try the other environment
                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("App Store Server API returned {Status} for {Txn}.",
                        resp.StatusCode, transactionId);
                    continue;
                }

                var body = await resp.Content.ReadFromJsonAsync<TransactionInfoResponse>(cancellationToken: ct);
                if (!string.IsNullOrEmpty(body?.SignedTransactionInfo))
                    return body!.SignedTransactionInfo;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "App Store Server API call to {Host} failed.", host);
            }
        }
        return null;
    }

    private static string GenerateJwt(AppleOptions opt)
    {
        var header = new Dictionary<string, object> { ["alg"] = "ES256", ["kid"] = opt.KeyId, ["typ"] = "JWT" };
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["iss"] = opt.IssuerId,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(20).ToUnixTimeSeconds(),
            ["aud"] = "appstoreconnect-v1",
            ["bid"] = opt.BundleId,
        };

        var signingInput =
            $"{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header))}." +
            $"{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload))}";

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(File.ReadAllText(opt.PrivateKeyPath));
        var signature = ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private record TransactionInfoResponse(
        [property: JsonPropertyName("signedTransactionInfo")] string? SignedTransactionInfo);
}
