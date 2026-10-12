#if IOS
using StoreKit;
using UIKit;
#endif

namespace ReceiptVault.Services;

// Asks for an App Store rating at a moment the user has just got value from the app:
// their 3rd saved receipt, or their first successful export. Apple decides whether the
// prompt actually appears (max 3 times a year); we additionally ask at most once per
// app version and never within 90 days of the last ask.
public class ReviewPromptService : IReviewPromptService
{
    private const string SavedCountKey = "review_saved_count";
    private const string LastPromptVersionKey = "review_last_prompt_version";
    private const string LastPromptDateKey = "review_last_prompt_date";
    private const int SavesBeforePrompt = 3;
    private static readonly TimeSpan MinInterval = TimeSpan.FromDays(90);

    public async Task OnReceiptSavedAsync()
    {
        var saved = Preferences.Get(SavedCountKey, 0) + 1;
        Preferences.Set(SavedCountKey, saved);
        if (saved >= SavesBeforePrompt)
            await TryPromptAsync();
    }

    public async Task OnExportCompletedAsync()
    {
        if (Preferences.Get(SavedCountKey, 0) >= 1)
            await TryPromptAsync();
    }

    private async Task TryPromptAsync()
    {
        var version = AppInfo.Current.VersionString;
        if (Preferences.Get(LastPromptVersionKey, string.Empty) == version)
            return;

        var last = Preferences.Get(LastPromptDateKey, DateTime.MinValue);
        if (DateTime.UtcNow - last < MinInterval)
            return;

        Preferences.Set(LastPromptVersionKey, version);
        Preferences.Set(LastPromptDateKey, DateTime.UtcNow);

        // Let navigation / the share sheet finish settling so the prompt lands on a calm screen.
        await Task.Delay(TimeSpan.FromSeconds(1));
        await MainThread.InvokeOnMainThreadAsync(RequestReview);
    }

    private static void RequestReview()
    {
#if IOS
        var scene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault(s => s.ActivationState == UISceneActivationState.ForegroundActive);
        if (scene is null) return;
#pragma warning disable CA1422 // SKStoreReviewController is deprecated on iOS 18 but still works; the replacement is Swift-only.
        SKStoreReviewController.RequestReview(scene);
#pragma warning restore CA1422
#endif
        // Android: Play In-App Review needs an extra package — add when Android ships.
    }
}
