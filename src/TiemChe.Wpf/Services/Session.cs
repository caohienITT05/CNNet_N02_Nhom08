using CommunityToolkit.Mvvm.ComponentModel;
using TiemChe.Contracts.Auth;
using TiemChe.Domain.Enums;

namespace TiemChe.Wpf.Services;

public sealed partial class Session : ObservableObject
{
    [ObservableProperty] private UserProfile? profile;
    internal string? AccessToken { get; private set; }
    public bool IsCustomer => Profile?.Role == RoleName.Customer;
    public bool IsOperator => Profile?.Role is RoleName.Staff or RoleName.Admin;
    public bool IsAdmin => Profile?.Role == RoleName.Admin;
    public bool SignedIn => Profile is not null;
    public event Action? Expired;

    public void Start(string token) => AccessToken = token;

    public void Clear(bool expired = false)
    {
        AccessToken = null;
        Profile = null;
        if (expired)
            Expired?.Invoke();
    }
}
