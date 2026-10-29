namespace OctoPublisher.Tests;

public sealed class PlayStoreClientTests
{
    [Fact]
    public async Task CreateEditAsyncReturnsEditIdOnSuccessAsync()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", req.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(/*lang=json,strict*/ "{\"id\": \"edit-12345\", \"expiryTimeSeconds\": \"3600\"}", Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        var editId = await playClient.CreateEditAsync("com.example.app", "test-token");
        Assert.Equal("edit-12345", editId);
    }

    [Fact]
    public async Task CreateEditAsyncThrowsOnFailureAsync()
    {
        var mockHandler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(/*lang=json,strict*/ "{\"error\": \"Application not found\"}", Encoding.UTF8, "application/json")
            });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => playClient.CreateEditAsync("com.example.app", "token"));
    }

    [Fact]
    public async Task UploadBundleAsyncThrowsIfFileNotFoundAsync()
    {
        var playClient = new PlayStoreClient();
        _ = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            playClient.UploadBundleAsync("com.example.app", "edit-1", "non-existent.aab", "token"));
    }

    [Fact]
    public async Task UploadBundleAsyncStreamsDataAndReturnsVersionCodeAsync()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempFile, new byte[1024]);

        try
        {
            var mockHandler = new MockHttpMessageHandler(async (req, ct) =>
            {
                Assert.Contains("uploadType=media", req.RequestUri?.Query, StringComparison.Ordinal);
                Assert.Equal("application/octet-stream", req.Content?.Headers.ContentType?.MediaType);
                var bytes = await req.Content!.ReadAsByteArrayAsync(ct);
                Assert.Equal(1024, bytes.Length);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(/*lang=json,strict*/ "{\"versionCode\": 72, \"sha1\": \"abc\"}", Encoding.UTF8, "application/json")
                };
            });

            using var client = new HttpClient(mockHandler);
            var playClient = new PlayStoreClient(client);

            var versionCode = await playClient.UploadBundleAsync("com.example.app", "edit-1", tempFile, "token");
            Assert.Equal(72, versionCode);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task UploadApkAsyncStreamsDataAndReturnsVersionCodeAsync()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempFile, new byte[512]);

        try
        {
            var mockHandler = new MockHttpMessageHandler(async (req, ct) =>
            {
                Assert.Contains("apks?uploadType=media", req.RequestUri?.ToString(), StringComparison.Ordinal);
                var bytes = await req.Content!.ReadAsByteArrayAsync(ct);
                Assert.Equal(512, bytes.Length);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(/*lang=json,strict*/ "{\"versionCode\": 42}", Encoding.UTF8, "application/json")
                };
            });

            using var client = new HttpClient(mockHandler);
            var playClient = new PlayStoreClient(client);

            var versionCode = await playClient.UploadApkAsync("com.example.app", "edit-1", tempFile, "token");
            Assert.Equal(42, versionCode);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task UpdateTrackAsyncSendsCorrectTrackPayloadAsync()
    {
        var mockHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            Assert.Contains("/tracks/production", req.RequestUri?.ToString(), StringComparison.Ordinal);
            var json = await req.Content!.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            Assert.Equal("production", doc.RootElement.GetProperty("track").GetString());
            var releases = doc.RootElement.GetProperty("releases");
            Assert.Equal("completed", releases[0].GetProperty("status").GetString());
            Assert.Equal("72", releases[0].GetProperty("versionCodes")[0].GetString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        var notes = new Dictionary<string, string> { ["ru-RU"] = "Обновление", ["en-US"] = "Update" };
        await playClient.UpdateTrackAsync("com.example.app", "edit-1", "production", 72, "v6.0.8", notes, "token");
    }

    [Fact]
    public async Task UpdateListingAsyncSendsListingPayloadAsync()
    {
        var mockHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            Assert.Contains("/listings/ru-RU", req.RequestUri?.ToString(), StringComparison.Ordinal);
            var json = await req.Content!.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            Assert.Equal("ru-RU", doc.RootElement.GetProperty("language").GetString());
            Assert.Equal("Full Desc", doc.RootElement.GetProperty("fullDescription").GetString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        await playClient.UpdateListingAsync("com.example.app", "edit-1", "ru-RU", "Full Desc", "Short Desc", "App", "token");
    }

    [Fact]
    public async Task GetListingAsyncReturnsNullWhenNotFoundAsync()
    {
        var mockHandler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        var result = await playClient.GetListingAsync("com.example.app", "edit-1", "ru-RU", "token");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetListingAsyncReturnsListingWhenFoundAsync()
    {
        var mockHandler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(/*lang=json,strict*/ "{\"language\":\"ru-RU\",\"title\":\"ExistingTitle\",\"fullDescription\":\"ExistingDesc\"}", Encoding.UTF8, "application/json")
            });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        var result = await playClient.GetListingAsync("com.example.app", "edit-1", "ru-RU", "token");
        Assert.NotNull(result);
        Assert.Equal("ExistingTitle", result.Title);
        Assert.Equal("ru-RU", result.Language);
    }

    [Fact]
    public async Task UpdateListingAsyncSafeMergePreservesExistingTitleAsync()
    {
        var getCallCount = 0;
        var putTitle = string.Empty;

        var mockHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            if (req.Method == HttpMethod.Get)
            {
                getCallCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(/*lang=json,strict*/ "{\"language\":\"ru-RU\",\"title\":\"LiveStoreTitle\",\"fullDescription\":\"Old\"}", Encoding.UTF8, "application/json")
                };
            }

            if (req.Method == HttpMethod.Put)
            {
                var body = await req.Content!.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(body);
                putTitle = doc.RootElement.GetProperty("title").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        await playClient.UpdateListingAsync("com.example.app", "edit-1", "ru-RU", "NewFull", "NewShort", null, "token");

        Assert.Equal(1, getCallCount);
        Assert.Equal("LiveStoreTitle", putTitle);
    }

    [Fact]
    public async Task DeleteAllImagesAsyncSendsDeleteAsync()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Contains("/listings/ru-RU/phoneScreenshots", req.RequestUri?.ToString(), StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        await playClient.DeleteAllImagesAsync("com.example.app", "edit-1", "ru-RU", "phoneScreenshots", "token");
    }

    [Fact]
    public async Task CommitEditAsyncCallsCommitEndpointAsync()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            Assert.Contains(":commit", req.RequestUri?.ToString(), StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        await playClient.CommitEditAsync("com.example.app", "edit-1", "token");
    }

    [Fact]
    public async Task DeleteEditAsyncSendsDeleteHttpAsync()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        using var client = new HttpClient(mockHandler);
        var playClient = new PlayStoreClient(client);

        await playClient.DeleteEditAsync("com.example.app", "edit-1", "token");
    }
}
