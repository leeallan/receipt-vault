using Android.App;
using Android.Content;

namespace ReceiptVault.Services;

// Saves an exported file wherever the user chooses (Downloads, Drive, …) via the system
// "Save as" screen (ACTION_CREATE_DOCUMENT). Android's share sheet has no equivalent of
// iOS's "Save to Files", and this route needs no storage permission on any version.
public static class DocumentSaver
{
    private const int RequestCode = 0x5A7E;
    private static TaskCompletionSource<Android.Net.Uri?>? pending;

    // Returns true once the file is written; false if the user backed out.
    public static async Task<bool> SaveAsync(string filePath)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current activity to show the save screen.");

        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(MimeType(filePath));
        intent.PutExtra(Intent.ExtraTitle, Path.GetFileName(filePath));

        pending?.TrySetResult(null);
        pending = new TaskCompletionSource<Android.Net.Uri?>();
        activity.StartActivityForResult(intent, RequestCode);

        var uri = await pending.Task;
        if (uri is null) return false;

        await using var output = activity.ContentResolver!.OpenOutputStream(uri, "w")
            ?? throw new IOException("Couldn't open the chosen location for writing.");
        await using var input = File.OpenRead(filePath);
        await input.CopyToAsync(output);
        return true;
    }

    // Called from MainActivity.OnActivityResult.
    internal static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode) return;
        pending?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
        pending = null;
    }

    private static string MimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".csv" => "text/csv",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream",
    };
}
