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
}
