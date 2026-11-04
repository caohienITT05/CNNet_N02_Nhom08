using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Wpf.Services;

namespace TiemChe.Wpf.ViewModels;

public abstract partial class Screen : ObservableObject
{
    private CancellationTokenSource? lifetime;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? notice;
    [ObservableProperty] private bool isError;

    public abstract string Title { get; }
    protected virtual Task LoadAsync(CancellationToken ct) => Task.CompletedTask;

    protected CancellationToken LifetimeToken => lifetime?.Token ?? CancellationToken.None;

    internal Task ActivateAsync()
    {
        lifetime = new CancellationTokenSource();
        OnActivated();
        return RunAsync(LoadAsync);
    }

    internal void Deactivate()
    {
        OnDeactivated();
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
    }

    protected virtual void OnActivated() { }
    protected virtual void OnDeactivated() { }

    [RelayCommand] private Task ReloadAsync() => RunAsync(LoadAsync);

    protected async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (IsBusy || lifetime is null)
            return;
        var ct = lifetime.Token;
        IsBusy = true;
        Notice = null;
        IsError = false;
        try { await operation(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception) when (ct.IsCancellationRequested) { }
        catch (ApiException error) { Report(error.Message); }
        catch (InvalidOperationException error) { Report(error.Message); }
        catch (HttpRequestException) { Report("Không kết nối được máy chủ Tiệm Chè. Kiểm tra API và chứng chỉ HTTPS."); }
        catch (OperationCanceledException) { Report("Yêu cầu quá thời gian. Vui lòng thử lại."); }
        catch (JsonException) { Report("Dữ liệu từ máy chủ không hợp lệ."); }
        catch (Exception error) when (error is IOException or Win32Exception or UnauthorizedAccessException)
        { Report("Không thể đọc, lưu hoặc in tệp. Vui lòng kiểm tra thiết bị và quyền truy cập."); }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError(error.ToString());
            Report("Có lỗi xảy ra. Vui lòng thử lại.");
        }
        finally { IsBusy = false; }
    }

    protected void Report(string message, bool error = true)
    {
        IsError = error;
        Notice = message;
    }

    protected static int WholeNumber(string? text, string label, int min, int max) =>
        int.TryParse(text, out var value) && value >= min && value <= max ? value :
            throw new InvalidOperationException($"{label} phải là số từ {min} đến {max}.");

    protected static long Amount(string? text, string label) =>
        long.TryParse(text, out var value) && value >= 0 ? value :
            throw new InvalidOperationException($"{label} phải là số nguyên không âm.");
}
