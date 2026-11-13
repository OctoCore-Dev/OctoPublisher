namespace OctoPublisher.Tests;

public class MStoreListingManagerTests
{
    [Fact]
    public void ApplyPackageUpdateReplacesExistingPackage()
    {
        var submission = new DevCenterSubmission
        {
            ApplicationPackages =
            [
                new ApplicationPackage { FileName = "OldApp_1.0.0.0_x64.msix", FileStatus = "Uploaded" },
                new ApplicationPackage { FileName = "NewApp_2.0.0.0_x64.msix", FileStatus = "PendingDelete" }
            ]
        };

        MStoreListingManager.ApplyPackageUpdate(submission, "NewApp_2.0.0.0_x64.msix");

        var packages = submission.ApplicationPackages;
        Assert.NotNull(packages);
        Assert.Equal(2, packages.Count);

        var oldPkg = packages.First(p => p.FileName == "OldApp_1.0.0.0_x64.msix");
        var newPkg = packages.First(p => p.FileName == "NewApp_2.0.0.0_x64.msix");

        Assert.Equal("PendingDelete", oldPkg.FileStatus);
        Assert.Equal("PendingUpload", newPkg.FileStatus);
    }

    [Fact]
    public void ApplyPackageUpdateAddsNewPackageWhenNotPreviouslyPresent()
    {
        var submission = new DevCenterSubmission
        {
            ApplicationPackages =
            [
                new ApplicationPackage { FileName = "OldApp.msix", FileStatus = "Uploaded" }
            ]
        };

        MStoreListingManager.ApplyPackageUpdate(submission, "BrandNewApp.msix");

        var packages = submission.ApplicationPackages;
        Assert.NotNull(packages);
        Assert.Equal(2, packages.Count);

        var oldPkg = packages.First(p => p.FileName == "OldApp.msix");
        var addedPkg = packages.First(p => p.FileName == "BrandNewApp.msix");

        Assert.Equal("PendingDelete", oldPkg.FileStatus);
        Assert.Equal("PendingUpload", addedPkg.FileStatus);
    }

    [Fact]
    public void ApplyLocalizedMetadataInjectsCorrectLanguageNotes()
    {
        var submission = new DevCenterSubmission
        {
            Listings = new Dictionary<string, DevCenterListing>
            {
                ["ru-RU"] = new DevCenterListing(),
                ["en-US"] = new DevCenterListing()
            }
        };

        var notesByLang = new Dictionary<string, string>
        {
            ["ru-RU"] = "Русские заметки к обновлению",
            ["en-US"] = "English release notes"
        };

        _ = MStoreListingManager.ApplyLocalizedMetadata(submission, notesByLang);

        Assert.Equal("Русские заметки к обновлению", submission.Listings["ru-RU"].ReleaseNotes);
        Assert.Equal("English release notes", submission.Listings["en-US"].ReleaseNotes);
    }

    [Fact]
    public void ApplyLocalizedMetadataFallsBackToDefaultNotesWhenLanguageNotFound()
    {
        var submission = new DevCenterSubmission
        {
            Listings = new Dictionary<string, DevCenterListing>
            {
                ["de-DE"] = new DevCenterListing()
            }
        };

        _ = MStoreListingManager.ApplyLocalizedMetadata(submission, defaultNotes: "Global release notes");

        Assert.Equal("Global release notes", submission.Listings["de-DE"].ReleaseNotes);
    }

    [Fact]
    public void ResolveStoreDirectoryLoadsTitleDescriptionAndFeatures()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var ruDir = Path.Combine(tempDir, "ru-RU");
        _ = Directory.CreateDirectory(ruDir);

        try
        {
            File.WriteAllText(Path.Combine(ruDir, "title.txt"), "Obxodka Windows");
            File.WriteAllText(Path.Combine(ruDir, "description.txt"), "Полное описание Windows");
            File.WriteAllText(Path.Combine(ruDir, "release_notes.txt"), "Заметки Windows");
            File.WriteAllText(Path.Combine(ruDir, "features.txt"), "# Комментарий игнорируется\nБыстрый VPN\nFluent UI\nNo-Logs\n");

            var result = MStoreListingManager.ResolveStoreDirectory(tempDir);

            _ = Assert.Single(result);
            var info = result["ru-ru"];
            Assert.Equal("Obxodka Windows", info.Title);
            Assert.Equal("Полное описание Windows", info.Description);
            Assert.Equal("Заметки Windows", info.ReleaseNotes);
            Assert.Equal(3, info.Features.Count);
            Assert.Equal("Быстрый VPN", info.Features[0]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ApplyLocalizedMetadataAppliesStructuredListings()
    {
        var submission = new DevCenterSubmission
        {
            Listings = new Dictionary<string, DevCenterListing>
            {
                ["ru-RU"] = new DevCenterListing(),
                ["en-US"] = new DevCenterListing()
            }
        };

        var structured = new Dictionary<string, MStoreListingInfo>
        {
            ["ru-RU"] = new MStoreListingInfo
            {
                Title = "Obxodka Client",
                Description = "Описание",
                Features = ["Фича 1", "Фича 2"]
            }
        };

        _ = MStoreListingManager.ApplyLocalizedMetadata(submission, structuredListings: structured);

        Assert.Equal("Obxodka Client", submission.Listings["ru-RU"].Title);
        Assert.Equal("Описание", submission.Listings["ru-RU"].Description);
        Assert.Equal(2, submission.Listings["ru-RU"].Features?.Count);
        Assert.Null(submission.Listings["en-US"].Title);
    }

    [Fact]
    public void BaseListingSerializesCorrectlyInJson()
    {
        var submission = new DevCenterSubmission
        {
            Listings = new Dictionary<string, DevCenterListing>
            {
                ["ru-RU"] = new DevCenterListing
                {
                    Title = "obxodka",
                    Description = "VPN client",
                    ReleaseNotes = "Release notes"
                }
            }
        };

        var json = JsonSerializer.Serialize(submission);
        Assert.Contains("\"baseListing\":{", json);
        Assert.Contains("\"title\":\"obxodka\"", json);
        Assert.Contains("\"description\":\"VPN client\"", json);
        Assert.Contains("\"releaseNotes\":\"Release notes\"", json);
    }

    [Fact]
    public void ApplyLocalizedMetadataConfiguresImagesAndReturnsExtraFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var iconPath = Path.Combine(tempDir, "icon.png");
            var boxArtPath = Path.Combine(tempDir, "boxart.png");
            var posterPath = Path.Combine(tempDir, "poster.png");
            File.WriteAllBytes(iconPath, [1, 2, 3]);
            File.WriteAllBytes(boxArtPath, [4, 5, 6]);
            File.WriteAllBytes(posterPath, [7, 8, 9]);

            var submission = new DevCenterSubmission
            {
                Listings = new Dictionary<string, DevCenterListing>
                {
                    ["ru-RU"] = new DevCenterListing()
                }
            };

            var structured = new Dictionary<string, MStoreListingInfo>
            {
                ["ru-RU"] = new MStoreListingInfo
                {
                    IconPath = iconPath,
                    BoxArtPath = boxArtPath,
                    PosterPath = posterPath
                }
            };

            var extraFiles = MStoreListingManager.ApplyLocalizedMetadata(submission, structuredListings: structured);

            Assert.Equal(3, extraFiles.Count);
            var listing = submission.Listings["ru-RU"];
            Assert.NotNull(listing.BaseListing?.Images);
            Assert.Equal(3, listing.BaseListing.Images.Count);

            var iconImg = listing.BaseListing.Images.First(i => i.ImageType == "Icon");
            Assert.Equal("images/ru-ru_icon.png", iconImg.FileName);
            Assert.Equal("PendingUpload", iconImg.FileStatus);

            var boxArtImg = listing.BaseListing.Images.First(i => i.ImageType == "StoreLogoSquare");
            Assert.Equal("images/ru-ru_boxart.png", boxArtImg.FileName);
            Assert.Equal("PendingUpload", boxArtImg.FileStatus);

            var posterImg = listing.BaseListing.Images.First(i => i.ImageType == "StoreLogo9x16");
            Assert.Equal("images/ru-ru_poster.png", posterImg.FileName);
            Assert.Equal("PendingUpload", posterImg.FileStatus);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ApplyLocalizedMetadataConfiguresTrailers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var trailerPath = Path.Combine(tempDir, "trailer.mp4");
            var thumbPath = Path.Combine(tempDir, "trailer_thumbnail.png");
            File.WriteAllBytes(trailerPath, [1, 2, 3]);
            File.WriteAllBytes(thumbPath, [4, 5, 6]);

            var submission = new DevCenterSubmission
            {
                Listings = new Dictionary<string, DevCenterListing>
                {
                    ["ru-RU"] = new DevCenterListing()
                }
            };

            var structured = new Dictionary<string, MStoreListingInfo>
            {
                ["ru-RU"] = new MStoreListingInfo
                {
                    TrailerPath = trailerPath,
                    TrailerThumbnailPath = thumbPath,
                    TrailerTitle = "Официальный трейлер"
                }
            };

            var extraFiles = MStoreListingManager.ApplyLocalizedMetadata(submission, structuredListings: structured);

            Assert.Equal(2, extraFiles.Count);
            Assert.NotNull(submission.Trailers);
            _ = Assert.Single(submission.Trailers);

            var trailer = submission.Trailers[0];
            Assert.Equal("trailers/ru-ru_trailer.mp4", trailer.VideoFileName);
            Assert.NotNull(trailer.TrailerAssets);
            Assert.True(trailer.TrailerAssets.ContainsKey("ru-RU"));

            var asset = trailer.TrailerAssets["ru-RU"];
            Assert.Equal("Официальный трейлер", asset.Title);
            Assert.NotNull(asset.ImageList);
            _ = Assert.Single(asset.ImageList);
            Assert.Equal("images/ru-ru_trailer_thumb.png", asset.ImageList[0].FileName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
