using MamCom.Data.Device;

namespace MamCom.UI;

/// <summary>Holds the screen for the moment the dice roll (SPEC §8.1-2: "hiệu ứng khoảng 1 giây"). A test stands in with one that does not wait.</summary>
public interface ISpinPacer
{
    ValueTask WaitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Waits <see cref="Duration"/>, unless the person asked their device for less animation: then the result is shown at once, because
/// an effect they chose not to see is only a delay.
/// </summary>
public sealed class TimerSpinPacer(IDeviceFeatures device, TimeProvider? time = null) : ISpinPacer
{
    /// <summary>About a second, as SPEC §8.1-2 says.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        if (await device.PrefersReducedMotionAsync().ConfigureAwait(false))
        {
            return;
        }

        await Task.Delay(Duration, _time, cancellationToken).ConfigureAwait(false);
    }
}
