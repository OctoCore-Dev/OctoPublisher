namespace OctoPublisher.Tests;

public sealed class AzureBlobUploaderTests
{
    [Fact]
    public async Task UploadPackageAsyncThrowsFileNotFoundWhenPackageDoesNotExistAsync()
    {
        var uploader = new AzureBlobUploader();

        _ = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            uploader.UploadPackageAsync("https://blob.core.windows.net/sas", "non_existent_file.msix"));
    }

    [Fact]
    public async Task UploadPackageAsyncUploadsSingleBlockForSmallFileAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.msix");
        await File.WriteAllTextAsync(tempFile, "sample msix package content");

        try
        {
            var handler = new MockHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.True(request.Headers.Contains("x-ms-blob-type"));
                Assert.Equal("BlockBlob", request.Headers.GetValues("x-ms-blob-type").First());
                Assert.Equal("application/zip", request.Content?.Headers.ContentType?.MediaType);
                return new HttpResponseMessage(HttpStatusCode.Created);
            });

            using var httpClient = new HttpClient(handler);
            var uploader = new AzureBlobUploader(httpClient);

            var progressTriggered = false;
            await uploader.UploadPackageAsync(
                "https://storage.blob.core.windows.net/container/package.zip?sas_token",
                tempFile,
                onProgress: (_, _, _) => progressTriggered = true);

            _ = Assert.Single(handler.CapturedRequests);
            Assert.True(progressTriggered);
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
    public async Task UploadPackageAsyncThrowsExceptionWhenUploadFailsAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.zip");
        await File.WriteAllTextAsync(tempFile, "sample zip content");

        try
        {
            var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    """<Error><Code>AuthenticationFailed</Code><Message>Signature expired</Message></Error>""",
                    Encoding.UTF8,
                    "application/xml")
            });

            using var httpClient = new HttpClient(handler);
            var uploader = new AzureBlobUploader(httpClient);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                uploader.UploadPackageAsync("https://storage.blob.core.windows.net/sas", tempFile));

            Assert.Contains("403", exception.Message);
            Assert.Contains("AuthenticationFailed", exception.Message);
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
