namespace SaigonChe.Web.ViewModels;

public sealed class StaffRowViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsLocked { get; init; }
}
