namespace Octo.PlayStore.Metadata;

public static class PlayStoreListingManager
{
    private static readonly string[] t_imageExtensions = [".png", ".jpg", ".jpeg"];

    public static Dictionary<string, string> ResolveReleaseNotes(string? notesPath, string? notesDir)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(notesDir) && Directory.Exists(notesDir))
        {
            foreach (var file in Directory.EnumerateFiles(notesDir, "*.*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var content = File.ReadAllText(file).Trim();
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                var lang = NormalizeLanguage(name);
                result[lang] = content;
            }
        }

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(notesPath) && File.Exists(notesPath))
        {
            var content = File.ReadAllText(notesPath).Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                result["en-US"] = content;
            }
        }

        return result;
    }

    public static Dictionary<string, (string? Full, string? Short)> ResolveListings(string? descPath, string? descDir)
    {
        var full = ResolveFullListings(descPath, descDir);
        var result = new Dictionary<string, (string? Full, string? Short)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in full)
        {
            if (!string.IsNullOrEmpty(v.FullDescription) || !string.IsNullOrEmpty(v.ShortDescription))
            {
                result[k] = (v.FullDescription, v.ShortDescription);
            }
        }
        return result;
    }

    public static Dictionary<string, PlayListingInfo> ResolveFullListings(string? descPath, string? descDir, string? fallbackIcon = null)
    {
        var result = new Dictionary<string, PlayListingInfo>(StringComparer.OrdinalIgnoreCase);

        var hasValidDir = !string.IsNullOrWhiteSpace(descDir) && Directory.Exists(descDir);
        var rootIcon = !string.IsNullOrWhiteSpace(fallbackIcon) && File.Exists(fallbackIcon) ? fallbackIcon : null;
        var rootFeature = hasValidDir ? FindFirstFile(descDir!, ["feature_graphic.png", "feature_graphic.jpg", "feature.png", "feature.jpg"]) : null;
        var rootPhone = hasValidDir ? CollectScreenshots(descDir!, ["phoneScreenshots", "phone", "screenshots"]) : [];
        var rootSeven = hasValidDir ? CollectScreenshots(descDir!, ["sevenInchScreenshots", "tablet7"]) : [];
        var rootTen = hasValidDir ? CollectScreenshots(descDir!, ["tenInchScreenshots", "tablet10"]) : [];

        if (hasValidDir)
        {
            rootIcon ??= FindFirstFile(descDir!, ["icon.png", "icon.jpg", "icon.jpeg"]);

            foreach (var subDir in Directory.EnumerateDirectories(descDir!))
            {
                var dirName = Path.GetFileName(subDir);
                if (IsScreenshotDirectoryName(dirName))
                {
                    continue;
                }

                var lang = NormalizeLanguage(dirName);
                var info = new PlayListingInfo { Language = lang };

                var titleFile = FindFirstFile(subDir, ["title.txt", "name.txt"]);
                if (titleFile != null)
                {
                    info.Title = File.ReadAllText(titleFile).Trim();
                }

                var shortFile = FindFirstFile(subDir, ["short_description.txt", "short.txt"]);
                if (shortFile != null)
                {
                    info.ShortDescription = File.ReadAllText(shortFile).Trim();
                }

                var fullFile = FindFirstFile(subDir, ["full_description.txt", "description.txt"]);
                if (fullFile != null)
                {
                    info.FullDescription = File.ReadAllText(fullFile).Trim();
                }

                var localIcon = FindFirstFile(subDir, ["icon.png", "icon.jpg", "icon.jpeg"]);
                info.IconPath = localIcon ?? rootIcon;

                var localFeature = FindFirstFile(subDir, ["feature_graphic.png", "feature_graphic.jpg", "feature.png", "feature.jpg"]);
                info.FeatureGraphicPath = localFeature ?? rootFeature;

                var localPhone = CollectScreenshots(subDir, ["phoneScreenshots", "phone", "screenshots"]);
                info.PhoneScreenshots = localPhone.Count > 0 ? localPhone : [.. rootPhone];

                var localSeven = CollectScreenshots(subDir, ["sevenInchScreenshots", "tablet7"]);
                info.SevenInchScreenshots = localSeven.Count > 0 ? localSeven : [.. rootSeven];

                var localTen = CollectScreenshots(subDir, ["tenInchScreenshots", "tablet10"]);
                info.TenInchScreenshots = localTen.Count > 0 ? localTen : [.. rootTen];

                if (!string.IsNullOrEmpty(info.Title) ||
                    !string.IsNullOrEmpty(info.ShortDescription) ||
                    !string.IsNullOrEmpty(info.FullDescription) ||
                    !string.IsNullOrEmpty(info.IconPath) ||
                    !string.IsNullOrEmpty(info.FeatureGraphicPath) ||
                    info.PhoneScreenshots.Count > 0)
                {
                    result[lang] = info;
                }
            }
        }

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(descPath) && File.Exists(descPath))
        {
            var content = File.ReadAllText(descPath).Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                result["en-US"] = new PlayListingInfo
                {
                    Language = "en-US",
                    FullDescription = content,
                    IconPath = rootIcon
                };
            }
        }

        return result;
    }

    private static string? FindFirstFile(string dir, string[] candidates)
    {
        foreach (var c in candidates)
        {
            var p = Path.Combine(dir, c);
            if (File.Exists(p))
            {
                return p;
            }
        }
        return null;
    }

    private static List<string> CollectScreenshots(string parentDir, string[] folderNames)
    {
        foreach (var name in folderNames)
        {
            var folder = Path.Combine(parentDir, name);
            if (Directory.Exists(folder))
            {
                return [.. Directory.EnumerateFiles(folder)
                    .Where(f => t_imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];
            }
        }
        return [];
    }

    private static bool IsScreenshotDirectoryName(string name) =>
        name.Equals("phoneScreenshots", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("sevenInchScreenshots", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("tenInchScreenshots", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("screenshots", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("graphics", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeLanguage(string raw)
    {
        var parts = raw.Split(['-', '_']);
        return parts.Length >= 2
            ? $"{parts[^2].ToLowerInvariant()}-{parts[^1].ToUpperInvariant()}"
            : raw.Length == 2 ? raw.ToLowerInvariant() : raw;
    }
}
