using System.Globalization;
using Greenline;
using Xunit;

public class AreaFormatTests
{
    [Theory]
    [InlineData(0f, "0")]
    [InlineData(0.5f, "0.5")]
    [InlineData(1f, "1")]
    [InlineData(2f, "2")]
    [InlineData(0.25f, "0.3")]
    [InlineData(1.04f, "1.0")]
    public void Light_shows_as_the_HUD_shows_it(float light, string text)
    {
        Assert.Equal(text, AreaFormat.Light(light));
    }

    [Fact]
    public void Light_has_a_point_in_each_culture()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");
            Assert.Equal("0.5", AreaFormat.Light(0.5f));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Theory]
    [InlineData(1, "+1")]
    [InlineData(0, "0")]
    [InlineData(-2, "-2")]
    [InlineData(-5, "-5")]
    public void Temperature_has_a_plus_above_0(int temp, string text)
    {
        Assert.Equal(text, AreaFormat.Temp(temp));
    }
}
