using ReceiptVault.Shared;

namespace ReceiptVault.Server.Verification;

// Placeholder for Google Play verification. When Android goes live this validates the
// purchase token against the Play Developer API (purchases.subscriptionsv2 / products.get)
// and maps the result the same way AppleTransactionVerifier does. The seam is here so the
// endpoint and storage never need to change to add Android.
public class GoogleStoreVerifier : IStoreVerifier
{
    public StorePlatform Platform => StorePlatform.Google;

    public Task<VerificationResult> VerifyAsync(VerifyEntitlementRequest request, CancellationToken ct) =>
        Task.FromResult(VerificationResult.Invalid("Google Play verification is not enabled yet."));
}
