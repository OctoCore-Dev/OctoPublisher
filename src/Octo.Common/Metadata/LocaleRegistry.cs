namespace Octo.Common.Metadata;

public static class LocaleRegistry
{
    private static readonly Dictionary<string, string> t_aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ru"] = "ru-RU",
        ["ru-ru"] = "ru-RU",
        ["en"] = "en-US",
        ["us"] = "en-US",
        ["en-us"] = "en-US",
        ["fr"] = "fr-FR",
        ["fr-fr"] = "fr-FR",
        ["de"] = "de-DE",
        ["de-de"] = "de-DE",
        ["es"] = "es-ES",
        ["es-es"] = "es-ES",
        ["it"] = "it-IT",
        ["it-it"] = "it-IT",
        ["pt"] = "pt-BR",
        ["br"] = "pt-BR",
        ["pt-br"] = "pt-BR",
        ["zh"] = "zh-CN",
        ["cn"] = "zh-CN",
        ["zh-cn"] = "zh-CN",
        ["ja"] = "ja-JP",
        ["jp"] = "ja-JP",
        ["ja-jp"] = "ja-JP",
        ["ko"] = "ko-KR",
        ["kr"] = "ko-KR",
        ["ko-kr"] = "ko-KR",
        ["tr"] = "tr-TR",
        ["tr-tr"] = "tr-TR",
        ["uk"] = "uk-UA",
        ["ua"] = "uk-UA",
        ["uk-ua"] = "uk-UA",
        ["kk"] = "kk-KZ",
        ["kz"] = "kk-KZ",
        ["kk-kz"] = "kk-KZ",
        ["pl"] = "pl-PL",
        ["pl-pl"] = "pl-PL",
        ["ar"] = "ar-SA",
        ["sa"] = "ar-SA",
        ["ar-sa"] = "ar-SA"
    };

    public const string DefaultLocale = "ru-RU";

    public const int PlayTitleMax = 30;
    public const int PlayShortDescMax = 80;
    public const int PlayFullDescMax = 4000;
    public const int MStoreTitleMax = 256;
    public const int MStoreFullDescMax = 10000;
    public const int MStoreFeatureLineMax = 200;
    public const int MStoreMaxFeatures = 20;
    public const int MStoreReleaseNotesMax = 1500;
    public const int WhatsNewMax = 500;

    public static readonly string[] PlaceholderMarkers =
    [
        "[ШАБЛОН]",
        "[TEMPLATE]",
        "[ЗАГЛУШКА]",
        "TODO:",
        "example.com",
        "https://privew.com",
        "https://example.com",
        "App Name",
        "Название приложения"
    ];

    public static string NormalizeLocale(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DefaultLocale;
        }

        var trimmed = input.Trim();
        if (t_aliases.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        if (trimmed.Length == 5 && trimmed[2] == '-')
        {
            var lang = trimmed[..2].ToLowerInvariant();
            var country = trimmed[3..].ToUpperInvariant();
            return $"{lang}-{country}";
        }

        return trimmed;
    }

    public static string GetDefaultTitle(string locale)
    {
        return locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "[ШАБЛОН] Название приложения"
            : "[TEMPLATE] App Name";
    }

    public static string GetDefaultShortDescription(string locale)
    {
        return locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "[ШАБЛОН] Краткое описание возможностей приложения (до 80 знаков)."
            : "[TEMPLATE] Short description of your application features (up to 80 chars).";
    }

    public static string GetDefaultFullDescription(string locale, bool isWindows = false)
    {
        var isRu = locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        return isRu
            ? (isWindows
                ? "[ШАБЛОН] Описание приложения для Microsoft Store (Windows 11).\n\nКлючевые преимущества:\n- Быстрое прямое подключение\n- Элегантный интерфейс в системном стиле Windows 11\n- Низкое потребление ресурсов и фоновая работа"
                : "[ШАБЛОН] Полное описание приложения для Google Play.\n\nОсновные возможности:\n- Высокая скорость работы и стабильность\n- Современный интерфейс Microsoft Fluent 2\n- Надёжная защита данных и конфиденциальность\n\nТехническая поддержка: support@example.com")
            : (isWindows
                ? "[TEMPLATE] Microsoft Store application description (Windows 11).\n\nKey features:\n- High-speed direct connection\n- Fluent 2 modern design\n- Low resource footprint"
                : "[TEMPLATE] Full detailed application description for Google Play.\n\nKey features:\n- Fast and reliable performance\n- Modern Fluent 2 user interface\n- Data privacy and security\n\nSupport: support@example.com");
    }

    public static string GetDefaultFeatures(string locale)
    {
        return locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "Высокая скорость прямого подключения\nСовременный интерфейс Microsoft Fluent 2\nАвтоматическое обновление и фоновая работа"
            : "High-speed direct connectivity\nModern Fluent 2 UI\nAutomatic background updates";
    }

    public static string GetDefaultReleaseNotes(string locale)
    {
        return locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "Что нового в этой версии:\n- Оптимизация сетевого стека и стабильности\n- Улучшения интерфейса и производительности"
            : "What's new in this release:\n- Network stack and stability improvements\n- UI and performance optimizations";
    }

    public static string GetDefaultPrivacyUrl() => "https://example.com/politika-konfidencialnosti";
}
