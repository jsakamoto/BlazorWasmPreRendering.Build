using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build;

public class IndexHtmlParser
{
    public static IndexHtmlFragments Parse(string indexHtmlPath, string rootComponentSelector, string? headOutletComponentSelector, bool deleteLoadingContents)
    {
        var indexHtmlText = File.ReadAllText(indexHtmlPath);
        indexHtmlText = indexHtmlText.Replace("\r\n", "\n");

        // Sweep the pre-rendered contents inside the index.html that was rendered when the last time publishing.
        var prerenderMarkers = new[] {
            (Begin:"<!-- %%-PRERENDERING-LOADER-BEGIN-%% -->\n", End:"\n<!-- %%-PRERENDERING-LOADER-END-%% -->\n"),
            (Begin:"<!-- %%-PRERENDERING-BEGIN-%% -->\n", End:"\n<!-- %%-PRERENDERING-END-%% -->\n"),
            (Begin:"<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% -->\n", End:"\n<!-- %%-PRERENDERING-HEADOUTLET-END-%% -->\n")
        };
        foreach (var prerenderMarker in prerenderMarkers)
        {
            for (; ; )
            {
                var indexOfPreRenderMarkerBegin = indexHtmlText.IndexOf(prerenderMarker.Begin);
                var indexOfPreRenderMarkerEnd = indexHtmlText.IndexOf(prerenderMarker.End);
                if (indexOfPreRenderMarkerBegin == -1 || indexOfPreRenderMarkerEnd == -1) break;
                indexHtmlText =
                    indexHtmlText[0..indexOfPreRenderMarkerBegin] +
                    indexHtmlText[(indexOfPreRenderMarkerEnd + prerenderMarker.End.Length)..];
            }
        }

        const string markerText = "%%-PRERENDERING-SEGMENT-%%";
        const string markerComment = "<!--" + markerText + "-->";

        var parser = new HtmlParser();
        var indexHtmlDoc = parser.ParseDocument(indexHtmlText);

        foreach (var eachSelector in new[] { rootComponentSelector, headOutletComponentSelector ?? "head::after" })
        {
            var selector = eachSelector;
            var insertPosition = AdjacentPosition.BeforeEnd;
            foreach (var pseudoSelector in new[] { "::before", "::after" })
            {
                if (selector.EndsWith(pseudoSelector))
                {
                    if (pseudoSelector == "::before")
                        insertPosition = AdjacentPosition.AfterBegin;
                    selector = selector.Substring(0, selector.Length - pseudoSelector.Length);
                }
            }

            var componentElement = indexHtmlDoc.QuerySelector(selector);
            if (componentElement == null) throw new Exception($"The element matches with selector \"{selector}\" was not found in the index.html.");

            if (eachSelector == rootComponentSelector)
            {
                if (deleteLoadingContents) componentElement.InnerHtml = ""; // delete the "Loading..." contents inside of the root component element.
                componentElement.Insert(AdjacentPosition.AfterBegin, markerComment);
                componentElement.Insert(AdjacentPosition.BeforeEnd, markerComment);
            }
            else
            {
                componentElement.Insert(insertPosition, markerComment);
            }
        }

        using var stringWriter = new StringWriter();
        indexHtmlDoc.ToHtml(stringWriter, new CustomHtmlMarkupFormatter());
        indexHtmlText = stringWriter.ToString();

        var fragments = new List<string>();
        var indexOfMarker = -1;
        while ((indexOfMarker = indexHtmlText.IndexOf(markerComment)) > -1)
        {
            fragments.Add(indexHtmlText[0..indexOfMarker]);
            indexHtmlText = indexHtmlText[(indexOfMarker + markerComment.Length)..];
        }

        return new IndexHtmlFragments(
            firstPart: fragments.First(),
            middlePart: fragments.Skip(1).FirstOrDefault() ?? "",
            loaderPart: fragments.Skip(2).FirstOrDefault() ?? "",
            lastPart: indexHtmlText);
    }
}
