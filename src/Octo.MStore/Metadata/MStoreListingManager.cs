namespace Octo.MStore.Metadata;

public sealed class MStoreListingInfo
{
    public string Language { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ReleaseNotes { get; set; }
    public List<string> Features { get; set; } = [];
}

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
        string? defaultDescription = null,
        IReadOnlyDictionary<string, MStoreListingInfo>? structuredListings = null)
    {
        if (submission.Listings == null || submission.Listings.Count == 0)
        {
            return;
        }

        foreach (var (langKey, listing) in submission.Listings)
        {
            var matchedInfo = FindMatchingInfo(langKey, structuredListings);
            if (matchedInfo != null)
            {
                if (!string.IsNullOrWhiteSpace(matchedInfo.Title))
                {
                    listing.Title = matchedInfo.Title.Trim();
                }
                if (!string.IsNullOrWhiteSpace(matchedInfo.Description))
                {
                    listing.Description = matchedInfo.Description.Trim();
                }
                if (!string.IsNullOrWhiteSpace(matchedInfo.ReleaseNotes))
                {
                    listing.ReleaseNotes = matchedInfo.ReleaseNotes.Trim();
                }
                if (matchedInfo.Features.Count > 0)
                {
                    listing.Features = matchedInfo.Features;
                }
            }

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

    public static Dictionary<string, MStoreListingInfo> ResolveStoreDirectory(string? storeDir)
    {
        var result = new Dictionary<string, MStoreListingInfo>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(storeDir) || !Directory.Exists(storeDir))
        {
            return result;
        }

        foreach (var subDir in Directory.EnumerateDirectories(storeDir))
        {
            var lang = ExtractLanguageTag(Path.GetFileName(subDir));
            var info = new MStoreListingInfo { Language = lang };

            var titleFile = FindFirstFile(subDir, ["title.txt", "name.txt"]);
            if (titleFile != null)
            {
                info.Title = File.ReadAllText(titleFile).Trim();
            }

            var descFile = FindFirstFile(subDir, ["description.txt", "full_description.txt", "desc.txt"]);
            if (descFile != null)
            {
                info.Description = File.ReadAllText(descFile).Trim();
            }

            var notesFile = FindFirstFile(subDir, ["release_notes.txt", "whatsnew.txt", "notes.txt"]);
            if (notesFile != null)
            {
                info.ReleaseNotes = File.ReadAllText(notesFile).Trim();
            }

            var featFile = FindFirstFile(subDir, ["features.txt", "feature.txt"]);
            if (featFile != null)
            {
                var lines = File.ReadAllLines(featFile)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith('#'))
                    .Take(20)
                    .ToList();
                info.Features = lines;
            }

            if (!string.IsNullOrEmpty(info.Title) ||
                !string.IsNullOrEmpty(info.Description) ||
                !string.IsNullOrEmpty(info.ReleaseNotes) ||
                info.Features.Count > 0)
            {
                result[lang] = info;
            }
        }

        return result;
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

    public static string ExtractLanguageTag(string fileName)
    {
        var parts = fileName.Split(['-', '_']);
        return parts.Length >= 2 ? $"{parts[^2]}-{parts[^1]}".ToLowerInvariant() : fileName.ToLowerInvariant();
    }

    private static MStoreListingInfo? FindMatchingInfo(string targetLang, IReadOnlyDictionary<string, MStoreListingInfo>? dict)
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
