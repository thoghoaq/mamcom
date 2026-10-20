using Microsoft.JSInterop;

namespace MamCom.Data.Device;

/// <summary>What the screens ask of the phone itself, when it can: a short buzz, and whether the person wants less movement.</summary>
public interface IDeviceFeatures
{
    /// <summary>Buzzes for a moment when the device has a vibration motor and lets the page use it (Android browsers do; iOS does not).</summary>
    /// <returns>True when the device buzzed.</returns>
    ValueTask<bool> VibrateAsync(int milliseconds);

    /// <summary>The person asked their device for less animation (<c>prefers-reduced-motion</c>), so the screens should not make them wait for an effect.</summary>
    ValueTask<bool> PrefersReducedMotionAsync();
}

/// <summary>
/// <see cref="IDeviceFeatures"/> over <c>device.js</c> of the app host, which checks that each feature exists before it uses it.
/// A host without that script, or a browser without the feature, gives false, never an error.
/// </summary>
public sealed class BrowserDeviceFeatures(IJSRuntime js) : IDeviceFeatures
{
    public async ValueTask<bool> VibrateAsync(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(milliseconds);

        return await Ask("mamcomDevice.vibrate", milliseconds).ConfigureAwait(false);
    }

    public async ValueTask<bool> PrefersReducedMotionAsync() => await Ask("mamcomDevice.prefersReducedMotion").ConfigureAwait(false);

    private async ValueTask<bool> Ask(string function, params object?[] arguments)
    {
        try
        {
            return await js.InvokeAsync<bool>(function, arguments).ConfigureAwait(false);
        }
        catch (JSException)
        {
            return false;   // the script is not there, or the browser does not know the function
        }
    }
}
