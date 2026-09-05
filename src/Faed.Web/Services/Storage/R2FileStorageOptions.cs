namespace Faed.Web.Services.Storage;

public sealed class R2FileStorageOptions
{
    public const string SectionName = "FileStorage:R2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}
