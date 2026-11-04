using TiemChe.Domain.Enums;

namespace TiemChe.Domain.Models;

public sealed class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public required string OrderNumber { get; set; }
    public required Guid CheckoutRequestId { get; set; }
    public string? AppliedCouponCode { get; set; }
    // Lưu thông tin giao hàng tại thời điểm đặt để hóa đơn luôn đúng.
    public required string ShippingRecipientName { get; set; }
    public required string ShippingPhone { get; set; }
    public required string ShippingAddress { get; set; }
    public long SubtotalVnd { get; set; }
    public long DiscountVnd { get; set; }
    public long ShippingFeeVnd { get; set; }
    public long TotalVnd { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<OrderItem> Items { get; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; } = new List<OrderStatusHistory>();
    public Payment Payment { get; set; } = null!;
}

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductSizeId { get; set; }
    public ProductSize ProductSize { get; set; } = null!;
    public required string ProductName { get; set; }
    public required string SizeName { get; set; }
    public long UnitPriceVnd { get; set; }
    public int Quantity { get; set; }
    public long LineTotalVnd { get; set; }
    public ICollection<OrderItemTopping> Toppings { get; } = new List<OrderItemTopping>();
}

public sealed class OrderItemTopping
{
    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;
    public int ToppingId { get; set; }
    public Topping Topping { get; set; } = null!;
    public required string ToppingName { get; set; }
    public long UnitPriceVnd { get; set; }
}

public sealed class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Unpaid;
    public long AmountVnd { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public User? ConfirmedByUser { get; set; }
}

public sealed class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public OrderStatus? PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
}
