namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

public class IndexHtmlFragments
{
    public string FirstPart { get; init; }

    public string MiddlePart { get; init; }

    public string LastPart { get; init; }

    public IndexHtmlFragments(string firstPart, string middlePart, string lastPart)
    {
        this.FirstPart = firstPart;
        this.MiddlePart = middlePart;
        this.LastPart = lastPart;
    }
}
