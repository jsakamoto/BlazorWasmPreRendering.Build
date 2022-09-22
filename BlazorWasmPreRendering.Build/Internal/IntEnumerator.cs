using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer.Internal
{
    internal class IntEnumerator
    {
        public static IEnumerable<int> ParseRangeText(string rangeText)
        {
            static Exception createException(string text) => new FormatException($"\"{text}\" is invalid raneg text. Range text should be like \"1,2,3\", \"4-6\", \"7,8-9,10,21-30\", etc.");

            if (!Regex.IsMatch(rangeText, @"^([\d ]+([- ]+[\d ]+)? *, *)*([\d ]+([- ]+[\d ]+)?)( *, *)?$"))
            {
                throw createException(rangeText);
            }

            var rangeParts = rangeText.Split(',').Select(text => text.Trim());
            foreach (var rangePart in rangeParts)
            {
                if (string.IsNullOrEmpty(rangePart)) continue;

                var rangePair = rangePart.Split('-');
                if (rangePair.Length == 1) yield return int.Parse(rangePart);
                else
                {
                    var rangeBegin = int.Parse(rangePair.First());
                    var rangeEnd = int.Parse(rangePair.Last());
                    if (rangeBegin > rangeEnd) throw createException(rangeText);

                    for (var number = rangeBegin; number <= rangeEnd; number++)
                    {
                        yield return number;
                    }
                }
            }
        }
    }
}
