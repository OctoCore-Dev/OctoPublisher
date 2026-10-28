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
            var ver = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.1";
            AnsiTerminal.Info($"OctoPublisher CLI v{ver} (.NET 10)");
            return 0;
        }

        var primaryCommand = args[0].ToLowerInvariant();
        try
        {
            return primaryCommand switch
            {
                "mstore" => await RunMStoreCommandAsync(args.AsSpan(1).ToArray()),
                "playstore" => await RunPlayStoreCommandAsync(args.AsSpan(1).ToArray()),
                _ => HandleUnknownCommand(primaryCommand)
            };
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunMStoreCommandAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintMStoreHelp();
            return 0;
        }

        var action = args[0].ToLowerInvariant();
        var flags = ParseFlags(args.AsSpan(1).ToArray());

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
        AnsiTerminal.Header("Microsoft Store Configuration Setup");

        var tenantId = PromptOrFlag(flags, "tenant-id", "Enter Azure AD Tenant ID");
        var clientId = PromptOrFlag(flags, "client-id", "Enter Azure AD Client ID");
        var clientSecret = PromptOrFlag(flags, "client-secret", "Enter Azure AD Client Secret", isSecret: true);
        var appId = PromptOrFlag(flags, "app-id", "Enter Microsoft Store App ID");
        var sellerId = PromptOrFlag(flags, "seller-id", "Enter Partner Center Seller ID (optional)", isOptional: true);

        var isLocal = flags.ContainsKey("local");

        var creds = new MStoreCredentials(tenantId, clientId, clientSecret, appId, string.IsNullOrWhiteSpace(sellerId) ? null : sellerId);
        MStoreCredentials.SaveConfig(creds, isLocal);

        var targetFile = isLocal ? ".octopublish.json" : "~/.octo/mstore.json";
        AnsiTerminal.Success($"Configuration saved successfully to {targetFile}");
        AnsiTerminal.Info("You can now run 'octopublish mstore publish --package <path>' without credential flags.");
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
            ? throw new ArgumentException($"Missing required configuration parameter: {flagName}")
            : input ?? string.Empty;
    }

    private static async Task<int> HandleMStorePublishAsync(
        MStoreClient client,
        MStoreCredentials credentials,
        Dictionary<string, string> flags)
    {
        AnsiTerminal.Header("Microsoft Store Publishing");

        if (!flags.TryGetValue("package", out var packagePath) || string.IsNullOrWhiteSpace(packagePath))
        {
            AnsiTerminal.Error("Missing required flag: --package <path to .msix/.zip>");
            return 1;
        }

        Dictionary<string, string>? notesByLang = null;
        if (flags.TryGetValue("notes-dir", out var notesDir))
        {
            notesByLang = MStoreListingManager.LoadDirectoryFiles(notesDir);
        }

        Dictionary<string, string>? descByLang = null;
        if (flags.TryGetValue("desc-dir", out var descDir))
        {
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
            defaultDesc);

        return result.ExitCode;
    }

    private static async Task<int> HandleMStoreStatusAsync(
        MStoreClient client,
        MStoreCredentials credentials)
    {
        AnsiTerminal.Header("Microsoft Store Status Inspection");
        var tokenService = new MStoreTokenService();
        var token = await tokenService.GetAccessTokenAsync(credentials);

        var app = await client.GetApplicationAsync(credentials.AppId, token, CancellationToken.None);
        AnsiTerminal.Detail("Application Name", app.PrimaryName ?? "None");
        AnsiTerminal.Detail("App ID", app.Id ?? credentials.AppId);

        if (app.LastPublishedApplicationSubmission != null)
        {
            AnsiTerminal.Detail("Last Published Submission", app.LastPublishedApplicationSubmission.Id ?? "None");
        }

        if (app.PendingApplicationSubmission != null && !string.IsNullOrWhiteSpace(app.PendingApplicationSubmission.Id))
        {
            var subId = app.PendingApplicationSubmission.Id;
            var sub = await client.GetSubmissionAsync(credentials.AppId, subId, token, CancellationToken.None);
            AnsiTerminal.Detail("Active Pending Submission", subId);
            AnsiTerminal.Detail("Status", sub?.Status ?? "Unknown");
        }
        else
        {
            AnsiTerminal.Info("No pending submissions active.");
        }

        return 0;
    }

    private static async Task<int> HandleMStoreDeleteDraftAsync(
        MStoreClient client,
        MStoreCredentials credentials)
    {
        AnsiTerminal.Header("Microsoft Store Draft Cleanup");
        var tokenService = new MStoreTokenService();
        var token = await tokenService.GetAccessTokenAsync(credentials);

        var app = await client.GetApplicationAsync(credentials.AppId, token, CancellationToken.None);
        if (app.PendingApplicationSubmission == null || string.IsNullOrWhiteSpace(app.PendingApplicationSubmission.Id))
        {
            AnsiTerminal.Info("No pending draft submission found to delete.");
            return 0;
        }

        var subId = app.PendingApplicationSubmission.Id;
        AnsiTerminal.Step("Delete", $"Attempting to delete pending draft submission: {subId}");

        var deleted = await client.DeleteSubmissionAsync(credentials.AppId, subId, token, CancellationToken.None);
        if (deleted)
        {
            AnsiTerminal.Success($"Draft submission {subId} deleted successfully.");
            return 0;
        }

        AnsiTerminal.Error($"Failed to delete submission {subId}. It may already be in Certification or Release status.");
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
        var flags = ParseFlags(args.AsSpan(1).ToArray());

        return action switch
        {
            "publish" => await HandlePlayStorePublishAsync(flags),
            _ => UnknownSubcommand("playstore", action)
        };
    }

    private static async Task<int> HandlePlayStorePublishAsync(Dictionary<string, string> flags)
    {
        AnsiTerminal.Header("Google Play Store Publishing");

        var bundlePath = flags.GetValueOrDefault("bundle") ?? flags.GetValueOrDefault("package");
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            AnsiTerminal.Error("Missing required flag: --bundle <path> (path to .aab or .apk)");
            return 2;
        }

        var packageName = flags.GetValueOrDefault("package-name")
            ?? Environment.GetEnvironmentVariable("PLAY_PACKAGE_NAME");
        if (string.IsNullOrWhiteSpace(packageName))
        {
            AnsiTerminal.Error("Missing required flag: --package-name <id> (or PLAY_PACKAGE_NAME env)");
            return 2;
        }

        var serviceAccount = flags.GetValueOrDefault("service-account")
            ?? Environment.GetEnvironmentVariable("PLAY_CONFIG_JSON")
            ?? Environment.GetEnvironmentVariable("PLAY_SERVICE_ACCOUNT_JSON");
        if (string.IsNullOrWhiteSpace(serviceAccount))
        {
            AnsiTerminal.Error("Missing required flag: --service-account <path or json> (or PLAY_CONFIG_JSON env)");
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
        AnsiTerminal.Error($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    private static int UnknownSubcommand(string module, string subcmd)
    {
        AnsiTerminal.Error($"Unknown {module} subcommand: {subcmd}");
        return 1;
    }

    private static Dictionary<string, string> ParseFlags(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var key = arg[2..];
                dict[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
            }
        }
        return dict;
    }

    private static void PrintHelp()
    {
        AnsiTerminal.Header("OctoPublisher CLI");
        Console.WriteLine("Universal multi-store deployment automation engine for OctoCore applications.");
        Console.WriteLine();
        Console.WriteLine("Usage: octopublish <module> <action> [options]");
        Console.WriteLine();
        Console.WriteLine("Available Modules:");
        Console.WriteLine("  mstore     Microsoft Store (Windows MSIX / AppX submissions)");
        Console.WriteLine("  playstore  Google Play Store (Android AAB / APK releases)");
        Console.WriteLine();
        Console.WriteLine("Global Options:");
        Console.WriteLine("  -v, --version  Show version information");
        Console.WriteLine("  -h, --help     Show help information");
        Console.WriteLine();
        Console.WriteLine("Run 'octopublish mstore --help' or 'octopublish playstore --help'.");
    }

    private static void PrintMStoreHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Microsoft Store Module");
        Console.WriteLine("Commands:");
        Console.WriteLine("  publish       Upload package, update metadata, and commit submission to certification");
        Console.WriteLine("  status        Inspect current application and pending submission status");
        Console.WriteLine("  delete-draft  Delete uncommitted draft submission if stuck in PendingCommit");
        Console.WriteLine("  configure     Save credentials locally to ~/.octo/mstore.json or .octopublish.json");
        Console.WriteLine();
        Console.WriteLine("Publish Options:");
        Console.WriteLine("  --package <path>        Path to .msix, .msixupload, or .zip archive (Required)");
        Console.WriteLine("  --app-id <id>           Store Product ID (Fallback: STORE_APP_ID secret)");
        Console.WriteLine("  --tenant-id <id>        Azure AD Tenant ID (Fallback: STORE_TENANT_ID secret)");
        Console.WriteLine("  --client-id <id>        Azure AD Client ID (Fallback: STORE_CLIENT_ID secret)");
        Console.WriteLine("  --client-secret <sec>   Azure AD Client Secret (Fallback: STORE_CLIENT_SECRET secret)");
        Console.WriteLine("  --seller-id <id>        Partner Center Seller ID (Fallback: SELLER_ID secret)");
        Console.WriteLine("  --notes <path>          Path to single release notes text file");
        Console.WriteLine("  --notes-dir <path>      Directory containing localized notes (whatsnew-ru-RU, etc.)");
        Console.WriteLine("  --desc <path>           Path to single store description file");
        Console.WriteLine("  --desc-dir <path>       Directory containing localized descriptions");
        Console.WriteLine();
        Console.WriteLine("Configure Options:");
        Console.WriteLine("  --local                 Save to current directory (.octopublish.json) instead of user home");
        Console.WriteLine();
    }

    private static void PrintPlayStoreHelp()
    {
        AnsiTerminal.Header("OctoPublisher: Google Play Store Module");
        Console.WriteLine("Commands:");
        Console.WriteLine("  publish       Upload AAB or APK, assign track, update listings and commit release");
        Console.WriteLine();
        Console.WriteLine("Publish Options:");
        Console.WriteLine("  --bundle <path>           Path to .aab or .apk package (Required)");
        Console.WriteLine("  --package-name <id>       Android application ID (Fallback: PLAY_PACKAGE_NAME env)");
        Console.WriteLine("  --service-account <path>  Path to service account JSON or JSON content (Fallback: PLAY_CONFIG_JSON env)");
        Console.WriteLine("  --track <name>            Target release track: internal, alpha, beta, production (Default: internal)");
        Console.WriteLine("  --notes <path>            Path to single release notes text file");
        Console.WriteLine("  --notes-dir <path>        Directory containing localized notes (whatsnew-ru-RU, etc.)");
        Console.WriteLine("  --desc <path>             Path to single store description file");
        Console.WriteLine("  --desc-dir <path>         Directory containing localized descriptions (packaging/store/...)");
        Console.WriteLine("  --icon <path>             Path to 512x512 PNG app icon");
        Console.WriteLine("  --release-name <name>     Custom release title / version name");
        Console.WriteLine();
    }
}
