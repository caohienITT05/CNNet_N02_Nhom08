using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using TiemChe.Wpf.Services;
using TiemChe.Wpf.ViewModels;
using TiemChe.Wpf.ViewModels.Admin;
using TiemChe.Wpf.ViewModels.Auth;
using TiemChe.Wpf.ViewModels.Customer;
using TiemChe.Wpf.ViewModels.Staff;
using TiemChe.Wpf.Views;

namespace TiemChe.Wpf;

public partial class App : Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(StartupEventArgs args)
    {
        base.OnStartup(args);
        CultureInfo.CurrentCulture = Display.Vietnamese;
        CultureInfo.CurrentUICulture = Display.Vietnamese;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("vi-VN")));
        try
        {
            var address = ApiConfiguration.FromEnvironment();
            services = new ServiceCollection()
                .AddSingleton(address)
                .AddSingleton(_ => new HttpClient { BaseAddress = address.BaseAddress, Timeout = TimeSpan.FromSeconds(30) })
                .AddSingleton<Session>().AddSingleton<ShopApi>().AddSingleton<DesktopDialogs>()
                .AddSingleton<OperationsMonitor>()
                .AddSingleton<Navigator>().AddSingleton<ShellViewModel>().AddSingleton<MainWindow>()
                .AddTransient<HomeViewModel>().AddTransient<CatalogViewModel>().AddTransient<ProductViewModel>()
                .AddTransient<AuthViewModel>().AddTransient<ProfileViewModel>()
                .AddTransient<WishlistViewModel>().AddTransient<CartViewModel>().AddTransient<CheckoutViewModel>()
                .AddTransient<AddressesViewModel>().AddTransient<OrdersViewModel>().AddTransient<OrderViewModel>()
                .AddTransient<DashboardViewModel>().AddTransient<CategoriesAdminViewModel>()
                .AddTransient<ToppingsAdminViewModel>().AddTransient<CouponsAdminViewModel>()
                .AddTransient<BannersAdminViewModel>().AddTransient<ProductsAdminViewModel>()
                .AddTransient<AccountsAdminViewModel>().AddTransient<ReviewsAdminViewModel>()
                .AddTransient<ShiftSummaryViewModel>().AddTransient<StaffOrderViewModel>()
                .AddTransient<CashShiftsViewModel>()
                .AddTransient<CustomerLookupViewModel>()
                .AddTransient<AvailabilityViewModel>()
                .BuildServiceProvider();
            MainWindow = services.GetRequiredService<MainWindow>();
            MainWindow.Show();
            await services.GetRequiredService<ShellViewModel>().StartAsync();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError(error.ToString());
            MessageBox.Show(error is InvalidOperationException ? error.Message : "Không thể khởi động Tiệm Chè.",
                "Tiệm Chè", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs args)
    {
        services?.Dispose();
        base.OnExit(args);
    }
}
