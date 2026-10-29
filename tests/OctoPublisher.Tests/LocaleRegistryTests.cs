namespace OctoPublisher.Tests;

public sealed class LocaleRegistryTests
{
    [Theory]
    [InlineData("ru", "ru-RU")]
    [InlineData("en", "en-US")]
    [InlineData("us", "en-US")]
    [InlineData("fr", "fr-FR")]
    [InlineData("de", "de-DE")]
    [InlineData("es", "es-ES")]
    [InlineData("zh", "zh-CN")]
    [InlineData("ja", "ja-JP")]
    [InlineData("it", "it-IT")]
    [InlineData("pt", "pt-BR")]
    [InlineData("tr", "tr-TR")]
    [InlineData("uk", "uk-UA")]
    [InlineData("kk", "kk-KZ")]
    [InlineData("ar", "ar-SA")]
    [InlineData("ru-RU", "ru-RU")]
    [InlineData("en-US", "en-US")]
    public void NormalizeLocaleMapsKnownAliasesCorrectly(string input, string expected)
    {
        var normalized = LocaleRegistry.NormalizeLocale(input);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void NormalizeLocaleReturnsDefaultOnEmptyInput()
    {
        var normalized = LocaleRegistry.NormalizeLocale("");
        Assert.Equal(LocaleRegistry.DefaultLocale, normalized);
    }

    [Fact]
    public void GetDefaultTitleReturnsRussianWhenLocaleStartsWithRu()
    {
        var title = LocaleRegistry.GetDefaultTitle("ru-RU");
        Assert.Contains("[ШАБЛОН]", title);
    }

    [Fact]
    public void GetDefaultTitleReturnsEnglishWhenLocaleStartsWithEn()
    {
        var title = LocaleRegistry.GetDefaultTitle("en-US");
        Assert.Contains("[TEMPLATE]", title);
    }
}
