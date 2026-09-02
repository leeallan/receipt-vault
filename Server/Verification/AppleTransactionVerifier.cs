using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ReceiptVault.Shared;

namespace ReceiptVault.Server.Verification;

// Verifies a StoreKit 2 signed transaction (JWS): validates the signing certificate chain
// to Apple's root, verifies the ES256 signature, then reads the decoded transaction to
// determine the product and whether the entitlement is currently active.
public class AppleTransactionVerifier : IStoreVerifier
{
    private readonly AppleOptions _options;
    private readonly AppStoreServerApiClient _api;
    private readonly ILogger<AppleTransactionVerifier> _logger;
    private readonly X509Certificate2? _appleRoot;

    public StorePlatform Platform => StorePlatform.Apple;

    public AppleTransactionVerifier(IOptions<AppleOptions> options,
        AppStoreServerApiClient api, ILogger<AppleTransactionVerifier> logger)
    {
        _options = options.Value;
        _api = api;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.RootCertPath) && File.Exists(_options.RootCertPath))
            _appleRoot = X509CertificateLoader.LoadCertificateFromFile(_options.RootCertPath);
        else if (_options.RequireChainValidation)
            _logger.LogWarning("Apple root cert not configured; chain validation will fail closed.");
    }

    public async Task<VerificationResult> VerifyAsync(VerifyEntitlementRequest request, CancellationToken ct)
    {
        // The signed transaction (JWS) may be supplied directly (webhook, or a future
        // StoreKit 2 client), or fetched from Apple by transaction id (the current app path,
        // because Plugin.InAppBilling can't produce a JWS itself).
        var signedTransaction = request.SignedTransaction;
        if (string.IsNullOrWhiteSpace(signedTransaction) && !string.IsNullOrWhiteSpace(request.TransactionId))
            signedTransaction = await _api.GetSignedTransactionAsync(request.TransactionId, ct);

        if (string.IsNullOrWhiteSpace(signedTransaction))
            return VerificationResult.Invalid("No verifiable transaction supplied.");

        try
        {
            var jws = SignedJws.Parse(signedTransaction);
            var chain = jws.GetCertificateChain();
            if (chain.Count == 0)
                return VerificationResult.Invalid("No signing certificate in token.");

            // Certificate chain → Apple root.
            if (_options.RequireChainValidation && _appleRoot is null)
                return VerificationResult.Invalid("Server missing Apple root certificate.");

            if (!SignedJws.ValidateChain(chain, _appleRoot, out var chainError))
                return VerificationResult.Invalid($"Certificate chain invalid: {chainError}");

            // Signature over header.payload with the leaf key.
            if (!jws.VerifySignature(chain[0]))
                return VerificationResult.Invalid("Signature verification failed.");

            var payload = jws.GetPayload<AppleTransactionPayload>();
            if (payload is null)
                return VerificationResult.Invalid("Could not read transaction payload.");

            // Must be our app.
            if (!string.Equals(payload.BundleId, _options.BundleId, StringComparison.OrdinalIgnoreCase))
                return VerificationResult.Invalid("Bundle id mismatch.");

            var expiresAt = payload.ExpiresDate is long exp
                ? DateTimeOffset.FromUnixTimeMilliseconds(exp)
                : (DateTimeOffset?)null;
            var revoked = payload.RevocationDate is not null;

            // Active if not revoked and (non-expiring, i.e. lifetime) or not yet expired.
            var isActive = !revoked && (expiresAt is null || expiresAt > DateTimeOffset.UtcNow);

            return new VerificationResult(
                IsValid: true,
                ProductId: payload.ProductId,
                OriginalTransactionId: payload.OriginalTransactionId ?? payload.TransactionId,
                ExpiresAt: expiresAt,
                IsActive: isActive,
                Error: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Apple transaction verification threw.");
            return VerificationResult.Invalid("Malformed or unverifiable transaction.");
        }
    }
}

// Subset of Apple's JWSTransactionDecodedPayload we rely on. Dates are epoch milliseconds.
public class AppleTransactionPayload
{
    [JsonPropertyName("productId")] public string? ProductId { get; set; }
    [JsonPropertyName("transactionId")] public string? TransactionId { get; set; }
    [JsonPropertyName("originalTransactionId")] public string? OriginalTransactionId { get; set; }
    [JsonPropertyName("bundleId")] public string? BundleId { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("environment")] public string? Environment { get; set; }
    [JsonPropertyName("purchaseDate")] public long? PurchaseDate { get; set; }
    [JsonPropertyName("expiresDate")] public long? ExpiresDate { get; set; }
    [JsonPropertyName("revocationDate")] public long? RevocationDate { get; set; }
}
