using MamCom.Data;
using Microsoft.AspNetCore.Components;

namespace MamCom.UI.Components;

/// <summary>A component that shows what <see cref="IAppData"/> knows and draws again when that changes.</summary>
public abstract class AppDataComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected IAppData Data { get; set; } = default!;

    protected override void OnInitialized() => Data.Changed += OnDataChanged;

    /// <summary>
    /// Runs when the data changed (a bundle arrived, an update was taken in), before the component is drawn again. A screen that holds
    /// something built from the bundle checks here whether it is still the one in use.
    /// </summary>
    protected virtual Task OnDataChangedAsync() => Task.CompletedTask;

    private void OnDataChanged() => _ = InvokeAsync(async () =>
    {
        try
        {
            await OnDataChangedAsync();
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);   // not lost: the app's error display gets it
            return;
        }

        StateHasChanged();
    });

    public void Dispose()
    {
        Data.Changed -= OnDataChanged;
        GC.SuppressFinalize(this);
    }
}
