namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared
{
    public class IndexHtmlFragments
    {
        public string FirstPart { get; } = "";
        public string MiddlePart { get; } = "";
        public string LastPart { get; } = "";

        public IndexHtmlFragments(string firstPart, string middlePart, string lastPart)
        {
            this.FirstPart = firstPart;
            this.MiddlePart = middlePart;
            this.LastPart = lastPart;
        }
    }
}
