namespace OctoPublisher.Cli.Validation;

public sealed class LinterResult
{
    public int ErrorCount => Errors.Count;
    public int WarningCount => Warnings.Count;
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool IsSuccess => ErrorCount == 0;
}

public static class MetadataLinter
{
    public static LinterResult Lint(string baseDir, bool strict = false)
    {
        var result = new LinterResult();

        if (!Directory.Exists(baseDir))
        {
            result.Errors.Add($"Каталог метаданных не найден: '{baseDir}'");
            return result;
        }

        LintWhatsNew(baseDir, result);
        LintAndroidStore(baseDir, result);
        LintWindowsStore(baseDir, result);

        if (strict && result.WarningCount > 0)
        {
            result.Errors.AddRange(result.Warnings);
        }

        return result;
    }

    private static void LintWhatsNew(string baseDir, LinterResult result)
    {
        var whatsNewDir = Path.Combine(baseDir, "whatsnew");
        if (!Directory.Exists(whatsNewDir))
        {
            return;
        }

        var files = Directory.GetFiles(whatsNewDir, "whatsnew-*.txt");
        if (files.Length == 0)
        {
            files = Directory.GetFiles(whatsNewDir, "whatsnew-*");
        }

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file).Trim();

            if (content.Length == 0)
            {
                result.Errors.Add($"[WhatsNew] Файл '{fileName}' пуст.");
                continue;
            }

            if (content.Length > LocaleRegistry.WhatsNewMax)
            {
                result.Errors.Add($"[WhatsNew] Файл '{fileName}' превышает лимит в {LocaleRegistry.WhatsNewMax} знаков (текущая длина: {content.Length}).");
            }

            CheckPlaceholders(file, content, result);
        }
    }

    private static void LintAndroidStore(string baseDir, LinterResult result)
    {
        var androidDir = Path.Combine(baseDir, "store", "android");
        if (!Directory.Exists(androidDir))
        {
            return;
        }

        var localeDirs = Directory.GetDirectories(androidDir);
        if (localeDirs.Length == 0)
        {
            result.Warnings.Add("[Android] В каталоге 'store/android' не обнаружено языковых папок.");
            return;
        }

        foreach (var dir in localeDirs)
        {
            var locale = Path.GetFileName(dir);
            LintAndroidLocale(dir, locale, result);
        }
    }

    private static void LintAndroidLocale(string dir, string locale, LinterResult result)
    {
        var titlePath = Path.Combine(dir, "title.txt");
        if (!File.Exists(titlePath))
        {
            result.Errors.Add($"[Android/{locale}] Отсутствует обязательный файл 'title.txt'.");
        }
        else
        {
            var title = File.ReadAllText(titlePath).Trim();
            if (title.Length == 0)
            {
                result.Errors.Add($"[Android/{locale}] Файл 'title.txt' пуст.");
            }
            else if (title.Length > LocaleRegistry.PlayTitleMax)
            {
                result.Errors.Add($"[Android/{locale}] Заголовок в 'title.txt' превышает лимит Google Play в {LocaleRegistry.PlayTitleMax} знаков (текущая длина: {title.Length}).");
            }
            CheckPlaceholders(titlePath, title, result);
        }

        var shortDescPath = Path.Combine(dir, "short_description.txt");
        if (File.Exists(shortDescPath))
        {
            var shortDesc = File.ReadAllText(shortDescPath).Trim();
            if (shortDesc.Length > LocaleRegistry.PlayShortDescMax)
            {
                result.Errors.Add($"[Android/{locale}] Краткое описание в 'short_description.txt' превышает лимит Google Play в {LocaleRegistry.PlayShortDescMax} знаков (текущая длина: {shortDesc.Length}).");
            }
            CheckPlaceholders(shortDescPath, shortDesc, result);
        }

        var fullDescPath = Path.Combine(dir, "full_description.txt");
        if (!File.Exists(fullDescPath))
        {
            result.Errors.Add($"[Android/{locale}] Отсутствует обязательный файл 'full_description.txt'.");
        }
        else
        {
            var fullDesc = File.ReadAllText(fullDescPath).Trim();
            if (fullDesc.Length == 0)
            {
                result.Errors.Add($"[Android/{locale}] Файл 'full_description.txt' пуст.");
            }
            else if (fullDesc.Length > LocaleRegistry.PlayFullDescMax)
            {
                result.Errors.Add($"[Android/{locale}] Полное описание в 'full_description.txt' превышает лимит Google Play в {LocaleRegistry.PlayFullDescMax} знаков (текущая длина: {fullDesc.Length}).");
            }
            CheckPlaceholders(fullDescPath, fullDesc, result);
        }

        var iconPath = Path.Combine(dir, "icon.png");
        if (!File.Exists(iconPath))
        {
            result.Errors.Add($"[Android/{locale}] Отсутствует иконка 'icon.png'.");
        }
        else
        {
            var dims = ImageTemplateGenerator.ReadPngDimensions(iconPath);
            if (dims == null)
            {
                result.Errors.Add($"[Android/{locale}] Иконка 'icon.png' повреждена или не является валидным PNG файлом.");
            }
            else if (dims.Value.Width != 512 || dims.Value.Height != 512)
            {
                result.Errors.Add($"[Android/{locale}] Иконка 'icon.png' должна иметь строгое разрешение 512x512 (обнаружено: {dims.Value.Width}x{dims.Value.Height}).");
            }
        }

        var bannerPath = Path.Combine(dir, "feature_graphic.png");
        if (File.Exists(bannerPath))
        {
            var dims = ImageTemplateGenerator.ReadPngDimensions(bannerPath);
            if (dims != null && (dims.Value.Width != 1024 || dims.Value.Height != 500))
            {
                result.Errors.Add($"[Android/{locale}] Промо-баннер 'feature_graphic.png' должен иметь строгое разрешение 1024x500 (обнаружено: {dims.Value.Width}x{dims.Value.Height}).");
            }
        }

        var screenshotsDir = Path.Combine(dir, "phoneScreenshots");
        if (Directory.Exists(screenshotsDir))
        {
            var screens = Directory.GetFiles(screenshotsDir, "*.png")
                .Concat(Directory.GetFiles(screenshotsDir, "*.jpg"))
                .Concat(Directory.GetFiles(screenshotsDir, "*.jpeg"))
                .ToArray();

            if (screens.Length is > 0 and < 2)
            {
                result.Warnings.Add($"[Android/{locale}] Google Play рекомендует загружать минимум 2 скриншота (найдено: {screens.Length}).");
            }

            foreach (var screen in screens)
            {
                var dims = ImageTemplateGenerator.ReadPngDimensions(screen);
                if (dims != null)
                {
                    var (w, h) = dims.Value;
                    if (w < 320 || h < 320 || w > 3840 || h > 3840)
                    {
                        result.Errors.Add($"[Android/{locale}] Скриншот '{Path.GetFileName(screen)}' выходит за допустимые границы 320-3840px (обнаружено: {w}x{h}).");
                    }
                    var ratio = (double)Math.Max(w, h) / Math.Min(w, h);
                    if (ratio > 2.3)
                    {
                        result.Warnings.Add($"[Android/{locale}] Соотношение сторон скриншота '{Path.GetFileName(screen)}' ({w}x{h}) превышает 2:1.");
                    }
                }
            }
        }

        LintPrivacy(dir, locale, "Android", result);
    }

    private static void LintWindowsStore(string baseDir, LinterResult result)
    {
        var windowsDir = Path.Combine(baseDir, "store", "windows");
        if (!Directory.Exists(windowsDir))
        {
            return;
        }

        var localeDirs = Directory.GetDirectories(windowsDir);
        if (localeDirs.Length == 0)
        {
            result.Warnings.Add("[Windows] В каталоге 'store/windows' не обнаружено языковых папок.");
            return;
        }

        foreach (var dir in localeDirs)
        {
            var locale = Path.GetFileName(dir);
            LintWindowsLocale(dir, locale, result);
        }
    }

    private static void LintWindowsLocale(string dir, string locale, LinterResult result)
    {
        var titlePath = Path.Combine(dir, "title.txt");
        if (File.Exists(titlePath))
        {
            var title = File.ReadAllText(titlePath).Trim();
            if (title.Length > LocaleRegistry.MStoreTitleMax)
            {
                result.Errors.Add($"[Windows/{locale}] Название в 'title.txt' превышает лимит в {LocaleRegistry.MStoreTitleMax} знаков (текущая длина: {title.Length}).");
            }
            CheckPlaceholders(titlePath, title, result);
        }

        var fullDescPath = Path.Combine(dir, "full_description.txt");
        if (!File.Exists(fullDescPath))
        {
            fullDescPath = Path.Combine(dir, "description.txt");
        }

        if (File.Exists(fullDescPath))
        {
            var fullDesc = File.ReadAllText(fullDescPath).Trim();
            if (fullDesc.Length == 0)
            {
                result.Errors.Add($"[Windows/{locale}] Файл описания '{Path.GetFileName(fullDescPath)}' пуст.");
            }
            else if (fullDesc.Length > LocaleRegistry.MStoreFullDescMax)
            {
                result.Errors.Add($"[Windows/{locale}] Описание в '{Path.GetFileName(fullDescPath)}' превышает лимит в {LocaleRegistry.MStoreFullDescMax} знаков (текущая длина: {fullDesc.Length}).");
            }
            CheckPlaceholders(fullDescPath, fullDesc, result);
        }

        var featuresPath = Path.Combine(dir, "features.txt");
        if (File.Exists(featuresPath))
        {
            var lines = File.ReadAllLines(featuresPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (lines.Length > LocaleRegistry.MStoreMaxFeatures)
            {
                result.Errors.Add($"[Windows/{locale}] Список 'features.txt' содержит более {LocaleRegistry.MStoreMaxFeatures} строк (текущее: {lines.Length}).");
            }
            foreach (var line in lines)
            {
                if (line.Trim().Length > LocaleRegistry.MStoreFeatureLineMax)
                {
                    result.Errors.Add($"[Windows/{locale}] Строка фичи превышает лимит {LocaleRegistry.MStoreFeatureLineMax} знаков: '{line.Trim()}'");
                }
            }
            CheckPlaceholders(featuresPath, string.Join("\n", lines), result);
        }

        var iconPath = Path.Combine(dir, "icon.png");
        if (File.Exists(iconPath))
        {
            var dims = ImageTemplateGenerator.ReadPngDimensions(iconPath);
            if (dims == null)
            {
                result.Errors.Add($"[Windows/{locale}] Логотип 'icon.png' поврежден или не является валидным PNG файлом.");
            }
            else if (dims.Value.Width != 300 || dims.Value.Height != 300)
            {
                result.Warnings.Add($"[Windows/{locale}] Логотип 'icon.png' имеет нестандартное разрешение {dims.Value.Width}x{dims.Value.Height} (рекомендуется строго 300x300).");
            }
        }

        var bannerPath = Path.Combine(dir, "banner.png");
        if (File.Exists(bannerPath))
        {
            var dims = ImageTemplateGenerator.ReadPngDimensions(bannerPath);
            if (dims != null && (dims.Value.Width != 1920 || dims.Value.Height != 1080))
            {
                result.Warnings.Add($"[Windows/{locale}] Баннер 'banner.png' имеет разрешение {dims.Value.Width}x{dims.Value.Height} (рекомендуется 1920x1080 SuperHeroArt).");
            }
        }

        LintPrivacy(dir, locale, "Windows", result);
    }

    private static void LintPrivacy(string dir, string locale, string platform, LinterResult result)
    {
        var privacyPath = Path.Combine(dir, "privacy.txt");
        if (!File.Exists(privacyPath))
        {
            return;
        }

        var url = File.ReadAllText(privacyPath).Trim();
        if (url.Length == 0)
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            result.Errors.Add($"[{platform}/{locale}] В 'privacy.txt' указан невалидный URL: '{url}'.");
            return;
        }

        if (uri.Host.Contains("example.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Contains("privew.com", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"[{platform}/{locale}] В 'privacy.txt' оставлен шаблонный тестовый адрес: '{url}'. Укажите реальный адрес политики конфиденциальности!");
        }
    }

    private static void CheckPlaceholders(string filePath, string content, LinterResult result)
    {
        var fileName = Path.GetFileName(filePath);
        var dirName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");

        foreach (var marker in LocaleRegistry.PlaceholderMarkers)
        {
            if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"[{dirName}/{fileName}] Обнаружен незаполненный маркер шаблона '{marker}'. Замените заглушку на реальный текст перед публикацией!");
                break;
            }
        }
    }
}
