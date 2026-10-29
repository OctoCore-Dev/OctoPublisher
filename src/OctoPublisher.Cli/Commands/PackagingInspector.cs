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
}
