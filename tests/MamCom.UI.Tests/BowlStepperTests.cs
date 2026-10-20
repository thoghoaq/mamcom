using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;

namespace MamCom.UI.Tests;

public class BowlStepperTests : BunitContext
{
    private readonly List<int> _steps = [];

    private IRenderedComponent<BowlStepper> Show(double value = 2, double step = 0.5, bool canDecrease = true, bool canIncrease = true, string name = "Bố") =>
        Render<BowlStepper>(p => p
            .Add(s => s.Value, value)
            .Add(s => s.Step, step)
            .Add(s => s.CanDecrease, canDecrease)
            .Add(s => s.CanIncrease, canIncrease)
            .Add(s => s.PersonName, name)
            .Add(s => s.OnStep, EventCallback.Factory.Create<int>(this, _steps.Add)));

    [Theory]
    [InlineData(2, "2 chén")]
    [InlineData(1.5, "1,5 chén")]
    [InlineData(0.5, "0,5 chén")]
    [InlineData(5, "5 chén")]
    public void It_shows_the_bowls_the_way_Vietnamese_writes_them(double value, string text)
    {
        Show(value).Find(".stepper__value").TextContent.Plain().ShouldBe(text);
    }

    [Fact]
    public void The_value_is_announced_when_it_changes()
    {
        Show().Find(".stepper__value").GetAttribute("role").ShouldBe("status");
    }

    [Fact]
    public void It_is_a_group_named_by_the_question_it_answers()
    {
        var group = Show().Find(".stepper");

        group.GetAttribute("role").ShouldBe("group");
        group.GetAttribute("aria-label").ShouldBe("Thường ăn mấy chén cơm?");
    }

    [Fact]
    public void Each_button_says_what_it_does_for_whom()
    {
        var buttons = Show(name: "Bà").FindAll("button");

        buttons.Count.ShouldBe(2);
        buttons[0].GetAttribute("aria-label").ShouldBe("Bớt 0,5 chén của Bà");
        buttons[1].GetAttribute("aria-label").ShouldBe("Thêm 0,5 chén cho Bà");
    }

    [Fact]
    public void The_size_of_the_step_is_the_one_it_is_given()
    {
        var buttons = Show(step: 0.25).FindAll("button");

        buttons[0].GetAttribute("aria-label").ShouldBe("Bớt 0,25 chén của Bố");
        buttons[1].GetAttribute("aria-label").ShouldBe("Thêm 0,25 chén cho Bố");
    }

    [Fact]
    public void The_buttons_are_plain_buttons_with_a_picture_and_no_text_of_their_own()
    {
        foreach (var button in Show().FindAll("button"))
        {
            button.GetAttribute("type").ShouldBe("button");   // inside a form they must not submit it
            button.ClassList.ShouldContain("button--icon");
            button.TextContent.Trim().ShouldBeEmpty();
            button.QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
        }
    }

    [Fact]
    public void The_first_button_draws_a_minus_and_the_second_a_plus()
    {
        var uses = Show().FindAll("button svg use").Select(u => u.GetAttribute("href")).ToArray();

        uses.Length.ShouldBe(2);
        uses[0].ShouldEndWith("#minus");
        uses[1].ShouldEndWith("#plus");
    }

    [Fact]
    public void The_minus_button_asks_for_a_step_down_and_the_plus_button_for_a_step_up()
    {
        var cut = Show();

        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();
        cut.FindAll("button")[1].Click();

        _steps.ShouldBe([-1, +1, +1]);
    }

    [Fact]
    public void A_button_that_cannot_go_further_is_disabled()
    {
        var atBottom = Show(canDecrease: false);
        atBottom.FindAll("button")[0].HasAttribute("disabled").ShouldBeTrue();
        atBottom.FindAll("button")[1].HasAttribute("disabled").ShouldBeFalse();

        var atTop = Show(canIncrease: false);
        atTop.FindAll("button")[0].HasAttribute("disabled").ShouldBeFalse();
        atTop.FindAll("button")[1].HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void It_shows_the_value_it_is_given_again_when_the_value_changes()
    {
        var cut = Show(2);

        cut.Render(p => p.Add(s => s.Value, 3.5).Add(s => s.PersonName, "Bố"));

        cut.Find(".stepper__value").TextContent.Plain().ShouldBe("3,5 chén");
    }
}
