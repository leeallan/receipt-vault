using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReceiptVault.Shared;

namespace ReceiptVault.Services;

public interface IEntitlementApi
{
    bool IsConfigured { get; }

    // Returns the server's authoritative entitlement, or null if the server isn't
    // configured or is unreachable (caller falls back to the local cache).
    Task<EntitlementResponse?> VerifyAsync(VerifyEntitlementRequest request, CancellationToken ct = default);
}

public class EntitlementApiClient(HttpClient http) : IEntitlementApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public bool IsConfigured => ServerConfig.IsConfigured;

    public async Task<EntitlementResponse?> VerifyAsync(
        VerifyEntitlementRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var url = $"{ServerConfig.BaseUrl.TrimEnd('/')}/entitlements/verify";
            using var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request, options: Json),
            };
            if (!string.IsNullOrEmpty(ServerConfig.ApiKey))
                message.Headers.Add("X-Api-Key", ServerConfig.ApiKey);

            using var response = await http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<EntitlementResponse>(Json, ct);
        }
        catch
        {
            // Network/DNS/timeout — treat as "unknown", let the caller use its cache.
            return null;
        }
    }
}
