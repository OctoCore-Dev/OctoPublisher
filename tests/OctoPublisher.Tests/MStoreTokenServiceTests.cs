namespace OctoPublisher.Tests;

public sealed class MStoreTokenServiceTests
{
    [Fact]
    public async Task GetAccessTokenAsyncReturnsTokenWhenResponseIsSuccessfulAsync()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(

                                                                               /*lang=json,strict*/
                                                                               """{"access_token":"mock_access_token_12345","token_type":"Bearer","expires_in":"3600"}""",
                Encoding.UTF8,
                "application/json")
        };

        var handler = new MockHttpMessageHandler(_ => mockResponse);
        using var client = new HttpClient(handler);
        var service = new MStoreTokenService(client);

        var credentials = new MStoreCredentials("tenant-001", "client-001", "secret-001", "app-001");

        var token = await service.GetAccessTokenAsync(credentials);

        Assert.Equal("mock_access_token_12345", token);
        _ = Assert.Single(handler.CapturedRequests);
        Assert.Equal("https://login.microsoftonline.com/tenant-001/oauth2/token", handler.CapturedRequests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAccessTokenAsyncCachesTokenOnSubsequentCallsAsync()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(

                                                                                   /*lang=json,strict*/
                                                                                   """{"access_token":"cached_token_abc","token_type":"Bearer","expires_in":"3600"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new MStoreTokenService(client);

        var credentials = new MStoreCredentials("tenant-001", "client-001", "secret-001", "app-001");

        var token1 = await service.GetAccessTokenAsync(credentials);
        var token2 = await service.GetAccessTokenAsync(credentials);

        Assert.Equal("cached_token_abc", token1);
        Assert.Equal("cached_token_abc", token2);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsyncThrowsExceptionWhenAzureAdFailsAsync()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(

                                                                               /*lang=json,strict*/
                                                                               """{"error":"invalid_client","error_description":"Invalid client secret provided"}""",
                Encoding.UTF8,
                "application/json")
        });

        using var client = new HttpClient(handler);
        var service = new MStoreTokenService(client);

        var credentials = new MStoreCredentials("tenant-001", "client-001", "bad-secret", "app-001");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAccessTokenAsync(credentials));

        Assert.Contains("Azure AD authentication failed with status 400", exception.Message);
        Assert.Contains("invalid_client", exception.Message);
    }

    [Fact]
    public async Task GetAccessTokenAsyncThrowsExceptionWhenTokenIsEmptyAsync()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(/*lang=json,strict*/ """{"access_token":"","token_type":"Bearer"}""", Encoding.UTF8, "application/json")
        });

        using var client = new HttpClient(handler);
        var service = new MStoreTokenService(client);

        var credentials = new MStoreCredentials("tenant-001", "client-001", "secret-001", "app-001");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAccessTokenAsync(credentials));

        Assert.Contains("Azure AD returned empty access token", exception.Message);
    }
}
