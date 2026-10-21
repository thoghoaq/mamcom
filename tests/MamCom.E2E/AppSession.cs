using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace MamCom.E2E;

/// <summary>One person with one phone: a browser context of its own (so its own storage) and the things the tests do again and again.</summary>
public sealed class AppSession : IAsyncDisposable
{
    private readonly List<string> _problems = [];

    public AppSession(IBrowserContext context, IPage page, string baseUrl)
    {
        Context = context;
        Page = page;
        BaseUrl = baseUrl;

        // What the app says about itself in the console, and what the page throws: a test that passes with an error behind it is not to be trusted.
        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                lock (_problems)
                {
                    _problems.Add($"{message.Type}: {message.Text}");
                }
            }
        };
        page.PageError += (_, error) =>
        {
            lock (_problems)
            {
                _problems.Add("page error: " + error);
            }
        };
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                lock (_problems)
                {
                    _problems.Add($"{response.Status}: {response.Url}");
                }
            }
        };
        page.RequestFailed += (_, request) =>
        {
            lock (_problems)
            {
                _problems.Add($"request failed: {request.Url} {request.Failure}");
            }
        };
    }

    /// <summary>The errors and warnings of the console, the errors of the page and the requests that failed, since the session began.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            lock (_problems)
            {
                return [.. _problems];
            }
        }
    }

    public IBrowserContext Context { get; }

    public IPage Page { get; }

    public string BaseUrl { get; }

    public Task GotoAsync(string path) => Page.GotoAsync($"{BaseUrl}/{path.TrimStart('/')}");

    /// <summary>The path of the page, with no address in front and no query behind.</summary>
    public string Path => new Uri(Page.Url).AbsolutePath;

    public ILocator Button(string name, bool exact = true) => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = exact });

    public ILocator Link(string name, bool exact = true) => Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = name, Exact = exact });

    public ILocator Heading(string name) => Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = name, Exact = true, Level = 1 });

    /// <summary>The cards of the tray, one per dish.</summary>
    public ILocator Cards => Page.Locator(".dish-card");

    public Task WaitForPathAsync(string path) =>
        Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == "/" + path.TrimStart('/'));

    /// <summary>The names of the dishes on the tray, in the order of the cards.</summary>
    public async Task<IReadOnlyList<string>> DishNamesAsync() => await Page.Locator(".dish-card__name").AllInnerTextsAsync();

    /// <summary>Waits until the dice have stopped: the cards are drawn and the list is no longer busy.</summary>
    public async Task WaitForTrayAsync()
    {
        await Heading("Mâm hôm nay").WaitForAsync();
        await Page.Locator(".tray[aria-busy=false]").WaitForAsync();
    }

    /// <summary>"Quay mâm": taps it and waits for the dice to stop. The label says "Đang quay…" while they roll, so the wait cannot end before the spin has begun.</summary>
    public async Task SpinAsync()
    {
        await Button("Quay mâm").ClickAsync();
        var rolling = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Đang quay…" });
        await rolling.WaitForAsync();
        await rolling.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    /// <summary>A first visit that is skipped through: "Bỏ qua" makes a house of two grown-ups, and the tray of the day is there.</summary>
    public async Task StartWithTwoAdultsAsync()
    {
        await GotoAsync("nha-minh");
        await Button("Bỏ qua").ClickAsync();
        await WaitForPathAsync("");
        await WaitForTrayAsync();
    }

    /// <summary>The tray on the screen is confirmed: the shopping list opens.</summary>
    public async Task ConfirmTrayAsync()
    {
        await Button("Chốt mâm").ClickAsync();
        await WaitForPathAsync("di-cho");
        await Heading("Đi chợ").WaitForAsync();
    }

    /// <summary>The cooking mode of the tray that was just confirmed, at its first step.</summary>
    public async Task OpenCookingAsync()
    {
        await Link("Bắt đầu nấu").ClickAsync();
        await WaitForPathAsync("nau");
        await Page.Locator(".cook__position").WaitForAsync();
    }

    public Task<T> EvaluateAsync<T>(string script, object? argument = null) => Page.EvaluateAsync<T>(script, argument);

    /// <summary>Puts a value among what the app keeps on the device (<c>mamcom.state.&lt;key&gt;</c>), for a test that starts from a state instead of getting there. Needs a page of the app to be open.</summary>
    public Task SeedAsync(string key, string json) =>
        Page.EvaluateAsync("([key, json]) => localStorage.setItem('mamcom.state.' + key, json)", new[] { key, json });

    public async Task<string?> StoredAsync(string key) =>
        await Page.EvaluateAsync<string?>("key => localStorage.getItem('mamcom.state.' + key)", key);

    /// <summary>What a screen reader and the eye both read: the text without the runs of white space, and the non-breaking spaces as spaces.</summary>
    public static string Words(string text) => Regex.Replace(text.Replace((char)0xA0, ' '), @"\s+", " ").Trim();

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}
