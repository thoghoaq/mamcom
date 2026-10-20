using AngleSharp.Dom;
using MamCom.Studio.Components;
using MamCom.Studio.Review;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.Studio.Tests;

/// <summary>A bUnit context wired to a private copy of the data folder, as the running Studio is wired to the real one.</summary>
public abstract class PageTestBase : BunitContext
{
    protected const string Id = "rau-muong-luoc";

    private readonly StudioData _data = new();

    protected PageTestBase()
    {
        Services.AddSingleton(_data.Folder);
        Services.AddSingleton(_data.Store);
        Services.AddSingleton(_data.Loader);
        Services.AddSingleton(_data.Reviewer);
        Services.AddSingleton(_data.Ingredients);
        Services.AddSingleton(_data.Images);
        Services.AddSingleton(_data.IngredientReviewer);
        Services.AddSingleton<MamCom.Core.IClock>(_data.Clock);
        Services.AddSingleton(Identity);
    }

    internal StudioData Data => _data;

    protected ReviewerIdentity Identity { get; } = new();

    protected NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    protected IRenderedComponent<DishWorkbench> Open(string id = Id, DishLocation location = DishLocation.Data) =>
        Render<DishWorkbench>(p => p.Add(x => x.Id, id).Add(x => x.Location, location));

    protected IRenderedComponent<IngredientWorkbench> OpenIngredient(string id = "gao") =>
        Render<IngredientWorkbench>(p => p.Add(x => x.Id, id));

    protected static IElement Button(IRenderedComponent<DishWorkbench> cut, string marker) => cut.Find($"[data-{marker}]");

    protected static string Text(IRenderedComponent<DishWorkbench> cut, string selector) => cut.Find(selector).TextContent.Trim();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _data.Dispose();
        }

        base.Dispose(disposing);
    }
}
