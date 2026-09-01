using System.Globalization;
using Faed.Web.Rendering;

namespace Faed.UnitTests;

public class AmmanTimeTests
{
    [Fact]
    public void FormatDateAndTime_UnderArabicCulture_UseEnglishAndAmmanTime()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-JO");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-JO");
            var utc = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

            Assert.Equal("31 Aug 2026 15:00", AmmanTime.FormatDateTime(utc));
            Assert.Equal("31 Aug 2026", AmmanTime.FormatDate(utc));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
