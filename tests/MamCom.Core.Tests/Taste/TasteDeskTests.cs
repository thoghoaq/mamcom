using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Taste;

namespace MamCom.Core.Tests.Taste;

public class TasteDeskTests
{
    private static TasteProfile Profile(IEnumerable<(string Id, DishPreference Preference)>? preferences = null, params string[] blocked) =>
        new((preferences ?? []).ToDictionary(p => p.Id, p => p.Preference), blocked);

    // ---- opening -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Nothing_saved_is_an_empty_profile_and_what_was_saved_is_kept()
    {
        TasteDesk.Open(null).ShouldBeSameAs(TasteProfile.Empty);
        var saved = Profile([("a", DishPreference.Liked)], "b");

        TasteDesk.Open(saved).ShouldBeSameAs(saved);
    }

    [Fact]
    public void A_profile_that_cannot_be_used_is_the_same_as_none()
    {
        TasteDesk.Open(new TasteProfile(null!, [])).ShouldBeSameAs(TasteProfile.Empty);
        TasteDesk.Open(new TasteProfile(new Dictionary<string, DishPreference>(), null!)).ShouldBeSameAs(TasteProfile.Empty);
        TasteDesk.Open(Profile(blocked: ["a", "a"])).ShouldBeSameAs(TasteProfile.Empty);
        TasteDesk.Open(Profile(blocked: [" "])).ShouldBeSameAs(TasteProfile.Empty);
        TasteDesk.Open(Profile([(" ", DishPreference.Liked)])).ShouldBeSameAs(TasteProfile.Empty);
        TasteDesk.Open(Profile([("a", (DishPreference)9)])).ShouldBeSameAs(TasteProfile.Empty);
    }

    [Fact]
    public void A_profile_is_usable_when_empty_or_made_of_known_preferences_and_distinct_ids()
    {
        TasteProfile.Empty.IsUsable.ShouldBeTrue();
        Profile([("a", DishPreference.Liked), ("b", DishPreference.Disliked), ("c", DishPreference.Neutral)], "d", "e").IsUsable.ShouldBeTrue();
    }

    [Fact]
    public void Ids_that_differ_only_in_case_are_different_dishes_in_every_part_of_the_profile()
    {
        Profile(blocked: ["a", "A"]).IsUsable.ShouldBeTrue();

        var rated = TasteDesk.Rate(TasteDesk.Rate(TasteProfile.Empty, ["a"], DishPreference.Liked), ["A"], DishPreference.Disliked);

        rated.Preferences.Count.ShouldBe(2);
        TasteDesk.PreferenceOf(rated, "a").ShouldBe(DishPreference.Liked);
        TasteDesk.PreferenceOf(rated, "A").ShouldBe(DishPreference.Disliked);
    }

    // ---- asking ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_dish_never_rated_is_neutral_and_one_not_blocked_is_not_blocked()
    {
        var profile = Profile([("a", DishPreference.Liked)], "b");

        TasteDesk.PreferenceOf(profile, "a").ShouldBe(DishPreference.Liked);
        TasteDesk.PreferenceOf(profile, "z").ShouldBe(DishPreference.Neutral);
        TasteDesk.IsBlocked(profile, "b").ShouldBeTrue();
        TasteDesk.IsBlocked(profile, "a").ShouldBeFalse();
        TasteDesk.IsBlocked(profile, "B").ShouldBeFalse();   // an id is compared exactly
    }

    // ---- rating ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_rating_is_recorded_for_each_dish_and_the_profile_it_came_from_is_not_changed()
    {
        var before = Profile([("a", DishPreference.Disliked)]);

        var after = TasteDesk.Rate(before, ["a", "b"], DishPreference.Liked);

        TasteDesk.PreferenceOf(after, "a").ShouldBe(DishPreference.Liked);   // a new answer replaces the old one
        TasteDesk.PreferenceOf(after, "b").ShouldBe(DishPreference.Liked);
        TasteDesk.PreferenceOf(before, "a").ShouldBe(DishPreference.Disliked);
        before.Preferences.ContainsKey("b").ShouldBeFalse();
    }

    [Fact]
    public void Neutral_takes_the_dish_out_of_the_list_and_leaves_the_others()
    {
        var before = Profile([("a", DishPreference.Liked), ("b", DishPreference.Disliked), ("c", DishPreference.Liked)]);

        var after = TasteDesk.Rate(before, ["a", "not-there"], DishPreference.Neutral);

        after.Preferences.Keys.OrderBy(k => k).ShouldBe(["b", "c"]);
        TasteDesk.PreferenceOf(after, "a").ShouldBe(DishPreference.Neutral);
    }

    [Fact]
    public void Rating_does_not_touch_the_blocked_dishes()
    {
        var after = TasteDesk.Rate(Profile(blocked: ["x", "y"]), ["a"], DishPreference.Liked);

        after.BlockedDishIds.ShouldBe(["x", "y"]);
    }

    [Fact]
    public void A_rating_of_no_dish_changes_nothing_and_a_rating_that_is_not_one_is_refused()
    {
        var before = Profile([("a", DishPreference.Liked)]);

        TasteDesk.Rate(before, [], DishPreference.Disliked).Preferences.ShouldBe(before.Preferences);
        Should.Throw<ArgumentOutOfRangeException>(() => TasteDesk.Rate(before, ["a"], (DishPreference)7));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_id_is_refused_when_rating_and_when_blocking(string id)
    {
        Should.Throw<ArgumentException>(() => TasteDesk.Rate(TasteProfile.Empty, [id], DishPreference.Liked));
        Should.Throw<ArgumentException>(() => TasteDesk.Block(TasteProfile.Empty, id));
    }

    // ---- the answer of the whole meal --------------------------------------------------------------------------------------

    [Fact]
    public void Dishes_that_were_all_given_the_same_answer_share_it_and_dishes_that_differ_have_none()
    {
        var profile = Profile([("a", DishPreference.Liked), ("b", DishPreference.Liked), ("c", DishPreference.Disliked)]);

        TasteDesk.CommonPreference(profile, ["a", "b"]).ShouldBe(DishPreference.Liked);
        TasteDesk.CommonPreference(profile, ["a", "c"]).ShouldBeNull();
        TasteDesk.CommonPreference(profile, ["x", "y"]).ShouldBe(DishPreference.Neutral);   // none was asked about
        TasteDesk.CommonPreference(profile, ["a", "x"]).ShouldBeNull();                      // one liked, one never asked about
        TasteDesk.CommonPreference(profile, []).ShouldBeNull();
    }

    // ---- blocking ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_blocked_dish_is_added_once_in_the_order_it_was_blocked_and_the_others_stay()
    {
        var one = TasteDesk.Block(TasteProfile.Empty, "a");
        var two = TasteDesk.Block(one, "b");
        var again = TasteDesk.Block(two, "a");

        two.BlockedDishIds.ShouldBe(["a", "b"]);
        again.ShouldBeSameAs(two);
        TasteProfile.Empty.BlockedDishIds.ShouldBeEmpty();   // the empty profile is shared and never changed
    }

    [Fact]
    public void Blocking_does_not_touch_the_ratings()
    {
        var after = TasteDesk.Block(Profile([("a", DishPreference.Liked)]), "b");

        TasteDesk.PreferenceOf(after, "a").ShouldBe(DishPreference.Liked);
    }

    [Fact]
    public void Unblocking_takes_the_dish_out_exactly_and_leaves_the_others()
    {
        var profile = Profile(blocked: ["a", "A", "b"]);

        TasteDesk.Unblock(profile, "a").BlockedDishIds.ShouldBe(["A", "b"]);
        TasteDesk.Unblock(profile, "zzz").BlockedDishIds.ShouldBe(["a", "A", "b"]);
    }

    [Fact]
    public void The_blocked_ids_are_the_set_the_spinner_takes()
    {
        var set = TasteDesk.BlockedSet(Profile(blocked: ["a", "b"]));

        set.Count.ShouldBe(2);
        set.Contains("a").ShouldBeTrue();
        set.Contains("A").ShouldBeFalse();
    }

    // ---- the profile as it is kept -----------------------------------------------------------------------------------------

    [Fact]
    public void A_profile_survives_being_written_and_read_back_with_the_names_of_the_preferences_in_the_file()
    {
        var profile = Profile([("canh-chua", DishPreference.Liked), ("ga-ran", DishPreference.Disliked)], "bo-luc-lac");

        var json = JsonSerializer.Serialize(profile, CoreJsonContext.Default.TasteProfile);
        var back = JsonSerializer.Deserialize(json, CoreJsonContext.Default.TasteProfile)!;

        json.ShouldContain("\"liked\"");
        json.ShouldContain("\"disliked\"");
        json.ShouldNotContain("isUsable", Case.Insensitive);
        back.Preferences["canh-chua"].ShouldBe(DishPreference.Liked);
        back.Preferences["ga-ran"].ShouldBe(DishPreference.Disliked);
        back.BlockedDishIds.ShouldBe(["bo-luc-lac"]);
    }

    // ---- bad input ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Null_arguments_are_refused()
    {
        var profile = TasteProfile.Empty;

        Should.Throw<ArgumentNullException>(() => TasteDesk.PreferenceOf(null!, "a"));
        Should.Throw<ArgumentNullException>(() => TasteDesk.IsBlocked(null!, "a"));
        Should.Throw<ArgumentNullException>(() => TasteDesk.Rate(null!, ["a"], DishPreference.Liked));
        Should.Throw<ArgumentNullException>(() => TasteDesk.Rate(profile, null!, DishPreference.Liked));
        Should.Throw<ArgumentNullException>(() => TasteDesk.CommonPreference(null!, ["a"]));
        Should.Throw<ArgumentNullException>(() => TasteDesk.CommonPreference(profile, null!));
        Should.Throw<ArgumentNullException>(() => TasteDesk.Block(null!, "a"));
        Should.Throw<ArgumentNullException>(() => TasteDesk.Block(profile, null!));
        Should.Throw<ArgumentNullException>(() => TasteDesk.Unblock(null!, "a"));
        Should.Throw<ArgumentNullException>(() => TasteDesk.BlockedSet(null!));
    }
}
