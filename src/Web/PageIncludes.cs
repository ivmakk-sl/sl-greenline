using System;
using System.Text.RegularExpressions;

namespace Greenline
{
    // Joins page.js from its files. No game or BepInEx type here, so the tests can link this file.
    public static class PageIncludes
    {
        private static readonly Regex IncludeLine = new Regex(@"^[ \t]*// @include (\S+)[ \t]*(?=\r?$)", RegexOptions.Multiline);

        // The wrapper text with each "// @include <file>" line replaced by the text of that file (from
        // `read`), with no line break at its end. The page tests join page.js the same way.
        public static string Join(string wrapper, Func<string, string> read) =>
            IncludeLine.Replace(wrapper, m => read(m.Groups[1].Value).TrimEnd());
    }
}
