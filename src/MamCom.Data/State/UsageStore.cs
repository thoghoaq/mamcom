using MamCom.Core;
using MamCom.Core.Usage;

namespace MamCom.Data.State;

/// <summary>The anonymous log of what was done in the app (SPEC §11), for the beta and "Xuất phản hồi".</summary>
public interface IUsageLog
{
    /// <summary>Adds an event at the time of the clock.</summary>
    ValueTask RecordAsync(UsageEventKind kind);

    /// <summary>Records <see cref="UsageEventKind.AppOpened"/> the first time it is asked in this page, and does nothing after.</summary>
    ValueTask RecordOpenedAsync();

    ValueTask<UsageLog> LoadAsync();
}

/// <summary><see cref="IUsageLog"/> over <see cref="IUserStateStore"/>, under the key <c>usage.v1</c>; "Xoá dữ liệu" removes it with the rest.</summary>
public sealed class UsageStore(IUserStateStore state, IClock clock) : IUsageLog
{
    public const string Key = "usage.v1";

    private bool _opened;

    public async ValueTask RecordAsync(UsageEventKind kind)
    {
        var log = await LoadAsync().ConfigureAwait(false);
        await state.SetAsync(Key, log.Append(kind, clock.Now)).ConfigureAwait(false);   // a write the browser refuses is only a lost event
    }

    public async ValueTask RecordOpenedAsync()
    {
        if (_opened)
        {
            return;
        }

        _opened = true;
        await RecordAsync(UsageEventKind.AppOpened).ConfigureAwait(false);
    }

    public async ValueTask<UsageLog> LoadAsync() => UsageLog.Open(await state.GetAsync<UsageLog>(Key).ConfigureAwait(false));
}
