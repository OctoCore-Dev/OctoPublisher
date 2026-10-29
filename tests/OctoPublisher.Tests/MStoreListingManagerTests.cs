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

        MStoreListingManager.ApplyLocalizedMetadata(submission, notesByLang);

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

        MStoreListingManager.ApplyLocalizedMetadata(submission, defaultNotes: "Global release notes");

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

        MStoreListingManager.ApplyLocalizedMetadata(submission, structuredListings: structured);

        Assert.Equal("Obxodka Client", submission.Listings["ru-RU"].Title);
        Assert.Equal("Описание", submission.Listings["ru-RU"].Description);
        Assert.Equal(2, submission.Listings["ru-RU"].Features?.Count);
        Assert.Null(submission.Listings["en-US"].Title);
    }
}
