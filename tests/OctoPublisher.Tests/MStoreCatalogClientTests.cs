namespace OctoPublisher.Tests;

public sealed class MStoreCatalogClientTests
{
    [Fact]
    public async Task GetProductAsyncParsesProductAndImagesSuccessfullyAsync()
    {
        var json = /*lang=json,strict*/ """
        {
            "Product": {
                "ProductId": "9TEST1234567",
                "LocalizedProperties": [
                    {
                        "Language": "ru-RU",
                        "ProductTitle": "Тестовое приложение",
                        "ProductDescription": "Полное описание приложения для тестов.",
                        "ShortDescription": "Краткое описание",
                        "Features": ["Фича 1", "Фича 2"],
                        "ReleaseNotes": "Обновление 1.0.5",
                        "PrivacyUrl": "https://example.com/privacy",
                        "Images": [
                            {
                                "ImagePurpose": "Logo",
                                "Uri": "//store-images.s-microsoft.com/image/apps.1.png",
                                "Width": 300,
                                "Height": 300
                            },
                            {
                                "ImagePurpose": "SuperHeroArt",
                                "Uri": "//store-images.s-microsoft.com/image/apps.banner.png",
                                "Width": 1920,
                                "Height": 1080
                            }
                        ]
                    }
                ]
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Contains("9TEST1234567", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var client = new MStoreCatalogClient(httpClient);

        var product = await client.GetProductAsync("9TEST1234567", "RU", "ru-RU");

        Assert.NotNull(product);
        Assert.Equal("9TEST1234567", product.ProductId);
        Assert.NotNull(product.LocalizedProperties);
        var prop = Assert.Single(product.LocalizedProperties);
        Assert.Equal("Тестовое приложение", prop.ProductTitle);
        Assert.Equal(2, prop.Images?.Count);
    }

    [Fact]
    public async Task GetProductAsyncReturnsNullOnHttpNotFoundAsync()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        var client = new MStoreCatalogClient(httpClient);

        var product = await client.GetProductAsync("NONEXISTENT", "US", "en-US");

        Assert.Null(product);
    }

    [Fact]
    public async Task DownloadImageBytesAsyncDownloadsValidBytesAsync()
    {
        var sampleBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("https://store-images.s-microsoft.com/image/test.png", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(sampleBytes)
            };
        });

        using var httpClient = new HttpClient(handler);
        var client = new MStoreCatalogClient(httpClient);

        var downloaded = await client.DownloadImageBytesAsync("//store-images.s-microsoft.com/image/test.png");

        Assert.NotNull(downloaded);
        Assert.Equal(sampleBytes, downloaded);
    }
}
