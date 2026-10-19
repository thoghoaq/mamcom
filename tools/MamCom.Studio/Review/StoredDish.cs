using System.Security.Cryptography;
using System.Text;
using MamCom.Core.Models;

namespace MamCom.Studio.Review;

/// <summary>A dish file as it is on disk right now.</summary>
/// <param name="Text">The file text, as read.</param>
/// <param name="Version">Identifies this exact text, so a save can tell that the file changed meanwhile.</param>
/// <param name="Dish">The parsed dish; null when the file is not a dish (see <paramref name="Problem"/>).</param>
/// <param name="Problem">Why the file could not be read as a dish; null when it could.</param>
public sealed record StoredDish(DishLocation Location, string Id, string Text, string Version, Dish? Dish, string? Problem)
{
    public string File => DataFolder.RelativeFile(Location, Id);

    public static string VersionOf(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
