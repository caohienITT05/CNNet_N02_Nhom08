using Microsoft.EntityFrameworkCore;
using TiemChe.Domain.Models;

namespace TiemChe.Infrastructure.Data.Configurations;

internal static class AccountConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Role>(role =>
        {
            role.Property(x => x.Name).HasConversion<string>().HasMaxLength(16);
            role.HasIndex(x => x.Name).IsUnique();
        });

        model.Entity<User>(user =>
        {
            user.Property(x => x.Email).HasMaxLength(254);
            user.Property(x => x.NormalizedEmail).HasMaxLength(254);
            user.HasIndex(x => x.NormalizedEmail).IsUnique();
            user.Property(x => x.PasswordHash).HasMaxLength(100);
            user.Property(x => x.FullName).HasMaxLength(120);
            user.Property(x => x.Phone).HasMaxLength(24);
            user.HasOne(x => x.Role).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Address>(address =>
        {
            address.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsDefault\" = 1");
            address.Property(x => x.RecipientName).HasMaxLength(120);
            address.Property(x => x.Phone).HasMaxLength(24);
            address.Property(x => x.AddressLine).HasMaxLength(500);
        });

        model.Entity<PasswordResetRequest>(reset =>
        {
            reset.Property(x => x.CodeHash).HasMaxLength(128);
            reset.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            reset.ToTable(table => table.HasCheckConstraint(
                "CK_PasswordResetRequests_Attempts", "\"AttemptCount\" >= 0"));
        });
    }
}
