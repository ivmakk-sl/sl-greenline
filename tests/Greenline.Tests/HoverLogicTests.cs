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

    // The arguments: the pot under the pointer (0 for none), the place of that pot on the screen in pixels,
    // and whether the pointer is over the web UI.

    [Fact]
    public void A_change_of_the_pot_goes_to_the_page_at_once()
    {
        var sender = new HoverLogic.Sender();

        Assert.True(sender.Send(5, 100, 100, false));
        Assert.True(sender.Send(6, 300, 120, false));
        Assert.True(sender.Send(0, 0, 0, false));
    }

    [Fact]
    public void A_pointer_move_on_the_same_pot_sends_nothing()
    {
        var sender = new HoverLogic.Sender();
        sender.Send(5, 100, 100, false);

        for (int i = 0; i < 30; i++) Assert.False(sender.Send(5, 100, 100, false));
    }

    [Fact]
    public void A_move_of_the_pot_on_the_screen_by_a_pixel_or_more_goes_to_the_page()
    {
        var sender = new HoverLogic.Sender();
        sender.Send(5, 100, 100, false);

        Assert.False(sender.Send(5, 100.3f, 99.8f, false));
        Assert.True(sender.Send(5, 101, 100, false));
        Assert.True(sender.Send(5, 101, 140, false));
    }

    [Fact]
    public void Nothing_goes_to_the_page_while_no_pot_is_under_the_pointer()
    {
        var sender = new HoverLogic.Sender();

        for (int i = 0; i < 30; i++) Assert.False(sender.Send(0, i, i, false));
    }

    [Fact]
    public void The_pot_leaving_sends_once_then_nothing()
    {
        var sender = new HoverLogic.Sender();
        sender.Send(5, 100, 100, false);

        Assert.True(sender.Send(0, 0, 0, false));
        Assert.False(sender.Send(0, 0, 0, false));
    }

    // The game gives the page no pointer move over the world, so the card of a grid cell gets no mouseleave
    // when the pointer leaves the web UI: one send of no pot closes it.
    [Fact]
    public void The_pointer_leaving_the_web_UI_sends_once()
    {
        var sender = new HoverLogic.Sender();
        Assert.False(sender.Send(0, 0, 0, true));
        Assert.False(sender.Send(0, 0, 0, true));

        Assert.True(sender.Send(0, 0, 0, false));
        Assert.False(sender.Send(0, 0, 0, false));
    }

    [Fact]
    public void Hide_is_needed_only_while_a_pot_card_shows()
    {
        var sender = new HoverLogic.Sender();
        Assert.False(sender.Hide());

        sender.Send(5, 100, 100, false);
        Assert.True(sender.Hide());
        Assert.False(sender.Hide());
    }

    [Fact]
    public void After_hide_the_same_pot_goes_to_the_page_again()
    {
        var sender = new HoverLogic.Sender();
        sender.Send(5, 100, 100, false);
        sender.Hide();

        Assert.True(sender.Send(5, 100, 100, false));
    }
}
