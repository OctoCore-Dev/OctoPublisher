namespace Octo.MStore.Metadata;

public static class MStoreListingManager
{
    public static void ApplyPackageUpdate(DevCenterSubmission submission, string packageFileName)
    {
        var updated = new List<ApplicationPackage>();
        var matched = false;

        var existing = submission.ApplicationPackages ?? submission.Packages ?? [];

        foreach (var pkg in existing)
        {
            if (string.Equals(pkg.FileName, packageFileName, StringComparison.OrdinalIgnoreCase))
            {
                pkg.FileStatus = "PendingUpload";
                updated.Add(pkg);
                matched = true;
            }
            else
            {
                pkg.FileStatus = "PendingDelete";
                updated.Add(pkg);
            }
        }

        if (!matched)
        {
            updated.Add(new ApplicationPackage
            {
                FileName = packageFileName,
                FileStatus = "PendingUpload"
            });
        }

        if (submission.ApplicationPackages != null)
        {
            submission.ApplicationPackages = updated;
        }
        else
        {
            submission.Packages = updated;
        }
    }

    public static void ApplyLocalizedMetadata(
        DevCenterSubmission submission,
        IReadOnlyDictionary<string, string>? releaseNotesByLang = null,
        IReadOnlyDictionary<string, string>? descriptionsByLang = null,
        string? defaultNotes = null,
        string? defaultDescription = null)
    {
        if (submission.Listings == null || submission.Listings.Count == 0)
        {
            return;
        }

        foreach (var (langKey, listing) in submission.Listings)
        {
            var note = FindMatchingValue(langKey, releaseNotesByLang) ?? defaultNotes;
            if (!string.IsNullOrWhiteSpace(note))
            {
                listing.ReleaseNotes = note.Trim();
            }

            var desc = FindMatchingValue(langKey, descriptionsByLang) ?? defaultDescription;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                listing.Description = desc.Trim();
            }
        }
    }

    public static Dictionary<string, string> LoadDirectoryFiles(string? directoryPath, string searchPattern = "*.*")
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var content = File.ReadAllText(file).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var langKey = ExtractLanguageTag(name);
            result[langKey] = content;
        }

        return result;
    }

    private static string ExtractLanguageTag(string fileName)
    {
        var parts = fileName.Split(['-', '_']);
        return parts.Length >= 2 ? $"{parts[^2]}-{parts[^1]}".ToLowerInvariant() : fileName.ToLowerInvariant();
    }

    private static string? FindMatchingValue(string targetLang, IReadOnlyDictionary<string, string>? dict)
    {
        if (dict == null || dict.Count == 0)
        {
            return null;
        }

        if (dict.TryGetValue(targetLang, out var exact))
        {
            return exact;
        }

        var normalizedTarget = targetLang.Replace('_', '-').ToLowerInvariant();
        foreach (var (k, v) in dict)
        {
            var normalizedKey = k.Replace('_', '-').ToLowerInvariant();
            if (normalizedTarget.Contains(normalizedKey) || normalizedKey.Contains(normalizedTarget))
            {
                return v;
            }
        }

        return null;
    }
}
