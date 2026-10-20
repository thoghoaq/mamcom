using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MamCom.Core;
using MamCom.Data;
using MamCom.Data.Device;
using MamCom.Data.Bundles;
using MamCom.Data.State;
using MamCom.Data.Storage;
using MamCom.UI;
using MamCom.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// One of each for the whole app (a WebAssembly app has a single scope). The data package is fetched from the address the app is served from.
builder.Services.AddScoped<IKeyValueStorage, LocalStorageKeyValueStorage>();
builder.Services.AddScoped<IUserStateStore, UserStateStore>();
builder.Services.AddScoped<IHouseholdStore, HouseholdStore>();
builder.Services.AddScoped<ITrayStore, TrayStore>();
builder.Services.AddScoped<IEatenLogStore, EatenLogStore>();
builder.Services.AddScoped<IShoppingStore, ShoppingStore>();
builder.Services.AddScoped<ICookingStore, CookingStore>();
builder.Services.AddScoped<ITasteStore, TasteStore>();
builder.Services.AddScoped<IDeviceFeatures, BrowserDeviceFeatures>();
builder.Services.AddScoped<IClock, SystemClock>();
builder.Services.AddScoped<IRandomSource>(_ => new SeededRandomSource((ulong)Random.Shared.NextInt64()));   // every visit draws differently; tests give a seed
builder.Services.AddScoped<ISpinPacer, TimerSpinPacer>();
builder.Services.AddScoped<ITicker, TimerTicker>();
builder.Services.AddScoped<BundleLoader>();
builder.Services.AddScoped<ThumbPreloader>();
builder.Services.AddScoped<AppData>();
builder.Services.AddScoped<IAppData>(sp => sp.GetRequiredService<AppData>());

await builder.Build().RunAsync();
