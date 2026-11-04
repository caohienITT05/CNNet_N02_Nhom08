using TiemChe.Domain.Enums;

namespace TiemChe.Contracts.Auth;

public sealed record RegisterRequest(string? Email, string? Password, string? FullName, string? Phone);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record ProfileRequest(string? FullName, string? Phone);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record ForgotPasswordRequest(string? Email);
public sealed record ResetPasswordRequest(string? Email, string? Code, string? NewPassword);
public sealed record UserProfile(int Id, string Email, string FullName, string? Phone, RoleName Role, bool IsActive);
public sealed record AccountStatusRequest(bool IsActive);
