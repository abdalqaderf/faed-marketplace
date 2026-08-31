using Faed.Web.Services.Abstractions;

namespace Faed.Web.Services;

/// <summary>Production <see cref="IClock"/> backed by the machine clock, always UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
