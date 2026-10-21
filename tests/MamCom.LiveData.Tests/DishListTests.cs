using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.RecipeDrafter.Plan;

namespace MamCom.LiveData.Tests;

/// <summary>
/// The owner's list of dishes to draft (<c>data/plan/dish-list.csv</c>, PLAN 4.1): it must read with the drafter's own parser, and a dish that
/// already has a file must be listed with the same id and slot, or the drafter would draft it again under another name.
/// Not how many dishes, nor which: the owner edits the list.
/// </summary>
public class DishListTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "data");

    private static IReadOnlyList<DishRequest> List() => DishList.Parse(File.ReadAllText(Path.Combine(Root, DishList.RelativePath)));

    [Fact]
    public void The_list_reads_with_the_parser_of_the_drafter()
    {
        Should.NotThrow(() => List());
        List().ShouldNotBeEmpty();
    }

    [Fact]
    public void Every_dish_that_has_a_file_is_on_the_list_with_its_id_and_its_slot()
    {
        var listed = List().ToDictionary(d => d.Id, StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var file in Directory.GetFiles(Path.Combine(Root, "dishes"), "*.json"))
        {
            var dish = JsonNode.Parse(File.ReadAllText(file))!;
            var id = dish["id"]!.GetValue<string>();
            var slot = dish["slot"]!.GetValue<string>() switch { "canh" => DishSlot.Soup, "man" => DishSlot.Main, _ => DishSlot.Vegetable };

            if (!listed.TryGetValue(id, out var request))
            {
                problems.Add($"{id}: has a file but is not on the list");
            }
            else if (request.Slot != slot)
            {
                problems.Add($"{id}: the list says {request.Slot}, the file says {slot}");
            }
        }

        problems.ShouldBeEmpty(string.Join("\n", problems));
    }
}
