using Microsoft.EntityFrameworkCore;
using TiemChe.Domain.Models;

namespace TiemChe.Infrastructure.Data.Configurations;

internal static class CatalogConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Category>().Property(x => x.Name).HasMaxLength(120);

        model.Entity<Product>(product =>
        {
            product.Property(x => x.Name).HasMaxLength(160);
            product.Property(x => x.Description).HasMaxLength(4000);
            product.Property(x => x.ImagePath).HasMaxLength(500);
            product.HasOne(x => x.Category).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ProductSize>(size =>
        {
            size.Property(x => x.Name).HasMaxLength(40);
            size.HasIndex(x => new { x.ProductId, x.Name }).IsUnique();
            size.HasOne(x => x.Product).WithMany(x => x.Sizes).OnDelete(DeleteBehavior.Restrict);
            size.ToTable(table => table.HasCheckConstraint(
                "CK_ProductSizes_Price", "\"PriceVnd\" >= 0"));
        });

        model.Entity<Topping>(topping =>
        {
            topping.Property(x => x.Name).HasMaxLength(120);
            topping.Property(x => x.ImagePath).HasMaxLength(500);
            topping.ToTable(table => table.HasCheckConstraint(
                "CK_Toppings_Price", "\"PriceVnd\" >= 0"));
        });

        model.Entity<ProductTopping>(link =>
        {
            link.HasKey(x => new { x.ProductId, x.ToppingId });
            link.HasOne(x => x.Product).WithMany(x => x.AllowedToppings);
            link.HasOne(x => x.Topping).WithMany();
        });

        model.Entity<Banner>(banner =>
        {
            banner.Property(x => x.Title).HasMaxLength(160);
            banner.Property(x => x.ImagePath).HasMaxLength(500);
            banner.HasOne(x => x.Product).WithMany().OnDelete(DeleteBehavior.SetNull);
            banner.ToTable(table => table.HasCheckConstraint(
                "CK_Banners_Dates", "\"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\""));
        });
    }
}
