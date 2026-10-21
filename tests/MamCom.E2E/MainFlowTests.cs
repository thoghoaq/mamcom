using Microsoft.Playwright;

namespace MamCom.E2E;

/// <summary>SPEC §10: "Onboarding → quay → khoá → quay → chốt → đi chợ → chế độ nấu", and what comes after (sau bữa), on a phone of 360×740.</summary>
public class MainFlowTests(SiteFixture site)
{
    // A house of two grown-ups, as "Bỏ qua" makes it, and what a household that blocked six of the seven soups of the seed has said.
    private const string TwoAdults = """{"region":"north","bowlSize":"medium","members":[{"displayName":"Bố","ageGroup":"adult","bowlsPerMeal":2,"tags":[],"avoid":[]},{"displayName":"Mẹ","ageGroup":"adult","bowlsPerMeal":2,"tags":[],"avoid":[]}],"availableMinutes":null}""";

    private const string AllSoupsButOneBlocked = """{"preferences":{},"blockedDishIds":["canh-bau-tom","canh-cu-cai-suon","canh-nam-dau-phu","canh-rau-cu-thap-cam","canh-rau-ngot-thit-bam","canh-trung-ca-chua"]}""";

    [Fact]
    public async Task From_the_first_visit_through_spinning_locking_confirming_shopping_and_cooking_to_the_question_after_the_meal()
    {
        await using var app = await site.OpenAsync();

        // Onboarding: three people, then the tray of the day is there at once.
        await app.GotoAsync("/");
        await app.Heading("Nhà mình có ai?").WaitForAsync();
        foreach (var role in new[] { "Bố", "Mẹ", "Con trai" })
        {
            await app.Button(role).ClickAsync();
        }

        (await app.Page.Locator(".member-card").CountAsync()).ShouldBe(3);
        await app.Button("Bắt đầu").ClickAsync();
        await app.WaitForPathAsync("");
        await app.WaitForTrayAsync();
        (await app.Cards.CountAsync()).ShouldBeGreaterThanOrEqualTo(3);

        // Spin, lock the middle dish, spin again: the locked one stays.
        var lockedName = (await app.DishNamesAsync())[1];
        var lockButton = app.Page.Locator(".dish-card__lock").Nth(1);
        await lockButton.ClickAsync();
        await Assertions.Expect(lockButton).ToHaveAttributeAsync("aria-pressed", "true");
        for (var spin = 0; spin < 3; spin++)
        {
            await app.SpinAsync();
            (await app.DishNamesAsync())[1].ShouldBe(lockedName);
        }

        // Confirm: the shopping list of this tray opens, a tick is kept across a reload.
        await app.ConfirmTrayAsync();
        var tick = app.Page.Locator(".shop-item__check").First;
        await tick.CheckAsync();
        await Assertions.Expect(tick).ToBeCheckedAsync();
        await app.Page.ReloadAsync();
        await Assertions.Expect(app.Page.Locator(".shop-item__check").First).ToBeCheckedAsync();

        // Cooking: the first step is the rice; its timer survives a reload, and the right half of the screen goes on.
        await app.OpenCookingAsync();
        await app.Button("Hẹn giờ", exact: false).ClickAsync();
        await Assertions.Expect(app.Page.Locator(".cook-chip")).ToHaveCountAsync(1);
        await app.Page.ReloadAsync();
        await Assertions.Expect(app.Page.Locator(".cook-chip")).ToHaveCountAsync(1);

        var steps = int.Parse((await app.Page.Locator(".cook__position").InnerTextAsync()).Split('/')[1]);
        steps.ShouldBeGreaterThan(2);
        await app.Page.Locator(".cook-zone--next").ClickAsync();
        AppSession.Words(await app.Page.Locator(".cook__position").InnerTextAsync()).ShouldBe($"Bước 2/{steps}");
        await app.Page.Locator(".cook-zone--back").ClickAsync();
        AppSession.Words(await app.Page.Locator(".cook__position").InnerTextAsync()).ShouldBe($"Bước 1/{steps}");
        for (var step = 1; step < steps; step++)
        {
            await app.Button("Tiếp").ClickAsync();
        }

        // The end of the meal: the question, one tap for the whole family, and back to the tray, confirmed.
        await app.Button("Xong bữa").ClickAsync();
        await app.WaitForPathAsync("sau-bua");
        await app.Heading("Cả nhà thích không?").WaitForAsync();
        var liked = app.Page.Locator(".chips--rate-whole").GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Thích" });
        await liked.ClickAsync();
        await Assertions.Expect(liked).ToHaveAttributeAsync("aria-checked", "true");
        (await app.StoredAsync("taste.v1")).ShouldNotBeNull().ShouldContain("liked");

        await app.Link("Xong").ClickAsync();
        await app.WaitForPathAsync("");
        await Assertions.Expect(app.Page.GetByText("Đã chốt mâm hôm nay.")).ToBeVisibleAsync();

        app.Problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_tray_of_the_day_and_its_lock_are_still_there_after_the_app_is_closed_and_opened_again()
    {
        await using var app = await site.OpenAsync();
        await app.StartWithTwoAdultsAsync();
        await app.Page.Locator(".dish-card__lock").Nth(0).ClickAsync();
        await app.SpinAsync();
        var names = await app.DishNamesAsync();

        await app.Page.ReloadAsync();
        await app.WaitForTrayAsync();

        (await app.DishNamesAsync()).ShouldBe(names);
        await Assertions.Expect(app.Page.Locator(".dish-card__lock").Nth(0)).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task A_swipe_along_a_card_changes_that_dish_only_and_a_swipe_up_or_down_changes_nothing()
    {
        await using var app = await site.OpenAsync();
        await app.StartWithTwoAdultsAsync();
        var before = await app.DishNamesAsync();

        // The swipe starts on the name of the dish: the most natural place, and one where a mouse used to start the drag of a link.
        var box = (await app.Cards.Nth(1).Locator(".dish-card__link").BoundingBoxAsync()).ShouldNotBeNull();
        var y = box.Y + (box.Height / 2);

        // Down and up by 150 px is the page being scrolled, not a swap.
        var x = box.X + 20;
        await app.Page.Mouse.MoveAsync(x, y);
        await app.Page.Mouse.DownAsync();
        await app.Page.Mouse.MoveAsync(x, y + 150, new MouseMoveOptions { Steps = 5 });
        await app.Page.Mouse.UpAsync();
        (await app.DishNamesAsync()).ShouldBe(before);

        await app.Page.Mouse.MoveAsync(x + 130, y);
        await app.Page.Mouse.DownAsync();
        await app.Page.Mouse.MoveAsync(x, y, new MouseMoveOptions { Steps = 5 });
        await app.Page.Mouse.UpAsync();

        await Assertions.Expect(app.Page.Locator(".dish-card__name").Nth(1)).Not.ToHaveTextAsync(before[1]);
        var after = await app.DishNamesAsync();
        after[0].ShouldBe(before[0]);
        after[2].ShouldBe(before[2]);
    }

    [Fact]
    public async Task A_dish_the_household_blocked_is_never_drawn_again_however_often_the_tray_is_spun()
    {
        await using var app = await site.OpenAsync();
        await app.GotoAsync("nha-minh");
        await app.Heading("Nhà mình có ai?").WaitForAsync();
        await app.SeedAsync("household.v1", TwoAdults);
        await app.SeedAsync("taste.v1", AllSoupsButOneBlocked);

        await app.GotoAsync("/");
        await app.WaitForTrayAsync();
        (await app.DishNamesAsync())[0].ShouldBe("Canh chua cá lóc");
        for (var spin = 0; spin < 6; spin++)
        {
            await app.SpinAsync();
            (await app.DishNamesAsync())[0].ShouldBe("Canh chua cá lóc");
        }
    }

    [Fact]
    public async Task A_name_left_empty_goes_back_to_the_name_it_had_and_a_card_stays_when_another_is_removed()
    {
        await using var app = await site.OpenAsync();
        await app.GotoAsync("nha-minh");
        await app.Button("Bố").ClickAsync();
        await app.Button("Mẹ").ClickAsync();
        var names = app.Page.Locator(".member-card__name");
        await Assertions.Expect(names).ToHaveCountAsync(2);

        await names.Nth(0).FillAsync("Ba Tèo");
        await names.Nth(0).BlurAsync();
        await names.Nth(0).FillAsync(string.Empty);
        await names.Nth(0).BlurAsync();
        await Assertions.Expect(names.Nth(0)).ToHaveValueAsync("Ba Tèo");

        // The second card is the same element after the first was removed, not a new one with the same words.
        await names.Nth(1).EvaluateAsync("element => element.dataset.same = 'yes'");
        await app.Button("Bỏ Ba Tèo").ClickAsync();
        await Assertions.Expect(names).ToHaveCountAsync(1);
        await Assertions.Expect(names.Nth(0)).ToHaveAttributeAsync("data-same", "yes");
        await Assertions.Expect(names.Nth(0)).ToHaveValueAsync("Mẹ");
    }

    [Fact]
    public async Task The_settings_keep_the_region_and_the_bowl_the_blocked_dishes_come_and_go_and_the_data_can_be_wiped()
    {
        await using var app = await site.OpenAsync();
        await app.StartWithTwoAdultsAsync();

        // Settings by the gear in the header: a region and a bowl, kept across a reload.
        await app.Link("Cài đặt").ClickAsync();
        await app.WaitForPathAsync("cai-dat");
        await app.Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "Nam", Exact = true }).ClickAsync();
        await app.Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "To", Exact = true }).ClickAsync();
        await Assertions.Expect(app.Page.Locator(".settings__status")).ToHaveTextAsync("Đã lưu.");
        await app.Page.ReloadAsync();
        await Assertions.Expect(app.Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "Nam", Exact = true })).ToHaveAttributeAsync("aria-checked", "true");
        await Assertions.Expect(app.Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "To", Exact = true })).ToHaveAttributeAsync("aria-checked", "true");

        // Block a dish on its own page, find it in the settings, let it back.
        await app.GotoAsync("/");
        await app.WaitForTrayAsync();
        var name = (await app.DishNamesAsync())[0];
        await app.Cards.Nth(0).GetByRole(AriaRole.Link).ClickAsync();
        await app.Heading(name).WaitForAsync();
        await app.Button("Không bao giờ quay món này").ClickAsync();
        await Assertions.Expect(app.Button("Cho quay lại")).ToBeVisibleAsync();
        await app.GotoAsync("cai-dat");
        await Assertions.Expect(app.Page.Locator(".blocked__name")).ToHaveTextAsync(name);
        await app.Button($"Bỏ chặn {name}").ClickAsync();
        await Assertions.Expect(app.Page.Locator(".blocked__item")).ToHaveCountAsync(0);

        // Wipe it: two taps, and the app is a first visit again.
        await app.Button("Xoá dữ liệu của tôi").ClickAsync();
        await app.Button("Xoá hết").ClickAsync();
        await app.Heading("Nhà mình có ai?").WaitForAsync();
        app.Path.ShouldBe("/nha-minh");
        (await app.EvaluateAsync<string[]>("Object.keys(localStorage).filter(key => key.startsWith('mamcom.state.'))")).ShouldBeEmpty();

        app.Problems.ShouldBeEmpty();
    }
}
