namespace OctoPublisher.Tests;

public class MStoreCredentialsTests
{
    [Fact]
    public void LoadFromExplicitParametersReturnsValidCredentials()
    {
        var creds = MStoreCredentials.Load(
            tenantId: "tenant-123",
            clientId: "client-456",
            clientSecret: "secret-789",
            appId: "app-abc",
            sellerId: "seller-999");

        Assert.Equal("tenant-123", creds.TenantId);
        Assert.Equal("client-456", creds.ClientId);
        Assert.Equal("secret-789", creds.ClientSecret);
        Assert.Equal("app-abc", creds.AppId);
        Assert.Equal("seller-999", creds.SellerId);
    }

    [Fact]
    public void LoadThrowsWhenTenantIdMissing()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MStoreCredentials.Load(
                tenantId: null,
                clientId: "client-456",
                clientSecret: "secret-789",
                appId: "app-abc"));

        Assert.Contains("Tenant ID", ex.Message);
    }

    [Fact]
    public void LoadThrowsWhenClientIdMissing()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MStoreCredentials.Load(
                tenantId: "tenant-123",
                clientId: "",
                clientSecret: "secret-789",
                appId: "app-abc"));

        Assert.Contains("Client ID", ex.Message);
    }

    [Fact]
    public void LoadThrowsWhenClientSecretMissing()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MStoreCredentials.Load(
                tenantId: "tenant-123",
                clientId: "client-456",
                clientSecret: "   ",
                appId: "app-abc"));

        Assert.Contains("Client Secret", ex.Message);
    }

    [Fact]
    public void LoadThrowsWhenAppIdMissing()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MStoreCredentials.Load(
                tenantId: "tenant-123",
                clientId: "client-456",
                clientSecret: "secret-789",
                appId: null));

        Assert.Contains("Store App ID", ex.Message);
    }

    [Fact]
    public void LoadPicksUpEnvironmentVariablesWhenArgsAreNull()
    {
        Environment.SetEnvironmentVariable("STORE_TENANT_ID", "env-tenant");
        Environment.SetEnvironmentVariable("STORE_CLIENT_ID", "env-client");
        Environment.SetEnvironmentVariable("STORE_CLIENT_SECRET", "env-secret");
        Environment.SetEnvironmentVariable("STORE_APP_ID", "env-app");

        try
        {
            var creds = MStoreCredentials.Load();

            Assert.Equal("env-tenant", creds.TenantId);
            Assert.Equal("env-client", creds.ClientId);
            Assert.Equal("env-secret", creds.ClientSecret);
            Assert.Equal("env-app", creds.AppId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORE_TENANT_ID", null);
            Environment.SetEnvironmentVariable("STORE_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("STORE_CLIENT_SECRET", null);
            Environment.SetEnvironmentVariable("STORE_APP_ID", null);
        }
    }
}
