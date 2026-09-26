using Greenline;
using Xunit;

public class ReplantTests
{
    // Seed item id and the Config_Plant id that the seed plants.
    private static readonly (int Item, int Plant)[] SeedItems =
    {
        (15033, 33), (15021, 21), (15099, 33), (15024, 24),
    };

    [Fact]
    public void SeedForCrop_gives_the_lowest_seed_item_of_the_crop()
    {
        Assert.Equal(15033, ReplantLogic.SeedForCrop(SeedItems, 33));
        Assert.Equal(15024, ReplantLogic.SeedForCrop(SeedItems, 24));
    }

    [Fact]
    public void SeedForCrop_gives_none_for_a_crop_with_no_seed_item()
    {
        Assert.Equal(0, ReplantLogic.SeedForCrop(SeedItems, 99));
    }

    // The seed item ids of the planting window's seed list, in the window order.
    private static readonly int[] SeedList = { 15024, 15021, 15033 };

    [Fact]
    public void FindSeed_gives_the_index_of_the_last_crop_seed()
    {
        Assert.Equal((2, ReplantLogic.Reason.Plant), ReplantLogic.FindSeed(15033, SeedList));
        Assert.Equal((0, ReplantLogic.Reason.Plant), ReplantLogic.FindSeed(15024, SeedList));
    }

    [Fact]
    public void FindSeed_gives_a_reason_when_the_crop_is_unknown_or_not_in_the_list()
    {
        Assert.Equal((-1, ReplantLogic.Reason.NoLastCrop), ReplantLogic.FindSeed(0, SeedList));
        Assert.Equal((-1, ReplantLogic.Reason.SeedNotInList), ReplantLogic.FindSeed(15099, SeedList));
    }

    [Fact]
    public void Classify_plants_when_every_check_passes()
    {
        Assert.Equal(ReplantLogic.Reason.Plant, ReplantLogic.Classify(true, true, true, true, 5, 2).Reason);
    }

    [Theory]
    [InlineData(false, true, true, true, ReplantLogic.Reason.TooFewSeeds)]
    [InlineData(true, true, true, false, ReplantLogic.Reason.DoesNotFit)]
    [InlineData(false, false, false, false, ReplantLogic.Reason.DoesNotFit)]
    [InlineData(false, false, true, true, ReplantLogic.Reason.TooFewSeeds)]
    [InlineData(true, false, false, true, ReplantLogic.Reason.NotEnoughLight)]
    [InlineData(true, true, false, true, ReplantLogic.Reason.TooCold)]
    public void Classify_gives_the_first_failed_check(bool enough, bool light, bool temp, bool fits, ReplantLogic.Reason reason)
    {
        Assert.Equal(reason, ReplantLogic.Classify(enough, light, temp, fits, 1, 2).Reason);
    }

    [Fact]
    public void Classify_keeps_the_seed_counts_for_too_few_seeds()
    {
        var decision = ReplantLogic.Classify(false, true, true, true, 1, 2);

        Assert.Equal((ReplantLogic.Reason.TooFewSeeds, 1, 2), (decision.Reason, decision.Have, decision.Need));
    }

    private const int Premium = 15503, Compound = 15502, Basic = 15501;

    private static Dictionary<int, int> Pool(params (int Item, int Count)[] items) =>
        items.ToDictionary(i => i.Item, i => i.Count);

    [Fact]
    public void PickFertilizer_takes_the_best_fertilizer_that_is_enough()
    {
        Assert.Equal(Premium, ReplantLogic.PickFertilizer(Pool((Premium, 2), (Compound, 5), (Basic, 9)), 2));
    }

    [Fact]
    public void PickFertilizer_takes_a_lower_fertilizer_when_the_best_is_short()
    {
        Assert.Equal(Compound, ReplantLogic.PickFertilizer(Pool((Premium, 1), (Compound, 5)), 2));
        Assert.Equal(Basic, ReplantLogic.PickFertilizer(Pool((Premium, 1), (Compound, 1), (Basic, 4)), 4));
    }

    [Fact]
    public void PickFertilizer_gives_none_when_no_fertilizer_is_enough()
    {
        Assert.Equal(0, ReplantLogic.PickFertilizer(Pool((Basic, 1)), 2));
        Assert.Equal(0, ReplantLogic.PickFertilizer(Pool(), 1));
    }

    private static readonly Dictionary<string, string> Words = new Dictionary<string, string>
    {
        ["reasonPrefix"] = "Auto-replant stopped: {0}",
        ["reasonNoLastCrop"] = "last crop unknown",
        ["reasonSeeds"] = "{0} of {1} {2}",
        ["reasonFit"] = "the crop does not fit this pot",
        ["reasonLight"] = "not enough light",
        ["reasonCold"] = "too cold",
    };

    [Theory]
    [InlineData(ReplantLogic.Reason.NoLastCrop, 0, 0, "Auto-replant stopped: last crop unknown")]
    [InlineData(ReplantLogic.Reason.TooFewSeeds, 1, 4, "Auto-replant stopped: 1 of 4 Strawberry Seeds")]
    [InlineData(ReplantLogic.Reason.DoesNotFit, 3, 1, "Auto-replant stopped: the crop does not fit this pot")]
    [InlineData(ReplantLogic.Reason.NotEnoughLight, 3, 1, "Auto-replant stopped: not enough light")]
    [InlineData(ReplantLogic.Reason.TooCold, 3, 1, "Auto-replant stopped: too cold")]
    public void ReasonText_gives_the_reason_line(ReplantLogic.Reason reason, int have, int need, string expected)
    {
        var decision = new ReplantLogic.Decision { Reason = reason, Have = have, Need = need };

        Assert.Equal(expected, ReplantLogic.ReasonText(decision, "Strawberry Seeds", Words));
    }

    [Fact]
    public void ReasonText_counts_no_seed_when_the_containers_have_none()
    {
        var decision = new ReplantLogic.Decision { Reason = ReplantLogic.Reason.SeedNotInList, Need = 2 };

        Assert.Equal("Auto-replant stopped: 0 of 2 Strawberry Seeds", ReplantLogic.ReasonText(decision, "Strawberry Seeds", Words));
    }

    [Fact]
    public void ReasonText_is_empty_for_a_planting()
    {
        Assert.Equal("", ReplantLogic.ReasonText(new ReplantLogic.Decision { Reason = ReplantLogic.Reason.Plant }, "x", Words));
    }
}
