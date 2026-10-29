namespace Octo.PlayStore.Models;

public sealed record GoogleServiceAccount(
    [property: JsonPropertyName("client_email")] string ClientEmail,
    [property: JsonPropertyName("private_key")] string PrivateKey,
    [property: JsonPropertyName("project_id")] string? ProjectId
);

public sealed record GoogleTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType
);

public sealed record PlayEditResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("expiryTimeSeconds")] string? ExpiryTimeSeconds
);

public sealed record PlayBundleResponse(
    [property: JsonPropertyName("versionCode")] int VersionCode,
    [property: JsonPropertyName("sha1")] string? Sha1,
    [property: JsonPropertyName("sha256")] string? Sha256
);

public sealed record PlayApkResponse(
    [property: JsonPropertyName("versionCode")] int VersionCode,
    [property: JsonPropertyName("binary")] PlayApkBinary? Binary
);

public sealed record PlayApkBinary(
    [property: JsonPropertyName("sha1")] string? Sha1,
    [property: JsonPropertyName("sha256")] string? Sha256
);

public sealed record PlayLocalizedText(
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("text")] string Text
);

public sealed record PlayRelease(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("versionCodes")] List<string> VersionCodes,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("releaseNotes")] List<PlayLocalizedText>? ReleaseNotes
);

public sealed record PlayTrackUpdateRequest(
    [property: JsonPropertyName("track")] string Track,
    [property: JsonPropertyName("releases")] List<PlayRelease> Releases
);

public sealed record PlayListing(
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("fullDescription")] string? FullDescription,
    [property: JsonPropertyName("shortDescription")] string? ShortDescription,
    [property: JsonPropertyName("video")] string? Video = null
);

public sealed record PlayListingsResponse(
    [property: JsonPropertyName("listings")] List<PlayListing>? Listings
);

public sealed record PlayImage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sha1")] string? Sha1 = null,
    [property: JsonPropertyName("sha256")] string? Sha256 = null
);

public sealed record PlayImagesResponse(
    [property: JsonPropertyName("images")] List<PlayImage>? Images
);

public sealed class PlayListingInfo
{
    public string Language { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }
    public string? IconPath { get; set; }
    public string? FeatureGraphicPath { get; set; }
    public List<string> PhoneScreenshots { get; set; } = [];
    public List<string> SevenInchScreenshots { get; set; } = [];
    public List<string> TenInchScreenshots { get; set; } = [];
}

public sealed record PlayStoreCredentials(
    string PackageName,
    string ServiceAccountJson,
    string Track = "internal"
);
