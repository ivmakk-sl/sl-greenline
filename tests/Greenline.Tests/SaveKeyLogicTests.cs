using Greenline;
using Xunit;

public class SaveKeyLogicTests
{
    [Fact]
    public void A_save_key_names_the_mod_the_kind_and_the_pot()
    {
        Assert.Equal("greenline.crop.4294990630", SaveKeyLogic.Key("crop", 4294990630));
    }

    [Fact]
    public void Rekey_reads_the_keys_that_Key_makes()
    {
        var entries = new Dictionary<string, int> { [SaveKeyLogic.Key("fert", 1)] = 1 };

        var result = SaveKeyLogic.Rekey(entries, new Dictionary<long, long> { [1] = 2 });

        Assert.Equal(new Dictionary<string, int> { [SaveKeyLogic.Key("fert", 2)] = 1 }, result);
    }

    [Theory]
    [InlineData("greenline.crop.1", true)]
    [InlineData("greenline.other", true)]
    [InlineData("game.counter.1", false)]
    [InlineData("greenlinex.crop.1", false)]
    public void A_mod_key_starts_with_the_mod_name(string key, bool mod)
    {
        Assert.Equal(mod, SaveKeyLogic.IsModKey(key));
    }

    [Fact]
    public void Rekey_maps_each_key_of_a_pot_to_its_new_id()
    {
        var entries = new Dictionary<string, int>
        {
            ["greenline.crop.100"] = 15027, ["greenline.fert.100"] = 1, ["greenline.replant.100"] = 0,
            ["greenline.crop.200"] = 15033,
        };

        var result = SaveKeyLogic.Rekey(entries, new Dictionary<long, long> { [100] = 900, [200] = 800 });

        Assert.Equal(new Dictionary<string, int>
        {
            ["greenline.crop.900"] = 15027, ["greenline.fert.900"] = 1, ["greenline.replant.900"] = 0,
            ["greenline.crop.800"] = 15033,
        }, result);
    }

    [Fact]
    public void Rekey_handles_two_pots_that_swap_ids()
    {
        var entries = new Dictionary<string, int> { ["greenline.crop.1"] = 15027, ["greenline.crop.2"] = 15033 };

        var result = SaveKeyLogic.Rekey(entries, new Dictionary<long, long> { [1] = 2, [2] = 1 });

        Assert.Equal(new Dictionary<string, int> { ["greenline.crop.2"] = 15027, ["greenline.crop.1"] = 15033 }, result);
    }

    [Fact]
    public void Rekey_keeps_a_new_id_that_is_the_old_id_of_another_pot()
    {
        var entries = new Dictionary<string, int> { ["greenline.crop.1"] = 15027, ["greenline.crop.2"] = 15033 };

        var result = SaveKeyLogic.Rekey(entries, new Dictionary<long, long> { [1] = 2, [2] = 3 });

        Assert.Equal(new Dictionary<string, int> { ["greenline.crop.2"] = 15027, ["greenline.crop.3"] = 15033 }, result);
    }

    [Fact]
    public void Rekey_drops_the_keys_of_a_removed_pot_and_skips_other_keys()
    {
        var entries = new Dictionary<string, int>
        {
            ["greenline.crop.1"] = 15027, ["greenline.fert.5"] = 1, ["game.counter.1"] = 7, ["greenline.crop.x"] = 3,
        };

        var result = SaveKeyLogic.Rekey(entries, new Dictionary<long, long> { [1] = 10 });

        Assert.Equal(new Dictionary<string, int> { ["greenline.crop.10"] = 15027 }, result);
    }
    [Fact]
    public void The_global_Auto_replant_option_is_the_replant_key_of_pot_id_0()
    {
        Assert.Equal("greenline.replant.0", SaveKeyLogic.Key("replant", SaveKeyLogic.GlobalPot));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Rekey_keeps_the_key_of_pot_id_0_with_no_new_id(int value)
    {
        var entries = new Dictionary<string, int> { ["greenline.replant.0"] = value, ["greenline.crop.5"] = 15027 };

        var result = SaveKeyLogic.Rekey(entries, new Dictionary<long, long> { [5] = 6 });

        Assert.Equal(new Dictionary<string, int> { ["greenline.replant.0"] = value, ["greenline.crop.6"] = 15027 }, result);
    }
}
