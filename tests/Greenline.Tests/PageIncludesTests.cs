using Greenline;
using Xunit;

public class PageIncludesTests
{
    private static readonly Dictionary<string, string> Files = new Dictionary<string, string>
    {
        ["a.js"] = "  var a = 1;\n",
        ["b.js"] = "  var b = a;\r\n\r\n",
    };

    [Fact]
    public void Each_include_line_becomes_its_file_in_order()
    {
        string joined = PageIncludes.Join("(function () {\n  // @include a.js\n  // @include b.js\n})();\n", name => Files[name]);

        Assert.Equal("(function () {\n  var a = 1;\n  var b = a;\n})();\n", joined);
    }

    [Fact]
    public void An_include_line_with_a_windows_line_end_is_joined()
    {
        string joined = PageIncludes.Join("x\r\n  // @include a.js\r\ny", name => Files[name]);

        Assert.Equal("x\r\n  var a = 1;\r\ny", joined);
    }
}
