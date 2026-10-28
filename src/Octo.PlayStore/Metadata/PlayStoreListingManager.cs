namespace Octo.PlayStore.Metadata;

public static class PlayStoreListingManager
{
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
        var result = new Dictionary<string, (string? Full, string? Short)>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(descDir) && Directory.Exists(descDir))
        {
            foreach (var subDir in Directory.EnumerateDirectories(descDir))
            {
                var lang = NormalizeLanguage(Path.GetFileName(subDir));
                string? full = null;
                string? shortText = null;

                var fullFile = Path.Combine(subDir, "full_description.txt");
                if (File.Exists(fullFile))
                {
                    full = File.ReadAllText(fullFile).Trim();
                }

                var shortFile = Path.Combine(subDir, "short_description.txt");
                if (File.Exists(shortFile))
                {
                    shortText = File.ReadAllText(shortFile).Trim();
                }

                if (!string.IsNullOrEmpty(full) || !string.IsNullOrEmpty(shortText))
                {
                    result[lang] = (full, shortText);
                }
            }
        }

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(descPath) && File.Exists(descPath))
        {
            var content = File.ReadAllText(descPath).Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                result["en-US"] = (content, null);
            }
        }

        return result;
    }

    public static string NormalizeLanguage(string raw)
    {
        var parts = raw.Split(['-', '_']);
        return parts.Length >= 2
            ? $"{parts[^2].ToLowerInvariant()}-{parts[^1].ToUpperInvariant()}"
            : raw.Length == 2 ? raw.ToLowerInvariant() : raw;
    }
}
