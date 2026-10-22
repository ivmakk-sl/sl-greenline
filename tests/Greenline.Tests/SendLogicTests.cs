using Greenline;
using Xunit;

public class SendLogicTests
{
    [Fact]
    public void A_no_script_request_asks_for_one_full_send()
    {
        var sends = new SendLogic();
        sends.RequestFull();

        Assert.True(sends.TakeFull(100f));
        Assert.False(sends.TakeFull(100.5f));
    }

    [Fact]
    public void No_request_asks_for_no_full_send()
    {
        Assert.False(new SendLogic().TakeFull(100f));
    }

    [Fact]
    public void A_second_no_script_request_within_5_seconds_asks_for_none()
    {
        var sends = new SendLogic();
        sends.RequestFull();
        Assert.True(sends.TakeFull(100f));

        sends.RequestFull();
        Assert.False(sends.TakeFull(104.9f));
        // The page posts no script again at its next check, so a refused request is not kept.
        Assert.False(sends.TakeFull(106f));
    }

    [Fact]
    public void A_no_script_request_after_5_seconds_asks_for_one_more()
    {
        var sends = new SendLogic();
        sends.RequestFull();
        Assert.True(sends.TakeFull(100f));

        sends.RequestFull();
        Assert.True(sends.TakeFull(105f));
    }

    [Fact]
    public void A_no_frame_or_a_check_request_forgets_the_last_push_once()
    {
        var sends = new SendLogic();
        Assert.False(sends.TakeForget());

        sends.RequestForget();
        Assert.True(sends.TakeForget());
        Assert.False(sends.TakeForget());
    }
}
