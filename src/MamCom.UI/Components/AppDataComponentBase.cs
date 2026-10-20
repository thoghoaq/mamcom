using MamCom.Data;
using Microsoft.AspNetCore.Components;

namespace MamCom.UI.Components;

/// <summary>A component that shows what <see cref="IAppData"/> knows and draws again when that changes.</summary>
public abstract class AppDataComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected IAppData Data { get; set; } = default!;

    protected override void OnInitialized() => Data.Changed += OnDataChanged;

    private void OnDataChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Data.Changed -= OnDataChanged;
        GC.SuppressFinalize(this);
    }
}
