namespace Octo.PlayStore.Client;

public sealed class PlayStoreClient(HttpClient? httpClient = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private readonly JsonSerializerOptions _ignoreNullOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private const string ApiBaseUrl = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications";
    private const string UploadBaseUrl = "https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications";

    public async Task<string> CreateEditAsync(string packageName, string token, CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to create Google Play edit: {response.StatusCode} - {content}");
        }

        var edit = JsonSerializer.Deserialize<PlayEditResponse>(content);
        return edit?.Id ?? throw new InvalidOperationException("Failed to parse edit ID from Google Play response.");
    }

    public async Task<int> UploadBundleAsync(
        string packageName,
        string editId,
        string bundlePath,
        string token,
        CancellationToken ct = default)
    {
        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException("Android App Bundle (.aab) file not found.", bundlePath);
        }

        var uri = $"{UploadBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/bundles?uploadType=media";

        await using var stream = File.OpenRead(bundlePath);
        using var content = new StreamContent(stream, 64 * 1024);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        var respString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to upload bundle to Google Play: {response.StatusCode} - {respString}");
        }

        var bundleResp = JsonSerializer.Deserialize<PlayBundleResponse>(respString);
        return bundleResp?.VersionCode ?? throw new InvalidOperationException("Invalid bundle upload response from Google Play.");
    }

    public async Task<int> UploadApkAsync(
        string packageName,
        string editId,
        string apkPath,
        string token,
        CancellationToken ct = default)
    {
        if (!File.Exists(apkPath))
        {
            throw new FileNotFoundException("Android APK file not found.", apkPath);
        }

        var uri = $"{UploadBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/apks?uploadType=media";

        await using var stream = File.OpenRead(apkPath);
        using var content = new StreamContent(stream, 64 * 1024);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        var respString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to upload APK to Google Play: {response.StatusCode} - {respString}");
        }

        var apkResp = JsonSerializer.Deserialize<PlayApkResponse>(respString);
        return apkResp?.VersionCode ?? throw new InvalidOperationException("Invalid APK upload response from Google Play.");
    }

    public async Task UpdateTrackAsync(
        string packageName,
        string editId,
        string track,
        int versionCode,
        string? releaseName = null,
        Dictionary<string, string>? releaseNotes = null,
        string? token = null,
        CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/tracks/{Uri.EscapeDataString(track)}";

        List<PlayLocalizedText>? notes = null;
        if (releaseNotes is { Count: > 0 })
        {
            notes = [.. releaseNotes.Select(kv => new PlayLocalizedText(kv.Key, kv.Value))];
        }

        var release = new PlayRelease(
            Name: releaseName ?? versionCode.ToString(CultureInfo.InvariantCulture),
            VersionCodes: [versionCode.ToString(CultureInfo.InvariantCulture)],
            Status: "completed",
            ReleaseNotes: notes
        );

        var trackReq = new PlayTrackUpdateRequest(track, [release]);
        var json = JsonSerializer.Serialize(trackReq);

        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await _httpClient.SendAsync(request, ct);
        var respString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to update track '{track}' on Google Play: {response.StatusCode} - {respString}");
        }
    }

    public async Task<PlayListing?> GetListingAsync(
        string packageName,
        string editId,
        string language,
        string token,
        CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var respString = await response.Content.ReadAsStringAsync(ct);
        return !response.IsSuccessStatusCode
            ? throw new HttpRequestException($"Failed to get listing for '{language}': {response.StatusCode} - {respString}")
            : JsonSerializer.Deserialize<PlayListing>(respString);
    }

    public async Task<List<PlayListing>> GetListingsAsync(
        string packageName,
        string editId,
        string token,
        CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var respString = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to get listings: {response.StatusCode} - {respString}");
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<PlayListingsResponse>(content);
        return parsed?.Listings ?? [];
    }

    public async Task<bool> DeleteListingAsync(
        string packageName,
        string editId,
        string language,
        string token,
        CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
    }

    public async Task UpdateListingAsync(
        string packageName,
        string editId,
        string language,
        string? fullDescription,
        string? shortDescription,
        string? title,
        string token,
        CancellationToken ct = default)
    {
        var resolvedTitle = title;
        var resolvedFull = fullDescription;
        var resolvedShort = shortDescription;

        if (string.IsNullOrWhiteSpace(resolvedTitle) ||
            string.IsNullOrWhiteSpace(resolvedFull) ||
            string.IsNullOrWhiteSpace(resolvedShort))
        {
            try
            {
                var existing = await GetListingAsync(packageName, editId, language, token, ct);
                if (existing != null)
                {
                    if (string.IsNullOrWhiteSpace(resolvedTitle) && !string.IsNullOrWhiteSpace(existing.Title))
                    {
                        resolvedTitle = existing.Title;
                    }
                    if (string.IsNullOrWhiteSpace(resolvedFull) && !string.IsNullOrWhiteSpace(existing.FullDescription))
                    {
                        resolvedFull = existing.FullDescription;
                    }
                    if (string.IsNullOrWhiteSpace(resolvedShort) && !string.IsNullOrWhiteSpace(existing.ShortDescription))
                    {
                        resolvedShort = existing.ShortDescription;
                    }
                }
            }
            catch
            {
            }
        }

        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}";

        var listing = new PlayListing(language, resolvedTitle, resolvedFull, resolvedShort);
        var json = JsonSerializer.Serialize(listing, _ignoreNullOptions);

        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        var respString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to update listing for '{language}': {response.StatusCode} - {respString}");
        }
    }

    public async Task DeleteAllImagesAsync(
        string packageName,
        string editId,
        string language,
        string imageType,
        string token,
        CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(imageType)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var respString = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to delete {imageType} images for '{language}': {response.StatusCode} - {respString}");
        }
    }

    public async Task UploadImageAsync(
        string packageName,
        string editId,
        string language,
        string imageType,
        string imagePath,
        string token,
        CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image file not found.", imagePath);
        }

        var ext = Path.GetExtension(imagePath).ToLowerInvariant();
        var mime = ext is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png";

        var uri = $"{UploadBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(imageType)}?uploadType=media";

        await using var stream = File.OpenRead(imagePath);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(mime);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        var respString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to upload {imageType} image: {response.StatusCode} - {respString}");
        }
    }

    public async Task CommitEditAsync(string packageName, string editId, string token, CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}:commit";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        var respString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to commit Google Play edit: {response.StatusCode} - {respString}");
        }
    }

    public async Task DeleteEditAsync(string packageName, string editId, string token, CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var respString = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to delete Google Play edit: {response.StatusCode} - {respString}");
        }
    }

    public async Task<List<PlayImage>> GetImagesAsync(
        string packageName,
        string editId,
        string language,
        string imageType,
        string token,
        CancellationToken ct = default)
    {
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(imageType)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<PlayImagesResponse>(content);
        return parsed?.Images ?? [];
    }

    public async Task<byte[]?> DownloadImageBytesAsync(string url, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(url, ct);
        return !response.IsSuccessStatusCode ? null : await response.Content.ReadAsByteArrayAsync(ct);
    }
}
