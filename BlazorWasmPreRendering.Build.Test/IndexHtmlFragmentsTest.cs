using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;

namespace BlazorWasmPreRendering.Build.Test;

public class IndexHtmlFragmentsTest
{
    [Test]
    public void Load_Test()
    {
        // Given
        var indexHtmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "index (2).html");

        // When
        var indexHtmlFragments = IndexHtmlFragments.Load(
            indexHtmlPath,
            selectorOfRootComponent: "#app",
            selectorOfHeadOutletComponent: "head::after");

        // Then
        indexHtmlFragments.FirstPart.Is(
            "<!DOCTYPE html><html><head>\n" +
            "    <meta charset=\"utf-8\"/>\n");
        indexHtmlFragments.MiddlePart.Is(
            "</head>\n" +
            "<body>\n" +
            "    <div id=\"app\">\n" +
            "        <div>Loading...</div>\n" +
            "        <img src=\"foo.png\"/>\n" +
            "    ");
        indexHtmlFragments.LastPart.Is(
            "</div>\n\n</body></html>");
    }

    [Test]
    public void Load_OncePrerendered_Test()
    {
        // Given
        var indexHtmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "index (3).html");

        // When
        var indexHtmlFragments = IndexHtmlFragments.Load(
            indexHtmlPath,
            selectorOfRootComponent: "app,#app",
            selectorOfHeadOutletComponent: "head::after");

        // Then
        indexHtmlFragments.FirstPart.Is(
            "<!DOCTYPE html><html><head>\n" +
            "    <meta charset=\"utf-8\"/>\n");
        indexHtmlFragments.MiddlePart.Is(
            "</head>\n" +
            "<body>\n" +
            "    <div id=\"app\">\n" +
            "        <div>Loading...</div>\n" +
            "        <img src=\"foo.png\"/>\n" +
            "    ");
        indexHtmlFragments.LastPart.Is(
            "</div>\n\n</body></html>");
    }
}
