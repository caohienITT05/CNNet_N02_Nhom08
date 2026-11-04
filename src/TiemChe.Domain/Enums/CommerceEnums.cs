namespace TiemChe.Domain.Enums;

public enum RoleName
{
    Customer = 1,
    Staff = 2,
    Admin = 3
}

public enum OrderStatus
{
    Pending = 1,
    Preparing = 2,
    Delivering = 3,
    Completed = 4,
    Cancelled = 5
}

public enum OrderChannel
{
    Online = 1,
    InStore = 2
}

public enum PaymentMethod
{
    CashOnDelivery = 1,
    BankTransfer = 2
}

public enum PaymentStatus
{
    Unpaid = 1,
    Paid = 2,
    Cancelled = 3,
    Refunded = 4
}

public enum CouponKind
{
    FixedAmount = 1,
    Percentage = 2
}

public enum AvailabilityEntityType
{
    Product = 1,
    ProductSize = 2,
    Topping = 3
}
