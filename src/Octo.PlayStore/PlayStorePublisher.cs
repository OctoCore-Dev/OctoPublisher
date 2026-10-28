namespace Octo.PlayStore;

public sealed class PlayStorePublisher(GoogleAuthService? authService = null, PlayStoreClient? client = null)
{
    private readonly GoogleAuthService _authService = authService ?? new GoogleAuthService();
    private readonly PlayStoreClient _client = client ?? new PlayStoreClient();

    public async Task<PublisherResult> PublishAsync(
        string bundleOrApkPath,
        string packageName,
        string serviceAccountInput,
        string track = "internal",
        string? notesPath = null,
        string? notesDir = null,
        string? descPath = null,
        string? descDir = null,
        string? iconPath = null,
        string? releaseName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return PublisherResult.Failed(2, "Package name must be specified.");
        }

        if (string.IsNullOrWhiteSpace(bundleOrApkPath) || !File.Exists(bundleOrApkPath))
        {
            return PublisherResult.Failed(2, $"Package file not found at: '{bundleOrApkPath}'");
        }

        if (string.IsNullOrWhiteSpace(serviceAccountInput))
        {
            return PublisherResult.Failed(2, "Google Service Account JSON or file path must be specified.");
        }

        var ext = Path.GetExtension(bundleOrApkPath).ToLowerInvariant();
        if (ext is not ".aab" and not ".apk")
        {
            return PublisherResult.Failed(2, $"Unsupported package format '{ext}'. Expected .aab or .apk.");
        }

        try
        {
            AnsiTerminal.Info($"Authenticating with Google Play API for package '{packageName}'...");
            var account = GoogleAuthService.ParseServiceAccount(serviceAccountInput);
            var token = await _authService.GetAccessTokenAsync(account, ct);
            AnsiTerminal.Success($"Authenticated as: {account.ClientEmail}");

            AnsiTerminal.Info("Creating Google Play edit transaction...");
            var editId = await _client.CreateEditAsync(packageName, token, ct);
            AnsiTerminal.Success($"Edit transaction created: {editId}");

            int versionCode;
            if (ext == ".aab")
            {
                AnsiTerminal.Info($"Uploading Android App Bundle: {Path.GetFileName(bundleOrApkPath)}...");
                versionCode = await _client.UploadBundleAsync(packageName, editId, bundleOrApkPath, token, ct);
            }
            else
            {
                AnsiTerminal.Info($"Uploading Android APK: {Path.GetFileName(bundleOrApkPath)}...");
                versionCode = await _client.UploadApkAsync(packageName, editId, bundleOrApkPath, token, ct);
            }
            AnsiTerminal.Success($"Package uploaded successfully. Version Code: {versionCode}");

            var releaseNotes = PlayStoreListingManager.ResolveReleaseNotes(notesPath, notesDir);
            AnsiTerminal.Info($"Assigning version {versionCode} to track '{track}' ({releaseNotes.Count} localized notes)...");
            await _client.UpdateTrackAsync(packageName, editId, track, versionCode, releaseName, releaseNotes, token, ct);
            AnsiTerminal.Success($"Track '{track}' assigned.");

            var listings = PlayStoreListingManager.ResolveListings(descPath, descDir);
            foreach (var (lang, (full, shortText)) in listings)
            {
                AnsiTerminal.Info($"Updating store listing metadata for '{lang}'...");
                await _client.UpdateListingAsync(packageName, editId, lang, full, shortText, null, token, ct);
            }

            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            {
                AnsiTerminal.Info($"Uploading high-res app icon: {Path.GetFileName(iconPath)}...");
                await _client.UploadImageAsync(packageName, editId, "ru-RU", "icon", iconPath, token, ct);
                await _client.UploadImageAsync(packageName, editId, "en-US", "icon", iconPath, token, ct);
                AnsiTerminal.Success("App icon updated.");
            }

            AnsiTerminal.Info("Committing edit to Google Play Console...");
            await _client.CommitEditAsync(packageName, editId, token, ct);
            AnsiTerminal.Success($"Release committed to Google Play track '{track}' successfully!");

            return PublisherResult.Success(
                $"Published package {packageName} (v{versionCode}) to Google Play track '{track}'.",
                editId);
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"Google Play publishing failed: {ex.Message}");
            return PublisherResult.Failed(1, ex.Message);
        }
    }
}
