using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class OrderService(ITiemCheData data, CartService carts, CheckoutSettings settings, TimeProvider time)
{
    public async Task<CheckoutQuote> QuoteAsync(int userId, CheckoutRequest request, CancellationToken ct)
    {
        ValidatePayment(request.PaymentMethod);
        var cart = await carts.RequireAsync(userId, request.CartVersion, ct);
        return (await CalculateAsync(cart, request.CouponCode, ct)).Quote;
    }

    public async Task<OrderDetails> CheckoutAsync(int userId, CheckoutRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty)
            throw Check.Invalid("Mã yêu cầu đặt hàng không hợp lệ.");
        await using var transaction = await data.BeginWriteAsync(ct);
        var existing = await DetailedOrders().SingleOrDefaultAsync(
            x => x.UserId == userId && x.CheckoutRequestId == request.RequestId, ct);
        if (existing is not null)
        {
            await transaction.CommitAsync(ct);
            return Describe(existing);
        }

        ValidatePayment(request.PaymentMethod);
        var address = await data.Addresses.SingleOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == userId, ct)
            ?? throw Check.Invalid("Vui lòng chọn địa chỉ giao hàng hợp lệ.");
        var cart = await carts.RequireAsync(userId, request.CartVersion, ct);
        var (quote, coupon) = await CalculateAsync(cart, request.CouponCode, ct);
        if (request.ExpectedTotalVnd is null)
            throw Check.Invalid("Vui lòng xác nhận tổng tiền trước khi đặt đơn.");
        if (request.ExpectedTotalVnd != quote.TotalVnd)
            throw Check.Conflict("Tổng tiền đã thay đổi. Vui lòng tính lại và xác nhận đơn.");
        var now = time.GetUtcNow().UtcDateTime;
        if (coupon is not null)
        {
            var reserved = await EligibleCoupons(coupon.Code, quote.SubtotalVnd, now)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedCount, x => x.UsedCount + 1), ct);
            if (reserved == 0)
                throw Check.Conflict("Mã giảm giá vừa thay đổi. Vui lòng tính lại đơn.");
        }

        var order = new Order
        {
            UserId = userId,
            CouponId = coupon?.Id,
            CheckoutRequestId = request.RequestId,
            OrderNumber = $"TC{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            AppliedCouponCode = quote.CouponCode,
            ShippingRecipientName = address.RecipientName,
            ShippingPhone = address.Phone,
            ShippingAddress = address.AddressLine,
            SubtotalVnd = quote.SubtotalVnd,
            DiscountVnd = quote.DiscountVnd,
            ShippingFeeVnd = quote.ShippingFeeVnd,
            TotalVnd = quote.TotalVnd,
            PlacedAtUtc = now,
            Payment = new Payment { Method = request.PaymentMethod, AmountVnd = quote.TotalVnd }
        };
        order.StatusHistory.Add(new OrderStatusHistory { NewStatus = order.Status, ChangedAtUtc = now, ChangedByUserId = userId });
        foreach (var item in cart.Items.OrderBy(x => x.Id))
        {
            var line = new OrderItem
            {
                ProductSize = item.ProductSize,
                ProductSizeId = item.ProductSizeId,
                ProductName = item.ProductSize.Product.Name,
                SizeName = item.ProductSize.Name,
                UnitPriceVnd = item.ProductSize.PriceVnd,
                Quantity = item.Quantity,
                LineTotalVnd = CartService.LineTotal(item)
            };
            foreach (var topping in item.Toppings)
                line.Toppings.Add(new OrderItemTopping
                {
                    ToppingId = topping.ToppingId,
                    ToppingName = topping.Topping.Name,
                    UnitPriceVnd = topping.Topping.PriceVnd
                });
            order.Items.Add(line);
        }
        data.Orders.Add(order);
        data.CartItems.RemoveRange(cart.Items);
        cart.Version = Guid.NewGuid();
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(order);
    }

    public async Task<PageResult<OrderSummary>> ListAsync(int? userId, OrderStatus? status, string? search,
        int? page, int? pageSize, CancellationToken ct)
    {
        var pagination = Check.Page(page, pageSize);
        var term = Check.Optional(search, 120, "từ khóa");
        var query = data.Orders.AsNoTracking().Where(x => userId == null || x.UserId == userId);
        if (status.HasValue)
            query = query.Where(x => x.Status == status);
        if (term is not null)
        {
            var code = term.ToUpperInvariant();
            var recipient = VietnameseSearch.Normalize(term);
            query = query.Where(x => x.OrderNumber.Contains(code) || VietnameseSearch.Contains(x.ShippingRecipientName, recipient)
                || x.ShippingPhone.Contains(term));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.PlacedAtUtc).ThenByDescending(x => x.Id)
            .Skip((pagination.Page - 1) * pagination.Size).Take(pagination.Size)
            .Select(x => new OrderSummary(x.Id, x.OrderNumber, x.Status, x.Payment.Status, x.TotalVnd,
                x.PlacedAtUtc, x.ShippingRecipientName)).ToListAsync(ct);
        return new PageResult<OrderSummary>(rows, count, pagination.Page, pagination.Size);
    }

    public async Task<OrderDetails> GetAsync(int id, int? userId, CancellationToken ct) =>
        Describe(await FindAsync(id, userId, ct));

    public Task<OrderDetails> CancelAsync(int id, int userId, VersionRequest request, CancellationToken ct) =>
        TransitionAsync(id, userId, false, new OrderStatusRequest(request.Version, OrderStatus.Cancelled, null), ct);

    public Task<OrderDetails> UpdateStatusAsync(int id, int actorId, OrderStatusRequest request, CancellationToken ct) =>
        TransitionAsync(id, actorId, true, request, ct);

    private async Task<OrderDetails> TransitionAsync(int id, int actorId, bool staff, OrderStatusRequest request,
        CancellationToken ct)
    {
        var note = Check.Optional(request.Note, 500, "ghi chú");
        await using var transaction = await data.BeginWriteAsync(ct);
        var order = await FindAsync(id, staff ? null : actorId, ct);
        ValidateVersion(order, request.Version);
        var now = time.GetUtcNow().UtcDateTime;
        var allowed = (order.Status, request.Status) switch
        {
            (OrderStatus.Pending, OrderStatus.Cancelled) => order.Payment.Status == PaymentStatus.Unpaid,
            (OrderStatus.Preparing, OrderStatus.Cancelled) => staff && order.Payment.Status == PaymentStatus.Unpaid,
            (OrderStatus.Pending, OrderStatus.Preparing) => staff,
            (OrderStatus.Preparing, OrderStatus.Delivering) => staff &&
                (order.Payment.Method == PaymentMethod.CashOnDelivery || order.Payment.Status == PaymentStatus.Paid),
            (OrderStatus.Delivering, OrderStatus.Completed) => staff,
            _ => false
        };
        if (!allowed)
            throw Check.Conflict("Không thể đổi sang trạng thái này. Vui lòng kiểm tra đơn và thanh toán.");
        if (request.Status == OrderStatus.Cancelled)
        {
            order.Payment.Status = PaymentStatus.Cancelled;
            if (order.CouponId is int couponId)
                await data.Coupons.Where(x => x.Id == couponId && x.UsedCount > 0)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedCount, x => x.UsedCount - 1), ct);
        }
        if (request.Status == OrderStatus.Completed)
        {
            order.CompletedAtUtc = now;
            if (order.Payment.Method == PaymentMethod.CashOnDelivery)
                MarkPaid(order.Payment, actorId, now);
        }
        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = order.Status,
            NewStatus = request.Status,
            ChangedAtUtc = now,
            ChangedByUserId = actorId,
            Note = note
        });
        order.Status = request.Status;
        order.Version = Guid.NewGuid();
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(order);
    }

    public async Task<OrderDetails> ConfirmTransferAsync(int id, int actorId, VersionRequest request, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var order = await FindAsync(id, null, ct);
        ValidateVersion(order, request.Version);
        if (order.Payment.Method != PaymentMethod.BankTransfer || order.Payment.Status != PaymentStatus.Unpaid
            || order.Status is not (OrderStatus.Pending or OrderStatus.Preparing))
            throw Check.Conflict("Đơn này không có khoản chuyển khoản đang chờ xác nhận.");
        MarkPaid(order.Payment, actorId, time.GetUtcNow().UtcDateTime);
        order.Version = Guid.NewGuid();
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(order);
    }

    private async Task<(CheckoutQuote Quote, Coupon? Coupon)> CalculateAsync(Cart cart, string? rawCode,
        CancellationToken ct)
    {
        if (cart.Items.Count == 0)
            throw Check.Invalid("Giỏ hàng đang trống.");
        if (cart.Items.Any(x => !CartService.IsAvailable(x)))
            throw Check.Conflict("Giỏ có món hoặc topping đã ngừng bán. Vui lòng kiểm tra lại.");
        var subtotal = cart.Items.Sum(CartService.LineTotal);
        var code = Check.Optional(rawCode, 40, "mã giảm giá")?.ToUpperInvariant();
        Coupon? coupon = null;
        long discount = 0;
        if (code is not null)
        {
            coupon = await EligibleCoupons(code, subtotal, time.GetUtcNow().UtcDateTime)
                .AsNoTracking().SingleOrDefaultAsync(ct)
                ?? throw Check.Invalid("Mã giảm giá đã hết hạn, hết lượt hoặc đơn chưa đạt điều kiện.");
            discount = coupon.Kind == CouponKind.FixedAmount
                ? coupon.DiscountValue
                : (long)decimal.Round(subtotal * (decimal)coupon.DiscountValue / 10_000, 0, MidpointRounding.AwayFromZero);
            discount = Math.Min(subtotal, Math.Min(discount, coupon.MaxDiscountVnd ?? long.MaxValue));
        }
        var shipping = Check.Price(settings.ShippingFeeVnd);
        return (new CheckoutQuote(subtotal, discount, shipping, checked(subtotal - discount + shipping), code), coupon);
    }

    private IQueryable<Coupon> EligibleCoupons(string code, long subtotal, DateTime now) =>
        data.Coupons.Where(x => x.Code == code && x.IsActive && x.StartsAtUtc <= now
            && (x.EndsAtUtc == null || x.EndsAtUtc > now) && x.MinSubtotalVnd <= subtotal
            && (x.UsageLimit == null || x.UsedCount < x.UsageLimit));

    private IQueryable<Order> DetailedOrders() => data.Orders
        .Include(x => x.Payment).Include(x => x.StatusHistory)
        .Include(x => x.Items).ThenInclude(x => x.ProductSize)
        .Include(x => x.Items).ThenInclude(x => x.Toppings).AsSplitQuery();

    private async Task<Order> FindAsync(int id, int? userId, CancellationToken ct) =>
        await DetailedOrders().SingleOrDefaultAsync(x => x.Id == id && (userId == null || x.UserId == userId), ct)
        ?? throw Check.Missing("Không tìm thấy đơn hàng.");

    private void ValidatePayment(PaymentMethod method)
    {
        if (!Enum.IsDefined(method))
            throw Check.Invalid("Phương thức thanh toán không hợp lệ.");
        if (method == PaymentMethod.BankTransfer && !settings.AllowsBankTransfer)
            throw Check.Invalid("Cửa hàng chưa cấu hình tài khoản nhận chuyển khoản.");
    }

    private static void ValidateVersion(Order order, Guid version)
    {
        if (order.Version != version)
            throw Check.Conflict("Đơn đã được cập nhật. Vui lòng tải lại.");
    }

    private static void MarkPaid(Payment payment, int actor, DateTime now)
    {
        payment.Status = PaymentStatus.Paid;
        payment.ConfirmedByUserId = actor;
        payment.ConfirmedAtUtc = now;
    }

    private OrderDetails Describe(Order order)
    {
        var payment = order.Payment;
        BankTransferDto? transfer = payment.Method == PaymentMethod.BankTransfer && settings.AllowsBankTransfer
            ? new BankTransferDto(settings.BankName!, settings.AccountNumber!, settings.AccountName!, order.OrderNumber)
            : null;
        return new OrderDetails(order.Id, order.OrderNumber, order.Version, order.Status, order.PlacedAtUtc,
            order.ShippingRecipientName, order.ShippingPhone, order.ShippingAddress,
            new CheckoutQuote(order.SubtotalVnd, order.DiscountVnd, order.ShippingFeeVnd, order.TotalVnd, order.AppliedCouponCode),
            new PaymentDto(payment.Method, payment.Status, payment.AmountVnd, payment.ConfirmedAtUtc, transfer),
            order.Items.OrderBy(x => x.Id).Select(x => new OrderItemDto(x.Id, x.ProductSize.ProductId, x.ProductName,
                x.SizeName, x.UnitPriceVnd, x.Quantity, x.LineTotalVnd,
                x.Toppings.OrderBy(t => t.ToppingId).Select(t => new OrderToppingDto(t.ToppingName, t.UnitPriceVnd)).ToArray())).ToArray(),
            order.StatusHistory.OrderBy(x => x.ChangedAtUtc).ThenBy(x => x.Id)
                .Select(x => new OrderHistoryDto(x.PreviousStatus, x.NewStatus, x.ChangedAtUtc, x.Note)).ToArray());
    }
}
