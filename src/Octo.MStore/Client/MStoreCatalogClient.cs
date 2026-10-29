namespace Octo.MStore.Client;

public sealed class DisplayCatalogResponse
{
    [JsonPropertyName("Product")]
    public DisplayCatalogProduct? Product { get; set; }
}

public sealed class DisplayCatalogProduct
{
    [JsonPropertyName("ProductId")]
    public string? ProductId { get; set; }

    [JsonPropertyName("LocalizedProperties")]
    public List<DisplayCatalogLocalizedProperty>? LocalizedProperties { get; set; }
}

public sealed class DisplayCatalogLocalizedProperty
{
    [JsonPropertyName("Language")]
    public string? Language { get; set; }

    [JsonPropertyName("ProductTitle")]
    public string? ProductTitle { get; set; }

    [JsonPropertyName("ProductDescription")]
    public string? ProductDescription { get; set; }

    [JsonPropertyName("ShortDescription")]
    public string? ShortDescription { get; set; }

    [JsonPropertyName("Features")]
    public List<string>? Features { get; set; }

    [JsonPropertyName("ReleaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("PrivacyUrl")]
    public string? PrivacyUrl { get; set; }

    [JsonPropertyName("SupportUri")]
    public string? SupportUri { get; set; }

    [JsonPropertyName("Images")]
    public List<DisplayCatalogImage>? Images { get; set; }
}

public sealed class DisplayCatalogImage
{
    [JsonPropertyName("ImagePurpose")]
    public string? ImagePurpose { get; set; }

    [JsonPropertyName("Uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("Width")]
    public int Width { get; set; }

    [JsonPropertyName("Height")]
    public int Height { get; set; }
}

public sealed class MStoreCatalogClient(HttpClient? httpClient = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<DisplayCatalogProduct?> GetProductAsync(string productId, string market = "US", string language = "en-US", CancellationToken ct = default)
    {
        var url = $"https://displaycatalog.mp.microsoft.com/v7.0/products/{Uri.EscapeDataString(productId)}?market={Uri.EscapeDataString(market)}&languages={Uri.EscapeDataString(language)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("OctoPublisher/1.1.0");

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var catalog = JsonSerializer.Deserialize<DisplayCatalogResponse>(json);
        return catalog?.Product;
    }

    public async Task<byte[]?> DownloadImageBytesAsync(string uri, CancellationToken ct = default)
    {
        var fullUrl = uri.StartsWith("//", StringComparison.Ordinal) ? "https:" + uri : uri;
        if (!fullUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !fullUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            fullUrl = "https://" + fullUrl.TrimStart('/');
        }

        using var response = await _httpClient.GetAsync(fullUrl, ct);
        return !response.IsSuccessStatusCode ? null : await response.Content.ReadAsByteArrayAsync(ct);
    }
}
