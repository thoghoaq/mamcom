using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class ReviewQueueTests
{
    private static ReviewQueue<string> Queue(string current, params string[] items) =>
        ReviewQueue<string>.Of(items, i => i == current, i => i.EndsWith('?'));

    [Fact]
    public void The_next_one_is_the_first_waiting_after_the_current_one()
    {
        var queue = Queue("b?", "a?", "b?", "c", "d?", "e?");

        queue.Next.ShouldBe("d?");
        queue.Waiting.ShouldBe(4);
    }

    [Fact]
    public void After_the_end_it_goes_round_to_the_start()
    {
        Queue("e?", "a", "b?", "c?", "d", "e?").Next.ShouldBe("b?");
    }

    [Fact]
    public void A_current_one_not_in_the_list_starts_from_the_top()
    {
        Queue("x", "a", "b?", "c?").Next.ShouldBe("b?");
    }

    [Fact]
    public void The_current_one_is_never_its_own_next_and_none_left_is_null()
    {
        var queue = Queue("b?", "a", "b?", "c");

        queue.Next.ShouldBeNull();
        queue.Waiting.ShouldBe(1);
        Queue("a", "a", "b").ShouldBe(new ReviewQueue<string>(null, 0));
        Queue("a").Next.ShouldBeNull();
    }

    [Fact]
    public void A_flash_is_taken_once()
    {
        var flash = new ReviewFlash();
        flash.Take().ShouldBeNull();

        flash.Set("Đã duyệt.", "ok");

        flash.Take().ShouldBe(("Đã duyệt.", "ok"));
        flash.Take().ShouldBeNull();
    }
}
