namespace Faed.Web.Data.Seed;

/// <summary>
/// Configuration for the deterministic development/demo data set.
/// The demo seed is <b>never</b> production data: it is applied only when the app runs in the
/// <c>Development</c> environment, <see cref="Enabled"/> is <c>true</c>, and a
/// <see cref="Password"/> is supplied out-of-band (user secrets or an environment variable).
/// No password is stored in source control.
/// </summary>
public sealed class DemoDataOptions
{
    public const string SectionName = "Faed:DemoSeed";

    /// <summary>Opt-in switch. Even in Development the seed does nothing unless this is set.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The single shared password given to every seeded demo account. Supplied via
    /// <c>dotnet user-secrets set "Faed:DemoSeed:Password" "&lt;value&gt;"</c> or the
    /// <c>Faed__DemoSeed__Password</c> environment variable — never committed.
    /// </summary>
    public string? Password { get; set; }
}
