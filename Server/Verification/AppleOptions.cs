namespace ReceiptVault.Server.Verification;

public class AppleOptions
{
    // The app's bundle id; a verified transaction must match this.
    public string BundleId { get; set; } = "com.receiptvault.app";

    // Path to Apple Root CA - G3 (DER .cer), used to validate the JWS signing chain.
    // Download from https://www.apple.com/certificateauthority/AppleRootCA-G3.cer.
    public string? RootCertPath { get; set; }

    // When true (production), a request whose chain can't be validated to the Apple root is
    // rejected. Left false only for local dev where the root cert isn't configured.
    public bool RequireChainValidation { get; set; } = true;
}
