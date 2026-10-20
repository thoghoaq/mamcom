using MamCom.Core.Models;
using MamCom.Studio.Editing;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class CookFormTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [Fact]
    public void A_new_form_is_today_with_nothing_typed_and_no_photo()
    {
        var form = new CookForm(Today);

        form.CookedAt.ShouldBe(Today);
        form.Minutes.ShouldBeNull();
        form.Servings.ShouldBeNull();
        form.FinishedGrams.ShouldBeNull();
        form.Photo.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void The_entry_holds_what_was_typed_and_no_photo_is_no_photo()
    {
        var form = new CookForm(Today) { CookedAt = new DateOnly(2026, 10, 12), Minutes = 47, Servings = 4, FinishedGrams = 1450 };

        form.ToEntry().ShouldBe(new CookTestEntry(new DateOnly(2026, 10, 12), 47, 4, 1450, null));
    }

    [Fact]
    public void Empty_numbers_stay_empty_in_the_entry_and_never_turn_into_zero()
    {
        var entry = new CookForm(Today).ToEntry();

        entry.Minutes.ShouldBeNull();
        entry.Servings.ShouldBeNull();
        entry.FinishedGrams.ShouldBeNull();
    }

    [Fact]
    public void A_photo_is_in_the_entry_as_soon_as_a_file_is_named()
    {
        var form = new CookForm(Today) { Minutes = 47 };
        form.Photo.Thumb = "mon-200.webp";
        form.Photo.Source = " tự chụp ";
        form.Photo.License = "CC0";

        form.ToEntry().Photo.ShouldBe(new ImageRef("mon-200.webp", null, "tự chụp", "CC0", false));
    }

    [Fact]
    public void A_source_and_a_license_alone_are_not_a_photo()
    {
        var form = new CookForm(Today) { Minutes = 47 };
        form.Photo.Source = "tự chụp";
        form.Photo.License = "CC0";
        form.Photo.IsIllustration = true;

        form.Photo.IsEmpty.ShouldBeTrue();
        form.ToEntry().Photo.ShouldBeNull();
    }

    [Theory]
    [InlineData("a.webp", "", false)]
    [InlineData("", "b.webp", false)]
    [InlineData("a.webp", "b.webp", false)]
    [InlineData("", "", true)]
    public void A_picture_is_empty_only_when_neither_file_is_named(string thumb, string full, bool empty)
    {
        var image = new ImageForm { Thumb = thumb, Full = full };

        image.IsEmpty.ShouldBe(empty);
    }

    [Fact]
    public void A_blank_text_of_the_picture_is_no_value_and_the_others_are_trimmed()
    {
        var image = new ImageForm { Thumb = " a.webp ", Full = "   ", Source = "\t", License = " CC0 ", IsIllustration = true };

        image.ToImageRef().ShouldBe(new ImageRef("a.webp", null, null, "CC0", true));
    }
}
