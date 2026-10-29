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

            var fullListings = PlayStoreListingManager.ResolveFullListings(descPath, descDir, iconPath);
            foreach (var (lang, info) in fullListings)
            {
                if (!string.IsNullOrWhiteSpace(info.Title) ||
                    !string.IsNullOrWhiteSpace(info.ShortDescription) ||
                    !string.IsNullOrWhiteSpace(info.FullDescription))
                {
                    AnsiTerminal.Info($"Updating store listing metadata for '{lang}'...");
                    await _client.UpdateListingAsync(
                        packageName,
                        editId,
                        lang,
                        info.FullDescription,
                        info.ShortDescription,
                        info.Title,
                        token,
                        ct);
                }

                if (!string.IsNullOrWhiteSpace(info.IconPath) && File.Exists(info.IconPath))
                {
                    AnsiTerminal.Info($"Uploading app icon for '{lang}': {Path.GetFileName(info.IconPath)}...");
                    await _client.UploadImageAsync(packageName, editId, lang, "icon", info.IconPath, token, ct);
                    AnsiTerminal.Success($"App icon updated for '{lang}'.");
                }

                if (!string.IsNullOrWhiteSpace(info.FeatureGraphicPath) && File.Exists(info.FeatureGraphicPath))
                {
                    AnsiTerminal.Info($"Uploading feature graphic for '{lang}': {Path.GetFileName(info.FeatureGraphicPath)}...");
                    await _client.UploadImageAsync(packageName, editId, lang, "featureGraphic", info.FeatureGraphicPath, token, ct);
                    AnsiTerminal.Success($"Feature graphic updated for '{lang}'.");
                }

                if (info.PhoneScreenshots.Count > 0)
                {
                    AnsiTerminal.Info($"Uploading {info.PhoneScreenshots.Count} phone screenshot(s) for '{lang}'...");
                    await _client.DeleteAllImagesAsync(packageName, editId, lang, "phoneScreenshots", token, ct);
                    foreach (var shot in info.PhoneScreenshots)
                    {
                        await _client.UploadImageAsync(packageName, editId, lang, "phoneScreenshots", shot, token, ct);
                    }
                    AnsiTerminal.Success($"Phone screenshots updated for '{lang}'.");
                }

                if (info.SevenInchScreenshots.Count > 0)
                {
                    AnsiTerminal.Info($"Uploading {info.SevenInchScreenshots.Count} 7\" screenshot(s) for '{lang}'...");
                    await _client.DeleteAllImagesAsync(packageName, editId, lang, "sevenInchScreenshots", token, ct);
                    foreach (var shot in info.SevenInchScreenshots)
                    {
                        await _client.UploadImageAsync(packageName, editId, lang, "sevenInchScreenshots", shot, token, ct);
                    }
                    AnsiTerminal.Success($"7\" tablet screenshots updated for '{lang}'.");
                }

                if (info.TenInchScreenshots.Count > 0)
                {
                    AnsiTerminal.Info($"Uploading {info.TenInchScreenshots.Count} 10\" screenshot(s) for '{lang}'...");
                    await _client.DeleteAllImagesAsync(packageName, editId, lang, "tenInchScreenshots", token, ct);
                    foreach (var shot in info.TenInchScreenshots)
                    {
                        await _client.UploadImageAsync(packageName, editId, lang, "tenInchScreenshots", shot, token, ct);
                    }
                    AnsiTerminal.Success($"10\" tablet screenshots updated for '{lang}'.");
                }
            }

            if (fullListings.Count == 0 && !string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
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
