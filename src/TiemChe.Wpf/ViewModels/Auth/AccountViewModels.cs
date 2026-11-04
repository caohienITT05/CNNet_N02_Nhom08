using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Auth;
using TiemChe.Wpf.Services;
using TiemChe.Wpf.ViewModels.Admin;
using TiemChe.Wpf.ViewModels.Customer;

namespace TiemChe.Wpf.ViewModels.Auth;

public sealed partial class AuthViewModel(ShopApi api, Session session, Navigator navigation) : Screen
{
    public override string Title => "Tài khoản";
    public string? InitialMessage { get; set; }
    public Func<Task>? AfterCustomerLogin { get; set; }
    [ObservableProperty] private int tabIndex;
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string resetCode = "";
    [ObservableProperty] private string newPassword = "";

    protected override Task LoadAsync(CancellationToken ct)
    {
        if (InitialMessage is not null)
            Report(InitialMessage, false);
        return Task.CompletedTask;
    }

    [RelayCommand] private Task LoginAsync() => RunAsync(async ct =>
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password))
            throw new InvalidOperationException("Vui lòng nhập email và mật khẩu.");
        var token = await api.PostAsync<TokenReply>("auth/login", new LoginRequest(Email, Password), ct);
        Password = "";
        session.Start(token.AccessToken);
        try
        {
            var profile = await api.GetAsync<UserProfile>("profile", ct);
            ct.ThrowIfCancellationRequested();
            session.Profile = profile;
        }
        catch
        {
            if (session.AccessToken == token.AccessToken)
                session.Clear();
            throw;
        }
        if (session.IsAdmin)
            await navigation.ShowAsync<DashboardViewModel>();
        else if (session.IsOperator)
            await navigation.ShowAsync<OrdersViewModel>(x => x.Operations = true);
        else if (AfterCustomerLogin is not null)
            await AfterCustomerLogin();
        else
            await navigation.ShowAsync<HomeViewModel>();
    });

    [RelayCommand] private Task RegisterAsync() => RunAsync(async ct =>
    {
        await api.PostAsync("auth/register", new RegisterRequest(Email, Password, FullName, Phone), ct);
        Password = "";
        TabIndex = 0;
        Report("Đã tạo tài khoản. Bạn có thể đăng nhập.", false);
    });

    [RelayCommand] private Task SendCodeAsync() => RunAsync(async ct =>
    {
        await api.PostAsync("auth/forgot-password", new ForgotPasswordRequest(Email), ct);
        Report("Nếu email đã đăng ký, mã đặt lại sẽ được gửi đến hộp thư.", false);
    });

    [RelayCommand] private Task ResetAsync() => RunAsync(async ct =>
    {
        await api.PostAsync("auth/reset-password", new ResetPasswordRequest(Email, ResetCode, NewPassword), ct);
        NewPassword = ResetCode = Password = "";
        TabIndex = 0;
        Report("Đã đặt lại mật khẩu. Vui lòng đăng nhập.", false);
    });

    private sealed record TokenReply(string AccessToken);
}

public sealed partial class ProfileViewModel(ShopApi api, Session session, Navigator navigation) : Screen
{
    public override string Title => "Hồ sơ của tôi";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string role = "";
    [ObservableProperty] private string currentPassword = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private string confirmPassword = "";

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var profile = await api.GetAsync<UserProfile>("profile", ct);
        Email = profile.Email;
        FullName = profile.FullName;
        Phone = profile.Phone ?? "";
        Role = Display.Label(profile.Role);
    }

    [RelayCommand] private Task SaveAsync() => RunAsync(async ct =>
    {
        var profile = await api.PutAsync<UserProfile>("profile", new ProfileRequest(FullName, Phone), ct);
        ct.ThrowIfCancellationRequested();
        session.Profile = profile;
        FullName = profile.FullName;
        Phone = profile.Phone ?? "";
        Report("Đã lưu hồ sơ.", false);
    });

    [RelayCommand] private Task ChangePasswordAsync() => RunAsync(async ct =>
    {
        if (NewPassword != ConfirmPassword)
            throw new InvalidOperationException("Hai mật khẩu mới phải giống nhau.");
        await api.PostAsync("profile/password", new ChangePasswordRequest(CurrentPassword, NewPassword), ct);
        ct.ThrowIfCancellationRequested();
        CurrentPassword = NewPassword = ConfirmPassword = "";
        session.Clear();
        await navigation.ShowAsync<AuthViewModel>(x =>
        {
            x.Email = Email;
            x.InitialMessage = "Đã đổi mật khẩu. Vui lòng đăng nhập lại.";
        });
    });
}
