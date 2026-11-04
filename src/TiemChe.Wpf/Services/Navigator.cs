using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using TiemChe.Wpf.ViewModels;

namespace TiemChe.Wpf.Services;

public sealed partial class Navigator(IServiceProvider services) : ObservableObject
{
    [ObservableProperty] private Screen? current;

    public async Task ShowAsync<T>(Action<T>? initialize = null) where T : Screen
    {
        var next = services.GetRequiredService<T>();
        initialize?.Invoke(next);
        Current?.Deactivate();
        Current = next;
        await next.ActivateAsync();
    }
}
