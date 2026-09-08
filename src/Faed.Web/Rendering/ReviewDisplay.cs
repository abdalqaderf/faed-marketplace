namespace Faed.Web.Rendering;

/// <summary>View helper for rendering merchant review ratings.</summary>
public static class ReviewDisplay
{
    /// <summary>A 0–5 rating as filled and empty star glyphs.</summary>
    public static string Stars(int rating) => new string('★', Math.Clamp(rating, 0, 5))
        + new string('☆', 5 - Math.Clamp(rating, 0, 5));
}
