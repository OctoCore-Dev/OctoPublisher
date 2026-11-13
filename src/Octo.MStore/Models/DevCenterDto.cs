namespace Octo.MStore.Models;

public sealed class DevCenterApplication
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("primaryName")]
    public string? PrimaryName { get; set; }

    [JsonPropertyName("packageFamilyName")]
    public string? PackageFamilyName { get; set; }

    [JsonPropertyName("pendingApplicationSubmission")]
    public ApplicationSubmissionInfo? PendingApplicationSubmission { get; set; }

    [JsonPropertyName("lastPublishedApplicationSubmission")]
    public ApplicationSubmissionInfo? LastPublishedApplicationSubmission { get; set; }
}

public sealed class ApplicationSubmissionInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("resourceLocation")]
    public string? ResourceLocation { get; set; }
}

public sealed class DevCenterSubmission
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("applicationId")]
    public string? ApplicationId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("statusDetails")]
    public DevCenterStatusDetails? StatusDetails { get; set; }

    [JsonPropertyName("fileUploadUrl")]
    public string? FileUploadUrl { get; set; }

    [JsonPropertyName("applicationPackages")]
    public List<ApplicationPackage>? ApplicationPackages { get; set; }

    [JsonPropertyName("packages")]
    public List<ApplicationPackage>? Packages { get; set; }

    [JsonPropertyName("listings")]
    public Dictionary<string, DevCenterListing>? Listings { get; set; }

    [JsonPropertyName("trailers")]
    public List<DevCenterTrailer>? Trailers { get; set; }

    [JsonPropertyName("packageDeliveryOptions")]
    public PackageDeliveryOptions? PackageDeliveryOptions { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class ApplicationPackage
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("fileStatus")]
    public string? FileStatus { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class DevCenterImage
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("fileStatus")]
    public string? FileStatus { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("imageType")]
    public string? ImageType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class DevCenterTrailerAsset
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("imageList")]
    public List<DevCenterImage>? ImageList { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class DevCenterTrailer
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("videoFileName")]
    public string? VideoFileName { get; set; }

    [JsonPropertyName("videoFileId")]
    public string? VideoFileId { get; set; }

    [JsonPropertyName("trailerAssets")]
    public Dictionary<string, DevCenterTrailerAsset>? TrailerAssets { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class DevCenterBaseListing
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("features")]
    public List<string>? Features { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("copyrightAndTrademarkInfo")]
    public string? CopyrightAndTrademarkInfo { get; set; }

    [JsonPropertyName("privacyPolicy")]
    public string? PrivacyPolicy { get; set; }

    [JsonPropertyName("supportContact")]
    public string? SupportContact { get; set; }

    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    [JsonPropertyName("images")]
    public List<DevCenterImage>? Images { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class DevCenterListing
{
    [JsonPropertyName("baseListing")]
    public DevCenterBaseListing? BaseListing { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    //прокси-свойства для обратной совместимости с кодом и тестами
    [JsonIgnore]
    public string? Description
    {
        get => BaseListing?.Description;
        set
        {
            BaseListing ??= new DevCenterBaseListing();
            BaseListing.Description = value;
        }
    }

    [JsonIgnore]
    public string? ReleaseNotes
    {
        get => BaseListing?.ReleaseNotes;
        set
        {
            BaseListing ??= new DevCenterBaseListing();
            BaseListing.ReleaseNotes = value;
        }
    }

    [JsonIgnore]
    public List<string>? Features
    {
        get => BaseListing?.Features;
        set
        {
            BaseListing ??= new DevCenterBaseListing();
            BaseListing.Features = value;
        }
    }

    [JsonIgnore]
    public string? Title
    {
        get => BaseListing?.Title;
        set
        {
            BaseListing ??= new DevCenterBaseListing();
            BaseListing.Title = value;
        }
    }
}

public sealed class PackageDeliveryOptions
{
    [JsonPropertyName("packageRollout")]
    public PackageRollout? PackageRollout { get; set; }
}

public sealed class PackageRollout
{
    [JsonPropertyName("isPackageRollout")]
    public bool IsPackageRollout { get; set; }

    [JsonPropertyName("packageRolloutPercentage")]
    public float PackageRolloutPercentage { get; set; }
}

public sealed class DevCenterCommitResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public sealed class DevCenterSubmissionStatus
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("statusDetails")]
    public DevCenterStatusDetails? StatusDetails { get; set; }
}

public sealed class DevCenterStatusDetails
{
    [JsonPropertyName("errors")]
    public List<DevCenterErrorDetail>? Errors { get; set; }

    [JsonPropertyName("warnings")]
    public List<DevCenterErrorDetail>? Warnings { get; set; }

    [JsonPropertyName("certificationReports")]
    public List<DevCenterCertificationReport>? CertificationReports { get; set; }
}

public sealed class DevCenterCertificationReport
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("reportUrl")]
    public string? ReportUrl { get; set; }
}

public sealed class DevCenterErrorDetail
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

public sealed class DevCenterError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}
