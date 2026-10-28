namespace Octo.MStore.Auth;

public sealed class MStoreTokenService
{
    private readonly HttpClient _httpClient;
    private string? _cachedToken;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public MStoreTokenService(HttpClient? httpClient = null) => _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<string> GetAccessTokenAsync(MStoreCredentials credentials, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _expiresAtUtc.AddMinutes(-5))
        {
            return _cachedToken;
        }

        var tokenEndpoint = $"https://login.microsoftonline.com/{credentials.TenantId}/oauth2/token";
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["resource"] = "https://manage.devcenter.microsoft.com"
        };

        request.Content = new FormUrlEncodedContent(parameters);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Azure AD authentication failed with status {(int)response.StatusCode}: {errorBody}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: ct).ConfigureAwait(false);
        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Azure AD returned empty access token");
        }

        _cachedToken = tokenResponse.AccessToken;
        var expiresInSeconds = int.TryParse(tokenResponse.ExpiresIn, out var secs) ? secs : 3600;
        _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresInSeconds);

        return _cachedToken;
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public string? ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}
