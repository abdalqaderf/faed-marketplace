namespace Faed.Web.Models.Enums;

/// <summary>
/// What an uploaded listing image shows (docs/04-DOMAIN-MODEL.md §3).
/// <see cref="Defect"/> is kept distinguishable from ordinary product photography so
/// disclosure can be surfaced prominently instead of hidden among catalogue shots
/// (docs/01-PRD.md §8, docs/07-UI-UX-SPEC.md §4).
/// </summary>
public enum ListingMediaType
{
    Product = 0,
    Defect = 1,
    Packaging = 2,
}
