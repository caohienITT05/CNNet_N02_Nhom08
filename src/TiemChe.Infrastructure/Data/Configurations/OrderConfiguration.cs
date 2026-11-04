using Microsoft.EntityFrameworkCore;
using TiemChe.Domain.Models;

namespace TiemChe.Infrastructure.Data.Configurations;

internal static class OrderConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Order>(order =>
        {
            order.Property(x => x.OrderNumber).HasMaxLength(32);
            order.Property(x => x.AppliedCouponCode).HasMaxLength(40);
            order.Property(x => x.ShippingRecipientName).HasMaxLength(120);
            order.Property(x => x.ShippingPhone).HasMaxLength(24);
            order.Property(x => x.ShippingAddress).HasMaxLength(500);
            order.Property(x => x.Version).IsConcurrencyToken();
            order.HasIndex(x => x.OrderNumber).IsUnique();
            order.HasIndex(x => new { x.UserId, x.CheckoutRequestId }).IsUnique();
            order.HasIndex(x => new { x.UserId, x.PlacedAtUtc });
            order.HasIndex(x => new { x.Status, x.PlacedAtUtc });
            order.HasIndex(x => x.CompletedAtUtc);
            order.HasOne(x => x.User).WithMany().OnDelete(DeleteBehavior.Restrict);
            order.HasOne(x => x.Coupon).WithMany().OnDelete(DeleteBehavior.Restrict);
            order.ToTable(table => table.HasCheckConstraint("CK_Orders_Amounts",
                "\"SubtotalVnd\" >= 0 AND \"DiscountVnd\" BETWEEN 0 AND \"SubtotalVnd\" " +
                "AND \"ShippingFeeVnd\" >= 0 AND \"TotalVnd\" = \"SubtotalVnd\" - \"DiscountVnd\" + \"ShippingFeeVnd\""));
        });

        model.Entity<OrderItem>(item =>
        {
            item.Property(x => x.ProductName).HasMaxLength(160);
            item.Property(x => x.SizeName).HasMaxLength(40);
            item.HasOne(x => x.Order).WithMany(x => x.Items).OnDelete(DeleteBehavior.Restrict);
            item.HasOne(x => x.ProductSize).WithMany().OnDelete(DeleteBehavior.Restrict);
            item.ToTable(table => table.HasCheckConstraint(
                "CK_OrderItems_Amounts", "\"Quantity\" > 0 AND \"UnitPriceVnd\" >= 0 AND \"LineTotalVnd\" >= 0"));
        });

        model.Entity<OrderItemTopping>(link =>
        {
            link.HasKey(x => new { x.OrderItemId, x.ToppingId });
            link.Property(x => x.ToppingName).HasMaxLength(120);
            link.HasOne(x => x.OrderItem).WithMany(x => x.Toppings).OnDelete(DeleteBehavior.Restrict);
            link.HasOne(x => x.Topping).WithMany().OnDelete(DeleteBehavior.Restrict);
            link.ToTable(table => table.HasCheckConstraint(
                "CK_OrderItemToppings_Price", "\"UnitPriceVnd\" >= 0"));
        });

        model.Entity<Payment>(payment =>
        {
            payment.HasOne(x => x.Order).WithOne(x => x.Payment)
                .HasForeignKey<Payment>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            payment.HasOne(x => x.ConfirmedByUser).WithMany().OnDelete(DeleteBehavior.Restrict);
            payment.ToTable(table => table.HasCheckConstraint(
                "CK_Payments_Amount", "\"AmountVnd\" >= 0"));
        });

        model.Entity<OrderStatusHistory>(history =>
        {
            history.Property(x => x.Note).HasMaxLength(500);
            history.HasIndex(x => new { x.OrderId, x.ChangedAtUtc });
            history.HasOne(x => x.Order).WithMany(x => x.StatusHistory).OnDelete(DeleteBehavior.Restrict);
            history.HasOne(x => x.ChangedByUser).WithMany().OnDelete(DeleteBehavior.Restrict);
        });
    }
}
