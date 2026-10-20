using System.Globalization;
using MamCom.Core.Models;
using MamCom.DataBuilder.Validation;
using MamCom.Studio.Review;
using MamCom.UI;

namespace MamCom.Studio;

/// <summary>The words Studio uses for the values of the data, all taken from <c>Strings.vi.resx</c>.</summary>
public static class Labels
{
    public static string Status(DishStatus status) => status switch
    {
        DishStatus.Draft => Strings.Studio_Status_Draft,
        DishStatus.Validated => Strings.Studio_Status_Validated,
        DishStatus.Reviewed => Strings.Studio_Status_Reviewed,
        DishStatus.CookTested => Strings.Studio_Status_CookTested,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string Slot(DishSlot slot) => slot switch
    {
        DishSlot.Soup => Strings.Studio_Slot_Soup,
        DishSlot.Main => Strings.Studio_Slot_Main,
        DishSlot.Vegetable => Strings.Studio_Slot_Vegetable,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    public static string Region(Region region) => region switch
    {
        Core.Models.Region.North => Strings.Studio_Region_North,
        Core.Models.Region.Central => Strings.Studio_Region_Central,
        Core.Models.Region.South => Strings.Studio_Region_South,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };

    public static string Spicy(SpicyLevel spicy) => spicy switch
    {
        SpicyLevel.None => Strings.Studio_Spicy_None,
        SpicyLevel.Optional => Strings.Studio_Spicy_Optional,
        SpicyLevel.Required => Strings.Studio_Spicy_Required,
        _ => throw new ArgumentOutOfRangeException(nameof(spicy), spicy, null),
    };

    public static string Method(CookingMethod method) => method switch
    {
        CookingMethod.Boil => Strings.Studio_Method_Boil,
        CookingMethod.Steam => Strings.Studio_Method_Steam,
        CookingMethod.Simmer => Strings.Studio_Method_Simmer,
        CookingMethod.Braise => Strings.Studio_Method_Braise,
        CookingMethod.StirFry => Strings.Studio_Method_StirFry,
        CookingMethod.Fry => Strings.Studio_Method_Fry,
        CookingMethod.Grill => Strings.Studio_Method_Grill,
        CookingMethod.Toss => Strings.Studio_Method_Toss,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    public static string Scale(ScaleKind scale) => scale switch
    {
        ScaleKind.Linear => Strings.Studio_Scale_Linear,
        ScaleKind.Sublinear => Strings.Studio_Scale_Sublinear,
        ScaleKind.Integer => Strings.Studio_Scale_Integer,
        ScaleKind.Fixed => Strings.Studio_Scale_Fixed,
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null),
    };

    public static string Author(DraftAuthor author) => author switch
    {
        DraftAuthor.Llm => Strings.Studio_Author_Llm,
        DraftAuthor.Human => Strings.Studio_Author_Human,
        _ => throw new ArgumentOutOfRangeException(nameof(author), author, null),
    };

    public static string Severity(Severity severity) => severity switch
    {
        DataBuilder.Validation.Severity.Error => Strings.Studio_Severity_Error,
        DataBuilder.Validation.Severity.Warning => Strings.Studio_Severity_Warning,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

    public static string Aisle(Aisle aisle) => aisle switch
    {
        Core.Models.Aisle.MeatAndFish => Strings.Studio_Aisle_MeatAndFish,
        Core.Models.Aisle.Produce => Strings.Studio_Aisle_Produce,
        Core.Models.Aisle.EggsAndTofu => Strings.Studio_Aisle_EggsAndTofu,
        Core.Models.Aisle.DryGoodsAndSpices => Strings.Studio_Aisle_DryGoodsAndSpices,
        Core.Models.Aisle.Other => Strings.Studio_Aisle_Other,
        _ => throw new ArgumentOutOfRangeException(nameof(aisle), aisle, null),
    };

    /// <summary>The key of a region-keyed object: the default for every region, or one region.</summary>
    public static string Key(string key) => key switch
    {
        RegionKeys.Default => Strings.Studio_Ing_Key_default,
        "north" => Strings.Studio_Region_North,
        "central" => Strings.Studio_Region_Central,
        "south" => Strings.Studio_Region_South,
        _ => key,
    };

    /// <summary>
    /// The nutrition database an ingredient's numbers are from; an id that has no word here is shown as it is. The ids have
    /// dashes (<c>usda-fdc</c>) and the names of the strings have underscores, so that each string is a property of Strings.
    /// </summary>
    public static string Db(string db) => Lookup("Studio_Db_" + db.Replace('-', '_')) ?? db;

    /// <summary>Season ids are data, so an id that has no word here is shown as it is.</summary>
    public static string Season(string id) => Lookup("Studio_Season_" + id) ?? id;

    /// <summary>Protein source ids are data, so an id that has no word here is shown as it is.</summary>
    public static string Protein(string id) => Lookup("Studio_Protein_" + id) ?? id;

    /// <summary>What RecipeDrafter wrote for how a draft did: <c>valid</c>, <c>warnings</c>, <c>invalid</c>, <c>failed</c>; anything else as it is.</summary>
    public static string NoteValidation(string validation) => Lookup("Studio_Note_" + validation) ?? validation;

    public static string Review(ReviewStatus status) => status switch
    {
        ReviewStatus.Saved => Strings.Studio_Review_Saved,
        ReviewStatus.Unchanged => Strings.Studio_Review_Unchanged,
        ReviewStatus.Approved => Strings.Studio_Review_Approved,
        ReviewStatus.Reopened => Strings.Studio_Review_Reopened,
        ReviewStatus.NotFound => Strings.Studio_Review_NotFound,
        ReviewStatus.Conflict => Strings.Studio_Review_Conflict,
        ReviewStatus.NotEditable => Strings.Studio_Review_NotEditable,
        ReviewStatus.NotApprovable => Strings.Studio_Review_NotApprovable,
        ReviewStatus.NotReopenable => Strings.Studio_Review_NotReopenable,
        ReviewStatus.ReviewerMissing => Strings.Studio_Review_ReviewerMissing,
        ReviewStatus.HasErrors => Strings.Studio_Review_HasErrors,
        ReviewStatus.FinalExists => Strings.Studio_Review_FinalExists,
        ReviewStatus.DataNotUsable => Strings.Studio_Review_DataNotUsable,
        ReviewStatus.CookTested => Strings.Studio_Review_CookTested,
        ReviewStatus.CookTestUndone => Strings.Studio_Review_CookTestUndone,
        ReviewStatus.NotCookTestable => Strings.Studio_Review_NotCookTestable,
        ReviewStatus.NotCookTested => Strings.Studio_Review_NotCookTested,
        ReviewStatus.MinutesMissing => Strings.Studio_Review_MinutesMissing,
        ReviewStatus.CookDateInFuture => Strings.Studio_Review_CookDateInFuture,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string IngredientReview(IngredientStatus status) => status switch
    {
        IngredientStatus.Saved => Strings.Studio_IngReview_Saved,
        IngredientStatus.Unchanged => Strings.Studio_IngReview_Unchanged,
        IngredientStatus.NutritionChosen => Strings.Studio_IngReview_NutritionChosen,
        IngredientStatus.NutritionCleared => Strings.Studio_IngReview_NutritionCleared,
        IngredientStatus.NotFound => Strings.Studio_IngReview_NotFound,
        IngredientStatus.Conflict => Strings.Studio_IngReview_Conflict,
        IngredientStatus.HasErrors => Strings.Studio_IngReview_HasErrors,
        IngredientStatus.VerifierMissing => Strings.Studio_IngReview_VerifierMissing,
        IngredientStatus.NoSuchCandidate => Strings.Studio_IngReview_NoSuchCandidate,
        IngredientStatus.CandidateHasNoNumbers => Strings.Studio_IngReview_CandidateHasNoNumbers,
        IngredientStatus.DataNotUsable => Strings.Studio_Review_DataNotUsable,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>The look of the message about an ingredient result: it went well, it changed nothing, or it did not go through.</summary>
    public static string IngredientTone(IngredientStatus status) => status switch
    {
        IngredientStatus.Saved or IngredientStatus.NutritionChosen or IngredientStatus.NutritionCleared => "ok",
        IngredientStatus.Unchanged => "info",
        _ => "danger",
    };

    /// <summary>The look of the message about a review result: it went well, it changed nothing, or it did not go through.</summary>
    public static string ReviewTone(ReviewStatus status) => status switch
    {
        ReviewStatus.Saved or ReviewStatus.Approved or ReviewStatus.Reopened or ReviewStatus.CookTested or ReviewStatus.CookTestUndone => "ok",
        ReviewStatus.Unchanged => "info",
        _ => "danger",
    };

    private static string? Lookup(string key) => Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
}
