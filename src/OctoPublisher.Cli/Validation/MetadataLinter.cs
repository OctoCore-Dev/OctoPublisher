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
            var info = new FileInfo(iconPath);
            if (info.Length > 5 * 1024 * 1024)
            {
                result.Errors.Add($"[Windows/{locale}] Логотип 'icon.png' превышает лимит в 5 МБ (текущий размер: {info.Length / 1048576.0:0.0} МБ).");
            }

            var dims = ImageTemplateGenerator.ReadPngDimensions(iconPath);
            if (dims == null)
            {
                result.Errors.Add($"[Windows/{locale}] Логотип 'icon.png' поврежден или не является валидным PNG файлом.");
            }
            else if (dims.Value.Width != 300 || dims.Value.Height != 300)
            {
                result.Errors.Add($"[Windows/{locale}] Значок-плитка 'icon.png' должна иметь строгое разрешение 300x300 (обнаружено: {dims.Value.Width}x{dims.Value.Height}).");
            }
        }

        var boxArtPath = Path.Combine(dir, "boxart.png");
        if (File.Exists(boxArtPath))
        {
            var info = new FileInfo(boxArtPath);
            if (info.Length > 5 * 1024 * 1024)
            {
                result.Errors.Add($"[Windows/{locale}] Обложка 'boxart.png' превышает лимит в 5 МБ (текущий размер: {info.Length / 1048576.0:0.0} МБ).");
            }

            var dims = ImageTemplateGenerator.ReadDimensions(boxArtPath);
            if (dims == null)
            {
                result.Errors.Add($"[Windows/{locale}] Обложка 'boxart.png' повреждена или не является валидным изображением.");
            }
            else if (!((dims.Value.Width == 1080 && dims.Value.Height == 1080) || (dims.Value.Width == 2160 && dims.Value.Height == 2160)))
            {
                result.Errors.Add($"[Windows/{locale}] Квадратная обложка 1:1 'boxart.png' должна иметь разрешение 1080x1080 или 2160x2160 (обнаружено: {dims.Value.Width}x{dims.Value.Height}).");
            }
        }

        var posterPath = Path.Combine(dir, "poster.png");
        if (File.Exists(posterPath))
        {
            var info = new FileInfo(posterPath);
            if (info.Length > 5 * 1024 * 1024)
            {
                result.Errors.Add($"[Windows/{locale}] Постер 'poster.png' превышает лимит в 5 МБ (текущий размер: {info.Length / 1048576.0:0.0} МБ).");
            }

            var dims = ImageTemplateGenerator.ReadDimensions(posterPath);
            if (dims == null)
            {
                result.Errors.Add($"[Windows/{locale}] Постер 'poster.png' поврежден или не является валидным изображением.");
            }
            else if (!((dims.Value.Width == 720 && dims.Value.Height == 1080) || (dims.Value.Width == 1440 && dims.Value.Height == 2160)))
            {
                result.Errors.Add($"[Windows/{locale}] Постер 9:16 'poster.png' должен иметь разрешение 720x1080 или 1440x2160 (обнаружено: {dims.Value.Width}x{dims.Value.Height}).");
            }
        }

        var bannerPath = Path.Combine(dir, "banner.png");
        if (File.Exists(bannerPath))
        {
            var info = new FileInfo(bannerPath);
            if (info.Length > 5 * 1024 * 1024)
            {
                result.Errors.Add($"[Windows/{locale}] Баннер 'banner.png' превышает лимит в 5 МБ (текущий размер: {info.Length / 1048576.0:0.0} МБ).");
            }

            var dims = ImageTemplateGenerator.ReadDimensions(bannerPath);
            if (dims != null && (dims.Value.Width != 1920 || dims.Value.Height != 1080))
            {
                result.Warnings.Add($"[Windows/{locale}] Супербаннер 16:9 'banner.png' имеет разрешение {dims.Value.Width}x{dims.Value.Height} (рекомендуется 1920x1080 SuperHeroArt).");
            }
        }

        var keyArtPath = Path.Combine(dir, "branded_key_art.png");
        if (File.Exists(keyArtPath))
        {
            var info = new FileInfo(keyArtPath);
            if (info.Length > 5 * 1024 * 1024)
            {
                result.Errors.Add($"[Windows/{locale}] Заглавная иллюстрация Xbox 'branded_key_art.png' превышает лимит в 5 МБ (текущий размер: {info.Length / 1048576.0:0.0} МБ).");
            }

            var dims = ImageTemplateGenerator.ReadDimensions(keyArtPath);
            if (dims == null)
            {
                result.Errors.Add($"[Windows/{locale}] Заглавная иллюстрация Xbox 'branded_key_art.png' повреждена или не является валидным изображением.");
            }
            else if (dims.Value.Width != 584 || dims.Value.Height != 800)
            {
                result.Errors.Add($"[Windows/{locale}] Заглавная иллюстрация Xbox 'branded_key_art.png' должна иметь строгое разрешение 584x800 (обнаружено: {dims.Value.Width}x{dims.Value.Height}).");
            }
        }

        var screenshotsDir = Path.Combine(dir, "screenshots");
        if (Directory.Exists(screenshotsDir))
        {
            var screens = Directory.GetFiles(screenshotsDir, "*.png")
                .Concat(Directory.GetFiles(screenshotsDir, "*.jpg"))
                .Concat(Directory.GetFiles(screenshotsDir, "*.jpeg"))
                .ToArray();

            foreach (var screen in screens)
            {
                var sInfo = new FileInfo(screen);
                if (sInfo.Length > 5 * 1024 * 1024)
                {
                    result.Errors.Add($"[Windows/{locale}] Скриншот '{Path.GetFileName(screen)}' превышает лимит в 5 МБ.");
                }

                var dims = ImageTemplateGenerator.ReadDimensions(screen);
                if (dims != null && (dims.Value.Width != 1920 || dims.Value.Height != 1080))
                {
                    result.Warnings.Add($"[Windows/{locale}] Скриншот '{Path.GetFileName(screen)}' имеет разрешение {dims.Value.Width}x{dims.Value.Height} (рекомендуется строго 1920x1080 16:9).");
                }
            }
        }

        //валидация видеотрейлера
        LintWindowsTrailer(dir, locale, result);

        LintPrivacy(dir, locale, "Windows", result);
    }

    private static void LintWindowsTrailer(string dir, string locale, LinterResult result)
    {
        var trailersDir = Path.Combine(dir, "trailers");
        var searchDir = Directory.Exists(trailersDir) ? trailersDir : dir;

        if (!Directory.Exists(searchDir))
        {
            return;
        }

        //проверка недопустимых видеоформатов в папке трейлеров
        if (Directory.Exists(trailersDir))
        {
            var invalidVideos = Directory.GetFiles(trailersDir, "*.*")
                .Where(f => f.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".flv", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase));

            foreach (var inv in invalidVideos)
            {
                result.Errors.Add($"[Windows/{locale}/Trailer] Формат файла '{Path.GetFileName(inv)}' не поддерживается Microsoft Store. Допустимы только MP4 или MOV.");
            }
        }

        var videoFiles = Directory.GetFiles(searchDir, "*.mp4")
            .Concat(Directory.GetFiles(searchDir, "*.mov"))
            .ToArray();

        if (videoFiles.Length == 0)
        {
            return;
        }

        foreach (var videoFile in videoFiles)
        {
            var fileName = Path.GetFileName(videoFile);
            var info = new FileInfo(videoFile);

            //проверка размера: до 2 ГБ
            const long maxVideoBytes = 2L * 1024 * 1024 * 1024;
            if (info.Length > maxVideoBytes)
            {
                result.Errors.Add($"[Windows/{locale}/Trailer] Трейлер '{fileName}' превышает максимальный лимит в 2 ГБ (текущий: {info.Length / (1024.0 * 1024 * 1024):0.0} ГБ).");
            }

            //проверка разрешения: строго 1920x1080
            var dims = VideoMetadataReader.ReadDimensions(videoFile);
            if (dims == null)
            {
                result.Errors.Add($"[Windows/{locale}/Trailer] Не удалось прочитать видеопоток '{fileName}'. Убедитесь, что файл является валидным MP4 или MOV контейнером с видеодорожкой 1920x1080.");
            }
            else if (dims.Value.Width != 1920 || dims.Value.Height != 1080)
            {
                result.Errors.Add($"[Windows/{locale}/Trailer] Трейлер '{fileName}' должен иметь строгое разрешение 1920x1080 (обнаружено: {dims.Value.Width}x{dims.Value.Height}).");
            }

            //проверка обложки трейлера: строго 1920x1080 PNG
            var thumbFile = Path.Combine(Path.GetDirectoryName(videoFile)!, "trailer_thumbnail.png");
            if (!File.Exists(thumbFile))
            {
                thumbFile = Path.Combine(Path.GetDirectoryName(videoFile)!, "thumbnail.png");
            }

            if (!File.Exists(thumbFile))
            {
                result.Errors.Add($"[Windows/{locale}/Trailer] Для трейлера '{fileName}' отсутствует обязательная обложка 'trailer_thumbnail.png' (строго 1920x1080 PNG).");
            }
            else
            {
                var thumbDims = ImageTemplateGenerator.ReadPngDimensions(thumbFile);
                if (thumbDims == null)
                {
                    result.Errors.Add($"[Windows/{locale}/Trailer] Обложка трейлера '{Path.GetFileName(thumbFile)}' повреждена или не является PNG.");
                }
                else if (thumbDims.Value.Width != 1920 || thumbDims.Value.Height != 1080)
                {
                    result.Errors.Add($"[Windows/{locale}/Trailer] Обложка трейлера '{Path.GetFileName(thumbFile)}' должна иметь строгое разрешение 1920x1080 (обнаружено: {thumbDims.Value.Width}x{thumbDims.Value.Height}).");
                }
            }

            //проверка заголовка трейлера: до 255 символов
            var titleFile = Path.Combine(Path.GetDirectoryName(videoFile)!, "trailer_title.txt");
            if (!File.Exists(titleFile))
            {
                titleFile = Path.Combine(Path.GetDirectoryName(videoFile)!, "title.txt");
            }

            if (File.Exists(titleFile))
            {
                var titleText = File.ReadAllText(titleFile).Trim();
                if (titleText.Length > 255)
                {
                    result.Errors.Add($"[Windows/{locale}/Trailer] Заголовок трейлера в '{Path.GetFileName(titleFile)}' превышает лимит в 255 знаков (текущая длина: {titleText.Length}).");
                }
            }

            //проверка дополнительных файлов: субтитры (.vtt < 50 МБ), аудиоописание (.mp3 < 500 МБ)
            var parentDir = Path.GetDirectoryName(videoFile)!;
            foreach (var vtt in Directory.GetFiles(parentDir, "*.vtt"))
            {
                if (new FileInfo(vtt).Length > 50L * 1024 * 1024)
                {
                    result.Errors.Add($"[Windows/{locale}/Trailer] Файл субтитров '{Path.GetFileName(vtt)}' превышает лимит Microsoft Store в 50 МБ.");
                }
            }

            foreach (var mp3 in Directory.GetFiles(parentDir, "*.mp3"))
            {
                if (new FileInfo(mp3).Length > 500L * 1024 * 1024)
                {
                    result.Errors.Add($"[Windows/{locale}/Trailer] Файл аудиоописания '{Path.GetFileName(mp3)}' превышает лимит Microsoft Store в 500 МБ.");
                }
            }

            //предупреждение против встроенных возрастных рейтингов
            result.Warnings.Add($"[Windows/{locale}/Trailer] Убедитесь, что внутри видео '{fileName}' нет встроенных графических значков возрастных рейтингов (12+, 16+, PEGI), иначе Microsoft Store отклонит публикацию.");
        }
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
