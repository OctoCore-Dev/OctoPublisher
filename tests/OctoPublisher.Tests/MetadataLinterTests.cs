namespace OctoPublisher.Tests;

public sealed class MetadataLinterTests
{
    [Fact]
    public void LintReturnsErrorWhenDirectoryDoesNotExist()
    {
        var result = MetadataLinter.Lint("non_existent_dir_12345");
        Assert.False(result.IsSuccess);
        _ = Assert.Single(result.Errors);
    }

    [Fact]
    public void LintDetectsPlaceholdersInScaffoldedDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            _ = PackagingScaffolder.Init(tempDir, "all", "ru-RU");
            var result = MetadataLinter.Lint(tempDir);

            Assert.False(result.IsSuccess);
            Assert.True(result.Errors.Count > 0);
            Assert.Contains(result.Errors, e => e.Contains("Обнаружен незаполненный маркер"));
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
    public void LintPassesWhenAllPlaceholdersAndLimitsAreValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var androidDir = Path.Combine(tempDir, "store", "android", "ru-RU");
            _ = Directory.CreateDirectory(androidDir);

            File.WriteAllText(Path.Combine(androidDir, "title.txt"), "Мое Приложение");
            File.WriteAllText(Path.Combine(androidDir, "short_description.txt"), "Отличное быстрое приложение для работы.");
            File.WriteAllText(Path.Combine(androidDir, "full_description.txt"), "Подробное качественное описание приложения без шаблонных слов.");
            File.WriteAllBytes(Path.Combine(androidDir, "icon.png"), ImageTemplateGenerator.GeneratePng(512, 512));
            File.WriteAllBytes(Path.Combine(androidDir, "feature_graphic.png"), ImageTemplateGenerator.GeneratePng(1024, 500));

            var screensDir = Path.Combine(androidDir, "phoneScreenshots");
            _ = Directory.CreateDirectory(screensDir);
            File.WriteAllBytes(Path.Combine(screensDir, "01.png"), ImageTemplateGenerator.GeneratePng(1080, 1920));
            File.WriteAllBytes(Path.Combine(screensDir, "02.png"), ImageTemplateGenerator.GeneratePng(1080, 1920));

            var result = MetadataLinter.Lint(tempDir);
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);
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
    public void LintFailsWhenTitleExceedsThirtyCharsInAndroid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var androidDir = Path.Combine(tempDir, "store", "android", "ru-RU");
            _ = Directory.CreateDirectory(androidDir);

            File.WriteAllText(Path.Combine(androidDir, "title.txt"), "Очень длинное название приложения больше 30 знаков");
            File.WriteAllText(Path.Combine(androidDir, "full_description.txt"), "Описание");
            File.WriteAllBytes(Path.Combine(androidDir, "icon.png"), ImageTemplateGenerator.GeneratePng(512, 512));

            var result = MetadataLinter.Lint(tempDir);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Errors, e => e.Contains("превышает лимит Google Play"));
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
    public void LintFailsWhenWindowsIconHasWrongDimensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var winDir = Path.Combine(tempDir, "store", "windows", "ru-RU");
            _ = Directory.CreateDirectory(winDir);

            File.WriteAllText(Path.Combine(winDir, "title.txt"), "Obxodka");
            File.WriteAllText(Path.Combine(winDir, "full_description.txt"), "Описание");
            File.WriteAllBytes(Path.Combine(winDir, "icon.png"), ImageTemplateGenerator.GeneratePng(200, 200));

            var result = MetadataLinter.Lint(tempDir);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Errors, e => e.Contains("icon.png") && e.Contains("300x300"));
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
    public void LintFailsWhenWindowsBoxArtHasWrongDimensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var winDir = Path.Combine(tempDir, "store", "windows", "ru-RU");
            _ = Directory.CreateDirectory(winDir);

            File.WriteAllText(Path.Combine(winDir, "title.txt"), "Obxodka");
            File.WriteAllText(Path.Combine(winDir, "full_description.txt"), "Описание");
            File.WriteAllBytes(Path.Combine(winDir, "boxart.png"), ImageTemplateGenerator.GeneratePng(500, 500));

            var result = MetadataLinter.Lint(tempDir);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Errors, e => e.Contains("boxart.png") && e.Contains("1080x1080"));
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
    public void LintFailsWhenWindowsPosterHasWrongDimensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var winDir = Path.Combine(tempDir, "store", "windows", "ru-RU");
            _ = Directory.CreateDirectory(winDir);

            File.WriteAllText(Path.Combine(winDir, "title.txt"), "Obxodka");
            File.WriteAllText(Path.Combine(winDir, "full_description.txt"), "Описание");
            File.WriteAllBytes(Path.Combine(winDir, "poster.png"), ImageTemplateGenerator.GeneratePng(500, 500));

            var result = MetadataLinter.Lint(tempDir);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Errors, e => e.Contains("poster.png") && e.Contains("720x1080"));
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
    public void LintPassesWhenWindowsAssetsAreValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var winDir = Path.Combine(tempDir, "store", "windows", "ru-RU");
            _ = Directory.CreateDirectory(winDir);

            File.WriteAllText(Path.Combine(winDir, "title.txt"), "Obxodka");
            File.WriteAllText(Path.Combine(winDir, "full_description.txt"), "Описание приложения для Windows");
            File.WriteAllBytes(Path.Combine(winDir, "icon.png"), ImageTemplateGenerator.GeneratePng(300, 300));
            File.WriteAllBytes(Path.Combine(winDir, "boxart.png"), ImageTemplateGenerator.GeneratePng(1080, 1080));
            File.WriteAllBytes(Path.Combine(winDir, "poster.png"), ImageTemplateGenerator.GeneratePng(720, 1080));
            File.WriteAllBytes(Path.Combine(winDir, "banner.png"), ImageTemplateGenerator.GeneratePng(1920, 1080));

            var result = MetadataLinter.Lint(tempDir);
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);
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
    public void LintFailsWhenWindowsTrailerHasNoThumbnail()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var winDir = Path.Combine(tempDir, "store", "windows", "ru-RU");
            var trailerDir = Path.Combine(winDir, "trailers");
            _ = Directory.CreateDirectory(trailerDir);

            File.WriteAllText(Path.Combine(winDir, "title.txt"), "Obxodka");
            File.WriteAllText(Path.Combine(winDir, "full_description.txt"), "Описание");
            File.WriteAllBytes(Path.Combine(trailerDir, "trailer.mp4"), [0, 0, 0, 8, 0x66, 0x74, 0x79, 0x70]); // dummy mp4

            var result = MetadataLinter.Lint(tempDir);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Errors, e => e.Contains("trailer_thumbnail.png"));
            Assert.Contains(result.Warnings, w => w.Contains("возрастных рейтингов"));
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
