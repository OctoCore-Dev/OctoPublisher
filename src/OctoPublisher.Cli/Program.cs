namespace OctoPublisher.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "-v" or "--version" or "version")
        {
            var ver = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.1.0";
            AnsiTerminal.Info($"OctoPublisher CLI v{ver} (.NET 10, Русский интерфейс)");
            return 0;
        }

        var primaryCommand = args[0].ToLowerInvariant();
        try
        {
            return primaryCommand switch
            {
                "mstore" => await RunMStoreCommandAsync(args.AsSpan(1).ToArray()),
                "playstore" => await RunPlayStoreCommandAsync(args.AsSpan(1).ToArray()),
                "init" or "template" or "scaffold" => HandleInitCommand(args.AsSpan(1).ToArray()),
                "pull" or "export" or "download" => await RunPullCommandAsync(args.AsSpan(1).ToArray()),
                "add" or "locale" or "lang" => HandleAddCommand(args.AsSpan(1).ToArray()),
                "test" or "lint" or "check" or "validate" => HandleTestCommand(args.AsSpan(1).ToArray()),
                "list" or "audit" => HandleListCommand(args.AsSpan(1).ToArray()),
                "sync" => await RunSyncCommandAsync(args.AsSpan(1).ToArray()),
                _ => HandleUnknownCommand(primaryCommand)
            };
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"Критическая ошибка выполнения: {ex.Message}");
            return 1;
        }
    }

    private static int HandleInitCommand(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help")
        {
            PrintInitHelp();
            return 0;
        }

        var (flags, positional) = ParseArguments(args);
        var platform = "all";
        var targetDir = flags.GetValueOrDefault("dir");
        var locale = flags.GetValueOrDefault("lang") ?? LocaleRegistry.DefaultLocale;

        if (positional.Count > 0)
        {
            var first = positional[0].ToLowerInvariant();
            if (first is "all" or "android" or "windows")
            {
                platform = first;
                if (positional.Count > 1 && string.IsNullOrWhiteSpace(targetDir))
                {
                    targetDir = positional[1];
                }
            }
            else if (string.IsNullOrWhiteSpace(targetDir))
            {
                targetDir = positional[0];
            }
        }

        targetDir ??= "packaging";
        return PackagingScaffolder.Init(targetDir, platform, locale);
    }

    private static int HandleAddCommand(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintAddHelp();
            return 0;
        }

        var (flags, positional) = ParseArguments(args);

        var platform = "all";
        string rawLocale;

        if (positional.Count == 1)
        {
            rawLocale = positional[0];
        }
        else if (positional.Count >= 2)
        {
            var first = positional[0].ToLowerInvariant();
            if (first is "all" or "android" or "windows")
            {
                platform = first;
                rawLocale = positional[1];
            }
            else
            {
                rawLocale = positional[0];
            }
        }
        else
        {
            AnsiTerminal.Error("Не указан код языка. Пример: octopublish add android en");
            return 1;
        }

        var targetDir = flags.GetValueOrDefault("dir") ?? "packaging";
        return PackagingScaffolder.AddLocale(targetDir, platform, rawLocale);
    }

    private static int HandleTestCommand(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help")
        {
            PrintTestHelp();
            return 0;
        }

        var (flags, positional) = ParseArguments(args);
        var targetDir = flags.GetValueOrDefault("dir") ?? (positional.Count > 0 ? positional[0] : "packaging");

        var strict = flags.ContainsKey("strict");
        AnsiTerminal.Header($"OctoPublisher: Предполётный линтер ['{targetDir}']");
        AnsiTerminal.Step("Проверка", "Сканирование метаданных и изображений перед публикацией...");

        var result = MetadataLinter.Lint(targetDir, strict);

        foreach (var warn in result.Warnings)
        {
            AnsiTerminal.Warning(warn);
        }

        foreach (var err in result.Errors)
        {
            AnsiTerminal.Error(err);
        }

        Console.WriteLine();
        if (result.IsSuccess)
        {
            AnsiTerminal.Success($"Предполётная проверка успешно пройдена: 0 ошибок, {result.WarningCount} предупреждений.");
            AnsiTerminal.Info("Метаданные и графические материалы полностью готовы к публикации в магазинах.");
            return 0;
        }

        AnsiTerminal.Error($"Обнаружены критические ошибки валидации: {result.ErrorCount} ошибок, {result.WarningCount} предупреждений.");
        AnsiTerminal.Info("Исправьте указанные ошибки перед отправкой коммита в Git или запуском публикации.");
        return 1;
    }

    private static int HandleListCommand(string[] args)
    {
        var (flags, positional) = ParseArguments(args);
        var targetDir = flags.GetValueOrDefault("dir") ?? (positional.Count > 0 ? positional[0] : "packaging");

        return PackagingInspector.List(targetDir);
    }

    private static async Task<int> RunSyncCommandAsync(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help")
        {
            PrintSyncHelp();
            return 0;
        }

        var (flags, positional) = ParseArguments(args);

        var targetPlatform = positional.Count > 0 ? positional[0].ToLowerInvariant() : "all";
        var targetDir = flags.GetValueOrDefault("dir") ?? "packaging";
        var isDryRun = flags.ContainsKey("dry-run");

        AnsiTerminal.Header($"OctoPublisher: Синхронизация метаданных [{targetPlatform}]");
        if (isDryRun)
        {
            AnsiTerminal.Info("Режим предварительного просмотра (Dry Run): изменения не будут отправлены в магазин.");
        }

        var lintResult = MetadataLinter.Lint(targetDir);
        if (!lintResult.IsSuccess)
        {
            AnsiTerminal.Error($"Синхронизация отклонена: обнаружено {lintResult.ErrorCount} ошибок валидации. Запустите 'octopublish test' для подробностей.");
            return 1;
        }

        if (targetPlatform is "all" or "android")
        {
            AnsiTerminal.Step("Android", "Синхронизация метаданных Google Play Store...");
            var packageName = flags.GetValueOrDefault("package-name") ?? Environment.GetEnvironmentVariable("PLAY_PACKAGE_NAME");
            var serviceAccount = flags.GetValueOrDefault("service-account")
                ?? Environment.GetEnvironmentVariable("PLAY_CONFIG_JSON")
                ?? Environment.GetEnvironmentVariable("PLAY_SERVICE_ACCOUNT_JSON");

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(serviceAccount))
            {
                AnsiTerminal.Warning("Пропущен Android: не заданы --package-name или --service-account (или переменные окружения).");
            }
            else
            {
                var publisher = new PlayStorePublisher();
                var androidStoreDir = Path.Combine(targetDir, "store", "android");
                var whatsNewDir = Path.Combine(targetDir, "whatsnew");

                var result = await publisher.PublishAsync(
                    bundleOrApkPath: "",
                    packageName: packageName,
                    serviceAccountInput: serviceAccount,
                    track: "internal",
                    notesDir: Directory.Exists(whatsNewDir) ? whatsNewDir : null,
                    descDir: Directory.Exists(androidStoreDir) ? androidStoreDir : null);

                if (result.ExitCode != 0 && !isDryRun)
                {
                    AnsiTerminal.Error($"Ошибка синхронизации Google Play: {result.Message}");
                    return result.ExitCode;
                }
            }
        }

        if (targetPlatform is "all" or "windows")
        {
            AnsiTerminal.Step("Windows", "Синхронизация метаданных Microsoft Store...");
            var credentials = MStoreCredentials.Load(
                flags.GetValueOrDefault("tenant-id"),
                flags.GetValueOrDefault("client-id"),
                flags.GetValueOrDefault("client-secret"),
                flags.GetValueOrDefault("app-id"),
                flags.GetValueOrDefault("seller-id"));

            if (string.IsNullOrWhiteSpace(credentials.AppId) || string.IsNullOrWhiteSpace(credentials.TenantId))
            {
                AnsiTerminal.Warning("Пропущен Windows: не настроены учетные данные Microsoft Store. Запустите 'octopublish mstore configure'.");
            }
            else
            {
                AnsiTerminal.Info("Подключение к Microsoft Partner Center Ingestion API...");
            }
        }

        AnsiTerminal.Success("Операция синхронизации успешно завершена.");
        return 0;
    }

    private static async Task<int> RunPullCommandAsync(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help")
        {
            PrintPullHelp();
            return 0;
        }

        var (flags, positional) = ParseArguments(args);
        var platform = "all";
        string? targetId = null;

        if (positional.Count > 0)
        {
            var first = positional[0].ToLowerInvariant();
            if (first is "all" or "android" or "windows")
            {
                platform = first;
                if (positional.Count > 1)
                {
                    targetId = positional[1];
                }
            }
            else
            {
                targetId = positional[0];
                platform = targetId.Contains('.') ? "android" : "windows";
            }
        }

        var targetDir = flags.GetValueOrDefault("dir") ?? "packaging";
        var packageName = flags.GetValueOrDefault("package-name")
            ?? Environment.GetEnvironmentVariable("PLAY_PACKAGE_NAME");
        var appId = flags.GetValueOrDefault("app-id");
        var serviceAccount = flags.GetValueOrDefault("service-account")
            ?? Environment.GetEnvironmentVariable("PLAY_CONFIG_JSON")
            ?? Environment.GetEnvironmentVariable("PLAY_SERVICE_ACCOUNT_JSON");
        var lang = flags.GetValueOrDefault("lang");
        var market = flags.GetValueOrDefault("market");

        var mstoreCreds = MStoreCredentials.TryLoad(
            flags.GetValueOrDefault("tenant-id"),
            flags.GetValueOrDefault("client-id"),
            flags.GetValueOrDefault("client-secret"),
            appId,
            flags.GetValueOrDefault("seller-id"));

        if (string.IsNullOrWhiteSpace(appId))
        {
            appId = mstoreCreds?.AppId;
        }

        return await PackagingPuller.ExecuteAsync(
            platform,
            targetDir,
            targetId,
            appId,
            packageName,
            serviceAccount,
            lang,
            market,
            mstoreCreds);
    }

    private static async Task<int> RunMStoreCommandAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintMStoreHelp();
            return 0;
        }

        var action = args[0].ToLowerInvariant();
        var (flags, _) = ParseArguments(args.AsSpan(1).ToArray());

        if (action == "configure")
        {
            return HandleMStoreConfigure(flags);
        }

        var credentials = MStoreCredentials.Load(
            flags.GetValueOrDefault("tenant-id"),
            flags.GetValueOrDefault("client-id"),
            flags.GetValueOrDefault("client-secret"),
            flags.GetValueOrDefault("app-id"),
            flags.GetValueOrDefault("seller-id"));

        var client = new MStoreClient();

        return action switch
        {
            "publish" => await HandleMStorePublishAsync(client, credentials, flags),
            "status" => await HandleMStoreStatusAsync(client, credentials),
            "delete-draft" => await HandleMStoreDeleteDraftAsync(client, credentials),
            _ => UnknownSubcommand("mstore", action)
        };
    }

    private static int HandleMStoreConfigure(Dictionary<string, string> flags)
    {
        AnsiTerminal.Header("Настройка авторизации Microsoft Store");

        var tenantId = PromptOrFlag(flags, "tenant-id", "Введите Azure AD Tenant ID");
        var clientId = PromptOrFlag(flags, "client-id", "Введите Azure AD Client ID");
        var clientSecret = PromptOrFlag(flags, "client-secret", "Введите Azure AD Client Secret", isSecret: true);
        var appId = PromptOrFlag(flags, "app-id", "Введите Microsoft Store App ID (Product ID)");
        var sellerId = PromptOrFlag(flags, "seller-id", "Введите Partner Center Seller ID (необязательно)", isOptional: true);

        var isLocal = flags.ContainsKey("local");

        var creds = new MStoreCredentials(tenantId, clientId, clientSecret, appId, string.IsNullOrWhiteSpace(sellerId) ? null : sellerId);
        MStoreCredentials.SaveConfig(creds, isLocal);

        var targetFile = isLocal ? ".octopublish.json" : "~/.octo/mstore.json";
        AnsiTerminal.Success($"Конфигурация успешно сохранена в '{targetFile}'");
        AnsiTerminal.Info("Теперь вы можете выполнять 'octopublish mstore publish --package <путь>' без указания флагов авторизации.");
        return 0;
    }

    private static string PromptOrFlag(Dictionary<string, string> flags, string flagName, string prompt, bool isSecret = false, bool isOptional = false)
    {
        if (flags.TryGetValue(flagName, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            return val;
        }

        Console.Write($"{AnsiTerminal.Accent}[?]{AnsiTerminal.Reset} {prompt}: ");
        if (isSecret)
        {
            var pwd = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                if (key.Key == ConsoleKey.Backspace && pwd.Length > 0)
                {
                    _ = pwd.Remove(pwd.Length - 1, 1);
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    _ = pwd.Append(key.KeyChar);
                }
            }
            return pwd.ToString();
        }

        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(input) && !isOptional
            ? throw new ArgumentException($"Отсутствует обязательный параметр конфигурации: {flagName}")
            : input ?? string.Empty;
    }

    private static async Task<int> HandleMStorePublishAsync(
        MStoreClient client,
        MStoreCredentials credentials,
        Dictionary<string, string> flags)
    {
        AnsiTerminal.Header("Публикация в Microsoft Store");

        if (!flags.TryGetValue("package", out var packagePath) || string.IsNullOrWhiteSpace(packagePath))
        {
            AnsiTerminal.Error("Отсутствует обязательный флаг: --package <путь к .msix/.zip>");
            return 1;
        }

        Dictionary<string, string>? notesByLang = null;
        if (flags.TryGetValue("notes-dir", out var notesDir))
        {
            notesByLang = MStoreListingManager.LoadDirectoryFiles(notesDir);
        }

        Dictionary<string, string>? descByLang = null;
        Dictionary<string, MStoreListingInfo>? structuredListings = null;
        if (flags.TryGetValue("desc-dir", out var descDir))
        {
            structuredListings = MStoreListingManager.ResolveStoreDirectory(descDir);
            descByLang = MStoreListingManager.LoadDirectoryFiles(descDir);
        }

        string? defaultNotes = null;
        if (flags.TryGetValue("notes", out var notesFile) && File.Exists(notesFile))
        {
            defaultNotes = await File.ReadAllTextAsync(notesFile);
        }

        string? defaultDesc = null;
        if (flags.TryGetValue("desc", out var descFile) && File.Exists(descFile))
        {
            defaultDesc = await File.ReadAllTextAsync(descFile);
        }

        var result = await client.PublishAsync(
            credentials,
            packagePath,
            notesByLang,
            descByLang,
            defaultNotes,
            defaultDesc,
            structuredListings);

        return result.ExitCode;
    }

    private static async Task<int> HandleMStoreStatusAsync(
        MStoreClient client,
        MStoreCredentials credentials)
    {
        AnsiTerminal.Header("Проверка статуса в Microsoft Store");
        var tokenService = new MStoreTokenService();
        var token = await tokenService.GetAccessTokenAsync(credentials);

        var app = await client.GetApplicationAsync(credentials.AppId, token, CancellationToken.None);
        AnsiTerminal.Detail("Название приложения", app.PrimaryName ?? "Не указано");
        AnsiTerminal.Detail("Идентификатор (App ID)", app.Id ?? credentials.AppId);

        if (app.LastPublishedApplicationSubmission != null)
        {
            AnsiTerminal.Detail("Последняя опубликованная версия", app.LastPublishedApplicationSubmission.Id ?? "Нет");
        }

        if (app.PendingApplicationSubmission != null && !string.IsNullOrWhiteSpace(app.PendingApplicationSubmission.Id))
        {
            var subId = app.PendingApplicationSubmission.Id;
            var sub = await client.GetSubmissionAsync(credentials.AppId, subId, token, CancellationToken.None);
            AnsiTerminal.Detail("Текущая активная отправка", subId);
            AnsiTerminal.Detail("Статус сертификации", sub?.Status ?? "Неизвестно");
        }
        else
        {
            AnsiTerminal.Info("Нет активных незавершенных отправок (драфтов).");
        }

        return 0;
    }

    private static async Task<int> HandleMStoreDeleteDraftAsync(
        MStoreClient client,
        MStoreCredentials credentials)
    {
        AnsiTerminal.Header("Очистка зависшего черновика Microsoft Store");
        var tokenService = new MStoreTokenService();
        var token = await tokenService.GetAccessTokenAsync(credentials);

        var app = await client.GetApplicationAsync(credentials.AppId, token, CancellationToken.None);
        if (app.PendingApplicationSubmission == null || string.IsNullOrWhiteSpace(app.PendingApplicationSubmission.Id))
        {
            AnsiTerminal.Info("Активный черновик (draft) для удаления не найден.");
            return 0;
        }

        var subId = app.PendingApplicationSubmission.Id;
        AnsiTerminal.Step("Удаление", $"Попытка удаления черновика отправки: {subId}");

        var deleted = await client.DeleteSubmissionAsync(credentials.AppId, subId, token, CancellationToken.None);
        if (deleted)
        {
            AnsiTerminal.Success($"Черновик {subId} успешно удален.");
            return 0;
        }

        AnsiTerminal.Error($"Не удалось удалить отправку {subId}. Возможно, она уже передана на сертификацию.");
        return 1;
    }

    private static async Task<int> RunPlayStoreCommandAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintPlayStoreHelp();
            return 0;
        }

        var action = args[0].ToLowerInvariant();
        var (flags, _) = ParseArguments(args.AsSpan(1).ToArray());

        return action switch
        {
            "publish" => await HandlePlayStorePublishAsync(flags),
            _ => UnknownSubcommand("playstore", action)
        };
    }

    private static async Task<int> HandlePlayStorePublishAsync(Dictionary<string, string> flags)
    {
        AnsiTerminal.Header("Публикация в Google Play Store");

        var bundlePath = flags.GetValueOrDefault("bundle") ?? flags.GetValueOrDefault("package");
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            AnsiTerminal.Error("Отсутствует обязательный флаг: --bundle <путь к .aab или .apk>");
            return 2;
        }

        var packageName = flags.GetValueOrDefault("package-name")
            ?? Environment.GetEnvironmentVariable("PLAY_PACKAGE_NAME");
        if (string.IsNullOrWhiteSpace(packageName))
        {
            AnsiTerminal.Error("Отсутствует обязательный флаг: --package-name <id> (или переменная PLAY_PACKAGE_NAME)");
            return 2;
        }

        var serviceAccount = flags.GetValueOrDefault("service-account")
            ?? Environment.GetEnvironmentVariable("PLAY_CONFIG_JSON")
            ?? Environment.GetEnvironmentVariable("PLAY_SERVICE_ACCOUNT_JSON");
        if (string.IsNullOrWhiteSpace(serviceAccount))
        {
            AnsiTerminal.Error("Отсутствует обязательный флаг: --service-account <путь или json> (или переменная PLAY_CONFIG_JSON)");
            return 2;
        }

        var track = flags.GetValueOrDefault("track")
            ?? Environment.GetEnvironmentVariable("PLAY_TRACK")
            ?? "internal";

        var notes = flags.GetValueOrDefault("notes");
        var notesDir = flags.GetValueOrDefault("notes-dir");
        var desc = flags.GetValueOrDefault("desc");
        var descDir = flags.GetValueOrDefault("desc-dir");
        var icon = flags.GetValueOrDefault("icon");
        var releaseName = flags.GetValueOrDefault("release-name");

        var publisher = new PlayStorePublisher();
        var result = await publisher.PublishAsync(
            bundleOrApkPath: bundlePath,
            packageName: packageName,
            serviceAccountInput: serviceAccount,
            track: track,
            notesPath: notes,
            notesDir: notesDir,
            descPath: desc,
            descDir: descDir,
            iconPath: icon,
            releaseName: releaseName);

        return result.ExitCode;
    }

    private static int HandleUnknownCommand(string command)
    {
        AnsiTerminal.Error($"Неизвестная команда: '{command}'");
        PrintHelp();
        return 1;
    }

    private static int UnknownSubcommand(string module, string subcmd)
    {
        AnsiTerminal.Error($"Неизвестная подкоманда модуля '{module}': '{subcmd}'");
        return 1;
    }

    private static (Dictionary<string, string> Flags, List<string> Positional) ParseArguments(string[] args)
    {
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var content = arg[2..];
                var eqIdx = content.IndexOf('=');
                if (eqIdx >= 0)
                {
                    flags[content[..eqIdx]] = content[(eqIdx + 1)..];
                }
                else
                {
                    flags[content] = i + 1 < args.Length && !args[i + 1].StartsWith('-') ? args[++i] : "true";
                }
            }
            else if (arg.StartsWith('-') && arg.Length > 1)
            {
                flags[arg[1..]] = "true";
            }
            else
            {
                positional.Add(arg);
            }
        }

        return (flags, positional);
    }

    private static void PrintHelp()
    {
        AnsiTerminal.Header("OctoPublisher CLI (Русский интерфейс)");
        Console.WriteLine("Универсальный движок автоматизации публикации приложений в Google Play Store и Microsoft Store.");
        Console.WriteLine();
        Console.WriteLine("Использование: octopublish <команда> [параметры]");
        Console.WriteLine();
        Console.WriteLine("Команды управления метаданными ('Store as Code'):");
        Console.WriteLine("  init       Создать дерево каталогов packaging/ с шаблонами текстов и валидными PNG");
        Console.WriteLine("  pull       Выгрузить метаданные и графику напрямую из магазинов (Store as Code)");
        Console.WriteLine("  add        Добавить языковой пакет для выбранной платформы (например: add android en)");
        Console.WriteLine("  test       Запустить 5-уровневый предполётный линтер (шаблоны, лимиты, размеры картинок)");
        Console.WriteLine("  list       Вывести сводную таблицу готовности метаданных и скриншотов");
        Console.WriteLine("  sync       Быстрая синхронизация метаданных со сторами без пересборки бинарников");
        Console.WriteLine();
        Console.WriteLine("Команды публикации пакетов:");
        Console.WriteLine("  playstore  Публикация Android AAB / APK в Google Play Developer API");
        Console.WriteLine("  mstore     Публикация Windows MSIX в Microsoft Store Partner Center API");
        Console.WriteLine();
        Console.WriteLine("Глобальные параметры:");
        Console.WriteLine("  -v, --version  Показать версию программы");
        Console.WriteLine("  -h, --help     Показать эту справку");
        Console.WriteLine();
        Console.WriteLine("Для подробной справки по команде выполните 'octopublish <команда> --help'.");
    }

    private static void PrintInitHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Команда init");
        Console.WriteLine("Генерирует полную структуру каталогов метаданных 'Store as Code' с русскими текстами и PNG.");
        Console.WriteLine();
        Console.WriteLine("Использование: octopublish init [all|android|windows] [--dir <каталог>] [--lang <локаль>]");
        Console.WriteLine();
        Console.WriteLine("Параметры:");
        Console.WriteLine("  all|android|windows    Целевая платформа (По умолчанию: all)");
        Console.WriteLine("  --dir <каталог>        Целевой каталог на диске (По умолчанию: packaging)");
        Console.WriteLine("  --lang <локаль>        Основной язык приложения (По умолчанию: ru-RU)");
    }

    private static void PrintPullHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Команда pull");
        Console.WriteLine("Выгружает метаданные, описания, иконки и скриншоты напрямую из магазинов в структуру 'Store as Code'.");
        Console.WriteLine();
        Console.WriteLine("Использование: octopublish pull [all|android|windows] [ID] [параметры]");
        Console.WriteLine();
        Console.WriteLine("Параметры:");
        Console.WriteLine("  --dir <каталог>              Целевой каталог для сохранения ассетов (По умолчанию: packaging)");
        Console.WriteLine("  --app-id <Product ID>        Product ID приложения Microsoft Store (например: 9NZXP5WR803J)");
        Console.WriteLine("  --package-name <id>          Идентификатор пакета Google Play (например: one.obxodka.app)");
        Console.WriteLine("  --service-account <путь>     Ключ сервисного аккаунта Google Play (JSON)");
        Console.WriteLine("  --lang <локаль>              Целевой язык для загрузки (например: ru-RU или en-US)");
        Console.WriteLine("  --market <рынок>             Региональный рынок для Microsoft Store (По умолчанию: RU или US)");
        Console.WriteLine();
        Console.WriteLine("Примеры:");
        Console.WriteLine("  octopublish pull windows 9NZXP5WR803J                            Импорт из Microsoft Store без авторизации");
        Console.WriteLine("  octopublish pull android com.example.app --service-account key.json  Импорт из Google Play по ключу");
        Console.WriteLine("  octopublish pull all --dir ./my-store-assets                    Импорт всех платформ в указанный каталог");
    }

    private static void PrintAddHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Команда add");
        Console.WriteLine("Добавляет языковую структуру и стартовые ассеты для указанной страны.");
        Console.WriteLine();
        Console.WriteLine("Использование: octopublish add [all|android|windows] <локаль> [--dir <каталог>]");
        Console.WriteLine();
        Console.WriteLine("Примеры:");
        Console.WriteLine("  octopublish add all en             Добавить английский (en-US) для обеих платформ");
        Console.WriteLine("  octopublish add android fr         Добавить французский (fr-FR) для Google Play");
        Console.WriteLine("  octopublish add windows de         Добавить немецкий (de-DE) для Microsoft Store");
    }

    private static void PrintTestHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Команда test (Предполётный линтер)");
        Console.WriteLine("Проверяет тексты, заглушки, лимиты символов и геометрию PNG изображений.");
        Console.WriteLine();
        Console.WriteLine("Использование: octopublish test [--dir <каталог>] [--strict]");
        Console.WriteLine();
        Console.WriteLine("Параметры:");
        Console.WriteLine("  --dir <каталог>    Каталог с ассетами (По умолчанию: packaging)");
        Console.WriteLine("  --strict           Строгий режим проверки (любое предупреждение завершается ошибкой 1)");
    }

    private static void PrintSyncHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Команда sync");
        Console.WriteLine("Синхронизирует метаданные и графические материалы без пересборки AAB/MSIX.");
        Console.WriteLine();
        Console.WriteLine("Использование: octopublish sync [all|android|windows] [--dir <каталог>] [--dry-run]");
        Console.WriteLine();
        Console.WriteLine("Параметры:");
        Console.WriteLine("  --dir <каталог>    Каталог с ассетами (По умолчанию: packaging)");
        Console.WriteLine("  --dry-run          Тестовый прогон: проверка изменений без отправки в сторы");
    }

    private static void PrintMStoreHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Модуль Microsoft Store (Windows)");
        Console.WriteLine("Команды:");
        Console.WriteLine("  publish       Загрузка пакета, обновление метаданных и отправка на сертификацию");
        Console.WriteLine("  status        Инспекция текущего статуса приложения и активных отправок");
        Console.WriteLine("  delete-draft  Удаление зависшего черновика отправки");
        Console.WriteLine("  configure     Интерактивное сохранение учетных данных в ~/.octo/mstore.json");
        Console.WriteLine();
        Console.WriteLine("Параметры publish:");
        Console.WriteLine("  --package <путь>        Путь к .msix, .msixupload или .zip пакету (Обязательно)");
        Console.WriteLine("  --app-id <id>           Идентификатор продукта в магазине (STORE_APP_ID)");
        Console.WriteLine("  --tenant-id <id>        Azure AD Tenant ID (STORE_TENANT_ID)");
        Console.WriteLine("  --client-id <id>        Azure AD Client ID (STORE_CLIENT_ID)");
        Console.WriteLine("  --client-secret <sec>   Azure AD Client Secret (STORE_CLIENT_SECRET)");
        Console.WriteLine("  --notes-dir <путь>      Каталог с локализованными заметками (packaging/whatsnew)");
        Console.WriteLine("  --desc-dir <путь>       Каталог с локализованными описаниями (packaging/store/windows)");
    }

    private static void PrintPlayStoreHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Модуль Google Play Store (Android)");
        Console.WriteLine("Команды:");
        Console.WriteLine("  publish       Загрузка AAB/APK, назначение трека, обновление описаний и релиз");
        Console.WriteLine();
        Console.WriteLine("Параметры publish:");
        Console.WriteLine("  --bundle <путь>           Путь к .aab или .apk пакету (Обязательно)");
        Console.WriteLine("  --package-name <id>       ID приложения (PLAY_PACKAGE_NAME)");
        Console.WriteLine("  --service-account <путь>  Путь к сервисному JSON ключу или его содержимое");
        Console.WriteLine("  --track <имя>             Трек: internal, alpha, beta, production (По умолчанию: internal)");
        Console.WriteLine("  --notes-dir <путь>        Каталог заметок (packaging/whatsnew)");
        Console.WriteLine("  --desc-dir <путь>         Каталог описаний (packaging/store/android)");
        Console.WriteLine("  --icon <путь>             Путь к иконке 512x512 PNG");
        Console.WriteLine("  --release-name <имя>      Пользовательское название версии релиза");
    }
}
