using System.Text.Json;
using Bunit.TestDoubles;
using MamCom.Core.Data;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.UI.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class DataNoticesTests : BunitContext
{
    private readonly FakeAppData _data = new();

    public DataNoticesTests()
    {
        Services.AddSingleton<IAppData>(_data);
    }

    private IRenderedComponent<DataNotices> Show() => Render<DataNotices>();

    [Fact]
    public void While_the_data_loads_the_app_says_so_and_nothing_else()
    {
        _data.Status = null;

        var cut = Show();

        cut.FindAll(".notice").Count.ShouldBe(1);
        cut.Find(".notice").TextContent.ShouldBe(Strings.Data_Loading);
        cut.Find(".notice").GetAttribute("role").ShouldBe("status");
    }

    [Fact]
    public void A_good_load_of_a_release_bundle_on_a_device_that_keeps_things_shows_no_notice()
    {
        _data.Status = BundleLoadStatus.Loaded;

        Show().FindAll(".notice").ShouldBeEmpty();
    }

    [Fact]
    public void A_trial_bundle_is_labelled_as_trial_data()
    {
        _data.Status = BundleLoadStatus.Loaded;
        _data.IsTrialData = true;

        var notice = Show().Find(".notice");

        notice.TextContent.ShouldBe(Strings.Data_Trial);
        notice.ClassList.ShouldContain("notice--warning");
    }

    [Theory]
    [InlineData(BundleLoadStatus.NeedsNetwork, "Data_NeedsNetwork", "notice--warning")]
    [InlineData(BundleLoadStatus.NotPublished, "Data_NotPublished", "notice--danger")]
    [InlineData(BundleLoadStatus.Invalid, "Data_Invalid", "notice--danger")]
    public void When_there_is_no_usable_data_each_reason_has_its_own_message_and_a_way_to_try_again(BundleLoadStatus status, string text, string tone)
    {
        _data.Status = status;

        var cut = Show();

        var notice = cut.Find(".notice");
        notice.GetAttribute("role").ShouldBe("alert");
        notice.ClassList.ShouldContain(tone);
        notice.QuerySelector("p")!.TextContent.ShouldBe(Text(text));
        notice.QuerySelector("button")!.TextContent.ShouldBe(Strings.Data_Retry);
        cut.FindAll(".notice").Count.ShouldBe(1);
    }

    [Fact]
    public void Trying_again_reloads_the_page_so_the_whole_start_runs_again()
    {
        _data.Status = BundleLoadStatus.NeedsNetwork;
        var cut = Show();

        cut.Find("button").Click();

        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.History.ShouldHaveSingleItem();
        var entry = navigation.History.Single();
        entry.Options.ForceLoad.ShouldBeTrue();   // a full load, not a soft change of route
        entry.Uri.ShouldBe(navigation.Uri);       // of the page the person is on
    }

    [Fact]
    public void When_the_browser_forgets_everything_between_visits_the_app_warns_about_the_family()
    {
        _data.Status = BundleLoadStatus.Loaded;
        _data.IsPersistent = false;

        Show().Find(".notice").TextContent.ShouldBe(Strings.Data_NotPersistent);
    }

    [Fact]
    public void Failing_to_load_is_not_buried_under_a_warning_about_storage()
    {
        _data.Status = BundleLoadStatus.Invalid;
        _data.IsPersistent = false;

        var cut = Show();

        cut.FindAll(".notice").Count.ShouldBe(1);
        cut.Markup.ShouldNotContain(Strings.Data_NotPersistent);
    }

    [Fact]
    public void A_waiting_update_is_offered_with_a_button_that_takes_it_in()
    {
        _data.Status = BundleLoadStatus.Loaded;
        _data.UpdateReady = true;
        var cut = Show();

        cut.Find(".notice p").TextContent.ShouldBe(Strings.Data_UpdateReady);
        cut.Find("button").TextContent.ShouldBe(Strings.Data_UpdateApply);

        cut.Find("button").Click();

        _data.Applied.ShouldBe(1);
        cut.FindAll(".notice").ShouldBeEmpty();   // taken in, so no more offer
    }

    [Fact]
    public void When_the_update_could_not_be_taken_in_the_button_works_again()
    {
        _data.Status = BundleLoadStatus.Loaded;
        _data.UpdateReady = true;
        _data.KeepsUpdate = true;   // the server did not answer: the update is still waiting
        var cut = Show();

        cut.Find("button").Click();

        _data.Applied.ShouldBe(1);
        cut.Find("button").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("button").Click();
        _data.Applied.ShouldBe(2);
    }

    [Fact]
    public async Task The_button_cannot_be_pressed_twice_while_the_update_is_being_taken_in()
    {
        _data.Status = BundleLoadStatus.Loaded;
        _data.UpdateReady = true;
        _data.ApplyGate = new TaskCompletionSource();
        var cut = Show();

        var click = cut.Find("button").ClickAsync(new());
        cut.Find("button").HasAttribute("disabled").ShouldBeTrue();

        _data.ApplyGate.SetResult();
        await click;
        cut.FindAll(".notice").ShouldBeEmpty();
        _data.Applied.ShouldBe(1);
    }

    [Fact]
    public void The_notices_follow_the_data_as_it_changes_without_the_page_being_asked()
    {
        _data.Status = null;
        var cut = Show();
        cut.Find(".notice").TextContent.ShouldBe(Strings.Data_Loading);

        _data.Status = BundleLoadStatus.Loaded;
        _data.IsTrialData = true;
        _data.Raise();

        cut.WaitForAssertion(() => cut.Find(".notice").TextContent.ShouldBe(Strings.Data_Trial));

        _data.UpdateReady = true;
        _data.Raise();

        cut.WaitForAssertion(() => cut.FindAll(".notice").Count.ShouldBe(2));
    }

    [Fact]
    public async Task A_notice_that_is_gone_stops_listening()
    {
        _data.Status = BundleLoadStatus.Loaded;
        var cut = Show();
        _data.Listeners.ShouldBe(1);

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }

    [Fact]
    public void The_notices_have_a_name_for_screen_readers()
    {
        _data.Status = BundleLoadStatus.Loaded;

        Show().Find("section").GetAttribute("aria-label").ShouldBe(Strings.Data_NoticesLabel);
    }

    [Fact]
    public void The_summary_counts_the_dishes_and_the_ingredients_once_there_is_a_bundle()
    {
        var cut = Render<DataSummary>();
        cut.Markup.Trim().ShouldBeEmpty();   // nothing yet

        var seed = Path.Combine(AppContext.BaseDirectory, "seed");
        var dishes = Directory.GetFiles(Path.Combine(seed, "dishes"), "*.json").Take(2).Select(f => JsonSerializer.Deserialize<Dish>(File.ReadAllText(f), CoreJson.Options)!).ToArray();
        var ingredients = Directory.GetFiles(Path.Combine(seed, "ingredients"), "*.json").Take(3).Select(f => JsonSerializer.Deserialize<Ingredient>(File.ReadAllText(f), CoreJson.Options)!).ToArray();
        dishes.Length.ShouldBe(2);
        ingredients.Length.ShouldBe(3);

        _data.Status = BundleLoadStatus.Loaded;
        _data.Bundle = new DataBundle("v", false, null!, ingredients, dishes);   // the summary reads only the two lists
        _data.Raise();

        cut.WaitForAssertion(() => cut.Find(".data-summary").TextContent.ShouldBe("Có 2 món và 3 nguyên liệu."));
    }

    private static string Text(string name) => typeof(Strings).GetProperty(name)!.GetValue(null)!.ToString()!;

    private sealed class FakeAppData : IAppData
    {
        private Action? _changed;

        public BundleLoadStatus? Status { get; set; } = BundleLoadStatus.Loaded;

        public DataBundle? Bundle { get; set; }

        public bool IsLoading => Status is null;

        public bool IsTrialData { get; set; }

        public bool UpdateReady { get; set; }

        public bool IsPersistent { get; set; } = true;

        public int Applied { get; private set; }

        public TaskCompletionSource? ApplyGate { get; set; }

        /// <summary>Taking the update in does not work: it stays waiting.</summary>
        public bool KeepsUpdate { get; set; }

        public int Listeners => _changed?.GetInvocationList().Length ?? 0;

        public event Action? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public void Raise() => _changed?.Invoke();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task ApplyUpdateAsync(CancellationToken cancellationToken = default)
        {
            Applied++;
            if (ApplyGate is not null)
            {
                await ApplyGate.Task;
            }

            UpdateReady = KeepsUpdate;
            Raise();
        }
    }
}
