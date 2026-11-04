using Microsoft.EntityFrameworkCore;
using TiemChe.Domain.Models;

namespace TiemChe.Infrastructure.Data.Configurations;

internal static class ShoppingConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Cart>(cart =>
        {
            cart.HasOne(x => x.User).WithOne().HasForeignKey<Cart>(x => x.UserId);
            cart.Property(x => x.Version).IsConcurrencyToken();
        });

        model.Entity<CartItem>(item =>
        {
            item.HasOne(x => x.ProductSize).WithMany().OnDelete(DeleteBehavior.Restrict);
            item.ToTable(table => table.HasCheckConstraint(
                "CK_CartItems_Quantity", "\"Quantity\" > 0"));
        });

        model.Entity<CartItemTopping>(link =>
        {
            link.HasKey(x => new { x.CartItemId, x.ToppingId });
            link.HasOne(x => x.Topping).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Wishlist>().HasKey(x => new { x.UserId, x.ProductId });

        model.Entity<Coupon>(coupon =>
        {
            coupon.Property(x => x.Code).HasMaxLength(40);
            coupon.HasIndex(x => x.Code).IsUnique();
            coupon.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Coupons_Discount",
                    "(\"Kind\" = 1 AND \"DiscountValue\" > 0) OR " +
                    "(\"Kind\" = 2 AND \"DiscountValue\" BETWEEN 1 AND 10000)");
                table.HasCheckConstraint("CK_Coupons_Amounts",
                    "\"MinSubtotalVnd\" >= 0 AND (\"MaxDiscountVnd\" IS NULL OR \"MaxDiscountVnd\" > 0)");
                table.HasCheckConstraint("CK_Coupons_Usage",
                    "\"UsedCount\" >= 0 AND (\"UsageLimit\" IS NULL OR \"UsageLimit\" >= \"UsedCount\")");
                table.HasCheckConstraint("CK_Coupons_Dates",
                    "\"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\"");
            });
        });

        model.Entity<Review>(review =>
        {
            review.Property(x => x.Comment).HasMaxLength(2000);
            review.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            review.HasOne(x => x.User).WithMany().OnDelete(DeleteBehavior.Restrict);
            review.HasOne(x => x.Product).WithMany().OnDelete(DeleteBehavior.Restrict);
            review.HasOne(x => x.OrderItem).WithOne().HasForeignKey<Review>(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            review.ToTable(table => table.HasCheckConstraint(
                "CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));
        });
    }
}
