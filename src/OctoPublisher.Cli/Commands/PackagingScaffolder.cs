namespace OctoPublisher.Cli.Commands;

public static class PackagingScaffolder
{
    public static int Init(string targetDir, string platform, string requestedLocale)
    {
        var locale = LocaleRegistry.NormalizeLocale(requestedLocale);
        AnsiTerminal.Header("OctoPublisher: Инициализация ассетов магазина");
        AnsiTerminal.Step("Инициализация", $"Создание структуры каталогов 'Store as Code' в '{targetDir}' (Основной язык: {locale})...");

        var createdCount = 0;
        var skippedCount = 0;

        var targetPlatform = platform.ToLowerInvariant();
        var initAndroid = targetPlatform is "all" or "android";
        var initWindows = targetPlatform is "all" or "windows";

        void EnsureDir(string relPath)
        {
            var fullPath = Path.Combine(targetDir, relPath);
            if (!Directory.Exists(fullPath))
            {
                _ = Directory.CreateDirectory(fullPath);
            }
        }

        void WriteText(string relPath, string content)
        {
            var fullPath = Path.Combine(targetDir, relPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            if (File.Exists(fullPath))
            {
                skippedCount++;
                return;
            }

            File.WriteAllText(fullPath, content.Trim());
            createdCount++;
        }

        void WriteImage(string relPath, int width, int height)
        {
            var fullPath = Path.Combine(targetDir, relPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            if (File.Exists(fullPath))
            {
                skippedCount++;
                return;
            }

            var pngBytes = ImageTemplateGenerator.GeneratePng(width, height);
            File.WriteAllBytes(fullPath, pngBytes);
            createdCount++;
        }

        WriteText(Path.Combine("whatsnew", $"whatsnew-{locale}.txt"), LocaleRegistry.GetDefaultReleaseNotes(locale));

        if (initAndroid)
        {
            var androidBase = Path.Combine("store", "android", locale);
            WriteText(Path.Combine(androidBase, "title.txt"), LocaleRegistry.GetDefaultTitle(locale));
            WriteText(Path.Combine(androidBase, "short_description.txt"), LocaleRegistry.GetDefaultShortDescription(locale));
            WriteText(Path.Combine(androidBase, "full_description.txt"), LocaleRegistry.GetDefaultFullDescription(locale, isWindows: false));
            WriteText(Path.Combine(androidBase, "privacy.txt"), LocaleRegistry.GetDefaultPrivacyUrl());

            WriteImage(Path.Combine(androidBase, "icon.png"), 512, 512);
            WriteImage(Path.Combine(androidBase, "feature_graphic.png"), 1024, 500);

            var screenDir = Path.Combine(androidBase, "phoneScreenshots");
            EnsureDir(screenDir);
            WriteImage(Path.Combine(screenDir, "01_скриншот_1080x1920.png"), 1080, 1920);
            WriteImage(Path.Combine(screenDir, "02_скриншот_1080x1920.png"), 1080, 1920);
        }

        if (initWindows)
        {
            var winBase = Path.Combine("store", "windows", locale);
            WriteText(Path.Combine(winBase, "title.txt"), LocaleRegistry.GetDefaultTitle(locale));
            WriteText(Path.Combine(winBase, "full_description.txt"), LocaleRegistry.GetDefaultFullDescription(locale, isWindows: true));
            WriteText(Path.Combine(winBase, "features.txt"), LocaleRegistry.GetDefaultFeatures(locale));
            WriteText(Path.Combine(winBase, "release_notes.txt"), LocaleRegistry.GetDefaultReleaseNotes(locale));
            WriteText(Path.Combine(winBase, "privacy.txt"), LocaleRegistry.GetDefaultPrivacyUrl());

            WriteImage(Path.Combine(winBase, "icon.png"), 300, 300);
            WriteImage(Path.Combine(winBase, "banner.png"), 1920, 1080);

            var winScreenDir = Path.Combine(winBase, "screenshots");
            EnsureDir(winScreenDir);
            WriteImage(Path.Combine(winScreenDir, "01_скриншот_1920x1080.png"), 1920, 1080);
        }

        AnsiTerminal.Success($"Инициализация завершена: создано {createdCount} файлов, пропущено существующих: {skippedCount}.");
        AnsiTerminal.Info($"Отредактируйте тексты и замените шаблонные изображения перед запуском 'octopublish test'.");
        return 0;
    }

    public static int AddLocale(string targetDir, string platform, string rawLocale)
    {
        var locale = LocaleRegistry.NormalizeLocale(rawLocale);
        AnsiTerminal.Header($"OctoPublisher: Добавление локали [{locale}]");

        var targetPlatform = platform.ToLowerInvariant();
        var addAndroid = targetPlatform is "all" or "android";
        var addWindows = targetPlatform is "all" or "windows";

        var createdCount = 0;

        void WriteText(string relPath, string content)
        {
            var fullPath = Path.Combine(targetDir, relPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            if (File.Exists(fullPath))
            {
                AnsiTerminal.Warning($"Файл уже существует: '{relPath}'");
                return;
            }

            File.WriteAllText(fullPath, content.Trim());
            createdCount++;
        }

        void WriteImage(string relPath, int width, int height)
        {
            var fullPath = Path.Combine(targetDir, relPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            if (File.Exists(fullPath))
            {
                return;
            }

            var pngBytes = ImageTemplateGenerator.GeneratePng(width, height);
            File.WriteAllBytes(fullPath, pngBytes);
            createdCount++;
        }

        WriteText(Path.Combine("whatsnew", $"whatsnew-{locale}.txt"), LocaleRegistry.GetDefaultReleaseNotes(locale));

        if (addAndroid)
        {
            var androidDir = Path.Combine(targetDir, "store", "android", locale);
            if (Directory.Exists(androidDir))
            {
                AnsiTerminal.Warning($"Языковой пакет Android '{locale}' уже существует в '{androidDir}'.");
            }
            else
            {
                var rel = Path.Combine("store", "android", locale);
                WriteText(Path.Combine(rel, "title.txt"), LocaleRegistry.GetDefaultTitle(locale));
                WriteText(Path.Combine(rel, "short_description.txt"), LocaleRegistry.GetDefaultShortDescription(locale));
                WriteText(Path.Combine(rel, "full_description.txt"), LocaleRegistry.GetDefaultFullDescription(locale, isWindows: false));
                WriteText(Path.Combine(rel, "privacy.txt"), LocaleRegistry.GetDefaultPrivacyUrl());

                WriteImage(Path.Combine(rel, "icon.png"), 512, 512);
                WriteImage(Path.Combine(rel, "feature_graphic.png"), 1024, 500);

                var screenDir = Path.Combine(androidDir, "phoneScreenshots");
                if (!Directory.Exists(screenDir))
                {
                    _ = Directory.CreateDirectory(screenDir);
                }
                WriteImage(Path.Combine(rel, "phoneScreenshots", "01_скриншот_1080x1920.png"), 1080, 1920);
                WriteImage(Path.Combine(rel, "phoneScreenshots", "02_скриншот_1080x1920.png"), 1080, 1920);

                AnsiTerminal.Step("Android", $"Создана языковая структура '{locale}' в '{androidDir}'");
            }
        }

        if (addWindows)
        {
            var winDir = Path.Combine(targetDir, "store", "windows", locale);
            if (Directory.Exists(winDir))
            {
                AnsiTerminal.Warning($"Языковой пакет Windows '{locale}' уже существует в '{winDir}'.");
            }
            else
            {
                var rel = Path.Combine("store", "windows", locale);
                WriteText(Path.Combine(rel, "title.txt"), LocaleRegistry.GetDefaultTitle(locale));
                WriteText(Path.Combine(rel, "full_description.txt"), LocaleRegistry.GetDefaultFullDescription(locale, isWindows: true));
                WriteText(Path.Combine(rel, "features.txt"), LocaleRegistry.GetDefaultFeatures(locale));
                WriteText(Path.Combine(rel, "release_notes.txt"), LocaleRegistry.GetDefaultReleaseNotes(locale));
                WriteText(Path.Combine(rel, "privacy.txt"), LocaleRegistry.GetDefaultPrivacyUrl());

                WriteImage(Path.Combine(rel, "icon.png"), 300, 300);
                WriteImage(Path.Combine(rel, "banner.png"), 1920, 1080);

                var winScreenDir = Path.Combine(winDir, "screenshots");
                if (!Directory.Exists(winScreenDir))
                {
                    _ = Directory.CreateDirectory(winScreenDir);
                }
                WriteImage(Path.Combine(rel, "screenshots", "01_скриншот_1920x1080.png"), 1920, 1080);

                AnsiTerminal.Step("Windows", $"Создана языковая структура '{locale}' в '{winDir}'");
            }
        }

        AnsiTerminal.Success($"Добавлена локаль '{locale}' (создано новых файлов: {createdCount}).");
        return 0;
    }
}
