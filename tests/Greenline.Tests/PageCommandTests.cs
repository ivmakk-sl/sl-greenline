using Greenline;
using Xunit;

public class PageCommandTests
{
    private const string Json = "{\"pots\":{}}";
    private const string Script = "window.__greenlineCss=\"x\";window.__greenline = window.__greenline || (function () { return {}; })();";

    [Fact]
    public void The_fast_command_sends_only_the_pot_data_when_the_page_has_the_script()
    {
        string js = PageJson.SetPotsCommand(Json);

        Assert.Equal("window.__greenline?window.__greenline.setPots(" + Json + "):'no script'", js);
        Assert.Equal("no script", PageJson.NoScript);
    }

    [Fact]
    public void The_fast_command_does_not_call_install()
    {
        Assert.DoesNotContain("install(", PageJson.SetPotsCommand(Json));
    }

    [Fact]
    public void The_check_command_calls_check_or_gives_no_script()
    {
        Assert.Equal("window.__greenline?window.__greenline.check():'no script'", PageJson.CheckCommand);
    }

    [Fact]
    public void The_full_command_is_the_script_then_the_pot_data()
    {
        string js = PageJson.SetPotsWithScriptCommand(Script, Json);

        Assert.StartsWith(Script, js);
        Assert.EndsWith(";window.__greenline.setPots(" + Json + ");", js);
    }

    [Fact]
    public void The_full_command_does_not_call_install()
    {
        Assert.DoesNotContain("install(", PageJson.SetPotsWithScriptCommand(Script, Json));
    }
}
