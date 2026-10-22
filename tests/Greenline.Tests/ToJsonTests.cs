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

        watermelon.Floor = 102;
        watermelon.Area = 1;
        potato.Floor = 102;
        potato.Area = 3;
        foreach (var pot in new[] { empty, spinach, poor })
        {
            pot.Floor = 1;
            pot.Area = 1;
        }

        return new List<GridLogic.Pot> { poor, spinach, empty, potato, watermelon };
    }

    // The grid of the fixture pots: the 2nd floor with an Interior pot and a Terrace pot, the Home floor with three
    // Interior pots.
    private static GridLogic.Grid FixtureGrid(List<GridLogic.Pot> pots)
    {
        var grid = GridLogic.Layout(pots, new[] { 102, 1 });
        foreach (var line in grid.Lines)
            line.Name = line.IsFloor ? (line.Floor == 102 ? "2F" : "Home") : "Terrace";
        return grid;
    }

    // The grid of one pot on the Home floor.
    private static GridLogic.Grid OneFloor(GridLogic.Pot pot)
    {
        pot.Floor = 1;
        pot.Area = 1;
        var grid = GridLogic.Layout(new[] { pot }, new[] { 1 });
        grid.Lines[0].Name = "Home";
        return grid;
    }

    // The area values of the fixture: Interior and Terrace on a clear day.
    private static Dictionary<int, GridLogic.AreaValues> FixtureAreas() => new Dictionary<int, GridLogic.AreaValues>
    {
        [3] = new GridLogic.AreaValues { Name = "Terrace", Light = "2", Temp = "0" },
        [1] = new GridLogic.AreaValues { Name = "Interior", Light = "1", Temp = "+1" },
    };

    private static List<KeyValuePair<string, string>> FixtureWords() => new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("stateGrowing", "Growing"),
        new KeyValuePair<string, string>("valueNone", "None"),
    };

    [Fact]
    public void ToJson_makes_the_fixture()
    {
        var pots = FixturePots();
        string json = GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, true, FixtureAreas());

        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "pots.json")));
        var actual = JsonNode.Parse(json);
        Assert.True(JsonNode.DeepEquals(expected, actual), json);
    }

    [Fact]
    public void ToJson_writes_each_line_with_its_area_and_each_cell_with_its_place()
    {
        var pots = FixturePots();
        var grid = JsonNode.Parse(GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, true))["grid"];

        var floor = grid["lines"][0];
        Assert.Equal("f102", floor["key"].GetValue<string>());
        Assert.Equal("floor", floor["kind"].GetValue<string>());
        Assert.Equal(1, floor["row"].GetValue<int>());
        Assert.Equal("2F", floor["name"].GetValue<string>());
        Assert.Equal(1, floor["area"].GetValue<int>());
        var zone = grid["lines"][1];
        Assert.Equal("z102:3", zone["key"].GetValue<string>());
        Assert.Equal("zone", zone["kind"].GetValue<string>());
        Assert.Equal(3, zone["area"].GetValue<int>());
        var cell = grid["cells"][1];
        Assert.Equal(1002, cell["pot"].GetValue<long>());
        Assert.Equal(4, cell["row"].GetValue<int>());
        Assert.Equal(1, cell["col"].GetValue<int>());
        Assert.Equal(3, grid["cols"].GetValue<int>());
        Assert.Equal(6, grid["rows"].GetValue<int>());
    }

    [Fact]
    public void ToJson_writes_the_values_of_each_area_by_id()
    {
        var pots = FixturePots();
        var areas = JsonNode.Parse(GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, true, FixtureAreas()))["areas"];

        Assert.Equal("Interior", areas["1"]["name"].GetValue<string>());
        Assert.Equal("1", areas["1"]["light"].GetValue<string>());
        Assert.Equal("+1", areas["1"]["temp"].GetValue<string>());
        Assert.Equal("0", areas["3"]["temp"].GetValue<string>());
    }

    [Fact]
    public void ToJson_writes_no_area_values_when_the_mod_has_none()
    {
        var pots = FixturePots();
        var areas = JsonNode.Parse(GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, true))["areas"];

        Assert.Empty(areas.AsObject());
    }

    [Fact]
    public void ToJson_is_the_same_while_the_area_values_stay()
    {
        var pots = FixturePots();
        var other = new Dictionary<int, GridLogic.AreaValues>();
        foreach (var kv in FixtureAreas().Reverse()) other[kv.Key] = new GridLogic.AreaValues { Name = kv.Value.Name, Light = kv.Value.Light, Temp = kv.Value.Temp };

        Assert.Equal(GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, true, FixtureAreas()),
            GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, true, other));
    }

    private static JsonNode GrowthOf(int flags, int remain)
    {
        var pot = Empty(1, 5001, "Large Planter", Large, 4, GridLogic.PotState.Growing);
        pot.Flags = flags;
        pot.GrowRemainSeconds = remain;
        pot.GrowTotalSeconds = 211920;
        var grid = OneFloor(pot);
        return JsonNode.Parse(GridLogic.ToJson("en", FixtureWords(), grid, new[] { pot }, false, 0, true))["pots"]["1"];
    }

    [Fact]
    public void ToJson_writes_no_time_to_mature_for_a_growing_crop_with_no_problem()
    {
        var pot = GrowthOf(0, 105960);

        Assert.Equal(0, pot["growRemainSeconds"].GetValue<int>());
        Assert.Equal(211920, pot["growTotalSeconds"].GetValue<int>());
    }

    [Fact]
    public void ToJson_writes_the_time_to_mature_for_a_growing_crop_with_a_problem()
    {
        var pot = GrowthOf(GridLogic.Drought, 105960);

        Assert.Equal(105960, pot["growRemainSeconds"].GetValue<int>());
        Assert.Equal(211920, pot["growTotalSeconds"].GetValue<int>());
    }

    [Fact]
    public void ToJson_lists_needFert_after_lightHigh()
    {
        var problems = GrowthOf(GridLogic.NeedFert | GridLogic.LightHigh | GridLogic.Pest, 105960)["problems"].AsArray();

        Assert.Equal(new[] { "pest", "lightHigh", "needFert" }, problems.Select(p => p.GetValue<string>()));
    }

    [Fact]
    public void ToJson_writes_the_time_to_mature_for_a_crop_that_needs_fertilizer()
    {
        Assert.Equal(105960, GrowthOf(GridLogic.NeedFert, 105960)["growRemainSeconds"].GetValue<int>());
    }

    [Fact]
    public void ToJson_lists_no_problem_for_a_withered_crop_that_needs_fertilizer()
    {
        var pot = Empty(1, 5001, "Large Planter", Large, 4, GridLogic.PotState.Withered);
        pot.Flags = GridLogic.NeedFert;
        var grid = OneFloor(pot);

        var json = JsonNode.Parse(GridLogic.ToJson("en", FixtureWords(), grid, new[] { pot }, false, 0, true))["pots"]["1"];

        Assert.Empty(json["problems"].AsArray());
    }

    [Fact]
    public void ToJson_writes_a_crop_that_never_withers_and_the_dose()
    {
        var pot = Empty(1, 5001, "Large Planter", Large, 4, GridLogic.PotState.Growing);
        pot.NeverWithers = true;
        pot.FertDose = "1/4";
        var grid = OneFloor(pot);

        var json = JsonNode.Parse(GridLogic.ToJson("en", FixtureWords(), grid, new[] { pot }, false, 0, true))["pots"]["1"];

        Assert.True(json["neverWithers"].GetValue<bool>());
        Assert.Equal("1/4", json["fertDose"].GetValue<string>());
    }

    [Fact]
    public void ToJson_is_the_same_while_a_crop_with_no_problem_grows()
    {
        Assert.True(JsonNode.DeepEquals(GrowthOf(0, 105960), GrowthOf(0, 105720)));
    }

    [Fact]
    public void ToJson_escapes_a_quote_a_backslash_and_a_line_break_in_a_name()
    {
        var pot = Empty(1, 0, "A \"big\" pot\\\nnew line", Small, 1, GridLogic.PotState.Empty);
        var grid = OneFloor(pot);

        string json = GridLogic.ToJson("en", new List<KeyValuePair<string, string>>(), grid, new[] { pot }, false, 0, true);

        Assert.Equal("A \"big\" pot\\\nnew line", JsonNode.Parse(json)["pots"]["1"]["potName"].GetValue<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToJson_writes_the_global_Auto_replant_option(bool on)
    {
        var pots = FixturePots();
        string json = GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, on);

        Assert.Equal(on, JsonNode.Parse(json)!["replantOn"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToJson_writes_the_pin_of_the_plant_list(bool pin)
    {
        var pots = FixturePots();
        string json = GridLogic.ToJson("en", FixtureWords(), FixtureGrid(pots), pots, true, 3, true, FixtureAreas(), pin);

        Assert.Equal(pin, JsonNode.Parse(json)!["pin"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("{\"on\":true}", true)]
    [InlineData("{\"on\":false}", false)]
    [InlineData("{ \"on\" : true }", true)]
    public void ParseReplant_reads_the_value_of_the_checkbox(string json, bool on)
    {
        Assert.Equal(on, PageJson.ParseReplant(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"on\":1}")]
    public void ParseReplant_rejects_a_message_with_no_value(string json)
    {
        Assert.Null(PageJson.ParseReplant(json));
    }

    [Fact]
    public void PlantPanelJson_has_the_pot_the_two_options_the_last_crop_line_and_the_words()
    {
        var words = new[] { new KeyValuePair<string, string>("autoFert", "Auto-\"fertilize\"") };
        var line = new ReplantLogic.CropLine { Label = "Last crop:", Crop = "Watermelon", Red = "Replant stopped: seeds 0/1" };

        var json = JsonNode.Parse(PageJson.PlantPanelJson(4294990630, false, true, line, "../../Res/Food/xigua.png", words))!;

        Assert.Equal("4294990630", json["pot"]!.GetValue<string>());
        Assert.False(json["replant"]!.GetValue<bool>());
        Assert.True(json["fert"]!.GetValue<bool>());
        Assert.Equal("Last crop:", json["lastCrop"]!["label"]!.GetValue<string>());
        Assert.Equal("Watermelon", json["lastCrop"]!["name"]!.GetValue<string>());
        Assert.Equal("../../Res/Food/xigua.png", json["lastCrop"]!["icon"]!.GetValue<string>());
        Assert.Equal("Replant stopped: seeds 0/1", json["lastCrop"]!["red"]!.GetValue<string>());
        Assert.Equal("Auto-\"fertilize\"", json["words"]!["autoFert"]!.GetValue<string>());
    }

    [Fact]
    public void PlantPanelJson_has_no_last_crop_when_the_mod_knows_none()
    {
        var json = JsonNode.Parse(PageJson.PlantPanelJson(12, true, false, null, "", new KeyValuePair<string, string>[0]))!;

        Assert.Null(json["lastCrop"]);
        Assert.True(json.AsObject().ContainsKey("lastCrop"));
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
