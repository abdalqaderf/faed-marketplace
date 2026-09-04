namespace Faed.Web.Services.Abstractions;

/// <summary>
/// Abstraction over the system clock so expiry logic (B2C reservations, B2B offers
/// and deals) stays deterministic and testable.
/// All values are UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
