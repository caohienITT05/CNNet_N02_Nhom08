using Microsoft.EntityFrameworkCore;
using TiemChe.Domain.Models;

namespace TiemChe.Infrastructure.Data.Configurations;

internal static class AvailabilityLogConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<AvailabilityLog>(log =>
        {
            log.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(16);
            log.Property(x => x.EntityName).HasMaxLength(200);
            log.HasIndex(x => x.ChangedAtUtc);
            log.HasOne(x => x.ChangedByUser).WithMany().OnDelete(DeleteBehavior.Restrict);
        });
    }
}
