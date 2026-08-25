namespace ReceiptVault.Services;

// Points the app at the entitlement server. While BaseUrl is empty (server not deployed
// yet) the app falls back to local, device-trusted entitlements — so purchasing keeps
// working through Phases 2–3 and switches to server-verified once the URL is set.
public static class ServerConfig
{
    // e.g. "https://receiptvault-api.azurewebsites.net"
    public const string BaseUrl = "";

    // Optional shared secret sent as X-Api-Key; must match the server's Api:Key.
    public const string ApiKey = "";

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
