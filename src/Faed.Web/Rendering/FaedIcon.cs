using System.Collections.Frozen;
using Microsoft.AspNetCore.Html;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: the single Faed interface-icon family.
///
/// Faed has no icon package and deliberately does not take one. Before this helper the
/// project drew a handful of one-off inline SVGs and filled the remaining gaps with Unicode
/// glyphs, which render inconsistently across platforms and share no sizing or stroke
/// weight. Every icon below is one 24x24 outline drawn on the same grid with the same 1.7
/// stroke, so the whole interface shares one visual family without a webfont, a CDN request
/// or a render-blocking asset.
///
/// Icons are presentation only. <see cref="Render"/> always marks the SVG
/// <c>aria-hidden</c>: an icon never carries an accessible name of its own, so every caller
/// supplies meaning through visible text, <c>aria-label</c> or native semantics.
/// </summary>
public static class FaedIcon
{
    /// <summary>Default rendered class. Sized in <c>em</c> so icons track adjacent text.</summary>
    public const string DefaultClass = "faed-icon";

    private static readonly FrozenDictionary<string, string> Paths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ---- Shell and navigation ----
            ["search"] = "<circle cx='11' cy='11' r='7'/><path d='m20 20-3.6-3.6'/>",
            ["bag"] = "<path d='M5.6 8h12.8l-1 11.1a1.6 1.6 0 0 1-1.6 1.4H8.2a1.6 1.6 0 0 1-1.6-1.4L5.6 8Z'/><path d='M9 8V6.4a3 3 0 0 1 6 0V8'/>",
            ["user"] = "<circle cx='12' cy='8.2' r='3.6'/><path d='M4.9 20a7.1 7.1 0 0 1 14.2 0'/>",
            ["menu"] = "<path d='M4 7h16M4 12h16M4 17h16'/>",
            ["close"] = "<path d='m6.2 6.2 11.6 11.6M17.8 6.2 6.2 17.8'/>",
            ["chevron-down"] = "<path d='m6.5 9.5 5.5 5.5 5.5-5.5'/>",
            ["chevron-right"] = "<path d='m9.5 5.8 6.2 6.2-6.2 6.2'/>",
            ["chevron-left"] = "<path d='m14.5 5.8-6.2 6.2 6.2 6.2'/>",
            ["arrow-right"] = "<path d='M4.2 12h15.1'/><path d='m13.4 6.1 5.9 5.9-5.9 5.9'/>",
            ["arrow-left"] = "<path d='M19.8 12H4.7'/><path d='m10.6 6.1-5.9 5.9 5.9 5.9'/>",
            ["logout"] = "<path d='M9.6 20H5.7A1.7 1.7 0 0 1 4 18.3V5.7A1.7 1.7 0 0 1 5.7 4h3.9'/><path d='m15.1 16.3 4.3-4.3-4.3-4.3M19.4 12H9.2'/>",

            // ---- Workspaces and destinations ----
            ["storefront"] = "<path d='M3.6 9.1 5.1 4.4h13.8l1.5 4.7'/><path d='M3.6 9.1a2.6 2.6 0 0 0 5.2 0 2.6 2.6 0 0 0 5.2 0 2.6 2.6 0 0 0 5.2 0'/><path d='M5.2 11.4v8.2h13.6v-8.2'/><path d='M10 19.6v-4.7h4v4.7'/>",
            ["shield"] = "<path d='M12 3.3 19 6v5.5c0 4.2-2.8 7.6-7 9.3-4.2-1.7-7-5.1-7-9.3V6l7-2.7Z'/>",
            ["shield-check"] = "<path d='M12 3.3 19 6v5.5c0 4.2-2.8 7.6-7 9.3-4.2-1.7-7-5.1-7-9.3V6l7-2.7Z'/><path d='m9 11.9 2.2 2.2 4.1-4.4'/>",
            ["dashboard"] = "<rect x='3.6' y='3.6' width='7' height='7' rx='1.7'/><rect x='13.4' y='3.6' width='7' height='7' rx='1.7'/><rect x='3.6' y='13.4' width='7' height='7' rx='1.7'/><rect x='13.4' y='13.4' width='7' height='7' rx='1.7'/>",
            ["receipt"] = "<path d='M6.2 3.6h11.6v17l-2.3-1.6-2.3 1.6-2.3-1.6-2.4 1.6-2.3-1.6V3.6Z'/><path d='M9.3 8.2h5.4M9.3 12.2h5.4'/>",
            ["package"] = "<path d='m12 3.4 8 4.1v9L12 20.6l-8-4.1v-9l8-4.1Z'/><path d='m4 7.5 8 4.2 8-4.2'/><path d='M12 11.7v8.9'/>",
            ["tag"] = "<path d='M11.4 3.6H20v8.6l-8.5 8.5a1.6 1.6 0 0 1-2.3 0l-6.3-6.3a1.6 1.6 0 0 1 0-2.3l8.5-8.5Z'/><circle cx='16.1' cy='7.9' r='1.3'/>",
            ["chart"] = "<path d='M4 19.8h16'/><path d='M7.5 19.8v-6.4M12 19.8V6.9M16.5 19.8v-9.3'/>",
            ["star"] = "<path d='m12 4 2.5 5.1 5.6.8-4.1 3.9 1 5.6-5-2.7-5 2.7 1-5.6-4.1-3.9 5.6-.8L12 4Z'/>",
            ["exchange"] = "<path d='M4.2 8.4h14.4l-3.4-3.4'/><path d='M19.8 15.6H5.4l3.4 3.4'/>",
            ["document"] = "<path d='M13.4 3.6H7.7A1.7 1.7 0 0 0 6 5.3v13.4a1.7 1.7 0 0 0 1.7 1.7h8.6a1.7 1.7 0 0 0 1.7-1.7V8.2l-4.6-4.6Z'/><path d='M13.4 3.6v4.6H18'/><path d='M9.3 12.6h5.4M9.3 16.1h5.4'/>",
            ["gear"] = "<circle cx='12' cy='12' r='3.1'/><path d='M18.9 14.1a1.6 1.6 0 0 0 .3 1.8l.1.1a1.9 1.9 0 1 1-2.7 2.7l-.1-.1a1.6 1.6 0 0 0-2.7 1.1v.3a1.9 1.9 0 1 1-3.8 0v-.2a1.6 1.6 0 0 0-2.8-1.1l-.1.1a1.9 1.9 0 1 1-2.7-2.7l.1-.1a1.6 1.6 0 0 0-1.1-2.7h-.3a1.9 1.9 0 1 1 0-3.8h.2A1.6 1.6 0 0 0 5.2 6.7l-.1-.1a1.9 1.9 0 1 1 2.7-2.7l.1.1a1.6 1.6 0 0 0 2.7-1.1v-.3a1.9 1.9 0 1 1 3.8 0v.2a1.6 1.6 0 0 0 2.8 1.1l.1-.1a1.9 1.9 0 1 1 2.7 2.7l-.1.1a1.6 1.6 0 0 0 1.1 2.7h.3a1.9 1.9 0 1 1 0 3.8h-.2a1.6 1.6 0 0 0-1.5 1Z'/>",

            // ---- Controls and actions ----
            ["sliders"] = "<path d='M4 7.4h8M17.4 7.4H20M4 16.6h2.6M12 16.6H20M4 12h5.4M14.8 12H20'/><circle cx='14.7' cy='7.4' r='2.3'/><circle cx='9.3' cy='16.6' r='2.3'/><circle cx='12.1' cy='12' r='2.3'/>",
            ["sort"] = "<path d='M7 4.4v15.2M7 19.6l-2.9-2.9M7 19.6l2.9-2.9'/><path d='M17 19.6V4.4M17 4.4l-2.9 2.9M17 4.4l2.9 2.9'/>",
            ["plus"] = "<path d='M12 5.2v13.6M5.2 12h13.6'/>",
            ["pencil"] = "<path d='M4.4 19.6h4L18.9 9.1a2.1 2.1 0 0 0-3-3L4.4 17.6v2Z'/><path d='m14.6 6.5 2.9 2.9'/>",
            ["trash"] = "<path d='M4.6 6.6h14.8'/><path d='M9.6 6.6V5a1.3 1.3 0 0 1 1.3-1.3h2.2A1.3 1.3 0 0 1 14.4 5v1.6'/><path d='m6.9 6.6.9 12.6a1.3 1.3 0 0 0 1.3 1.2h5.8a1.3 1.3 0 0 0 1.3-1.2l.9-12.6'/>",
            ["eye"] = "<path d='M2.9 12S6.5 5.9 12 5.9 21.1 12 21.1 12 17.5 18.1 12 18.1 2.9 12 2.9 12Z'/><circle cx='12' cy='12' r='2.9'/>",
            ["upload"] = "<path d='M12 15.6V4.2'/><path d='m7.7 8.5 4.3-4.3 4.3 4.3'/><path d='M4.6 15v3.5a1.6 1.6 0 0 0 1.6 1.6h11.6a1.6 1.6 0 0 0 1.6-1.6V15'/>",
            ["download"] = "<path d='M12 4.2v11.4'/><path d='m7.7 11.3 4.3 4.3 4.3-4.3'/><path d='M4.6 15v3.5a1.6 1.6 0 0 0 1.6 1.6h11.6a1.6 1.6 0 0 0 1.6-1.6V15'/>",
            ["external-link"] = "<path d='M14.2 4.2h5.6v5.6'/><path d='m19.8 4.2-8.6 8.6'/><path d='M17.9 14.4v4.1a1.6 1.6 0 0 1-1.6 1.6H5.7a1.6 1.6 0 0 1-1.6-1.6V7.9a1.6 1.6 0 0 1 1.6-1.6h4.1'/>",
            ["refresh"] = "<path d='M19.7 11.4A7.9 7.9 0 0 0 6.4 6.6L4.2 8.6'/><path d='M4.2 4.6v4h4'/><path d='M4.3 12.6a7.9 7.9 0 0 0 13.3 4.8l2.2-2'/><path d='M19.8 19.4v-4h-4'/>",

            // ---- Status and feedback ----
            ["check"] = "<path d='m5.2 12.5 4.6 4.6L18.8 7'/>",
            ["check-circle"] = "<circle cx='12' cy='12' r='8.4'/><path d='m8.4 12.2 2.5 2.5 4.7-5'/>",
            ["x-circle"] = "<circle cx='12' cy='12' r='8.4'/><path d='m9.4 9.4 5.2 5.2M14.6 9.4l-5.2 5.2'/>",
            ["alert-circle"] = "<circle cx='12' cy='12' r='8.4'/><path d='M12 7.7v4.9M12 16.1h.01'/>",
            ["info-circle"] = "<circle cx='12' cy='12' r='8.4'/><path d='M12 11.1v5.2M12 7.9h.01'/>",
            ["warning"] = "<path d='M10.6 4.4 3 17.6a1.6 1.6 0 0 0 1.4 2.4h15.2a1.6 1.6 0 0 0 1.4-2.4L13.4 4.4a1.6 1.6 0 0 0-2.8 0Z'/><path d='M12 9.5v3.7M12 16.5h.01'/>",
            ["clock"] = "<circle cx='12' cy='12' r='8.4'/><path d='M12 7.4V12l3.1 1.8'/>",

            // ---- Commerce detail ----
            ["pin"] = "<path d='M12 20.8s6.4-5.5 6.4-10.1a6.4 6.4 0 1 0-12.8 0c0 4.6 6.4 10.1 6.4 10.1Z'/><circle cx='12' cy='10.5' r='2.4'/>",
            ["truck"] = "<path d='M3.6 6.4h9.8v9.9H3.6z'/><path d='M13.4 9.7h3.3l2.7 2.9v3.7h-6z'/><circle cx='7.2' cy='18' r='1.7'/><circle cx='16.6' cy='18' r='1.7'/>",
            ["mail"] = "<rect x='3.6' y='5.4' width='16.8' height='13.2' rx='2'/><path d='m4.6 6.9 7.4 5.3 7.4-5.3'/>",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Renders one icon as an inline SVG. An unknown name renders nothing rather than
    /// throwing, so a typo can never take a page down; <see cref="Exists"/> covers checks.
    /// </summary>
    /// <param name="name">Icon key, for example <c>search</c>.</param>
    /// <param name="cssClass">Class applied to the SVG. Defaults to <see cref="DefaultClass"/>.</param>
    public static IHtmlContent Render(string name, string? cssClass = null)
    {
        if (!Paths.TryGetValue(name, out var path))
        {
            return HtmlString.Empty;
        }

        var css = string.IsNullOrWhiteSpace(cssClass) ? DefaultClass : cssClass;

        return new HtmlString(
            $"<svg class=\"{css}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" " +
            "stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\" " +
            $"aria-hidden=\"true\" focusable=\"false\">{path}</svg>");
    }

    public static bool Exists(string name) => Paths.ContainsKey(name);
}
