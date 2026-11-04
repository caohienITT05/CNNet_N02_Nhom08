using TiemChe.Domain.Enums;

namespace TiemChe.Domain.Models;

public sealed class Role
{
    public int Id { get; set; }
    public RoleName Name { get; set; }
}

public sealed class User
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
}

public sealed class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string RecipientName { get; set; }
    public required string Phone { get; set; }
    public required string AddressLine { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class PasswordResetRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string CodeHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public int AttemptCount { get; set; }
}
