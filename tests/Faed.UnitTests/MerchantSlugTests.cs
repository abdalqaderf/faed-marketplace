using Faed.Application.Merchants;

namespace Faed.UnitTests;

public class MerchantSlugTests
{
    [Theory]
    [InlineData("Amman Threads", "amman-threads")]
    [InlineData("  Zaid & Co.  ", "zaid-co")]
    [InlineData("Café Style", "cafe-style")]
    [InlineData("متجر", "merchant")]
    [InlineData("", "merchant")]
    public void Slugify_ProducesUrlSafeLowercaseSlug(string input, string expected)
    {
        Assert.Equal(expected, MerchantSlug.Slugify(input));
    }
}
