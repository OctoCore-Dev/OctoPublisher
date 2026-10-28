namespace Octo.MStore.Client;

public sealed class MStoreClient
{
    private readonly HttpClient _httpClient;
    private readonly MStoreTokenService _tokenService;
    private readonly AzureBlobUploader _blobUploader;
    private const string BaseUrl = "https://manage.devcenter.microsoft.com";

    public MStoreClient(
        HttpClient? httpClient = null,
        MStoreTokenService? tokenService = null,
        AzureBlobUploader? blobUploader = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _tokenService = tokenService ?? new MStoreTokenService(_httpClient);
        _blobUploader = blobUploader ?? new AzureBlobUploader();
    }

    public async Task<PublisherResult> PublishAsync(
        MStoreCredentials credentials,
        string packagePath,
        IReadOnlyDictionary<string, string>? releaseNotesByLang = null,
        IReadOnlyDictionary<string, string>? descriptionsByLang = null,
        string? defaultNotes = null,
        string? defaultDescription = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(packagePath))
        {
            return PublisherResult.Failed(1, $"Target package file not found: {packagePath}");
        }

        AnsiTerminal.Header("OctoPublisher: Microsoft Store Deployment");
        AnsiTerminal.Step("Auth", "Authenticating with Microsoft Entra ID (Azure AD)...");

        string accessToken;
        try
        {
            accessToken = await _tokenService.GetAccessTokenAsync(credentials, ct).ConfigureAwait(false);
            AnsiTerminal.Success("Authenticated with Microsoft Dev Center successfully.");
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"Authentication failed: {ex.Message}");
            return PublisherResult.Failed(2, ex.Message);
        }

        AnsiTerminal.Step("App", $"Fetching application info for App ID: {credentials.AppId}...");
        DevCenterApplication app;
        try
        {
            app = await GetApplicationAsync(credentials.AppId, accessToken, ct).ConfigureAwait(false);
            AnsiTerminal.Detail("Application Name", app.PrimaryName ?? "Unknown");
            AnsiTerminal.Detail("Package Family Name", app.PackageFamilyName ?? "None");
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"Failed retrieving application: {ex.Message}");
            return PublisherResult.Failed(3, ex.Message);
        }

        DevCenterSubmission? activeSubmission = null;
        var pendingInfo = app.PendingApplicationSubmission;

        if (pendingInfo != null && !string.IsNullOrWhiteSpace(pendingInfo.Id))
        {
            AnsiTerminal.Step("Inspect", $"Found existing submission: {pendingInfo.Id}");
            try
            {
                activeSubmission = await GetSubmissionAsync(credentials.AppId, pendingInfo.Id, accessToken, ct).ConfigureAwait(false);
                var status = activeSubmission?.Status ?? "Unknown";
                AnsiTerminal.Detail("Submission Status", status);

                if (string.Equals(status, "Release", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "Certification", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "CommitStarted", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "PreProcessing", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiTerminal.Warning($"Submission {pendingInfo.Id} is already in '{status}' stage in Microsoft Store pipeline.");
                    AnsiTerminal.Info("Skipping redundant re-submission to protect active release.");
                    return PublisherResult.Success($"Active submission already in processing: {status}", pendingInfo.Id);
                }

                if (string.Equals(status, "PendingCommit", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiTerminal.Info("Stale uncommitted draft detected. Deleting to obtain fresh Azure SAS URL...");
                    _ = await DeleteSubmissionAsync(credentials.AppId, pendingInfo.Id, accessToken, ct).ConfigureAwait(false);
                    activeSubmission = null;
                }
            }
            catch (Exception ex)
            {
                AnsiTerminal.Warning($"Could not inspect existing submission: {ex.Message}. Attempting fresh draft...");
                activeSubmission = null;
            }
        }

        if (activeSubmission == null || string.IsNullOrWhiteSpace(activeSubmission.FileUploadUrl))
        {
            AnsiTerminal.Step("Draft", "Creating new draft submission from last published baseline...");
            try
            {
                activeSubmission = await CreateSubmissionAsync(credentials.AppId, accessToken, ct).ConfigureAwait(false);
                AnsiTerminal.Success($"Created new draft submission: {activeSubmission.Id}");
            }
            catch (Exception ex)
            {
                AnsiTerminal.Error($"Could not create submission: {ex.Message}");
                return PublisherResult.Failed(4, ex.Message);
            }
        }

        if (string.IsNullOrWhiteSpace(activeSubmission.FileUploadUrl))
        {
            AnsiTerminal.Error("Dev Center did not provide a valid fileUploadUrl for package upload.");
            return PublisherResult.Failed(5, "Missing fileUploadUrl in submission response.");
        }

        var pkgName = Path.GetFileName(packagePath);
        var pkgLength = new FileInfo(packagePath).Length;
        AnsiTerminal.Step("Upload", $"Uploading package '{pkgName}' ({pkgLength / 1048576.0:0.0} MB) to Azure Storage SAS...");

        try
        {
            await _blobUploader.UploadPackageAsync(activeSubmission.FileUploadUrl, packagePath, (cur, total, speed) => AnsiTerminal.ProgressBar(cur, total, speed), ct).ConfigureAwait(false);
            AnsiTerminal.EndProgressBar();
            AnsiTerminal.Success("Package uploaded to Azure Storage successfully.");
        }
        catch (Exception ex)
        {
            AnsiTerminal.EndProgressBar();
            AnsiTerminal.Error($"Upload failed: {ex.Message}");
            return PublisherResult.Failed(6, ex.Message);
        }

        AnsiTerminal.Step("Metadata", "Applying package references and localized release notes...");
        MStoreListingManager.ApplyPackageUpdate(activeSubmission, pkgName);
        MStoreListingManager.ApplyLocalizedMetadata(activeSubmission, releaseNotesByLang, descriptionsByLang, defaultNotes, defaultDescription);

        try
        {
            activeSubmission = await UpdateSubmissionAsync(credentials.AppId, activeSubmission.Id!, activeSubmission, accessToken, ct).ConfigureAwait(false);
            AnsiTerminal.Success("Submission configuration updated.");
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"Failed updating submission metadata: {ex.Message}");
            return PublisherResult.Failed(7, ex.Message);
        }

        AnsiTerminal.Step("Commit", "Committing submission to Microsoft certification pipeline (POST /commit)...");
        try
        {
            var commitResp = await CommitSubmissionAsync(credentials.AppId, activeSubmission.Id!, accessToken, ct).ConfigureAwait(false);
            AnsiTerminal.Success($"Commit initiated. Current status: {commitResp.Status}");
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"Failed committing submission: {ex.Message}");
            return PublisherResult.Failed(8, ex.Message);
        }

        AnsiTerminal.Step("Monitor", "Polling Microsoft Store ingestion pipeline status...");
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(6), ct).ConfigureAwait(false);

            try
            {
                var check = await GetSubmissionAsync(credentials.AppId, activeSubmission.Id!, accessToken, ct).ConfigureAwait(false);
                var checkStatus = check?.Status ?? "Unknown";
                AnsiTerminal.Detail($"Poll {i + 1}/30", checkStatus);

                if (string.Equals(checkStatus, "CommitComplete", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(checkStatus, "Certification", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(checkStatus, "PreProcessing", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(checkStatus, "Release", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiTerminal.Success($"Submission reached '{checkStatus}' status!");
                    break;
                }

                if (string.Equals(checkStatus, "CommitFailed", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiTerminal.Error("Microsoft Dev Center rejected the submission (CommitFailed).");
                    return PublisherResult.Failed(9, "Submission commit failed in Dev Center pipeline.", activeSubmission.Id);
                }
            }
            catch (Exception ex)
            {
                AnsiTerminal.Detail($"Poll {i + 1}/30", $"Retry: {ex.Message}");
            }
        }

        AnsiTerminal.Header("Microsoft Store Publication Complete");
        return PublisherResult.Success($"Published submission {activeSubmission.Id}", activeSubmission.Id);
    }

    public async Task<DevCenterApplication> GetApplicationAsync(string appId, string token, CancellationToken ct)
    {
        var url = $"{BaseUrl}/v1.0/my/applications/{appId}";
        using var req = CreateRequest(HttpMethod.Get, url, token);
        using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(resp, ct).ConfigureAwait(false);
        var app = await resp.Content.ReadFromJsonAsync<DevCenterApplication>(cancellationToken: ct).ConfigureAwait(false);
        return app ?? throw new InvalidOperationException("Failed deserializing application response");
    }

    public async Task<DevCenterSubmission?> GetSubmissionAsync(string appId, string submissionId, string token, CancellationToken ct)
    {
        var url = $"{BaseUrl}/v1.0/my/applications/{appId}/submissions/{submissionId}";
        using var req = CreateRequest(HttpMethod.Get, url, token);
        using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(resp, ct).ConfigureAwait(false);
        return await resp.Content.ReadFromJsonAsync<DevCenterSubmission>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<DevCenterSubmission> CreateSubmissionAsync(string appId, string token, CancellationToken ct)
    {
        var url = $"{BaseUrl}/v1.0/my/applications/{appId}/submissions";
        using var req = CreateRequest(HttpMethod.Post, url, token);
        req.Content = new ByteArrayContent([]);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(resp, ct).ConfigureAwait(false);
        var sub = await resp.Content.ReadFromJsonAsync<DevCenterSubmission>(cancellationToken: ct).ConfigureAwait(false);
        return sub ?? throw new InvalidOperationException("Failed deserializing created submission");
    }

    public async Task<DevCenterSubmission> UpdateSubmissionAsync(string appId, string submissionId, DevCenterSubmission submission, string token, CancellationToken ct)
    {
        var url = $"{BaseUrl}/v1.0/my/applications/{appId}/submissions/{submissionId}";
        using var req = CreateRequest(HttpMethod.Put, url, token);
        var json = JsonSerializer.Serialize(submission);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(resp, ct).ConfigureAwait(false);
        var updated = await resp.Content.ReadFromJsonAsync<DevCenterSubmission>(cancellationToken: ct).ConfigureAwait(false);
        return updated ?? throw new InvalidOperationException("Failed deserializing updated submission");
    }

    public async Task<bool> DeleteSubmissionAsync(string appId, string submissionId, string token, CancellationToken ct)
    {
        var url = $"{BaseUrl}/v1.0/my/applications/{appId}/submissions/{submissionId}";
        using var req = CreateRequest(HttpMethod.Delete, url, token);
        using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }

    public async Task<DevCenterCommitResponse> CommitSubmissionAsync(string appId, string submissionId, string token, CancellationToken ct)
    {
        var url = $"{BaseUrl}/v1.0/my/applications/{appId}/submissions/{submissionId}/commit";
        using var req = CreateRequest(HttpMethod.Post, url, token);
        req.Content = new ByteArrayContent([]);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(resp, ct).ConfigureAwait(false);
        var commit = await resp.Content.ReadFromJsonAsync<DevCenterCommitResponse>(cancellationToken: ct).ConfigureAwait(false);
        return commit ?? throw new InvalidOperationException("Failed deserializing commit response");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new InvalidOperationException($"Dev Center API call returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}");
    }
}
