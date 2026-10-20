using MamCom.Core;
using MamCom.Studio.Components;
using MamCom.Studio.Review;
using MamCom.UI;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection(StudioOptions.Section).Get<StudioOptions>() ?? new StudioOptions();

DataFolder dataFolder;
try
{
    dataFolder = DataFolder.Locate(options.DataFolder, builder.Environment.ContentRootPath);
}
catch (DirectoryNotFoundException ex)
{
    Console.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.Studio_DataFolderMissing, ex.Message));
    return 1;
}

if (!builder.Environment.IsDevelopment())
{
    // Studio is always run from a build folder on the owner's machine. Without this, only the Development environment
    // finds the files of MamCom.UI (tokens.css, base.css), so "dotnet run --no-launch-profile" would show an unstyled page.
    builder.WebHost.UseStaticWebAssets();
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton(dataFolder);
builder.Services.AddSingleton<DishStore>();
builder.Services.AddSingleton<KitchenLoader>();
builder.Services.AddSingleton<DishReviewer>();
builder.Services.AddSingleton<IngredientStore>();
builder.Services.AddSingleton<IngredientReviewer>();
builder.Services.AddSingleton(new ReviewerIdentity(options.Reviewer));

var app = builder.Build();
var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

// The pictures an ingredient names live in data/images/out, outside the web root; this hands one over by its plain file name.
app.MapGet("/data-images/{name}", (string name, IngredientStore store) =>
    store.ImagePath(name) is { } path
        ? Results.File(path, contentTypes.TryGetContentType(name, out var type) ? type : "application/octet-stream")
        : Results.NotFound());
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
return 0;
