namespace Octo.MStore.Storage;

public sealed class AzureBlobUploader
{
    private readonly HttpClient _httpClient;
    private const int ChunkSize = 8 * 1024 * 1024;

    public AzureBlobUploader(HttpClient? httpClient = null) => _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(20) };

    public async Task UploadPackageAsync(
        string fileUploadUrl,
        string packagePath,
        Action<long, long, double>? onProgress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Package file not found", packagePath);
        }

        var zipPath = packagePath;
        var isTempZip = false;

        if (!packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipPath = Path.Combine(Path.GetTempPath(), $"octo_pkg_{Guid.NewGuid():N}.zip");
            isTempZip = true;
            CreateZipArchive(packagePath, zipPath);
        }

        var normalizedUrl = fileUploadUrl.Replace("+", "%2B");

        try
        {
            var fileInfo = new FileInfo(zipPath);
            var totalBytes = fileInfo.Length;

            if (totalBytes <= 32 * 1024 * 1024)
            {
                await UploadSingleBlockAsync(normalizedUrl, zipPath, totalBytes, onProgress, ct).ConfigureAwait(false);
            }
            else
            {
                await UploadChunkedBlocksAsync(normalizedUrl, zipPath, totalBytes, onProgress, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (isTempZip && File.Exists(zipPath))
            {
                try
                {
                    File.Delete(zipPath);
                }
                catch
                {
                }
            }
        }
    }

    private static void CreateZipArchive(string sourceFile, string targetZip)
    {
        if (File.Exists(targetZip))
        {
            File.Delete(targetZip);
        }

        using var archive = ZipFile.Open(targetZip, ZipArchiveMode.Create);
        var fileName = Path.GetFileName(sourceFile);
        _ = archive.CreateEntryFromFile(sourceFile, fileName, CompressionLevel.Optimal);
    }

    private async Task UploadSingleBlockAsync(
        string uploadUrl,
        string filePath,
        long totalBytes,
        Action<long, long, double>? onProgress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        request.Headers.Add("x-ms-blob-type", "BlockBlob");

        var content = new StreamContent(fileStream, 65536);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        request.Content = content;

        onProgress?.Invoke(0, totalBytes, 0);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Azure Blob upload failed with status {(int)response.StatusCode}: {error}");
        }

        var elapsedSeconds = Math.Max(0.1, stopwatch.Elapsed.TotalSeconds);
        onProgress?.Invoke(totalBytes, totalBytes, totalBytes / elapsedSeconds);
    }

    private async Task UploadChunkedBlocksAsync(
        string uploadUrl,
        string filePath,
        long totalBytes,
        Action<long, long, double>? onProgress,
        CancellationToken ct)
    {
        var blockIds = new List<string>();
        var buffer = new byte[ChunkSize];
        var uploadedBytes = 0L;
        var blockIndex = 0;
        var stopwatch = Stopwatch.StartNew();

        await using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, useAsync: true))
        {
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, ChunkSize), ct).ConfigureAwait(false)) > 0)
            {
                var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"block_{blockIndex:D6}"));
                blockIds.Add(blockId);

                var separator = uploadUrl.Contains('?') ? "&" : "?";
                var blockUrl = $"{uploadUrl}{separator}comp=block&blockid={Uri.EscapeDataString(blockId)}";

                using var request = new HttpRequestMessage(HttpMethod.Put, blockUrl);
                request.Content = new ByteArrayContent(buffer, 0, bytesRead);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    throw new InvalidOperationException($"Failed uploading block {blockIndex} (status {(int)response.StatusCode}): {err}");
                }

                uploadedBytes += bytesRead;
                blockIndex++;

                var speed = uploadedBytes / Math.Max(0.1, stopwatch.Elapsed.TotalSeconds);
                onProgress?.Invoke(uploadedBytes, totalBytes, speed);
            }
        }

        var commitSeparator = uploadUrl.Contains('?') ? "&" : "?";
        var commitUrl = $"{uploadUrl}{commitSeparator}comp=blocklist";

        var sb = new StringBuilder();
        _ = sb.Append("<BlockList>");
        foreach (var bId in blockIds)
        {
            _ = sb.Append(CultureInfo.InvariantCulture, $"<Latest>{bId}</Latest>");
        }
        _ = sb.Append("</BlockList>");

        using var commitRequest = new HttpRequestMessage(HttpMethod.Put, commitUrl);
        commitRequest.Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/xml");

        using var commitResponse = await _httpClient.SendAsync(commitRequest, ct).ConfigureAwait(false);
        if (!commitResponse.IsSuccessStatusCode)
        {
            var err = await commitResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Failed committing Azure Blob blocklist (status {(int)commitResponse.StatusCode}): {err}");
        }

        onProgress?.Invoke(totalBytes, totalBytes, totalBytes / Math.Max(0.1, stopwatch.Elapsed.TotalSeconds));
    }
}
