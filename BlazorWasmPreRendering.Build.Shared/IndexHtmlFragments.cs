namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared
{
    public class IndexHtmlFragments
    {
        public string FirstPart { get; set; } = "";
        public string MiddlePart { get; set; } = "";
        public string LastPart { get; set; } = "";

        public IndexHtmlFragments(string firstPart, string middlePart, string lastPart)
        {
            this.FirstPart = firstPart;
            this.MiddlePart = middlePart;
            this.LastPart = lastPart;
        }
    }
}
