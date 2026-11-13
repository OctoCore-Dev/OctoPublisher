namespace Octo.MStore.Metadata;

public sealed class MStoreListingInfo
{
    public string Language { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ReleaseNotes { get; set; }
    public List<string> Features { get; set; } = [];

    //графические ассеты для витрины
    public string? IconPath { get; set; }
    public string? BoxArtPath { get; set; }
    public string? PosterPath { get; set; }
    public string? BannerPath { get; set; }
    public string? BrandedKeyArtPath { get; set; }
    public List<string> ScreenshotPaths { get; set; } = [];

    //видеоанонс
    public string? TrailerPath { get; set; }
    public string? TrailerThumbnailPath { get; set; }
    public string? TrailerTitle { get; set; }
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

    public static List<KeyValuePair<string, string>> ApplyLocalizedMetadata(
        DevCenterSubmission submission,
        IReadOnlyDictionary<string, string>? releaseNotesByLang = null,
        IReadOnlyDictionary<string, string>? descriptionsByLang = null,
        string? defaultNotes = null,
        string? defaultDescription = null,
        IReadOnlyDictionary<string, MStoreListingInfo>? structuredListings = null)
    {
        var extraFiles = new List<KeyValuePair<string, string>>();

        if (submission.Listings == null || submission.Listings.Count == 0)
        {
            return extraFiles;
        }

        foreach (var (langKey, listing) in submission.Listings)
        {
            listing.BaseListing ??= new DevCenterBaseListing();

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

                //применение графических ассетов и формирование списка для ZIP-архива
                ApplyListingAssets(langKey, listing.BaseListing, matchedInfo, extraFiles);

                //применение видеотрейлера
                if (!string.IsNullOrWhiteSpace(matchedInfo.TrailerPath) && File.Exists(matchedInfo.TrailerPath))
                {
                    ApplyTrailer(submission, langKey, matchedInfo, extraFiles);
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

        //если ни в одном листинге нет трейлера, но в черновике остались старые трейлеры - очищаем их
        var anyTrailerProvided = structuredListings != null && structuredListings.Values.Any(info => !string.IsNullOrWhiteSpace(info?.TrailerPath) && File.Exists(info.TrailerPath));
        if (!anyTrailerProvided && submission.Trailers != null && submission.Trailers.Count > 0)
        {
            submission.Trailers.Clear();
        }

        return extraFiles;
    }

    private static void ApplyListingAssets(
        string langKey,
        DevCenterBaseListing baseListing,
        MStoreListingInfo info,
        List<KeyValuePair<string, string>> extraFiles)
    {
        baseListing.Images ??= [];

        //иконка плитки 1:1 (300x300)
        if (!string.IsNullOrWhiteSpace(info.IconPath) && File.Exists(info.IconPath))
        {
            var zipEntry = $"images/{langKey.ToLowerInvariant()}_icon.png";
            UpsertImage(baseListing.Images, zipEntry, "Icon", "App Icon");
            extraFiles.Add(new KeyValuePair<string, string>(zipEntry, info.IconPath));
        }

        //квадратная обложка 1:1 (1080x1080)
        if (!string.IsNullOrWhiteSpace(info.BoxArtPath) && File.Exists(info.BoxArtPath))
        {
            var zipEntry = $"images/{langKey.ToLowerInvariant()}_boxart.png";
            UpsertImage(baseListing.Images, zipEntry, "StoreLogoSquare", "Box Art");
            if (baseListing.Images.Any(i => string.Equals(i.ImageType, "XboxFeaturedPromotionalArt", StringComparison.OrdinalIgnoreCase)))
            {
                UpsertImage(baseListing.Images, zipEntry, "XboxFeaturedPromotionalArt", "Xbox Featured Promo");
            }
            extraFiles.Add(new KeyValuePair<string, string>(zipEntry, info.BoxArtPath));
        }

        //постер 9:16 (720x1080)
        if (!string.IsNullOrWhiteSpace(info.PosterPath) && File.Exists(info.PosterPath))
        {
            var zipEntry = $"images/{langKey.ToLowerInvariant()}_poster.png";
            UpsertImage(baseListing.Images, zipEntry, "StoreLogo9x16", "Poster 9:16");
            extraFiles.Add(new KeyValuePair<string, string>(zipEntry, info.PosterPath));
        }

        //промо-баннер 16:9 (1920x1080)
        if (!string.IsNullOrWhiteSpace(info.BannerPath) && File.Exists(info.BannerPath))
        {
            var zipEntry = $"images/{langKey.ToLowerInvariant()}_banner.png";
            UpsertImage(baseListing.Images, zipEntry, "PromotionalArt16x9", "Banner 16:9");
            if (baseListing.Images.Any(i => string.Equals(i.ImageType, "XboxTitledHeroArt", StringComparison.OrdinalIgnoreCase)))
            {
                UpsertImage(baseListing.Images, zipEntry, "XboxTitledHeroArt", "Xbox Hero Art");
            }
            extraFiles.Add(new KeyValuePair<string, string>(zipEntry, info.BannerPath));
        }

        //фирменная заглавная иллюстрация xbox (584x800)
        if (!string.IsNullOrWhiteSpace(info.BrandedKeyArtPath) && File.Exists(info.BrandedKeyArtPath))
        {
            var zipEntry = $"images/{langKey.ToLowerInvariant()}_branded_key_art.png";
            UpsertImage(baseListing.Images, zipEntry, "XboxBrandedKeyArt", "Branded Key Art");
            extraFiles.Add(new KeyValuePair<string, string>(zipEntry, info.BrandedKeyArtPath));
        }

        //скриншоты рабочего стола
        if (info.ScreenshotPaths.Count > 0)
        {
            //помечаем старые скриншоты на удаление
            foreach (var img in baseListing.Images.Where(i => string.Equals(i.ImageType, "Screenshot", StringComparison.OrdinalIgnoreCase)))
            {
                img.FileStatus = "PendingDelete";
            }

            for (var i = 0; i < info.ScreenshotPaths.Count; i++)
            {
                var screenPath = info.ScreenshotPaths[i];
                if (File.Exists(screenPath))
                {
                    var zipEntry = $"images/{langKey.ToLowerInvariant()}_screenshot_{i + 1:D2}.png";
                    baseListing.Images.Add(new DevCenterImage
                    {
                        FileName = zipEntry,
                        ImageType = "Screenshot",
                        FileStatus = "PendingUpload",
                        Description = $"Desktop Screenshot {i + 1}"
                    });
                    extraFiles.Add(new KeyValuePair<string, string>(zipEntry, screenPath));
                }
            }
        }
    }

    private static void UpsertImage(List<DevCenterImage> images, string zipEntry, string imageType, string description)
    {
        var existing = images.FirstOrDefault(i => string.Equals(i.ImageType, imageType, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.FileName = zipEntry;
            existing.FileStatus = "PendingUpload";
            existing.Description = description;
        }
        else
        {
            images.Add(new DevCenterImage
            {
                FileName = zipEntry,
                ImageType = imageType,
                FileStatus = "PendingUpload",
                Description = description
            });
        }
    }

    private static void ApplyTrailer(
        DevCenterSubmission submission,
        string langKey,
        MStoreListingInfo info,
        List<KeyValuePair<string, string>> extraFiles)
    {
        submission.Trailers ??= [];

        var ext = Path.GetExtension(info.TrailerPath) ?? ".mp4";
        var videoZipEntry = $"trailers/{langKey.ToLowerInvariant()}_trailer{ext}";
        extraFiles.Add(new KeyValuePair<string, string>(videoZipEntry, info.TrailerPath!));

        List<DevCenterImage>? thumbList = null;
        if (!string.IsNullOrWhiteSpace(info.TrailerThumbnailPath) && File.Exists(info.TrailerThumbnailPath))
        {
            var thumbZipEntry = $"images/{langKey.ToLowerInvariant()}_trailer_thumb.png";
            extraFiles.Add(new KeyValuePair<string, string>(thumbZipEntry, info.TrailerThumbnailPath));
            thumbList =
            [
                new DevCenterImage
                {
                    FileName = thumbZipEntry,
                    Description = "Trailer Thumbnail",
                    FileStatus = "PendingUpload"
                }
            ];
        }

        var trailerTitle = !string.IsNullOrWhiteSpace(info.TrailerTitle) ? info.TrailerTitle.Trim() : (info.Title ?? "Trailer");
        var trailerAsset = new DevCenterTrailerAsset
        {
            Title = trailerTitle,
            Description = trailerTitle,
            ImageList = thumbList
        };

        var existingTrailer = submission.Trailers.FirstOrDefault();
        if (existingTrailer != null)
        {
            existingTrailer.VideoFileName = videoZipEntry;
            existingTrailer.TrailerAssets ??= new Dictionary<string, DevCenterTrailerAsset>(StringComparer.OrdinalIgnoreCase);
            existingTrailer.TrailerAssets[langKey] = trailerAsset;
        }
        else
        {
            submission.Trailers.Add(new DevCenterTrailer
            {
                VideoFileName = videoZipEntry,
                TrailerAssets = new Dictionary<string, DevCenterTrailerAsset>(StringComparer.OrdinalIgnoreCase)
                {
                    [langKey] = trailerAsset
                }
            });
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

            //графические ассеты
            info.IconPath = FindFirstFile(subDir, ["icon.png", "icon_300.png", "logo.png"]);
            info.BoxArtPath = FindFirstFile(subDir, ["boxart.png", "box_art.png", "square_logo.png", "icon_1080.png"]);
            info.PosterPath = FindFirstFile(subDir, ["poster.png", "poster_9x16.png", "store_logo_9x16.png"]);
            info.BannerPath = FindFirstFile(subDir, ["banner.png", "superhero.png", "hero.png"]);
            info.BrandedKeyArtPath = FindFirstFile(subDir, ["branded_key_art.png", "key_art.png", "xbox_key_art.png"]);

            var screenshotsDir = Path.Combine(subDir, "screenshots");
            if (Directory.Exists(screenshotsDir))
            {
                info.ScreenshotPaths =
                [
                    .. Directory.EnumerateFiles(screenshotsDir, "*.*")
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f)
                ];
            }

            //видеотрейлер
            var trailersDir = Path.Combine(subDir, "trailers");
            var searchTrailerDir = Directory.Exists(trailersDir) ? trailersDir : subDir;
            var trailerFile = FindFirstFile(searchTrailerDir, ["trailer.mp4", "trailer.mov", "video.mp4"]);
            if (trailerFile != null)
            {
                info.TrailerPath = trailerFile;
                info.TrailerThumbnailPath = FindFirstFile(searchTrailerDir, ["trailer_thumbnail.png", "trailer_thumb.png", "thumbnail.png"]);
                var trailerTitleFile = FindFirstFile(searchTrailerDir, ["trailer_title.txt", "video_title.txt"]);
                if (trailerTitleFile != null)
                {
                    info.TrailerTitle = File.ReadAllText(trailerTitleFile).Trim();
                }
            }

            if (!string.IsNullOrEmpty(info.Title) ||
                !string.IsNullOrEmpty(info.Description) ||
                !string.IsNullOrEmpty(info.ReleaseNotes) ||
                info.Features.Count > 0 ||
                !string.IsNullOrEmpty(info.IconPath) ||
                !string.IsNullOrEmpty(info.BoxArtPath) ||
                !string.IsNullOrEmpty(info.PosterPath) ||
                !string.IsNullOrEmpty(info.BannerPath) ||
                info.ScreenshotPaths.Count > 0 ||
                !string.IsNullOrEmpty(info.TrailerPath))
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
