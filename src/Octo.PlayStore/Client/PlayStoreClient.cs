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
        var uri = $"{ApiBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}";

        var listing = new PlayListing(language, title, fullDescription, shortDescription);
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

        var uri = $"{UploadBaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(imageType)}?uploadType=media";

        await using var stream = File.OpenRead(imagePath);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

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
}
