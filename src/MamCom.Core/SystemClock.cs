namespace MamCom.Core;

public sealed class SystemClock(TimeProvider? timeProvider = null) : IClock
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public DateTimeOffset Now => _timeProvider.GetLocalNow();

    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
}
