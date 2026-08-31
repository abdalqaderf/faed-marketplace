namespace Faed.Application.Abstractions;

/// <summary>
/// Abstraction over the system clock so expiry logic (B2C reservations, B2B offers
/// and deals) stays deterministic and testable. See docs/06-ARCHITECTURE.md §8.
/// All values are UTC (AGENTS.md §6).
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
