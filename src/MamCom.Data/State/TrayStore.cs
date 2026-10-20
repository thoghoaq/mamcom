using MamCom.Core.Spinning;

namespace MamCom.Data.State;

/// <summary>The "Mâm hôm nay" screen as it was left (SPEC §8.1-2): the last trays, which one is shown, the quick filters.</summary>
public interface ITrayStore
{
    /// <returns>The session that was saved, or null when none was or it cannot be read. Whether it can go on today is for <c>TrayEngine.CanContinue</c> to say.</returns>
    ValueTask<TraySession?> LoadAsync();

    /// <returns>True when it is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SaveAsync(TraySession session);
}

/// <summary><see cref="ITrayStore"/> over <see cref="IUserStateStore"/>, under the key <c>tray.v1</c>.</summary>
public sealed class TrayStore(IUserStateStore state) : ITrayStore
{
    public const string Key = "tray.v1";

    public ValueTask<TraySession?> LoadAsync() => state.GetAsync<TraySession>(Key);

    public ValueTask<bool> SaveAsync(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return state.SetAsync(Key, session);
    }
}

/// <summary>The last day each dish was eaten, which the spinner reads to keep a dish off the table for a while (SPEC §5.2, §5.4).</summary>
public interface IEatenLogStore
{
    /// <returns>The log that was saved, or an empty one when none was or it cannot be read.</returns>
    ValueTask<EatenLog> LoadAsync();

    /// <returns>True when it is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SaveAsync(EatenLog log);
}

/// <summary><see cref="IEatenLogStore"/> over <see cref="IUserStateStore"/>, under the key <c>eaten.v1</c>.</summary>
public sealed class EatenLogStore(IUserStateStore state) : IEatenLogStore
{
    public const string Key = "eaten.v1";

    public async ValueTask<EatenLog> LoadAsync() => await state.GetAsync<EatenLog>(Key).ConfigureAwait(false) ?? EatenLog.Empty;

    public ValueTask<bool> SaveAsync(EatenLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return state.SetAsync(Key, log);
    }
}
