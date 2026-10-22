using Greenline;
using Xunit;

public class PageCommandTests
{
    private const string Json = "{\"pots\":{}}";
    private const string Script = "window.__greenlineCss=\"x\";window.__greenline = window.__greenline || (function () { return {}; })();";
    private const string PostNoScript = "window.vuplex.postMessage('slmod|greenline|no script');";

    [Fact]
    public void The_fast_command_sends_only_the_pot_data_when_the_page_has_the_script_else_posts_no_script()
    {
        string js = PageJson.SetPotsCommand(Json);

        Assert.Equal("if(window.__greenline)window.__greenline.setPots(" + Json + ");else " + PostNoScript, js);
        Assert.Equal("no script", PageJson.NoScript);
    }

    [Fact]
    public void The_fast_command_does_not_call_install()
    {
        Assert.DoesNotContain("install(", PageJson.SetPotsCommand(Json));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_check_command_calls_check_with_the_Verbose_flag_or_posts_no_script(bool verbose)
    {
        Assert.Equal("if(window.__greenline)window.__greenline.check(" + (verbose ? "true" : "false") + ");else " + PostNoScript,
            PageJson.CheckCommand(verbose));
    }

    // A command is a statement (if ... else ...), so it gives the web UI no value to send back to C#.
    [Fact]
    public void The_fast_and_the_check_command_give_no_value()
    {
        Assert.StartsWith("if(", PageJson.SetPotsCommand(Json));
        Assert.StartsWith("if(", PageJson.CheckCommand(false));
    }

    [Fact]
    public void The_full_command_is_the_script_then_the_pot_data()
    {
        string js = PageJson.SetPotsWithScriptCommand(Script, Json);

        Assert.StartsWith(Script, js);
        Assert.EndsWith(";window.__greenline.setPots(" + Json + ");void 0;", js);
    }

    [Fact]
    public void The_full_command_does_not_call_install()
    {
        Assert.DoesNotContain("install(", PageJson.SetPotsWithScriptCommand(Script, Json));
    }
}
