namespace OctoPublisher.Cli.Commands;

public static class PackagingInspector
{
    public static int List(string baseDir)
    {
        AnsiTerminal.Header($"OctoPublisher: Инспекция ассетов ['{baseDir}']");

        if (!Directory.Exists(baseDir))
        {
            AnsiTerminal.Error($"Каталог не найден: '{baseDir}'. Запустите 'octopublish init' для его создания.");
            return 1;
        }

        var androidDir = Path.Combine(baseDir, "store", "android");
        var windowsDir = Path.Combine(baseDir, "store", "windows");

        var hasRows = false;
        Console.WriteLine($"{AnsiTerminal.Dim}{"Платформа",-10} {"Локаль",-8} {"Название",-16} {"Описание",-16} {"Иконка",-12} {"Баннер",-12} {"Скриншоты",-12} {"Статус"}{AnsiTerminal.Reset}");
        Console.WriteLine(new string('-', 98));

        if (Directory.Exists(androidDir))
        {
            foreach (var dir in Directory.GetDirectories(androidDir))
            {
                hasRows = true;
                var locale = Path.GetFileName(dir);
                PrintAndroidRow(dir, locale);
            }
        }

        if (Directory.Exists(windowsDir))
        {
            foreach (var dir in Directory.GetDirectories(windowsDir))
            {
                hasRows = true;
                var locale = Path.GetFileName(dir);
                PrintWindowsRow(dir, locale);
            }
        }

        if (!hasRows)
        {
            AnsiTerminal.Warning($"В каталоге '{baseDir}' не найдено языковых папок Android или Windows.");
        }

        Console.WriteLine();
        return 0;
    }

    private static void PrintAndroidRow(string dir, string locale)
    {
        var titleFile = Path.Combine(dir, "title.txt");
        var titleStatus = File.Exists(titleFile)
            ? $"{File.ReadAllText(titleFile).Trim().Length} зн."
            : $"{AnsiTerminal.Red}нет{AnsiTerminal.Reset}";

        var descFile = Path.Combine(dir, "full_description.txt");
        var descStatus = File.Exists(descFile)
            ? $"{File.ReadAllText(descFile).Trim().Length} зн."
            : $"{AnsiTerminal.Red}нет{AnsiTerminal.Reset}";

        var iconFile = Path.Combine(dir, "icon.png");
        var iconDims = ImageTemplateGenerator.ReadPngDimensions(iconFile);
        var iconStatus = iconDims != null
            ? $"{iconDims.Value.Width}x{iconDims.Value.Height}"
            : (File.Exists(iconFile) ? $"{AnsiTerminal.Yellow}битый{AnsiTerminal.Reset}" : $"{AnsiTerminal.Red}нет{AnsiTerminal.Reset}");

        var bannerFile = Path.Combine(dir, "feature_graphic.png");
        var bannerDims = ImageTemplateGenerator.ReadPngDimensions(bannerFile);
        var bannerStatus = bannerDims != null
            ? $"{bannerDims.Value.Width}x{bannerDims.Value.Height}"
            : (File.Exists(bannerFile) ? $"{AnsiTerminal.Yellow}битый{AnsiTerminal.Reset}" : $"{AnsiTerminal.Muted}-{AnsiTerminal.Reset}");

        var screenDir = Path.Combine(dir, "phoneScreenshots");
        var screenCount = Directory.Exists(screenDir) ? Directory.GetFiles(screenDir).Length : 0;
        var screenStatus = $"{screenCount} шт.";

        var isReady = File.Exists(titleFile) && File.Exists(descFile) && iconDims != null && screenCount >= 2;
        var statusLabel = isReady
            ? $"{AnsiTerminal.Green}[Готово]{AnsiTerminal.Reset}"
            : $"{AnsiTerminal.Yellow}[Не завершено]{AnsiTerminal.Reset}";

        Console.WriteLine($"{"Android",-10} {locale,-8} {titleStatus,-16} {descStatus,-16} {iconStatus,-12} {bannerStatus,-12} {screenStatus,-12} {statusLabel}");
    }

    private static void PrintWindowsRow(string dir, string locale)
    {
        var titleFile = Path.Combine(dir, "title.txt");
        var titleStatus = File.Exists(titleFile)
            ? $"{File.ReadAllText(titleFile).Trim().Length} зн."
            : $"{AnsiTerminal.Muted}-{AnsiTerminal.Reset}";

        var descFile = Path.Combine(dir, "full_description.txt");
        if (!File.Exists(descFile))
        {
            descFile = Path.Combine(dir, "description.txt");
        }
        var descStatus = File.Exists(descFile)
            ? $"{File.ReadAllText(descFile).Trim().Length} зн."
            : $"{AnsiTerminal.Red}нет{AnsiTerminal.Reset}";

        var iconFile = Path.Combine(dir, "icon.png");
        var iconDims = ImageTemplateGenerator.ReadPngDimensions(iconFile);
        var iconStatus = iconDims != null
            ? $"{iconDims.Value.Width}x{iconDims.Value.Height}"
            : (File.Exists(iconFile) ? $"{AnsiTerminal.Yellow}битый{AnsiTerminal.Reset}" : $"{AnsiTerminal.Muted}-{AnsiTerminal.Reset}");

        var bannerFile = Path.Combine(dir, "banner.png");
        var bannerDims = ImageTemplateGenerator.ReadPngDimensions(bannerFile);
        var bannerStatus = bannerDims != null
            ? $"{bannerDims.Value.Width}x{bannerDims.Value.Height}"
            : (File.Exists(bannerFile) ? $"{AnsiTerminal.Yellow}битый{AnsiTerminal.Reset}" : $"{AnsiTerminal.Muted}-{AnsiTerminal.Reset}");

        var screenDir = Path.Combine(dir, "screenshots");
        var screenCount = Directory.Exists(screenDir) ? Directory.GetFiles(screenDir).Length : 0;
        var screenStatus = $"{screenCount} шт.";

        var isReady = File.Exists(descFile);
        var statusLabel = isReady
            ? $"{AnsiTerminal.Green}[Готово]{AnsiTerminal.Reset}"
            : $"{AnsiTerminal.Yellow}[Не завершено]{AnsiTerminal.Reset}";

        Console.WriteLine($"{"Windows",-10} {locale,-8} {titleStatus,-16} {descStatus,-16} {iconStatus,-12} {bannerStatus,-12} {screenStatus,-12} {statusLabel}");
    }

    public static int PrintAssetsGuide(string[] args)
    {
        AnsiTerminal.Header("OctoPublisher: Справочник графических ассетов и слотов витрин");
        Console.WriteLine("Полная спецификация файлов, разрешений и соответствующих слотов в панелях разработчика.");
        Console.WriteLine();

        AnsiTerminal.Step("Windows & Xbox", "Microsoft Partner Center (папка 'store/windows/<locale>/'):");
        Console.WriteLine($"{AnsiTerminal.Dim}{"Имя файла",-20} {"Слот в Partner Center",-30} {"Разрешение",-16} {"Лимит",-8} {"Назначение"}{AnsiTerminal.Reset}");
        Console.WriteLine(new string('-', 105));
        Console.WriteLine($"{"banner.png",-20} {"Супербаннер 16:9 / Xbox Hero",-30} {"1920x1080",-16} {"5 МБ",-8} {"Главная шапка страницы продукта и витрины Xbox"}");
        Console.WriteLine($"{"boxart.png",-20} {"Картинка 1:1 / Квадратная Xbox",-30} {"1080x1080",-16} {"5 МБ",-8} {"Большая квадратная обложка в каталоге Store"}");
        Console.WriteLine($"{"poster.png",-20} {"Постер 9:16",-30} {"720x1080",-16} {"5 МБ",-8} {"Вертикальный плакат каталога (требуется для Xbox)"}");
        Console.WriteLine($"{"branded_key_art.png",-20} {"Фирменная заглавная (Xbox)",-30} {"584x800",-16} {"5 МБ",-8} {"Главная заглавная иллюстрация дашборда Xbox"}");
        Console.WriteLine($"{"icon.png",-20} {"Значок-плитка 1:1",-30} {"300x300",-16} {"5 МБ",-8} {"Основная иконка в магазине Windows 10/11"}");
        Console.WriteLine($"{"icon_150.png",-20} {"Плитка 150x150",-30} {"150x150",-16} {"5 МБ",-8} {"Средняя плитка меню Пуск и результаты поиска"}");
        Console.WriteLine($"{"icon_71.png",-20} {"Плитка 71x71",-30} {"71x71",-16} {"5 МБ",-8} {"Малая плитка меню Пуск"}");
        Console.WriteLine($"{"screenshots/*.png",-20} {"Скриншоты рабочего стола",-30} {"1920x1080",-16} {"5 МБ",-8} {"Снимки приложения для ПК (до 10 шт)"}");
        Console.WriteLine($"{"trailers/*.mp4",-20} {"Трейлер (видеоанонс)",-30} {"1920x1080",-16} {"2 ГБ",-8} {"Видеоролик вверху описания Store (опция)"}");
        Console.WriteLine();

        AnsiTerminal.Step("Android", "Google Play Console (папка 'store/android/<locale>/'):");
        Console.WriteLine($"{AnsiTerminal.Dim}{"Имя файла",-22} {"Слот в Google Play",-28} {"Разрешение",-16} {"Лимит",-8} {"Назначение"}{AnsiTerminal.Reset}");
        Console.WriteLine(new string('-', 105));
        Console.WriteLine($"{"icon.png",-22} {"Значок приложения",-28} {"512x512",-16} {"1 МБ",-8} {"Основная иконка в Google Play Store (32-бит PNG)"}");
        Console.WriteLine($"{"feature_graphic.png",-22} {"Главный баннер",-28} {"1024x500",-16} {"15 МБ",-8} {"Промо-баннер вверху карточки в Google Play"}");
        Console.WriteLine($"{"phoneScreenshots/*.png",-22} {"Скриншоты для телефона",-28} {"1080x1920+",-16} {"8 МБ",-8} {"Снимки экранов смартфона (от 2 до 8 шт, 16:9/9:16)"}");
        Console.WriteLine();

        var baseDir = args.Length > 0 ? args[0] : "packaging";
        if (Directory.Exists(baseDir))
        {
            AnsiTerminal.Step("Проверка", $"Текущее состояние файлов в '{baseDir}':");
            AuditLocalAssets(baseDir);
        }

        return 0;
    }

    private static void AuditLocalAssets(string baseDir)
    {
        var winDir = Path.Combine(baseDir, "store", "windows");
        if (Directory.Exists(winDir))
        {
            foreach (var locDir in Directory.GetDirectories(winDir))
            {
                var loc = Path.GetFileName(locDir);
                Console.WriteLine($"  {AnsiTerminal.Bold}[Windows/{loc}]{AnsiTerminal.Reset}");
                CheckFile(locDir, "banner.png", "Супербаннер 16:9", 1920, 1080);
                CheckFile(locDir, "boxart.png", "Квадратная обложка 1:1", 1080, 1080);
                CheckFile(locDir, "poster.png", "Постер 9:16", 720, 1080);
                CheckFile(locDir, "branded_key_art.png", "Заглавная Xbox", 584, 800);
                CheckFile(locDir, "icon.png", "Иконка 1:1", 300, 300);
                CheckFile(locDir, "icon_150.png", "Плитка 150x150", 150, 150);
                CheckFile(locDir, "icon_71.png", "Плитка 71x71", 71, 71);
            }
        }

        var andDir = Path.Combine(baseDir, "store", "android");
        if (Directory.Exists(andDir))
        {
            foreach (var locDir in Directory.GetDirectories(andDir))
            {
                var loc = Path.GetFileName(locDir);
                Console.WriteLine($"  {AnsiTerminal.Bold}[Android/{loc}]{AnsiTerminal.Reset}");
                CheckFile(locDir, "icon.png", "Иконка приложения", 512, 512);
                CheckFile(locDir, "feature_graphic.png", "Главный баннер", 1024, 500);
            }
        }
        Console.WriteLine();
    }

    private static void CheckFile(string dir, string fileName, string title, int expectedW, int expectedH)
    {
        var filePath = Path.Combine(dir, fileName);
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"    {fileName,-22} {title,-26} : {AnsiTerminal.Muted}отсутствует{AnsiTerminal.Reset}");
            return;
        }

        var dims = ImageTemplateGenerator.ReadDimensions(filePath);
        if (dims == null)
        {
            Console.WriteLine($"    {fileName,-22} {title,-26} : {AnsiTerminal.Yellow}битый файл{AnsiTerminal.Reset}");
            return;
        }

        var match = dims.Value.Width == expectedW && dims.Value.Height == expectedH;
        var status = match
            ? $"{AnsiTerminal.Green}{dims.Value.Width}x{dims.Value.Height} [OK]{AnsiTerminal.Reset}"
            : $"{AnsiTerminal.Yellow}{dims.Value.Width}x{dims.Value.Height} (ожидается {expectedW}x{expectedH}){AnsiTerminal.Reset}";

        Console.WriteLine($"    {fileName,-22} {title,-26} : {status}");
    }
}
