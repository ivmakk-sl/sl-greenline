using System.Text.Json.Nodes;
using Greenline;
using Xunit;

public class ToJsonTests
{
    private const string Large = "../../Res/Furniture/UI_Item_Icon_plantcommon_large.png";
    private const string Medium = "../../Res/Furniture/UI_Item_Icon_plantcommon_middle.png";
    private const string Small = "../../Res/Furniture/UI_Item_Icon_plantcommon_small.png";

    private static GridLogic.Pot Empty(long id, long rowId, string potName, string potIcon, int potSize, GridLogic.PotState state) =>
        new GridLogic.Pot { PotId = id, RowId = rowId, ClickId = id + 4000, PotName = potName, PotIcon = potIcon, PotSize = potSize, State = state, CropName = "", CropIcon = "", FertName = "", FertIcon = "" };

    // The pots of tests/fixtures/pots.json: two floors, one pot of each state, one pot with two
    // problems, and pots with no game row.
    private static List<GridLogic.Pot> FixturePots()
    {
        var watermelon = Empty(1001, 5001, "Large Planter", Large, 4, GridLogic.PotState.Growing);
        watermelon.CropName = "Watermelon";
        watermelon.CropIcon = "../../Res/Food/UI_Item_Icon_Food_xigua.png";
        watermelon.CropSize = 4;
        watermelon.Flags = GridLogic.Pest | GridLogic.Drought;
        watermelon.GrowRemainSeconds = 105960;
        watermelon.GrowTotalSeconds = 211920;
        watermelon.FertName = "Compound Fertilizer";
        watermelon.FertIcon = "../../Res/Material/UI_Item_Icon_Mat_S_1500310101.png";
        watermelon.AutoFert = true;

        var potato = Empty(1002, 5002, "Small Planter", Small, 1, GridLogic.PotState.Mature);
        potato.CropName = "Potato";
        potato.CropIcon = "../../Res/Food/UI_Item_Icon_Food_M_1300900301.png";
        potato.CropSize = 1;
        potato.AutoReplant = false;

        var empty = Empty(1003, 0, "Small Planter", Small, 1, GridLogic.PotState.Empty);
        empty.AutoFert = true;

        var spinach = Empty(1004, 5004, "Medium Planter", Medium, 2, GridLogic.PotState.Withered);
        spinach.CropName = "Spinach";
        spinach.CropIcon = "../../Res/Food/UI_Item_Icon_Food_bocai_v2.png";
        spinach.CropSize = 1;
        spinach.Flags = GridLogic.Drought;

        var poor = Empty(1005, 0, "Medium Planter", Medium, 2, GridLogic.PotState.Poor);

        return new List<GridLogic.Pot> { poor, spinach, empty, potato, watermelon };
    }

    private static List<GridLogic.FloorLine> FixtureFloors() => new List<GridLogic.FloorLine>
    {
        new GridLogic.FloorLine { Floor = 102, Label = "2F", Pots = new List<long> { 1001, 1002 } },
        new GridLogic.FloorLine { Floor = 1, Label = "Home", Pots = new List<long> { 1003, 1004, 1005 } },
    };

    private static List<KeyValuePair<string, string>> FixtureWords() => new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("stateGrowing", "Growing"),
        new KeyValuePair<string, string>("valueNone", "None"),
    };

    [Fact]
    public void ToJson_makes_the_fixture()
    {
        string json = GridLogic.ToJson("en", FixtureWords(), FixtureFloors(), FixturePots(), true, 3);

        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "pots.json")));
        var actual = JsonNode.Parse(json);
        Assert.True(JsonNode.DeepEquals(expected, actual), json);
    }

    [Fact]
    public void ToJson_escapes_a_quote_a_backslash_and_a_line_break_in_a_name()
    {
        var pot = Empty(1, 0, "A \"big\" pot\\\nnew line", Small, 1, GridLogic.PotState.Empty);
        var floors = new List<GridLogic.FloorLine> { new GridLogic.FloorLine { Floor = 1, Label = "Home", Pots = new List<long> { 1 } } };

        string json = GridLogic.ToJson("en", new List<KeyValuePair<string, string>>(), floors, new[] { pot }, false, 0);

        Assert.Equal("A \"big\" pot\\\nnew line", JsonNode.Parse(json)["pots"]["1"]["potName"].GetValue<string>());
    }

    [Fact]
    public void PlantPanelJson_has_the_pot_the_two_options_the_reason_and_the_words()
    {
        var words = new[] { new KeyValuePair<string, string>("autoFert", "Auto-\"fertilize\"") };

        var json = JsonNode.Parse(PageJson.PlantPanelJson(4294990630, false, true, "0 of 1 Seeds", words))!;

        Assert.Equal("4294990630", json["pot"]!.GetValue<string>());
        Assert.False(json["replant"]!.GetValue<bool>());
        Assert.True(json["fert"]!.GetValue<bool>());
        Assert.Equal("0 of 1 Seeds", json["reason"]!.GetValue<string>());
        Assert.Equal("Auto-\"fertilize\"", json["words"]!["autoFert"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("{\"pot\":\"4294990630\",\"key\":\"fert\",\"on\":true}", 4294990630L, "fert", true)]
    [InlineData("{\"on\":false,\"key\":\"replant\",\"pot\":\"12\"}", 12L, "replant", false)]
    public void ParsePotOption_reads_the_pot_the_key_and_the_value(string json, long pot, string key, bool on)
    {
        var option = PageJson.ParsePotOption(json);

        Assert.NotNull(option);
        Assert.Equal((pot, key, on), option!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"pot\":\"\",\"key\":\"fert\",\"on\":true}")]
    [InlineData("{\"pot\":\"12\",\"key\":\"other\",\"on\":true}")]
    public void ParsePotOption_rejects_a_message_with_no_pot_or_an_unknown_key(string json)
    {
        Assert.Null(PageJson.ParsePotOption(json));
    }
}
