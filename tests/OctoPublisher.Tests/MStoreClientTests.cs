namespace OctoPublisher.Tests;

public sealed class MStoreClientTests
{
    private static readonly MStoreCredentials t_testCreds = new(
        "tenant-test",
        "client-test",
        "secret-test",
        "app-test-id");

    [Fact]
    public async Task GetApplicationAsyncReturnsApplicationOnSuccessAsync()
    {
        var json = /*lang=json,strict*/ """
        {
            "id": "app-test-id",
            "primaryName": "OctoApp",
            "packageFamilyName": "OctoApp_abc123"
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("https://manage.devcenter.microsoft.com/v1.0/my/applications/app-test-id", req.RequestUri!.ToString());
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", req.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler);
        var client = new MStoreClient(http);

        var app = await client.GetApplicationAsync("app-test-id", "test-token", CancellationToken.None);

        Assert.NotNull(app);
        Assert.Equal("app-test-id", app.Id);
        Assert.Equal("OctoApp", app.PrimaryName);
        Assert.Equal("OctoApp_abc123", app.PackageFamilyName);
    }

    [Fact]
    public async Task GetSubmissionAsyncReturnsNullOnNotFoundAsync()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var client = new MStoreClient(http);

        var sub = await client.GetSubmissionAsync("app-test-id", "sub-missing", "test-token", CancellationToken.None);

        Assert.Null(sub);
    }

    [Fact]
    public async Task GetSubmissionAsyncReturnsSubmissionOnSuccessAsync()
    {
        var json = /*lang=json,strict*/ """
        {
            "id": "sub-100",
            "status": "PendingCommit",
            "fileUploadUrl": "https://blob.core.windows.net/sas-target"
        }
        """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler);
        var client = new MStoreClient(http);

        var sub = await client.GetSubmissionAsync("app-test-id", "sub-100", "test-token", CancellationToken.None);

        Assert.NotNull(sub);
        Assert.Equal("sub-100", sub.Id);
        Assert.Equal("PendingCommit", sub.Status);
        Assert.Equal("https://blob.core.windows.net/sas-target", sub.FileUploadUrl);
    }

    [Fact]
    public async Task DeleteSubmissionAsyncReturnsTrueOnSuccessAsync()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var http = new HttpClient(handler);
        var client = new MStoreClient(http);

        var deleted = await client.DeleteSubmissionAsync("app-test-id", "sub-draft", "test-token", CancellationToken.None);

        Assert.True(deleted);
    }

    [Fact]
    public async Task DeleteSubmissionAsyncReturnsFalseOnFailureAsync()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict));
        using var http = new HttpClient(handler);
        var client = new MStoreClient(http);

        var deleted = await client.DeleteSubmissionAsync("app-test-id", "sub-conflict", "test-token", CancellationToken.None);

        Assert.False(deleted);
    }

    [Fact]
    public async Task PublishAsyncReturnsFailedWhenPackageNotFoundAsync()
    {
        var client = new MStoreClient();

        var result = await client.PublishAsync(t_testCreds, "non_existent_file.msix");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Target package file not found", result.Message);
    }

    [Fact]
    public async Task PublishAsyncSafelySkipsWhenActiveSubmissionInReleaseOrCertAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"publish_test_{Guid.NewGuid():N}.msix");
        await File.WriteAllTextAsync(tempFile, "fake msix payload");

        try
        {
            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri!.ToString();

                return uri.Contains("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """{"access_token":"valid_token","expires_in":"3600"}""", Encoding.UTF8, "application/json")
                    }
                    : uri.EndsWith("/v1.0/my/applications/app-test-id", StringComparison.OrdinalIgnoreCase)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "id": "app-test-id",
                            "primaryName": "TestApp",
                            "pendingApplicationSubmission": { "id": "sub-active-42" }
                        }
                        """, Encoding.UTF8, "application/json")
                    }
                    : uri.Contains("/submissions/sub-active-42", StringComparison.OrdinalIgnoreCase)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "id": "sub-active-42",
                            "status": "Release"
                        }
                        """, Encoding.UTF8, "application/json")
                    }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            using var http = new HttpClient(handler);
            var tokenService = new MStoreTokenService(http);
            var client = new MStoreClient(http, tokenService);

            var result = await client.PublishAsync(t_testCreds, tempFile);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Release", result.Message);
            Assert.Equal("sub-active-42", result.OperationId);
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
    public async Task PublishAsyncEndToEndReplacesDraftUploadsAndCommitsAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"publish_full_{Guid.NewGuid():N}.msix");
        await File.WriteAllTextAsync(tempFile, "fake msix payload");

        try
        {
            var deletedStaleDraft = false;
            var createdNewDraft = false;
            var uploadedBlob = false;
            var updatedMetadata = false;
            var committedSubmission = false;

            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri!.ToString();
                var method = req.Method;

                if (uri.Contains("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """{"access_token":"token_123","expires_in":"3600"}""", Encoding.UTF8, "application/json")
                    };
                }

                if (uri.EndsWith("/v1.0/my/applications/app-test-id", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "id": "app-test-id",
                            "primaryName": "TestApp",
                            "pendingApplicationSubmission": { "id": "stale-sub-1" }
                        }
                        """, Encoding.UTF8, "application/json")
                    };
                }

                if (method == HttpMethod.Get && uri.Contains("/submissions/stale-sub-1", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "id": "stale-sub-1",
                            "status": "PendingCommit"
                        }
                        """, Encoding.UTF8, "application/json")
                    };
                }

                if (method == HttpMethod.Delete && uri.Contains("/submissions/stale-sub-1", StringComparison.OrdinalIgnoreCase))
                {
                    deletedStaleDraft = true;
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                if (method == HttpMethod.Post && uri.EndsWith("/submissions", StringComparison.OrdinalIgnoreCase))
                {
                    createdNewDraft = true;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "id": "new-sub-99",
                            "status": "PendingCommit",
                            "fileUploadUrl": "https://storage.blob.core.windows.net/sas-dest",
                            "applicationPackages": [],
                            "listings": {}
                        }
                        """, Encoding.UTF8, "application/json")
                    };
                }

                if (method == HttpMethod.Put && uri.Contains("storage.blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
                {
                    uploadedBlob = true;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }

                if (method == HttpMethod.Put && uri.Contains("/submissions/new-sub-99", StringComparison.OrdinalIgnoreCase))
                {
                    updatedMetadata = true;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "id": "new-sub-99",
                            "status": "PendingCommit"
                        }
                        """, Encoding.UTF8, "application/json")
                    };
                }

                if (method == HttpMethod.Post && uri.EndsWith("/submissions/new-sub-99/commit", StringComparison.OrdinalIgnoreCase))
                {
                    committedSubmission = true;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "status": "CommitStarted"
                        }
                        """, Encoding.UTF8, "application/json")
                    };
                }

                return method == HttpMethod.Get && uri.EndsWith("/submissions/new-sub-99", StringComparison.OrdinalIgnoreCase)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(/*lang=json,strict*/ """
                        {
                            "id": "new-sub-99",
                            "status": "CommitComplete"
                        }
                        """, Encoding.UTF8, "application/json")
                    }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            using var http = new HttpClient(handler);
            var tokenService = new MStoreTokenService(http);
            var blobUploader = new AzureBlobUploader(http);
            var client = new MStoreClient(http, tokenService, blobUploader);

            var notesByLang = new Dictionary<string, string>
            {
                ["en-US"] = "Bug fixes and improvements"
            };

            var result = await client.PublishAsync(t_testCreds, tempFile, notesByLang);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("new-sub-99", result.OperationId);
            Assert.True(deletedStaleDraft);
            Assert.True(createdNewDraft);
            Assert.True(uploadedBlob);
            Assert.True(updatedMetadata);
            Assert.True(committedSubmission);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
