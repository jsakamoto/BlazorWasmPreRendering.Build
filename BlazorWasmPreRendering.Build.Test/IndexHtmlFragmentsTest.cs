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
            rootComponentSelector: "#app",
            headOutletComponentSelector: "head::after",
            deleteLoadingContents: false);

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
            rootComponentSelector: "app,#app",
            headOutletComponentSelector: "head::after",
            deleteLoadingContents: false);

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
    public void Load_with_DeleteLoadingContents_Test()
    {
        // Given
        var indexHtmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "index (2).html");

        // When
        var indexHtmlFragments = IndexHtmlFragments.Load(
            indexHtmlPath,
            rootComponentSelector: "#app",
            headOutletComponentSelector: "head::after",
            deleteLoadingContents: true); // <- delete "Loading..." contents to true!

        // Then
        indexHtmlFragments.FirstPart.Is(
            "<!DOCTYPE html><html><head>\n" +
            "    <meta charset=\"utf-8\"/>\n");

        indexHtmlFragments.MiddlePart.Is(
            "</head>\n" +
            "<body>\n" +
            "    <div id=\"app\">");

        indexHtmlFragments.LastPart.Is(
            "</div>\n\n</body></html>");
    }
}
