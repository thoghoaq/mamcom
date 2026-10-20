using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.Data.State;
using MamCom.Data.Storage;
using MamCom.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// One of each for the whole app (a WebAssembly app has a single scope). The data package is fetched from the address the app is served from.
builder.Services.AddScoped<IKeyValueStorage, LocalStorageKeyValueStorage>();
builder.Services.AddScoped<IUserStateStore, UserStateStore>();
builder.Services.AddScoped<IHouseholdStore, HouseholdStore>();
builder.Services.AddScoped<BundleLoader>();
builder.Services.AddScoped<ThumbPreloader>();
builder.Services.AddScoped<AppData>();
builder.Services.AddScoped<IAppData>(sp => sp.GetRequiredService<AppData>());

await builder.Build().RunAsync();
