using System.Globalization;
using MamCom.Core.Models;
using MamCom.Core.Shopping;

namespace MamCom.UI.Formatting;

/// <summary>The words of the shopping screen: the name of an aisle, how much to buy, how far along the shopping is (SPEC §8.1-4).</summary>
public static class ShoppingFormat
{
    public static string AisleName(Aisle aisle) => aisle switch
    {
        Aisle.MeatAndFish => Strings.Shop_Aisle_MeatAndFish,
        Aisle.Produce => Strings.Shop_Aisle_Produce,
        Aisle.EggsAndTofu => Strings.Shop_Aisle_EggsAndTofu,
        Aisle.DryGoodsAndSpices => Strings.Shop_Aisle_DryGoodsAndSpices,
        Aisle.Other => Strings.Shop_Aisle_Other,
        _ => throw new ArgumentOutOfRangeException(nameof(aisle), aisle, null),
    };

    /// <summary>"3 lạng", "2,5 kg", "1 con": the number as the text for Zalo writes it (decimal comma, up to two places) and the unit of the market.</summary>
    public static string Amount(ShoppingItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return string.Format(CultureInfo.InvariantCulture, Strings.Shop_AmountFormat, VietnameseNumber.Decimal(item.Quantity, 2), item.UnitDisplay);
    }

    /// <summary>"≈ 500 g" for what is bought by the piece and varies in size (a fish); null for the rest.</summary>
    public static string? Approx(ShoppingItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.ApproxGrams is { } grams
            ? string.Format(CultureInfo.InvariantCulture, Strings.Shop_ApproxFormat, VietnameseNumber.Whole(grams))
            : null;
    }

    /// <summary>"Đã mua 3/9".</summary>
    public static string Progress(ShoppingProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return string.Format(CultureInfo.InvariantCulture, Strings.Shop_ProgressFormat, progress.Done, progress.Total);
    }

    /// <summary>"Mâm hôm nay, cho 4 người."</summary>
    public static string Meta(int memberCount) => string.Format(CultureInfo.InvariantCulture, Strings.Shop_MetaFormat, memberCount);
}
