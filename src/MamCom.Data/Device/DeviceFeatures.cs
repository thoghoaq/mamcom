using Microsoft.JSInterop;

namespace MamCom.Data.Device;

/// <summary>What became of a text the person asked to send (<see cref="IDeviceFeatures.ShareTextAsync"/>).</summary>
public enum ShareOutcome
{
    /// <summary>The phone's share sheet took it (Zalo is on it).</summary>
    Shared,

    /// <summary>There is no share sheet, so it is on the clipboard for the person to paste.</summary>
    Copied,

    /// <summary>The person closed the share sheet without choosing anywhere to send it. Nothing was copied.</summary>
    Cancelled,

    /// <summary>Neither the share sheet nor the clipboard could be used.</summary>
    Failed,
}

/// <summary>Whether the screen is kept lit (<see cref="IDeviceFeatures.KeepScreenOnAsync"/>).</summary>
public enum WakeLockOutcome
{
    /// <summary>The screen stays on until <see cref="IDeviceFeatures.ReleaseScreenAsync"/> (and comes back on after the page was hidden).</summary>
    Active,

    /// <summary>The browser has no Screen Wake Lock.</summary>
    Unsupported,

    /// <summary>The browser has it but refused: a low battery, a hidden page, a setting of the device.</summary>
    Denied,
}

/// <summary>What the screens ask of the phone itself, when it can: a buzz, less movement, sending a text, a lit screen, an alarm.</summary>
public interface IDeviceFeatures
{
    /// <summary>Buzzes for a moment when the device has a vibration motor and lets the page use it (Android browsers do; iOS does not).</summary>
    /// <returns>True when the device buzzed.</returns>
    ValueTask<bool> VibrateAsync(int milliseconds);

    /// <summary>The person asked their device for less animation (<c>prefers-reduced-motion</c>), so the screens should not make them wait for an effect.</summary>
    ValueTask<bool> PrefersReducedMotionAsync();

    /// <summary>Offers a text to the share sheet of the phone (Web Share API), and puts it on the clipboard when there is no share sheet. Needs a tap of the person just before.</summary>
    /// <exception cref="ArgumentException">The text is empty.</exception>
    ValueTask<ShareOutcome> ShareTextAsync(string text);

    /// <summary>Asks the browser not to turn the screen off (Screen Wake Lock), and to give it back when the page comes to the front again. Stays until <see cref="ReleaseScreenAsync"/>.</summary>
    ValueTask<WakeLockOutcome> KeepScreenOnAsync();

    /// <summary>Lets the screen turn off again. Does nothing when it was not kept on.</summary>
    ValueTask ReleaseScreenAsync();

    /// <summary>Readies the sound of the alarm. Browsers only let a page make a sound after a tap, so this is called on the tap that starts a timer.</summary>
    /// <returns>True when the page can make a sound.</returns>
    ValueTask<bool> PrepareAlertAsync();

    /// <summary>Sounds the alarm of a timer that ran out: a buzz where the phone can, and beeps where <see cref="PrepareAlertAsync"/> got the page a sound.</summary>
    /// <returns>True when the phone buzzed or beeped. The screen says so in words either way.</returns>
    ValueTask<bool> AlertAsync();
}

/// <summary>
/// <see cref="IDeviceFeatures"/> over <c>device.js</c> of the app host, which checks that each feature exists before it uses it.
/// A host without that script, or a browser without the feature, gives false, <see cref="ShareOutcome.Failed"/> or
/// <see cref="WakeLockOutcome.Unsupported"/>, never an error.
/// </summary>
public sealed class BrowserDeviceFeatures(IJSRuntime js) : IDeviceFeatures
{
    public async ValueTask<bool> VibrateAsync(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(milliseconds);

        return await Ask("mamcomDevice.vibrate", false, milliseconds).ConfigureAwait(false);
    }

    public async ValueTask<bool> PrefersReducedMotionAsync() => await Ask("mamcomDevice.prefersReducedMotion", false).ConfigureAwait(false);

    public async ValueTask<ShareOutcome> ShareTextAsync(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        return await Ask("mamcomDevice.shareText", "failed", text).ConfigureAwait(false) switch
        {
            "shared" => ShareOutcome.Shared,
            "copied" => ShareOutcome.Copied,
            "cancelled" => ShareOutcome.Cancelled,
            _ => ShareOutcome.Failed,
        };
    }

    public async ValueTask<WakeLockOutcome> KeepScreenOnAsync() => await Ask("mamcomDevice.keepScreenOn", "unsupported").ConfigureAwait(false) switch
    {
        "active" => WakeLockOutcome.Active,
        "denied" => WakeLockOutcome.Denied,
        _ => WakeLockOutcome.Unsupported,
    };

    public async ValueTask ReleaseScreenAsync() => await Ask("mamcomDevice.releaseScreen", false).ConfigureAwait(false);

    public async ValueTask<bool> PrepareAlertAsync() => await Ask("mamcomDevice.prepareAlert", false).ConfigureAwait(false);

    public async ValueTask<bool> AlertAsync() => await Ask("mamcomDevice.alertUser", false).ConfigureAwait(false);

    private async ValueTask<T> Ask<T>(string function, T whenItCannot, params object?[] arguments)
    {
        try
        {
            return await js.InvokeAsync<T>(function, arguments).ConfigureAwait(false) ?? whenItCannot;
        }
        catch (JSException)
        {
            return whenItCannot;   // the script is not there, or the browser does not know the function
        }
    }
}
