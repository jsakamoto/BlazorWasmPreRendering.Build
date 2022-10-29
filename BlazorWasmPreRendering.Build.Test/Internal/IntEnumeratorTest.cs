using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Internal;

namespace BlazorWasmPreRendering.Build.Test.Internal;

public class IntEnumeratorTest
{
    [TestCase("12", new[] { 12 })]
    [TestCase("34, 5,6", new[] { 34, 5, 6 })]
    [TestCase("6-9", new[] { 6, 7, 8, 9 })]
    [TestCase("10- 11,1,", new[] { 10, 11, 1 })]
    [TestCase("2-4, 7,6 -6", new[] { 2, 3, 4, 7, 6 })]
    public void ParseRange_Test(string rangeText, int[] expectedNumbers)
    {
        var numbers = IntEnumerator.ParseRangeText(rangeText);
        numbers.Is(expectedNumbers);
    }

    [TestCase("")]
    [TestCase("9-0")]
    [TestCase("1,2,NaN")]
    [TestCase("1,-2-3")]
    [TestCase("4,5-,6")]
    public void ParseRange_InvalidRangeText_Test(string invalidRangeText)
    {
        var e = Assert.Throws<FormatException>((() => IntEnumerator.ParseRangeText(invalidRangeText).ToArray()));
        e.IsNotNull().Message.Is($"\"{invalidRangeText}\" is invalid raneg text. Range text should be like \"1,2,3\", \"4-6\", \"7,8-9,10,21-30\", etc.");
    }
}
