namespace Faed.Web.Services.Email;

public sealed class BrevoEmailOptions
{
    public const string SectionName = "Email:Brevo";

    public string ApiKey { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Faed";
}