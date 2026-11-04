using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Interfaces;

// DbSet giữ truy vấn có kiểu và DbContext điều phối một giao dịch cho cả nghiệp vụ.
public interface ITiemCheData
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<Address> Addresses { get; }
    DbSet<PasswordResetRequest> PasswordResetRequests { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductSize> ProductSizes { get; }
    DbSet<Topping> Toppings { get; }
    DbSet<ProductTopping> ProductToppings { get; }
    DbSet<Banner> Banners { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<CartItemTopping> CartItemToppings { get; }
    DbSet<Wishlist> Wishlists { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginWriteAsync(CancellationToken cancellationToken);
}
