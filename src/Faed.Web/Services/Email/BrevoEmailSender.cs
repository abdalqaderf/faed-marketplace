using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Email;

public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly BrevoEmailOptions _options;

    public BrevoEmailSender(
        HttpClient httpClient,
        IOptions<BrevoEmailOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Email:Brevo:ApiKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException(
                "Email:Brevo:FromEmail is not configured.");
        }
    }

    public async Task SendEmailAsync(
        string email,
        string subject,
        string htmlMessage)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v3/smtp/email");

        request.Headers.Add("api-key", _options.ApiKey);

        request.Content = JsonContent.Create(new
        {
            sender = new
            {
                name = _options.FromName,
                email = _options.FromEmail
            },
            to = new[]
            {
                new
                {
                    email
                }
            },
            subject,
            htmlContent = htmlMessage
        });

        using var response =
            await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Brevo email API failed with status {(int)response.StatusCode}: {responseBody}");
        }
    }
}