using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Internal;

/// <summary>
/// Swaps the static title elements of a prerendered page with comments, and back.
/// The JavaScript initializer turns these comments back into title elements at the end of the head element on startup.
/// </summary>
internal static class StaticTitleStash
{
    private const string HeadOutletBegin = "<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% -->";

    private const string HeadOutletEnd = "<!-- %%-PRERENDERING-HEADOUTLET-END-%% -->";

    private const string StashMarker = "%%-PRERENDERING-DEFAULT-TITLE-%%";

    private static readonly Regex TitleElementRegex = new(@"\G<title\b[^>]*>.*?</title\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex StashedTitleRegex = new(@"<!-- %%-PRERENDERING-DEFAULT-TITLE-%%(?: (?<text>\S*))? -->");

    private static readonly HtmlParser HtmlParser = new(new HtmlParserOptions { IsKeepingSourceReferences = true });

    /// <summary>
    /// Replaces the static title elements in the head element with comments, if the prerendered head outlet contents have a title element.
    /// </summary>
    /// <param name="html">The HTML text of a prerendered page.</param>
    public static string Stash(string html)
    {
        var headOutletBegin = html.IndexOf(HeadOutletBegin, StringComparison.Ordinal);
        if (headOutletBegin == -1) return html;
        var headOutletEnd = html.IndexOf(HeadOutletEnd, headOutletBegin, StringComparison.Ordinal);
        if (headOutletEnd == -1) return html;

        using var document = HtmlParser.ParseDocument(html);
        var titles = (document.Head?.QuerySelectorAll("title") ?? Enumerable.Empty<IElement>())
            .Select(title => (Text: title.TextContent, Index: title.SourceReference?.Position.Index ?? -1))
            .Where(title => title.Index != -1)
            .ToArray();
        static bool isInside(int index, int begin, int end) => begin < index && index < end;
        if (!titles.Any(title => isInside(title.Index, headOutletBegin, headOutletEnd))) return html;

        var stashed = new StringBuilder(html.Length);
        var copiedLength = 0;
        foreach (var title in titles.Where(title => !isInside(title.Index, headOutletBegin, headOutletEnd)).OrderBy(title => title.Index))
        {
            // AngleSharp tells only where an element starts, so the regex finds where it ends.
            var match = TitleElementRegex.Match(html, title.Index);
            if (!match.Success) continue;
            stashed.Append(html, copiedLength, title.Index - copiedLength);
            stashed.Append($"<!-- {StashMarker} {Uri.EscapeDataString(title.Text)} -->");
            copiedLength = title.Index + match.Length;
        }
        stashed.Append(html, copiedLength, html.Length - copiedLength);
        return stashed.ToString();
    }

    /// <summary>
    /// Replaces the comments that <see cref="Stash(string)"/> made with the original title elements.
    /// </summary>
    /// <param name="html">The HTML text that may contain stashed title elements.</param>
    public static string Restore(string html)
    {
        return StashedTitleRegex.Replace(html, match => $"<title>{WebUtility.HtmlEncode(Uri.UnescapeDataString(match.Groups["text"].Value))}</title>");
    }
}
