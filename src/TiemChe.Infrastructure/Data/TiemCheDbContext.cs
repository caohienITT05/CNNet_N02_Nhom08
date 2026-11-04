using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Services;
using TiemChe.Domain.Models;
using TiemChe.Infrastructure.Data.Configurations;
using TiemChe.Infrastructure.Data.Seed;

namespace TiemChe.Infrastructure.Data;

public sealed class TiemCheDbContext(DbContextOptions<TiemCheDbContext> options) : DbContext(options), ITiemCheData
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductSize> ProductSizes => Set<ProductSize>();
    public DbSet<Topping> Toppings => Set<Topping>();
    public DbSet<ProductTopping> ProductToppings => Set<ProductTopping>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartItemTopping> CartItemToppings => Set<CartItemTopping>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemTopping> OrderItemToppings => Set<OrderItemTopping>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public Task<IDbContextTransaction> BeginWriteAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(VietnameseSearch).GetMethod(nameof(VietnameseSearch.Contains))!)
            .HasName(VietnameseSearch.SqlName);
        AccountConfiguration.Configure(modelBuilder);
        CatalogConfiguration.Configure(modelBuilder);
        ShoppingConfiguration.Configure(modelBuilder);
        OrderConfiguration.Configure(modelBuilder);
        InitialData.Configure(modelBuilder);
    }

    // SQLite không lưu DateTimeKind. Khôi phục UTC khi đọc để hiển thị đúng giờ Việt Nam.
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
