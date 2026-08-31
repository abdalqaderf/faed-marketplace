using Faed.Domain.Identity;
using Faed.Infrastructure.Time;

namespace Faed.UnitTests;

/// <summary>
/// Minimal foundation smoke tests (TASK-001 Phase 5). Business-rule tests are added
/// alongside the features that introduce them.
/// </summary>
public class FoundationSmokeTests
{
    [Fact]
    public void FaedRoles_All_ContainsExactlyTheThreeMvpRoles()
    {
        Assert.Equal(new[] { "Buyer", "Merchant", "Admin" }, FaedRoles.All);
    }

    [Fact]
    public void SystemClock_UtcNow_IsUtcAndCurrent()
    {
        var before = DateTime.UtcNow;

        var now = new SystemClock().UtcNow;

        Assert.Equal(DateTimeKind.Utc, now.Kind);
        Assert.InRange(now, before.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }
}
