using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Text;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build;

internal class CustomHtmlMarkupFormatter : HtmlMarkupFormatter
{
    public override string OpenTag(IElement element, bool selfClosing)
    {
        var stringBuilder = StringBuilderPool.Obtain();
        stringBuilder.Append('<');
        if (!string.IsNullOrEmpty(element.Prefix))
        {
            stringBuilder.Append(element.Prefix).Append(':');
        }
        stringBuilder.Append(element.LocalName);
        foreach (var attribute in element.Attributes)
        {
            stringBuilder.Append(' ').Append(this.Attribute(attribute));
        }

        // DON'T FORGET TO APPEND A SLASH FOR SELF CLOSING TAG.
        if (selfClosing) stringBuilder.Append('/');

        stringBuilder.Append('>');
        return stringBuilder.ToPool();
    }
}
