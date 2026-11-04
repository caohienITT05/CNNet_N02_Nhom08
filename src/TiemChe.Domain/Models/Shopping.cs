using TiemChe.Domain.Enums;

namespace TiemChe.Domain.Models;

public sealed class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<CartItem> Items { get; } = new List<CartItem>();
}

public sealed class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public int ProductSizeId { get; set; }
    public ProductSize ProductSize { get; set; } = null!;
    public int Quantity { get; set; }
    public ICollection<CartItemTopping> Toppings { get; } = new List<CartItemTopping>();
}

public sealed class CartItemTopping
{
    public int CartItemId { get; set; }
    public CartItem CartItem { get; set; } = null!;
    public int ToppingId { get; set; }
    public Topping Topping { get; set; } = null!;
}

public sealed class Wishlist
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Coupon
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public CouponKind Kind { get; set; }
    // Lưu số đồng với mã cố định hoặc số điểm cơ bản với mã phần trăm (100 điểm = 1%).
    public long DiscountValue { get; set; }
    public long? MaxDiscountVnd { get; set; }
    public long MinSubtotalVnd { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public int UsedCount { get; set; }
    public int? UsageLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Review
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;
    public int Rating { get; set; }
    public required string Comment { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
