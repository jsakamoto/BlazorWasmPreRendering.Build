namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

public class IndexHtmlFragments
{
    public string FirstPart { get; init; }

    public string MiddlePart { get; init; }

    public string LoaderPart { get; init; }

    public string LastPart { get; init; }

    public IndexHtmlFragments(string firstPart, string middlePart, string loaderPart, string lastPart)
    {
        this.FirstPart = firstPart;
        this.MiddlePart = middlePart;
        this.LoaderPart = loaderPart;
        this.LastPart = lastPart;
    }
}
