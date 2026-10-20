namespace MamCom.UI;

/// <summary>Calls something every so often while a screen is open (the countdown of the cooking mode). A test stands in with one that is fired by hand.</summary>
public interface ITicker
{
    /// <summary>Calls <paramref name="onTick"/> every <paramref name="period"/> until the result is disposed. A tick that is still running when the next is due does not make a second one start.</summary>
    IDisposable Every(TimeSpan period, Func<Task> onTick);
}

/// <summary><see cref="ITicker"/> over a timer of the clock the app runs on.</summary>
public sealed class TimerTicker(TimeProvider? time = null) : ITicker
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public IDisposable Every(TimeSpan period, Func<Task> onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);

        var running = 0;
        return _time.CreateTimer(
            _ =>
            {
                if (Interlocked.Exchange(ref running, 1) == 1)
                {
                    return;   // the last tick is not over yet
                }

                _ = Run();

                async Task Run()
                {
                    try
                    {
                        await onTick().ConfigureAwait(false);
                    }
                    finally
                    {
                        Volatile.Write(ref running, 0);
                    }
                }
            },
            null,
            period,
            period);
    }
}
