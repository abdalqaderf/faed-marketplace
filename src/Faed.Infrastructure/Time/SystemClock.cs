using Faed.Application.Abstractions;

namespace Faed.Infrastructure.Time;

/// <summary>Production <see cref="IClock"/> backed by the machine clock, always UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
