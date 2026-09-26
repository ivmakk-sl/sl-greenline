using System.Globalization;
using Greenline;
using Xunit;

public class PageStyleTests
{
    [Fact]
    public void The_opacity_rule_sets_both_tokens_in_percent()
    {
        Assert.Equal(":root{--gl-panel-opacity:60%;--gl-card-opacity:96%}", PageStyle.OpacityRule(0.6f, 0.96f));
    }

    [Fact]
    public void A_fraction_keeps_one_decimal_with_a_point_in_any_culture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(":root{--gl-panel-opacity:45.5%;--gl-card-opacity:0%}", PageStyle.OpacityRule(0.455f, 0f));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Fact]
    public void A_value_out_of_range_applies_as_the_nearest_limit()
    {
        Assert.Equal(":root{--gl-panel-opacity:100%;--gl-card-opacity:0%}", PageStyle.OpacityRule(1.5f, -0.2f));
    }
}
