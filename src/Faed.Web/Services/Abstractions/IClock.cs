namespace Faed.Web.Services.Abstractions;

/// <summary>
/// Abstraction over the system clock so expiry logic (order reservations) stays
/// deterministic and testable.
/// All values are UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
