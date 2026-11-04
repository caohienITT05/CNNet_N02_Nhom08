using Microsoft.EntityFrameworkCore;
using TiemChe.Domain.Models;

namespace TiemChe.Infrastructure.Data.Configurations;

internal static class CashShiftConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<CashShift>(shift =>
        {
            shift.Property(x => x.Note).HasMaxLength(500);
            shift.HasIndex(x => new { x.UserId, x.OpenedAtUtc });
            shift.HasIndex(x => x.UserId).IsUnique().HasFilter("\"ClosedAtUtc\" IS NULL");
            shift.HasOne(x => x.User).WithMany().OnDelete(DeleteBehavior.Restrict);
            shift.ToTable(table => table.HasCheckConstraint(
                "CK_CashShifts_Amounts",
                "\"OpeningCashVnd\" >= 0 AND (\"CountedCashVnd\" IS NULL OR \"CountedCashVnd\" >= 0)"));
        });
    }
}
