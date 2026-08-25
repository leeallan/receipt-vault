using ReceiptVault.Shared;

namespace ReceiptVault.Server.Verification;

// The verified facts extracted from a store's signed proof of purchase.
public record VerificationResult(
    bool IsValid,
    string? ProductId,
    string? OriginalTransactionId,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    string? Error)
{
    public static VerificationResult Invalid(string error) =>
        new(false, null, null, null, false, error);
}

// Verifies a purchase against a specific app store. One implementation per platform.
public interface IStoreVerifier
{
    StorePlatform Platform { get; }
    Task<VerificationResult> VerifyAsync(VerifyEntitlementRequest request, CancellationToken ct);
}
