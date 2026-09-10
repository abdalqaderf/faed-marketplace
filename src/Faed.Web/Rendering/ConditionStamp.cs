using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: the condition stamp from DESIGN-BRIEF.md §5.4 — one hue (rust), four fill
/// levels, rendered as a battery meter beside the grade code. Fuller means newer to the
/// shelf: <c>GRADE A</c> shows four filled bars, <c>GRADE D</c> one (decision log #14). The
/// meter reads "how close to new", never "how faulty". This is the only place rust appears
/// on an action-free surface, and it renders in exactly one corner per screen (§5.4).
/// </summary>
public static class ConditionStamp
{
    /// <summary>Filled-bar count for a grade code A–D. Unknown codes read as a single bar.</summary>
    public static int FilledBars(string? conditionCode) => conditionCode?.Trim().ToUpperInvariant() switch
    {
        "A" => 4,
        "B" => 3,
        "C" => 2,
        "D" => 1,
        _ => 1,
    };

    /// <summary>The stamp markup — meter plus <c>GRADE X</c> label. <paramref name="large"/>
    /// selects the listing-detail size.</summary>
    public static IHtmlContent Render(string conditionCode, bool large = false)
    {
        var filled = FilledBars(conditionCode);
        var code = (conditionCode ?? string.Empty).Trim().ToUpperInvariant();

        var stamp = new TagBuilder("span");
        stamp.AddCssClass(large ? "fx-stamp fx-stamp--lg" : "fx-stamp");

        var meter = new TagBuilder("span");
        meter.AddCssClass("fx-stamp__meter");
        for (var i = 0; i < 4; i++)
        {
            var bar = new TagBuilder("span");
            bar.AddCssClass(i < filled ? "fx-stamp__bar is-on" : "fx-stamp__bar");
            meter.InnerHtml.AppendHtml(bar);
        }

        var grade = new TagBuilder("span");
        grade.AddCssClass("fx-stamp__grade");
        grade.InnerHtml.Append($"GRADE {code}");

        stamp.InnerHtml.AppendHtml(meter);
        stamp.InnerHtml.AppendHtml(grade);

        stamp.Attributes["aria-label"] = $"Condition grade {code}, {filled} of 4 — {filled} means newer to the shelf, not less faulty";
        return stamp;
    }
}
