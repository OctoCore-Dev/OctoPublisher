namespace OctoPublisher.Tests;

public sealed class PlayStoreTests
{
    [Fact]
    public void ParseServiceAccountThrowsOnInvalidJson() =>
        Assert.Throws<JsonException>(() => GoogleAuthService.ParseServiceAccount("not-json"));

    [Fact]
    public void ParseServiceAccountThrowsWhenFieldsMissing()
    {
        var invalid = /*lang=json,strict*/ "{\"client_email\": \"\"}";
        _ = Assert.Throws<InvalidOperationException>(() => GoogleAuthService.ParseServiceAccount(invalid));
    }

    [Fact]
    public void ParseServiceAccountParsesValidObject()
    {
        var json = /*lang=json,strict*/ "{\"client_email\": \"bot@test.iam.gserviceaccount.com\", \"private_key\": \"key\", \"project_id\": \"my-project\"}";
        var account = GoogleAuthService.ParseServiceAccount(json);

        Assert.Equal("bot@test.iam.gserviceaccount.com", account.ClientEmail);
        Assert.Equal("key", account.PrivateKey);
        Assert.Equal("my-project", account.ProjectId);
    }

    [Fact]
    public void CreateSignedJwtGeneratesValidThreePartToken()
    {
        using var rsa = RSA.Create(2048);
        var pkcs8Bytes = rsa.ExportPkcs8PrivateKey();
        var pemKey = $"-----BEGIN PRIVATE KEY-----\n{Convert.ToBase64String(pkcs8Bytes)}\n-----END PRIVATE KEY-----";

        var account = new GoogleServiceAccount("bot@example.iam.gserviceaccount.com", pemKey, "proj-1");
        var jwt = GoogleAuthService.CreateSignedJwt(account);

        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);

        var headerJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[0])));
        Assert.Contains("RS256", headerJson, StringComparison.Ordinal);

        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[1])));
        Assert.Contains("bot@example.iam.gserviceaccount.com", payloadJson, StringComparison.Ordinal);

        var sigBytes = Convert.FromBase64String(PadBase64(parts[2]));
        var signedData = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var valid = rsa.VerifyData(signedData, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(valid);
    }

    [Fact]
    public void NormalizeLanguageFormatsCorrectly()
    {
        Assert.Equal("ru-RU", PlayStoreListingManager.NormalizeLanguage("ru_RU"));
        Assert.Equal("ru-RU", PlayStoreListingManager.NormalizeLanguage("whatsnew-ru-RU"));
        Assert.Equal("en-US", PlayStoreListingManager.NormalizeLanguage("whatsnew-en_US"));
        Assert.Equal("en", PlayStoreListingManager.NormalizeLanguage("en"));
    }

    [Fact]
    public void ResolveReleaseNotesPicksSingleFile()
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, "Release notes for version 1");
            var notes = PlayStoreListingManager.ResolveReleaseNotes(temp, null);

            _ = Assert.Single(notes);
            Assert.Equal("Release notes for version 1", notes["en-US"]);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    [Fact]
    public void ResolveReleaseNotesPicksDirectoryFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "whatsnew-ru-RU"), "Русский текст");
            File.WriteAllText(Path.Combine(tempDir, "whatsnew-en-US"), "English text");

            var notes = PlayStoreListingManager.ResolveReleaseNotes(null, tempDir);

            Assert.Equal(2, notes.Count);
            Assert.Equal("Русский текст", notes["ru-RU"]);
            Assert.Equal("English text", notes["en-US"]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task PublishAsyncReturnsValidationErrorsAsync()
    {
        var publisher = new PlayStorePublisher();

        var res1 = await publisher.PublishAsync("", "com.test", "{}");
        Assert.Equal(2, res1.ExitCode);

        var res2 = await publisher.PublishAsync("nonexistent.aab", "com.test", "{}");
        Assert.Equal(2, res2.ExitCode);

        var temp = Path.GetTempFileName();
        try
        {
            var res3 = await publisher.PublishAsync(temp, "", "{}");
            Assert.Equal(2, res3.ExitCode);

            var res4 = await publisher.PublishAsync(temp, "com.test", "{}");
            Assert.Equal(2, res4.ExitCode);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    [Fact]
    public async Task PublishAsyncExecutesFullLifecycleSuccessfullyAsync()
    {
        using var rsa = RSA.Create(2048);
        var pemKey = $"-----BEGIN PRIVATE KEY-----\n{Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())}\n-----END PRIVATE KEY-----";
        var saJson = $"{{\"client_email\":\"bot@test.iam.gserviceaccount.com\",\"private_key\":\"{pemKey.Replace("\n", "\\n", StringComparison.Ordinal)}\"}}";

        var tempAab = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.aab");
        await File.WriteAllBytesAsync(tempAab, new byte[256]);

        try
        {
            var mockHandler = new MockHttpMessageHandler((req, _) =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.Contains("oauth2.googleapis.com/token", StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ "{\"access_token\":\"mock-token\",\"expires_in\":3600,\"token_type\":\"Bearer\"}", Encoding.UTF8, "application/json")
                    });
                }

                if (uri.EndsWith("/edits", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ "{\"id\":\"edit-999\"}", Encoding.UTF8, "application/json")
                    });
                }

                if (uri.Contains("/bundles", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ "{\"versionCode\":77}", Encoding.UTF8, "application/json")
                    });
                }

                var isTrackOrCommit = uri.Contains("/tracks/", StringComparison.OrdinalIgnoreCase)
                    || uri.Contains(":commit", StringComparison.OrdinalIgnoreCase);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(isTrackOrCommit ? "{}" : "{}", Encoding.UTF8, "application/json")
                });
            });

            using var httpClient = new HttpClient(mockHandler);
            var authService = new GoogleAuthService(httpClient);
            var playClient = new PlayStoreClient(httpClient);

            var publisher = new PlayStorePublisher(authService, playClient);
            var result = await publisher.PublishAsync(
                bundleOrApkPath: tempAab,
                packageName: "com.octocore.obxodka",
                serviceAccountInput: saJson,
                track: "production");

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("edit-999", result.OperationId);
        }
        finally
        {
            if (File.Exists(tempAab))
            {
                File.Delete(tempAab);
            }
        }
    }

    private static string PadBase64(string input)
    {
        var replaced = input.Replace('-', '+').Replace('_', '/');
        return (replaced.Length % 4) switch
        {
            2 => replaced + "==",
            3 => replaced + "=",
            _ => replaced
        };
    }
}
