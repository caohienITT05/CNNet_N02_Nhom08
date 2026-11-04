using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Wpf.Services;
using TiemChe.Wpf.ViewModels.Admin;
using TiemChe.Wpf.ViewModels.Auth;
using TiemChe.Wpf.ViewModels.Customer;

namespace TiemChe.Wpf.ViewModels;

public sealed record NavLink(string Key, string Label, string Icon);

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly Session session;
    public Navigator Navigation { get; }
    public string AccountName => session.Profile?.FullName ?? "Khách ghé thăm";
    public string AccountRole => session.Profile is null ? "Chưa đăng nhập" : Display.Label(session.Profile.Role);
    public bool SignedIn => session.SignedIn;
    public bool IsGuest => !session.SignedIn;
    [ObservableProperty] private IReadOnlyList<NavLink> links = [];

    public ShellViewModel(Navigator navigation, Session session)
    {
        Navigation = navigation;
        this.session = session;
        session.PropertyChanged += (_, _) => RefreshMenu();
        session.Expired += () => _ = Navigation.ShowAsync<AuthViewModel>(x => x.InitialMessage = "Phiên đã hết hiệu lực. Vui lòng đăng nhập lại.");
        RefreshMenu();
    }

    public Task StartAsync() => Navigation.ShowAsync<HomeViewModel>();

    private void RefreshMenu()
    {
        var menu = new List<NavLink>();
        if (!session.IsOperator)
        {
            menu.Add(new("home", "Trang chủ", "HomeOutline"));
            menu.Add(new("catalog", "Thực đơn", "SilverwareForkKnife"));
        }
        if (session.IsCustomer)
        {
            menu.Add(new("wishlist", "Món yêu thích", "HeartOutline"));
            menu.Add(new("cart", "Giỏ hàng", "CartOutline"));
            menu.Add(new("orders", "Đơn của tôi", "ReceiptTextOutline"));
            menu.Add(new("addresses", "Sổ địa chỉ", "MapMarkerOutline"));
        }
        if (session.IsAdmin)
            menu.Add(new("dashboard", "Tổng quan", "ChartBar"));
        if (session.IsOperator)
            menu.Add(new("operations", "Quản lý đơn", "ClipboardListOutline"));
        if (session.IsAdmin)
        {
            menu.Add(new("products", "Món và size", "BowlMixOutline"));
            menu.Add(new("categories", "Danh mục", "ShapeOutline"));
            menu.Add(new("toppings", "Topping", "PlusCircleOutline"));
            menu.Add(new("coupons", "Mã giảm giá", "TicketPercentOutline"));
            menu.Add(new("banners", "Banner", "ImageOutline"));
            menu.Add(new("reviews", "Đánh giá", "StarOutline"));
            menu.Add(new("customers", "Khách hàng", "AccountGroupOutline"));
            menu.Add(new("staff", "Nhân viên", "BadgeAccountOutline"));
        }
        if (session.SignedIn)
            menu.Add(new("profile", "Hồ sơ của tôi", "AccountOutline"));
        Links = menu;
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(AccountRole));
        OnPropertyChanged(nameof(SignedIn));
        OnPropertyChanged(nameof(IsGuest));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task GoAsync(string? route) => route switch
    {
        "home" => Navigation.ShowAsync<HomeViewModel>(),
        "catalog" => Navigation.ShowAsync<CatalogViewModel>(),
        "login" when IsGuest => Navigation.ShowAsync<AuthViewModel>(),
        "profile" when SignedIn => Navigation.ShowAsync<ProfileViewModel>(),
        "wishlist" when session.IsCustomer => Navigation.ShowAsync<WishlistViewModel>(),
        "cart" when session.IsCustomer => Navigation.ShowAsync<CartViewModel>(),
        "addresses" when session.IsCustomer => Navigation.ShowAsync<AddressesViewModel>(),
        "orders" when session.IsCustomer => Navigation.ShowAsync<OrdersViewModel>(),
        "operations" when session.IsOperator => Navigation.ShowAsync<OrdersViewModel>(x => x.Operations = true),
        "dashboard" when session.IsAdmin => Navigation.ShowAsync<DashboardViewModel>(),
        "products" when session.IsAdmin => Navigation.ShowAsync<ProductsAdminViewModel>(),
        "categories" when session.IsAdmin => Navigation.ShowAsync<CategoriesAdminViewModel>(),
        "toppings" when session.IsAdmin => Navigation.ShowAsync<ToppingsAdminViewModel>(),
        "coupons" when session.IsAdmin => Navigation.ShowAsync<CouponsAdminViewModel>(),
        "banners" when session.IsAdmin => Navigation.ShowAsync<BannersAdminViewModel>(),
        "reviews" when session.IsAdmin => Navigation.ShowAsync<ReviewsAdminViewModel>(),
        "customers" when session.IsAdmin => Navigation.ShowAsync<AccountsAdminViewModel>(),
        "staff" when session.IsAdmin => Navigation.ShowAsync<AccountsAdminViewModel>(x => x.Staff = true),
        _ => Task.CompletedTask
    };

    [RelayCommand]
    private Task LogoutAsync()
    {
        session.Clear();
        return Navigation.ShowAsync<HomeViewModel>();
    }
}
