using Greenline;
using Xunit;

public class HoverLogicTests
{
    [Fact]
    public void The_hovered_furniture_is_the_nearest_hit()
    {
        Assert.Equal(7, HoverLogic.Nearest(new[] { (5f, 9L), (2f, 7L), (8f, 3L) }));
    }

    [Fact]
    public void A_hit_that_is_no_furniture_is_skipped()
    {
        Assert.Equal(9, HoverLogic.Nearest(new[] { (5f, 9L), (1f, 0L) }));
    }

    [Fact]
    public void No_hit_is_no_furniture()
    {
        Assert.Equal(0, HoverLogic.Nearest(new (float, long)[0]));
    }

    [Fact]
    public void A_pot_change_goes_to_the_page_at_once()
    {
        var sender = new HoverLogic.Sender();

        Assert.True(sender.Send(5, 100, 100, 0f));
        Assert.True(sender.Send(6, 100, 100, 0.001f));
        Assert.True(sender.Send(0, 100, 100, 0.002f));
    }

    [Fact]
    public void A_pointer_move_goes_to_the_page_at_most_30_times_a_second()
    {
        var sender = new HoverLogic.Sender();
        sender.Send(5, 100, 100, 0f);

        Assert.True(sender.Send(5, 101, 100, 0.010f));
        Assert.False(sender.Send(5, 102, 100, 0.020f));
        Assert.True(sender.Send(5, 103, 100, 0.050f));
    }

    [Fact]
    public void Nothing_goes_to_the_page_while_the_pointer_stays_still()
    {
        var sender = new HoverLogic.Sender();
        sender.Send(5, 100, 100, 0f);

        Assert.False(sender.Send(5, 100, 100, 1f));
        Assert.False(sender.Send(5, 100, 100, 2f));
    }

    [Fact]
    public void Hide_is_needed_only_while_a_pot_card_shows()
    {
        var sender = new HoverLogic.Sender();
        Assert.False(sender.Hide());

        sender.Send(5, 100, 100, 0f);
        Assert.True(sender.Hide());
        Assert.False(sender.Hide());
    }

    [Fact]
    public void After_hide_the_same_pot_goes_to_the_page_again()
    {
        var sender = new HoverLogic.Sender();
        sender.Send(5, 100, 100, 0f);
        sender.Hide();

        Assert.True(sender.Send(5, 100, 100, 1f));
    }
}
