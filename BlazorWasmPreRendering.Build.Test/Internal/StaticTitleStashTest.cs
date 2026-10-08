using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Internal;

namespace BlazorWasmPreRendering.Build.Test.Internal;

public class StaticTitleStashTest
{
    [Test]
    public void Stash_Test()
    {
        var html =
            "<html><head>\n" +
            "<!-- <title>Commented Out</title> -->\n" +
            "<title data-foo=\"bar\">Tom &amp; Jerry --> 100%</title>\n" +
            "<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% -->\n" +
            "<title>Counter</title>\n" +
            "<!-- %%-PRERENDERING-HEADOUTLET-END-%% -->\n" +
            "</head><body><title>In Body</title></body></html>";

        var stashed = StaticTitleStash.Stash(html);

        stashed.Is(
            "<html><head>\n" +
            "<!-- <title>Commented Out</title> -->\n" +
            "<!-- %%-PRERENDERING-DEFAULT-TITLE-%% Tom%20%26%20Jerry%20--%3E%20100%25 -->\n" +
            "<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% -->\n" +
            "<title>Counter</title>\n" +
            "<!-- %%-PRERENDERING-HEADOUTLET-END-%% -->\n" +
            "</head><body><title>In Body</title></body></html>");
    }

    [Test]
    public void Stash_EmptyTitle_Test()
    {
        var html =
            "<html><head><title></title>" +
            "<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% --><title>Counter</title><!-- %%-PRERENDERING-HEADOUTLET-END-%% -->" +
            "</head></html>";

        var stashed = StaticTitleStash.Stash(html);

        stashed.Is(
            "<html><head><!-- %%-PRERENDERING-DEFAULT-TITLE-%%  -->" +
            "<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% --><title>Counter</title><!-- %%-PRERENDERING-HEADOUTLET-END-%% -->" +
            "</head></html>");
        StaticTitleStash.Restore(stashed).Is(html);
    }

    [Test]
    public void Stash_NoTitleInHeadOutlet_Test()
    {
        var html =
            "<html><head><title>MyApp</title>" +
            "<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% --><meta name=\"description\" content=\"foo\"><!-- %%-PRERENDERING-HEADOUTLET-END-%% -->" +
            "</head></html>";

        StaticTitleStash.Stash(html).Is(html);
    }

    [Test]
    public void Stash_NoHeadOutlet_Test()
    {
        var html = "<html><head><title>MyApp</title></head><body><title>Counter</title></body></html>";

        StaticTitleStash.Stash(html).Is(html);
    }

    [Test]
    public void Restore_Test()
    {
        var html =
            "<html><head>\n" +
            "<!-- %%-PRERENDERING-DEFAULT-TITLE-%% Tom%20%26%20Jerry%20%3C3%20--%3E -->\n" +
            "</head></html>";

        StaticTitleStash.Restore(html).Is(
            "<html><head>\n" +
            "<title>Tom &amp; Jerry &lt;3 --&gt;</title>\n" +
            "</head></html>");
    }
}
