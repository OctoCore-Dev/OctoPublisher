namespace Octo.PlayStore.Services;

public sealed class GoogleAuthService(HttpClient? httpClient = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public static GoogleServiceAccount ParseServiceAccount(string serviceAccountInput)
    {
        var json = File.Exists(serviceAccountInput)
            ? File.ReadAllText(serviceAccountInput)
            : serviceAccountInput;

        var account = JsonSerializer.Deserialize<GoogleServiceAccount>(json);
        return account is null || string.IsNullOrWhiteSpace(account.ClientEmail) || string.IsNullOrWhiteSpace(account.PrivateKey)
            ? throw new InvalidOperationException("Invalid Google Service Account JSON payload. Missing client_email or private_key.")
            : account;
    }

    public async Task<string> GetAccessTokenAsync(GoogleServiceAccount account, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedToken;
        }

        var assertion = CreateSignedJwt(account);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            })
        };

        using var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to acquire Google OAuth2 token: {response.StatusCode} - {content}");
        }

        var tokenResp = JsonSerializer.Deserialize<GoogleTokenResponse>(content);
        if (tokenResp is null || string.IsNullOrEmpty(tokenResp.AccessToken))
        {
            throw new InvalidOperationException("Empty access token returned by Google OAuth endpoint.");
        }

        _cachedToken = tokenResp.AccessToken;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResp.ExpiresIn - 300);

        return _cachedToken;
    }

    public static string CreateSignedJwt(GoogleServiceAccount account)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var headerJson = /*lang=json,strict*/ "{\"alg\":\"RS256\",\"typ\":\"JWT\"}";
        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

        var claims = new Dictionary<string, object>
        {
            ["iss"] = account.ClientEmail,
            ["scope"] = "https://www.googleapis.com/auth/androidpublisher",
            ["aud"] = "https://oauth2.googleapis.com/token",
            ["exp"] = now + 3600,
            ["iat"] = now
        };
        var claimJson = JsonSerializer.Serialize(claims);
        var claimB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(claimJson));

        var sigInput = $"{headerB64}.{claimB64}";
        var sigInputBytes = Encoding.UTF8.GetBytes(sigInput);

        using var rsa = RSA.Create();
        var cleanedKey = account.PrivateKey
            .Replace("-----BEGIN PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("-----END PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Trim();

        var keyBytes = Convert.FromBase64String(cleanedKey);
        rsa.ImportPkcs8PrivateKey(keyBytes, out _);

        var sigBytes = rsa.SignData(sigInputBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sigB64 = Base64UrlEncode(sigBytes);

        return $"{sigInput}.{sigB64}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
