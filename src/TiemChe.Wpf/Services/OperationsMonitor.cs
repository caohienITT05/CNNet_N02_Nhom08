using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TiemChe.Contracts.Operations;

namespace TiemChe.Wpf.Services;

public sealed partial class OperationsMonitor(ShopApi api, Session session) : ObservableObject
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DispatcherTimer? timer;
    [ObservableProperty] private ShiftSummary? summary;

    public int PendingCount => Summary?.PendingCount ?? 0;
    public event Action<int>? PendingIncreased;
    public event Action? Updated;

    public void Start()
    {
        if (timer is not null || !session.IsOperator)
            return;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += OnTick;
        timer.Start();
        _ = TryRefreshAsync();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
        if (Summary is not null)
            Summary = null;
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        if (!session.IsOperator)
            return;
        await gate.WaitAsync(ct);
        try { await FetchAsync(ct); }
        finally { gate.Release(); }
    }

    private async Task TryRefreshAsync()
    {
        if (!session.IsOperator || !await gate.WaitAsync(0))
            return;
        try { await FetchAsync(CancellationToken.None); }
        catch { }
        finally { gate.Release(); }
    }

    private async void OnTick(object? sender, EventArgs args)
    {
        try { await TryRefreshAsync(); }
        catch { }
    }

    private async Task FetchAsync(CancellationToken ct)
    {
        var next = await api.GetAsync<ShiftSummary>("operations/summary", ct);
        var previous = Summary?.PendingCount;
        Summary = next;
        OnPropertyChanged(nameof(PendingCount));
        Updated?.Invoke();
        if (previous is int old && next.PendingCount > old)
            PendingIncreased?.Invoke(next.PendingCount);
    }
}
