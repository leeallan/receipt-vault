using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ReceiptVault.Server.Verification;

// Minimal JWS (RFC 7515) reader for Apple's StoreKit 2 signed transactions and server
// notifications: they are ES256-signed JWS with the signing certificate chain embedded in
// the header's x5c field. We validate that chain to Apple's root, then verify the signature.
public sealed class SignedJws
{
    private readonly string _encodedHeader;
    private readonly string _encodedPayload;
    private readonly byte[] _signature;

    public JsonElement Header { get; }

    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    private SignedJws(string encodedHeader, string encodedPayload, byte[] signature, JsonElement header)
    {
        _encodedHeader = encodedHeader;
        _encodedPayload = encodedPayload;
        _signature = signature;
        Header = header;
    }

    public static SignedJws Parse(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new FormatException("Value is not a well-formed JWS.");

        var header = JsonSerializer.Deserialize<JsonElement>(Base64UrlDecode(parts[0]));
        var signature = Base64UrlDecode(parts[2]);
        return new SignedJws(parts[0], parts[1], signature, header);
    }

    // The certificate chain from the JWS header, leaf first.
    public IReadOnlyList<X509Certificate2> GetCertificateChain()
    {
        var certs = new List<X509Certificate2>();
        if (Header.TryGetProperty("x5c", out var x5c) && x5c.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in x5c.EnumerateArray())
            {
                var der = Convert.FromBase64String(entry.GetString()!);
                certs.Add(X509CertificateLoader.LoadCertificate(der));
            }
        }
        return certs;
    }

    // Verifies the ES256 signature using the leaf certificate's public key. JWS uses the
    // raw R||S signature format (IEEE P1363), not DER.
    public bool VerifySignature(X509Certificate2 signer)
    {
        using var ecdsa = signer.GetECDsaPublicKey();
        if (ecdsa is null) return false;

        var signingInput = Encoding.ASCII.GetBytes($"{_encodedHeader}.{_encodedPayload}");
        return ecdsa.VerifyData(signingInput, _signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public T? GetPayload<T>() =>
        JsonSerializer.Deserialize<T>(Base64UrlDecode(_encodedPayload), PayloadJson);

    // Validates the leaf certificate chains up to the supplied Apple root. When no root is
    // provided (local dev), chain validation is skipped — the caller decides whether that's
    // acceptable.
    public static bool ValidateChain(IReadOnlyList<X509Certificate2> chain,
        X509Certificate2? trustedRoot, out string? error)
    {
        error = null;
        if (chain.Count == 0) { error = "Empty certificate chain."; return false; }
        if (trustedRoot is null) return true; // dev mode: signature-only

        using var x509 = new X509Chain();
        x509.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        x509.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        x509.ChainPolicy.CustomTrustStore.Add(trustedRoot);
        for (var i = 1; i < chain.Count; i++)
            x509.ChainPolicy.ExtraStore.Add(chain[i]);

        var built = x509.Build(chain[0]);
        if (!built)
            error = string.Join("; ", x509.ChainStatus.Select(s => s.StatusInformation.Trim()));
        return built;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(s);
    }
}
