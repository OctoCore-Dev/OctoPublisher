namespace Octo.MStore.Models;

public sealed record MStoreCredentials(
    string TenantId,
    string ClientId,
    string ClientSecret,
    string AppId,
    string? SellerId = null)
{
    private static readonly JsonSerializerOptions t_jsonOptions = new() { WriteIndented = true };

    public static MStoreCredentials Load(
        string? tenantId = null,
        string? clientId = null,
        string? clientSecret = null,
        string? appId = null,
        string? sellerId = null)
    {
        var fileConfig = LoadFileConfig();

        var resolvedTenant = FirstNonEmpty(
            tenantId,
            Environment.GetEnvironmentVariable("STORE_TENANT_ID"),
            Environment.GetEnvironmentVariable("AZURE_AD_TENANT_ID"),
            fileConfig?.TenantId);

        var resolvedClient = FirstNonEmpty(
            clientId,
            Environment.GetEnvironmentVariable("STORE_CLIENT_ID"),
            Environment.GetEnvironmentVariable("AZURE_AD_APPLICATION_CLIENT_ID"),
            fileConfig?.ClientId);

        var resolvedSecret = FirstNonEmpty(
            clientSecret,
            Environment.GetEnvironmentVariable("STORE_CLIENT_SECRET"),
            Environment.GetEnvironmentVariable("AZURE_AD_APPLICATION_SECRET"),
            fileConfig?.ClientSecret);

        var resolvedAppId = FirstNonEmpty(
            appId,
            Environment.GetEnvironmentVariable("STORE_APP_ID"),
            Environment.GetEnvironmentVariable("MICROSOFT_STORE_APP_ID"),
            fileConfig?.AppId);

        var resolvedSeller = FirstNonEmpty(
            sellerId,
            Environment.GetEnvironmentVariable("SELLER_ID"),
            Environment.GetEnvironmentVariable("STORE_SELLER_ID"),
            fileConfig?.SellerId);

        return string.IsNullOrWhiteSpace(resolvedTenant)
            ? throw new ArgumentException("Missing Tenant ID. Specify via --tenant-id, STORE_TENANT_ID environment variable, or run 'octopublish mstore configure'.")
            : string.IsNullOrWhiteSpace(resolvedClient)
            ? throw new ArgumentException("Missing Client ID. Specify via --client-id, STORE_CLIENT_ID environment variable, or run 'octopublish mstore configure'.")
            : string.IsNullOrWhiteSpace(resolvedSecret)
            ? throw new ArgumentException("Missing Client Secret. Specify via --client-secret, STORE_CLIENT_SECRET environment variable, or run 'octopublish mstore configure'.")
            : string.IsNullOrWhiteSpace(resolvedAppId)
            ? throw new ArgumentException("Missing Store App ID. Specify via --app-id, STORE_APP_ID environment variable, or run 'octopublish mstore configure'.")
            : new MStoreCredentials(
            resolvedTenant,
            resolvedClient,
            resolvedSecret,
            resolvedAppId,
            resolvedSeller);
    }

    public static MStoreCredentials? TryLoad(
        string? tenantId = null,
        string? clientId = null,
        string? clientSecret = null,
        string? appId = null,
        string? sellerId = null)
    {
        try
        {
            return Load(tenantId, clientId, clientSecret, appId, sellerId);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveConfig(MStoreCredentials credentials, bool localDirectory = false)
    {
        var targetPath = localDirectory
            ? Path.Combine(Directory.GetCurrentDirectory(), ".octopublish.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".octo", "mstore.json");

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(credentials, t_jsonOptions);
        File.WriteAllText(targetPath, json);
    }

    private static MStoreCredentials? LoadFileConfig()
    {
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), ".octopublish.json");
        if (File.Exists(localPath))
        {
            try
            {
                var content = File.ReadAllText(localPath);
                return JsonSerializer.Deserialize<MStoreCredentials>(content);
            }
            catch
            {
            }
        }

        var globalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".octo", "mstore.json");
        if (File.Exists(globalPath))
        {
            try
            {
                var content = File.ReadAllText(globalPath);
                return JsonSerializer.Deserialize<MStoreCredentials>(content);
            }
            catch
            {
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v.Trim();
            }
        }
        return null;
    }
}
