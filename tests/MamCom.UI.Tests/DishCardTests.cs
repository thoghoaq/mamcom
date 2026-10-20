using MamCom.Core.Models;
using MamCom.Core.Spinning;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MamCom.UI.Tests;

public class DishCardTests : BunitContext
{
    private static Dish Soup => Seed("canh-chua-ca-loc");

    private static Dish Main => Seed("thit-kho-trung");

    private static Dish Greens => Seed("rau-muong-luoc");

    private static Dish Seed(string id) => TestConfig.SeedBundle.Dishes.Single(d => d.Id == id);

    private int _locks;
    private int _swaps;

    private IRenderedComponent<DishCard> Show(Dish? dish = null, DishFacts? facts = null, bool locked = false, bool rolling = false, bool disabled = false, bool spicy = false)
    {
        dish ??= Soup;
        return Render<DishCard>(p => p
            .Add(c => c.Dish, dish)
            .Add(c => c.Facts, facts ?? new DishFacts(dish.Id, 45, 450))
            .Add(c => c.IsLocked, locked)
            .Add(c => c.Rolling, rolling)
            .Add(c => c.Disabled, disabled)
            .Add(c => c.ShowSpicyNote, spicy)
            .Add(c => c.OnToggleLock, EventCallback.Factory.Create(this, () => _locks++))
            .Add(c => c.OnSwap, EventCallback.Factory.Create(this, () => _swaps++)));
    }

    private static async Task Swipe(IRenderedComponent<DishCard> cut, double fromX, double toX, double fromY = 50, double toY = 50)
    {
        await cut.Find("li").TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = fromX, ClientY = fromY });
        await cut.Find("li").TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = toX, ClientY = toY });
    }

    // ---- what the card says -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("canh-chua-ca-loc", "Canh", "slot--soup")]
    [InlineData("thit-kho-trung", "Món mặn", "slot--main")]
    [InlineData("rau-muong-luoc", "Rau", "slot--veg")]
    public void The_card_says_what_kind_of_dish_it_is_in_the_colour_of_that_kind(string id, string label, string colourClass)
    {
        var slot = Show(Seed(id)).Find(".dish-card__slot");

        slot.TextContent.ShouldBe(label);
        slot.ClassList.ShouldContain(colourClass);
    }

    [Fact]
    public void The_name_is_a_heading_so_a_screen_reader_can_jump_from_dish_to_dish()
    {
        var name = Show(Main).Find("h2");

        name.TextContent.ShouldBe(Main.Name);
        name.ClassList.ShouldContain("dish-card__name");
    }

    [Theory]
    [InlineData(0, "0 phút")]
    [InlineData(23, "23 phút")]
    [InlineData(60, "1 giờ")]
    [InlineData(95, "1 giờ 35 phút")]
    public void How_long_the_dish_takes_is_written_in_minutes_and_hours(int minutes, string expected)
    {
        var facts = Show(facts: new DishFacts(Soup.Id, minutes, 450)).FindAll(".dish-card__fact")[0];

        facts.TextContent.Plain().ShouldBe(expected);
    }

    [Fact]
    public void The_energy_is_an_estimate_for_one_serving_rounded_to_ten()
    {
        Show(facts: new DishFacts(Soup.Id, 20, 447)).FindAll(".dish-card__fact")[1].TextContent.Plain().ShouldBe("≈ 450 kcal/suất");
    }

    [Fact]
    public void An_energy_of_zero_that_is_known_is_shown_as_zero()
    {
        Show(facts: new DishFacts(Soup.Id, 20, 0)).FindAll(".dish-card__fact")[1].TextContent.Plain().ShouldBe("≈ 0 kcal/suất");
    }

    [Fact]
    public void An_energy_that_is_not_known_is_said_to_be_unknown_and_never_shown_as_zero()
    {
        var text = Show(facts: new DishFacts(Soup.Id, 20, null)).FindAll(".dish-card__fact")[1].TextContent;

        text.ShouldBe("chưa đủ số liệu");
    }

    [Fact]
    public void The_icons_of_the_facts_are_decoration()
    {
        Show().FindAll(".dish-card__fact svg").ShouldAllBe(s => s.GetAttribute("aria-hidden") == "true");
    }

    [Fact]
    public void The_note_about_chilli_is_there_only_when_asked_for()
    {
        Show(spicy: true).Find(".dish-card__aside").TextContent.ShouldBe("Để ớt riêng");
        Show(spicy: false).FindAll(".dish-card__aside").ShouldBeEmpty();
    }

    // ---- the picture ------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_dish_without_a_picture_has_the_tile_of_its_kind_without_words_and_named_for_a_screen_reader()
    {
        var cut = Show(Soup);

        var tile = cut.Find(".tile");
        tile.ClassList.ShouldContain("tile--compact");
        tile.GetAttribute("aria-label")!.ShouldStartWith("Chưa có ảnh");
        cut.FindAll("img").ShouldBeEmpty();
    }

    [Fact]
    public void A_dish_with_a_picture_shows_the_picture_from_the_images_folder_and_not_the_tile()
    {
        var withPicture = Soup with { Image = new ImageRef("canh chua-200.webp", "canh chua-800.webp", "tự chụp", "CC0", false) };

        var cut = Show(withPicture);

        var image = cut.Find("img.picture__image");
        image.GetAttribute("src").ShouldBe("images/canh%20chua-200.webp");
        image.GetAttribute("alt").ShouldBe(string.Empty);   // the name is written next to it
        image.GetAttribute("loading").ShouldBe("lazy");
        cut.FindAll(".tile").ShouldBeEmpty();
        cut.FindAll(".picture__badge").ShouldBeEmpty();
    }

    [Fact]
    public void A_picture_that_is_an_illustration_says_so()
    {
        var illustrated = Soup with { Image = new ImageRef("a-200.webp", null, "AI", "CC0", true) };

        Show(illustrated).Find(".picture__badge").TextContent.ShouldBe("Ảnh minh hoạ");
    }

    [Fact]
    public async Task A_picture_that_cannot_be_loaded_gives_way_to_the_tile_and_is_never_left_broken()
    {
        var cut = Show(Soup with { Image = new ImageRef("missing-200.webp", null, "tự chụp", "CC0", false) });

        await cut.Find("img").TriggerEventAsync("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

        cut.FindAll("img").ShouldBeEmpty();
        cut.Find(".tile").ShouldNotBeNull();
    }

    [Fact]
    public async Task Another_dish_with_a_picture_gets_its_own_chance_after_one_failed()
    {
        var cut = Show(Soup with { Image = new ImageRef("missing-200.webp", null, "tự chụp", "CC0", false) });
        await cut.Find("img").TriggerEventAsync("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

        cut.Render(p => p.Add(c => c.Dish, Main with { Image = new ImageRef("other-200.webp", null, "tự chụp", "CC0", false) }).Add(c => c.Facts, new DishFacts(Main.Id, 40, null)));

        cut.Find("img").GetAttribute("src").ShouldBe("images/other-200.webp");
    }

    // ---- the lock ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_unlocked_card_offers_to_lock_and_shows_an_open_lock()
    {
        var cut = Show();

        var button = cut.Find(".dish-card__lock");
        button.GetAttribute("aria-pressed").ShouldBe("false");
        button.GetAttribute("aria-label").ShouldBe($"Khoá {Soup.Name}");
        button.QuerySelector("use")!.GetAttribute("href")!.ShouldEndWith("#lock-open");
        cut.Find("li").ClassList.ShouldNotContain("dish-card--locked");
    }

    [Fact]
    public void A_locked_card_says_so_by_the_state_of_its_button_the_picture_of_a_closed_lock_and_its_outline()
    {
        var cut = Show(locked: true);

        var button = cut.Find(".dish-card__lock");
        button.GetAttribute("aria-pressed").ShouldBe("true");
        button.QuerySelector("use")!.GetAttribute("href")!.ShouldEndWith("#lock");
        cut.Find("li").ClassList.ShouldContain("dish-card--locked");
    }

    [Fact]
    public void Tapping_the_lock_asks_to_lock_or_unlock_and_does_not_swap()
    {
        var cut = Show();

        cut.Find(".dish-card__lock").Click();

        _locks.ShouldBe(1);
        _swaps.ShouldBe(0);
    }

    // ---- the swap ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Tapping_the_swap_asks_to_swap_the_dish()
    {
        var cut = Show();

        cut.Find(".dish-card__swap").Click();

        _swaps.ShouldBe(1);
        cut.Find(".dish-card__swap").GetAttribute("aria-label").ShouldBe($"Đổi món {Soup.Name}");
    }

    [Fact]
    public void A_locked_dish_cannot_be_swapped_and_a_disabled_card_cannot_be_changed_at_all()
    {
        var locked = Show(locked: true);
        locked.Find(".dish-card__swap").HasAttribute("disabled").ShouldBeTrue();
        locked.Find(".dish-card__lock").HasAttribute("disabled").ShouldBeFalse();   // it can be unlocked

        var disabled = Show(disabled: true);
        disabled.Find(".dish-card__swap").HasAttribute("disabled").ShouldBeTrue();
        disabled.Find(".dish-card__lock").HasAttribute("disabled").ShouldBeTrue();

        Show().Find(".dish-card__swap").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void The_buttons_are_plain_buttons_with_a_picture_only()
    {
        foreach (var button in Show().FindAll("button"))
        {
            button.GetAttribute("type").ShouldBe("button");
            button.ClassList.ShouldContain("button--icon");
            button.TextContent.Trim().ShouldBeEmpty();
            button.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        }
    }

    // ---- the swipe --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(250, 150)]   // to the left
    [InlineData(100, 200)]   // to the right
    [InlineData(100, 160)]   // exactly the least distance
    public async Task A_swipe_along_the_card_swaps_the_dish(double fromX, double toX)
    {
        var cut = Show();

        await Swipe(cut, fromX, toX);

        _swaps.ShouldBe(1);
    }

    [Theory]
    [InlineData(100, 159)]   // not far enough
    [InlineData(100, 100)]   // a tap
    public async Task A_short_swipe_or_a_tap_does_not_swap(double fromX, double toX)
    {
        var cut = Show();

        await Swipe(cut, fromX, toX);

        _swaps.ShouldBe(0);
    }

    [Fact]
    public async Task A_drag_that_is_mostly_up_and_down_is_a_person_scrolling_and_does_not_swap()
    {
        var cut = Show();

        await Swipe(cut, 100, 190, fromY: 20, toY: 150);   // 90 across but 130 along

        _swaps.ShouldBe(0);
    }

    [Fact]
    public async Task A_drag_that_is_more_across_than_along_but_not_twice_as_far_is_not_a_swipe()
    {
        var cut = Show();

        await Swipe(cut, 100, 200, fromY: 20, toY: 90);   // 100 across and 70 along

        _swaps.ShouldBe(0);
    }

    [Fact]
    public async Task A_swipe_that_is_twice_as_far_across_as_along_still_counts()
    {
        var cut = Show();

        await Swipe(cut, 100, 220, fromY: 20, toY: 80);   // 120 across, 60 along

        _swaps.ShouldBe(1);
    }

    [Fact]
    public async Task A_locked_card_ignores_a_swipe()
    {
        var cut = Show(locked: true);

        await Swipe(cut, 250, 100);

        _swaps.ShouldBe(0);
    }

    [Fact]
    public async Task A_disabled_card_ignores_a_swipe()
    {
        var cut = Show(disabled: true);

        await Swipe(cut, 250, 100);

        _swaps.ShouldBe(0);
    }

    [Fact]
    public async Task A_swipe_that_the_browser_cancelled_is_forgotten_and_a_release_with_no_press_does_nothing()
    {
        var cut = Show();

        await cut.Find("li").TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = 250, ClientY = 50 });
        await cut.Find("li").TriggerEventAsync("onpointercancel", new PointerEventArgs());
        await cut.Find("li").TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = 100, ClientY = 50 });

        _swaps.ShouldBe(0);
    }

    [Fact]
    public async Task One_swipe_counts_once()
    {
        var cut = Show();

        await Swipe(cut, 250, 100);
        await cut.Find("li").TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = 100, ClientY = 50 });   // a second release

        _swaps.ShouldBe(1);
    }

    [Fact]
    public async Task Pressing_the_lock_button_and_letting_go_on_it_is_not_a_swipe()
    {
        var cut = Show();

        await cut.Find(".dish-card__lock").TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = 300, ClientY = 90 });
        await cut.Find(".dish-card__lock").TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = 301, ClientY = 90 });

        _swaps.ShouldBe(0);
    }

    // ---- while the dice roll ----------------------------------------------------------------------------------------------

    [Fact]
    public void A_card_that_can_change_shows_it_while_the_dice_roll_and_a_locked_one_stays_still()
    {
        Show(rolling: true).Find("li").ClassList.ShouldContain("dish-card--rolling");
        Show(rolling: true, locked: true).Find("li").ClassList.ShouldNotContain("dish-card--rolling");
        Show(rolling: false).Find("li").ClassList.ShouldNotContain("dish-card--rolling");
    }

    [Fact]
    public void The_card_is_a_card_of_the_page_and_a_list_item()
    {
        var item = Show().Find("li");

        item.ClassList.ShouldContain("card");
        item.ClassList.ShouldContain("dish-card");
    }
}
