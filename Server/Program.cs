using System.Text.Json.Serialization;
using ReceiptVault.Server.Verification;
using ReceiptVault.Server.Storage;
using ReceiptVault.Shared;

var builder = WebApplication.CreateBuilder(args);

// Enums cross the wire as strings ("Apple", "Monthly", "CloudSync, PdfExport, ...").
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<AppleOptions>(builder.Configuration.GetSection("Apple"));

// Fetches signed transactions from Apple's App Store Server API.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AppStoreServerApiClient>();

// One verifier per platform; the endpoint dispatches by request.Platform.
builder.Services.AddSingleton<IStoreVerifier, AppleTransactionVerifier>();
builder.Services.AddSingleton<IStoreVerifier, GoogleStoreVerifier>();

// Durable Table Storage when configured, else an in-memory store for local dev.
var tableConn = builder.Configuration["Storage:TableConnectionString"];
if (!string.IsNullOrWhiteSpace(tableConn))
    builder.Services.AddSingleton<IEntitlementStore>(
        new AzureTableEntitlementStore(tableConn, "Entitlements"));
else
    builder.Services.AddSingleton<IEntitlementStore, InMemoryEntitlementStore>();

var app = builder.Build();

var apiKey = app.Configuration["Api:Key"];

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// The app posts its signed proof of purchase; we verify it, persist the entitlement keyed
// by the store's stable id, and return the authoritative entitlement state.
app.MapPost("/entitlements/verify", async (
    VerifyEntitlementRequest request,
    HttpRequest http,
    IEnumerable<IStoreVerifier> verifiers,
    IEntitlementStore store,
    CancellationToken ct) =>
{
    if (!ApiKeyOk(http, apiKey))
        return Results.Unauthorized();

    var verifier = verifiers.FirstOrDefault(v => v.Platform == request.Platform);
    if (verifier is null)
        return Results.BadRequest($"Unsupported platform '{request.Platform}'.");

    var result = await verifier.VerifyAsync(request, ct);

    // Verification failure (spoofed/expired/unknown) → no entitlement. Deliberately not an
    // error: the client just treats it as Free.
    if (!result.IsValid || string.IsNullOrEmpty(result.OriginalTransactionId))
        return Results.Ok(EntitlementResponse.Free);

    var tier = ProductCatalog.TierFor(result.ProductId ?? string.Empty);
    var entitlements = result.IsActive ? ProductCatalog.EntitlementsFor(tier) : Entitlement.None;

    await store.UpsertAsync(new EntitlementRecord
    {
        PartitionKey = request.Platform.ToString(),
        RowKey = result.OriginalTransactionId,
        ProductId = result.ProductId ?? string.Empty,
        Tier = tier.ToString(),
        IsActive = result.IsActive,
        ExpiresAt = result.ExpiresAt,
        UpdatedAt = DateTimeOffset.UtcNow,
    }, ct);

    var response = new EntitlementResponse(
        result.IsActive ? tier : ProductTier.Free,
        entitlements,
        result.IsActive,
        result.ExpiresAt);

    return Results.Ok(response);
});

// App Store Server Notifications V2 webhook. Apple POSTs { "signedPayload": "<JWS>" }.
// Skeleton: verify the payload, decode the enclosed transaction, and update the stored
// entitlement so renewals/cancellations/refunds are reflected without the app running.
// TODO: full notificationType/subtype handling + Google RTDN equivalent.
app.MapPost("/webhooks/appstore", async (
    AppStoreNotification body,
    IEnumerable<IStoreVerifier> verifiers,
    IEntitlementStore store,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    var log = loggerFactory.CreateLogger("AppStoreWebhook");

    if (string.IsNullOrWhiteSpace(body.SignedPayload))
        return Results.BadRequest();

    try
    {
        var outer = SignedJws.Parse(body.SignedPayload);
        var notification = outer.GetPayload<AppStoreNotificationPayload>();
        var signedTx = notification?.Data?.SignedTransactionInfo;
        if (string.IsNullOrWhiteSpace(signedTx))
            return Results.Ok(); // nothing actionable

        var apple = verifiers.First(v => v.Platform == StorePlatform.Apple);
        var result = await apple.VerifyAsync(
            new VerifyEntitlementRequest(StorePlatform.Apple, SignedTransaction: signedTx), ct);

        if (!result.IsValid || string.IsNullOrEmpty(result.OriginalTransactionId))
            return Results.Ok();

        // Terminal notification types revoke access regardless of the transaction's dates.
        var terminal = notification!.NotificationType is
            "EXPIRED" or "REFUND" or "REVOKE" or "GRACE_PERIOD_EXPIRED";
        var isActive = result.IsActive && !terminal;

        var tier = ProductCatalog.TierFor(result.ProductId ?? string.Empty);
        await store.UpsertAsync(new EntitlementRecord
        {
            PartitionKey = StorePlatform.Apple.ToString(),
            RowKey = result.OriginalTransactionId,
            ProductId = result.ProductId ?? string.Empty,
            Tier = tier.ToString(),
            IsActive = isActive,
            ExpiresAt = result.ExpiresAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        }, ct);

        log.LogInformation("Processed {Type} for {Txn}", notification.NotificationType, result.OriginalTransactionId);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to process App Store notification.");
        // 200 anyway so Apple doesn't hammer retries on a payload we can't parse.
        return Results.Ok();
    }
});

app.Run();

static bool ApiKeyOk(HttpRequest http, string? expected)
{
    if (string.IsNullOrEmpty(expected)) return true; // auth disabled (dev)
    return http.Headers.TryGetValue("X-Api-Key", out var provided)
        && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
}

// Webhook DTOs (subset of Apple's responseBodyV2 / decoded payload).
record AppStoreNotification(string? SignedPayload);

class AppStoreNotificationPayload
{
    [JsonPropertyName("notificationType")] public string? NotificationType { get; set; }
    [JsonPropertyName("subtype")] public string? Subtype { get; set; }
    [JsonPropertyName("data")] public AppStoreNotificationData? Data { get; set; }
}

class AppStoreNotificationData
{
    [JsonPropertyName("signedTransactionInfo")] public string? SignedTransactionInfo { get; set; }
    [JsonPropertyName("signedRenewalInfo")] public string? SignedRenewalInfo { get; set; }
}
