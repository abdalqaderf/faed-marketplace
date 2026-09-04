namespace Faed.Web.Models.Enums;

/// <summary>
/// What an uploaded listing image shows.
/// <see cref="Defect"/> is kept distinguishable from ordinary product photography so
/// disclosure can be surfaced prominently instead of hidden among catalogue shots
/// </summary>
public enum ListingMediaType
{
    Product = 0,
    Defect = 1,
    Packaging = 2,
}
