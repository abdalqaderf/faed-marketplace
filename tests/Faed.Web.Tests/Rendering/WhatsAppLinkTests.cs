using Faed.Web.Rendering;
using Xunit;

namespace Faed.Web.Tests.Rendering;

/// <summary>
/// The WhatsApp deep link (docs/BUSINESS-MODEL.md §8.3): a plain <c>wa.me</c> link with the
/// order reference pre-filled, no API and no package. The merchant types their phone as free
/// text, so a local <c>07…</c> number is promoted to Jordan's international form.
/// </summary>
public class WhatsAppLinkTests
{
    [Fact]
    public void Build_LocalJordanNumber_IsPromotedToInternational_WithEncodedMessage()
    {
        var link = WhatsAppLink.Build("079 123 4567", "Hi, I reserved a kettle on Faed. Order #FA7K2M");

        Assert.Equal(
            "https://wa.me/962791234567?text=Hi%2C%20I%20reserved%20a%20kettle%20on%20Faed.%20Order%20%23FA7K2M",
            link);
    }

    [Theory]
    [InlineData("+962 79 123 4567", "https://wa.me/962791234567")]
    [InlineData("00962791234567", "https://wa.me/962791234567")]
    public void Build_NormalisesCountryCodePrefixes(string phone, string expectedPrefix)
    {
        var link = WhatsAppLink.Build(phone, "hi");
        Assert.StartsWith(expectedPrefix + "?text=", link);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    public void Build_ReturnsNull_WhenThereAreNotEnoughDigits(string? phone)
    {
        Assert.Null(WhatsAppLink.Build(phone, "hi"));
    }
}
