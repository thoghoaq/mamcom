using Microsoft.Playwright;

namespace MamCom.E2E;

/// <summary>
/// SPEC §10 "Accessibility": contrast, tap targets, a name for every icon-only control, and text that follows the size the person chose (PLAN 3.9).
/// Every screen of the app is opened on a phone of 360×740, in the light and in the dark, and looked at by <see cref="Audit"/> in the browser itself,
/// where the real colours, sizes and names are, rather than by what the stylesheets say.
/// </summary>
public class AccessibilityTests(SiteFixture site)
{
    /// <summary>What is wrong with the page that is open, each as a line; an empty array when nothing is. Runs in the page.</summary>
    private const string Audit = """
        width => {
            const problems = [];
            const visible = el => {
                const box = el.getBoundingClientRect();
                const style = getComputedStyle(el);
                return box.width > 0 && box.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
            };
            const hidden = el => !!el.closest('[aria-hidden="true"]') || !!el.closest('.visually-hidden');
            const describe = el => {
                const classes = typeof el.className === 'string' && el.className.trim() ? '.' + el.className.trim().split(/\s+/).join('.') : '';
                return el.tagName.toLowerCase() + classes + ' "' + (el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 30) + '"';
            };
            const withoutIcons = el => {
                const copy = el.cloneNode(true);
                copy.querySelectorAll('svg').forEach(icon => icon.remove());
                return (copy.textContent || '').trim();
            };
            const nameOf = el => {
                const label = el.getAttribute('aria-label');
                if (label && label.trim()) return label.trim();
                const by = el.getAttribute('aria-labelledby');
                if (by) {
                    const text = by.split(/\s+/).map(id => (document.getElementById(id) || {}).textContent || '').join(' ').trim();
                    if (text) return text;
                }
                if (el.labels && el.labels.length) {
                    const text = [...el.labels].map(withoutIcons).join(' ').trim();
                    if (text) return text;
                }
                return withoutIcons(el) || el.getAttribute('title') || '';
            };

            // What a finger or a switch can reach.
            const controls = [...document.querySelectorAll('a[href], button, input, select, textarea, [role="button"], [role="radio"], [role="checkbox"]')]
                .filter(el => visible(el) && !hidden(el));

            // 1. A name for every control, so an icon alone is not all a screen reader has.
            for (const el of controls) {
                if (!nameOf(el)) problems.push('no accessible name: ' + describe(el));
            }

            // 2. A target of at least 48 CSS pixels each way (SPEC §8.2). A tick box is hit by its label.
            for (const el of controls) {
                const target = el.matches('input[type="checkbox"], input[type="radio"]') && el.closest('label') ? el.closest('label') : el;
                const box = target.getBoundingClientRect();
                if (box.height < 47.5 || box.width < 47.5) {
                    problems.push('target ' + Math.round(box.width) + 'x' + Math.round(box.height) + ': ' + describe(el));
                }
            }

            // 3. Nothing pushes the page sideways.
            // (The width is the one of the phone, not window.innerWidth: a phone that is given a page wider than itself draws the page smaller instead.)
            if (document.documentElement.scrollWidth > width) {
                problems.push('the page scrolls sideways: ' + document.documentElement.scrollWidth + ' > ' + width);
            }
            for (const el of controls) {
                const box = el.getBoundingClientRect();
                if (box.right > width + 1 || box.left < -1) problems.push('control outside the screen: ' + describe(el));
            }

            // 4. A page title, one main region and one h1.
            if (!document.title.trim()) problems.push('no page title');
            if (document.querySelectorAll('main').length !== 1) problems.push('main regions: ' + document.querySelectorAll('main').length);
            if (document.querySelectorAll('h1').length !== 1) problems.push('h1 headings: ' + document.querySelectorAll('h1').length);

            // 5. Text no smaller than 16px (CLAUDE.md), and a contrast that WCAG asks for.
            const parse = text => {
                const numbers = (text.match(/[\d.]+/g) || []).map(Number);
                return { r: numbers[0], g: numbers[1], b: numbers[2], a: numbers.length > 3 ? numbers[3] : 1 };
            };
            const over = (top, below) => {
                const a = top.a + below.a * (1 - top.a);
                if (a === 0) return { r: 0, g: 0, b: 0, a: 0 };
                const mix = (t, b) => (t * top.a + b * below.a * (1 - top.a)) / a;
                return { r: mix(top.r, below.r), g: mix(top.g, below.g), b: mix(top.b, below.b), a };
            };
            const luminance = c => {
                const channel = v => { const s = v / 255; return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4); };
                return 0.2126 * channel(c.r) + 0.7152 * channel(c.g) + 0.0722 * channel(c.b);
            };
            const background = el => {
                let result = { r: 0, g: 0, b: 0, a: 0 };
                for (let node = el; node && node.nodeType === 1; node = node.parentElement) {
                    const style = getComputedStyle(node);
                    if (style.backgroundImage !== 'none') return null;   // a picture: cannot be told from here
                    result = over(result, parse(style.backgroundColor));
                    if (result.a >= 0.999) return result;
                }
                return over(result, { r: 255, g: 255, b: 255, a: 1 });
            };
            const opacityOf = el => {
                let opacity = 1;
                for (let node = el; node && node.nodeType === 1; node = node.parentElement) opacity *= Number(getComputedStyle(node).opacity);
                return opacity;
            };
            const worst = new Map();
            for (const el of document.querySelectorAll('body *')) {
                const own = [...el.childNodes].filter(n => n.nodeType === 3 && n.textContent.trim()).length;
                if (!own || !visible(el) || hidden(el) || el.closest(':disabled')) continue;
                const style = getComputedStyle(el);
                const size = parseFloat(style.fontSize);
                if (size < 15.99) problems.push('text of ' + size + 'px: ' + describe(el));
                const back = background(el);
                if (!back) continue;
                const color = parse(style.color);
                color.a *= opacityOf(el);
                const fore = over(color, back);
                const lighter = Math.max(luminance(fore), luminance(back)), darker = Math.min(luminance(fore), luminance(back));
                const ratio = (lighter + 0.05) / (darker + 0.05);
                const large = size >= 24 || (size >= 18.66 && Number(style.fontWeight) >= 700);
                const needed = large ? 3 : 4.5;
                if (ratio < needed) worst.set(describe(el), 'contrast ' + ratio.toFixed(2) + ' < ' + needed + ': ' + describe(el));
            }
            problems.push(...worst.values());
            return problems;
        }
        """;

    /// <summary>The same page with the text at twice the size, as a person who set the system text large would have it: no sideways scroll, and the text did grow.</summary>
    private const string AuditLargeText = """
        width => {
            const problems = [];
            const sample = document.querySelector('main p, main li, main label, main h2');
            const before = parseFloat(getComputedStyle(sample).fontSize);
            document.documentElement.style.fontSize = '200%';
            const after = parseFloat(getComputedStyle(sample).fontSize);
            if (after < before * 1.9) problems.push('the text did not follow the size of the page: ' + before + 'px became ' + after + 'px');
            if (document.documentElement.scrollWidth > width) {
                problems.push('at twice the text size the page scrolls sideways: ' + document.documentElement.scrollWidth + ' > ' + width);
            }
            for (const el of document.querySelectorAll('a[href], button, input, select, [role="radio"]')) {
                const box = el.getBoundingClientRect();
                if (box.width > 0 && el.closest('[aria-hidden="true"]') === null && (box.right > width + 1 || box.left < -1)) {
                    problems.push('at twice the text size a control is outside the screen: ' + el.tagName + ' ' + (el.textContent || '').trim().slice(0, 30));
                }
            }
            document.documentElement.style.fontSize = '';
            return problems;
        }
        """;

    [Theory]
    [InlineData(ColorScheme.Light)]
    [InlineData(ColorScheme.Dark)]
    public async Task Every_screen_has_names_for_its_controls_targets_of_48_pixels_a_contrast_that_reads_and_text_that_grows_with_the_system_size(ColorScheme scheme)
    {
        await using var app = await site.OpenAsync(scheme);
        var findings = new List<string>();

        // The first visit: nobody in the house yet.
        await app.GotoAsync("nha-minh");
        await app.Heading("Nhà mình có ai?").WaitForAsync();
        await app.Button("Bố").ClickAsync();
        await app.Button("Con trai").ClickAsync();
        await Assertions.Expect(app.Page.Locator(".member-card")).ToHaveCountAsync(2);
        await AuditAsync(app, "nha-minh (first visit, two people)", findings);

        await app.Button("Bắt đầu").ClickAsync();
        await app.WaitForPathAsync("");
        await app.WaitForTrayAsync();
        await AuditAsync(app, "/ (tray)", findings);

        // From the fifth spin that was not followed by a choice the quick filters are offered; one of them is switched on.
        for (var spin = 0; spin < 5; spin++)
        {
            await app.SpinAsync();
        }

        await Assertions.Expect(app.Page.Locator(".tray-filters")).ToBeVisibleAsync();
        await app.Page.Locator(".tray-filters .chip").Nth(1).ClickAsync();
        await Assertions.Expect(app.Page.Locator(".tray-filters .chip").Nth(1)).ToHaveAttributeAsync("aria-pressed", "true");
        await app.Page.Locator(".tray[aria-busy=false]").WaitForAsync();
        await AuditAsync(app, "/ (tray, quick filters, one on)", findings);

        // A dish that was blocked, on its own page.
        var firstDishLink = await app.Cards.Nth(0).GetByRole(AriaRole.Link).GetAttributeAsync("href");
        await app.GotoAsync(firstDishLink!);
        await app.Page.Locator(".detail").WaitForAsync();
        await AuditAsync(app, firstDishLink!, findings);
        await app.Button("Không bao giờ quay món này").ClickAsync();
        await Assertions.Expect(app.Button("Cho quay lại")).ToBeVisibleAsync();
        await AuditAsync(app, firstDishLink + " (blocked)", findings);
        await app.Button("Cho quay lại").ClickAsync();

        await app.GotoAsync("/");
        await app.WaitForTrayAsync();
        await app.ConfirmTrayAsync();
        await AuditAsync(app, "di-cho", findings);
        await app.Page.Locator(".shop-item__check").Nth(0).CheckAsync();
        await app.Page.Locator(".shop-item__check").Nth(1).CheckAsync();
        await AuditAsync(app, "di-cho (two ticked)", findings);

        await app.OpenCookingAsync();
        await AuditAsync(app, "nau (first step)", findings);
        await app.Button("Hẹn giờ", exact: false).ClickAsync();
        await Assertions.Expect(app.Page.Locator(".cook-chip")).ToHaveCountAsync(1);
        await AuditAsync(app, "nau (a timer running)", findings);

        await app.GotoAsync("sau-bua");
        await app.Heading("Cả nhà thích không?").WaitForAsync();
        await app.Page.Locator(".rate-row").First.WaitForAsync();
        await AuditAsync(app, "sau-bua", findings);
        await app.Page.Locator(".rate-row").Nth(0).GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Thích" }).ClickAsync();
        await app.Page.Locator(".rate-row").Nth(1).GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Không" }).ClickAsync();
        await AuditAsync(app, "sau-bua (answers chosen)", findings);

        await app.GotoAsync("cai-dat");
        await app.Heading("Cài đặt").WaitForAsync();
        await app.Page.Locator(".settings__section").First.WaitForAsync();
        await AuditAsync(app, "cai-dat", findings);
        await app.Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "Nam", Exact = true }).ClickAsync();
        await AuditAsync(app, "cai-dat (a region chosen)", findings);
        await app.Button("Xoá dữ liệu của tôi").ClickAsync();
        await AuditAsync(app, "cai-dat (asking to delete)", findings);

        await app.GotoAsync("/");
        await app.Page.Locator(".notice--ok").WaitForAsync();
        await AuditAsync(app, "/ (tray confirmed)", findings);

        findings.ShouldBeEmpty(string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public async Task The_audit_does_find_what_it_is_there_to_find()
    {
        await using var app = await site.OpenAsync();
        await app.GotoAsync("nha-minh");
        await app.Heading("Nhà mình có ai?").WaitForAsync();

        // A button with no name and 20px, pale text, text of 10px, something wider than the screen, and text set in pixels at the head of the page.
        await app.Page.EvaluateAsync("""
            () => {
                document.body.insertAdjacentHTML('beforeend',
                    '<button style="width:20px;height:20px"></button>'
                    + '<p style="color:#cccccc;background:#ffffff;font-size:20px">pale</p>'
                    + '<span style="font-size:10px">tiny</span>'
                    + '<div style="width:1000px;height:1px"></div>');
                document.querySelector('main').insertAdjacentHTML('afterbegin', '<p style="font-size:18px">fixed</p>');
            }
            """);

        var found = string.Join(Environment.NewLine, await app.EvaluateAsync<string[]>(Audit, SiteFixture.PhoneWidth));
        var largeText = string.Join(Environment.NewLine, await app.EvaluateAsync<string[]>(AuditLargeText, SiteFixture.PhoneWidth));

        found.ShouldContain("no accessible name: button");
        found.ShouldContain("target 20x20: button");
        found.ShouldContain("the page scrolls sideways");
        found.ShouldContain("contrast 1.6");
        found.ShouldContain("text of 10px");
        largeText.ShouldContain("the text did not follow the size of the page");
    }

    [Fact]
    public async Task A_person_who_moves_by_keyboard_always_sees_where_the_focus_is()
    {
        await using var app = await site.OpenAsync();
        await app.StartWithTwoAdultsAsync();

        for (var press = 0; press < 12; press++)
        {
            await app.Page.Keyboard.PressAsync("Tab");
            var outline = await app.EvaluateAsync<string>("() => { const style = getComputedStyle(document.activeElement); return style.outlineStyle + ' ' + style.outlineWidth; }");
            outline.ShouldNotStartWith("none", Case.Sensitive, $"the {press + 1}th element that took the focus has no outline");
            outline.ShouldNotBe("auto 0px");
        }
    }

    [Fact]
    public async Task After_a_tap_that_replaces_a_button_the_focus_goes_to_a_control_and_not_to_the_top_of_the_page()
    {
        await using var app = await site.OpenAsync();
        await app.StartWithTwoAdultsAsync();
        await app.GotoAsync("cai-dat");
        await app.Heading("Cài đặt").WaitForAsync();

        await app.Button("Xoá dữ liệu của tôi").ClickAsync();
        await Assertions.Expect(app.Button("Thôi")).ToBeFocusedAsync();   // the safe one: Enter must not delete everything

        await app.Button("Thôi").ClickAsync();
        await Assertions.Expect(app.Button("Xoá dữ liệu của tôi")).ToBeFocusedAsync();
    }

    private static async Task AuditAsync(AppSession app, string where, List<string> findings)
    {
        // The screen draws before its data is in; the audit looks at the finished screen.
        await app.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        findings.AddRange((await app.EvaluateAsync<string[]>(Audit, SiteFixture.PhoneWidth)).Select(problem => $"[{where}] {problem}"));
        findings.AddRange((await app.EvaluateAsync<string[]>(AuditLargeText, SiteFixture.PhoneWidth)).Select(problem => $"[{where}] {problem}"));
    }
}
