using System.Collections.Generic;
using Greenline;
using Xunit;

public class RobotLogicTests
{
    private const int Watermelon = 15033, Corn = 15027;

    [Fact]
    public void The_placeholder_gives_the_seed_of_the_crop_of_a_mature_pot()
    {
        Assert.Equal(Corn, RobotLogic.SeedFor(RobotLogic.Placeholder, matureSeed: Corn, lastSeed: Watermelon, autoReplant: true));
    }

    [Fact]
    public void The_placeholder_gives_the_last_crop_of_a_pot_with_no_mature_crop()
    {
        Assert.Equal(Watermelon, RobotLogic.SeedFor(RobotLogic.Placeholder, matureSeed: 0, lastSeed: Watermelon, autoReplant: true));
    }

    [Fact]
    public void The_placeholder_gives_no_seed_with_no_last_crop()
    {
        Assert.Equal(0, RobotLogic.SeedFor(RobotLogic.Placeholder, matureSeed: 0, lastSeed: 0, autoReplant: true));
    }

    [Fact]
    public void The_placeholder_gives_no_seed_for_a_pot_with_its_own_Auto_replant_off()
    {
        Assert.Equal(0, RobotLogic.SeedFor(RobotLogic.Placeholder, matureSeed: Corn, lastSeed: Watermelon, autoReplant: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Corn)]
    public void A_preset_that_is_not_the_placeholder_stays(int preset)
    {
        Assert.Equal(preset, RobotLogic.SeedFor(preset, matureSeed: Watermelon, lastSeed: Watermelon, autoReplant: false));
    }

    [Fact]
    public void The_placeholder_must_be_no_item_of_the_game()
    {
        Assert.True(RobotLogic.PlaceholderIsFree(new HashSet<int> { Corn, Watermelon }));
        Assert.False(RobotLogic.PlaceholderIsFree(new HashSet<int> { Corn, RobotLogic.Placeholder }));
    }

    // The game's ranks and DecayRemain values of PlantChoreManager.TryResolveChore.
    private const int RankSow = 4, RankStall = 3, RankPest = 0;
    private const float NoDecay = float.MaxValue;

    [Theory]
    [InlineData(PatrolLogic.Till)]
    [InlineData(RobotLogic.RobotPlant)]
    public void A_Till_or_a_robot_Plant_goes_before_each_Harvest(int actionId)
    {
        var (rank, decay) = RobotLogic.FinishRank(actionId, RankSow, NoDecay);

        Assert.Equal(RobotLogic.RankHarvest, rank);
        Assert.Equal(RobotLogic.FinishDecay, decay);
    }

    [Fact]
    public void The_finish_decay_sorts_before_a_crop_at_its_last_second()
    {
        Assert.True(RobotLogic.FinishDecay < 0f);
    }

    [Theory]
    [InlineData(PatrolLogic.Harvest, 1, 120f)]
    [InlineData(PatrolLogic.Harvest, 1, 0f)]
    [InlineData(PatrolLogic.Pest, RankPest, NoDecay)]
    [InlineData(PatrolLogic.Weed, RankStall, NoDecay)]
    [InlineData(PatrolLogic.Water, RankStall, NoDecay)]
    [InlineData(PatrolLogic.Plant, RankSow, NoDecay)]
    [InlineData(4242, RankSow, NoDecay)]
    public void Each_other_chore_keeps_its_game_rank(int actionId, int gameRank, float gameDecay)
    {
        var (rank, decay) = RobotLogic.FinishRank(actionId, gameRank, gameDecay);

        Assert.Equal(gameRank, rank);
        Assert.Equal(gameDecay, decay);
    }
}
