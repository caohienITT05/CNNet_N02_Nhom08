using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Operations;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class OrderService(ITiemCheData data, CartService carts, CheckoutSettings settings, TimeProvider time,
    IPasswordHasher passwords)
{
    private const string WalkInEmail = "walkin@tiemche.local";

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
            return Describe(existing, false);
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
        return Describe(order, false);
    }

    public async Task<CheckoutQuote> StaffQuoteAsync(StaffOrderRequest request, CancellationToken ct)
    {
        if (request.Items is not { Count: > 0 } items)
            throw Check.Invalid("Vui lòng chọn ít nhất một món.");
        if (items.Count > 100)
            throw Check.Invalid("Một đơn chỉ có tối đa 100 dòng món.");
        var orderItems = await BuildStaffItemsAsync(items, ct);
        return (await CalculateQuoteAsync(orderItems.Sum(x => x.LineTotalVnd), request.CouponCode, 0, ct)).Quote;
    }

    public async Task<OrderDetails> CreateStaffOrderAsync(int actorId, StaffOrderRequest request, CancellationToken ct)
    {
        var requestId = request.RequestId ?? Guid.NewGuid();
        if (requestId == Guid.Empty)
            throw Check.Invalid("Mã yêu cầu đặt hàng không hợp lệ.");
        await using var transaction = await data.BeginWriteAsync(ct);
        var existing = await DetailedOrders().SingleOrDefaultAsync(
            x => x.Channel == OrderChannel.InStore && x.CreatedByStaffId == actorId
                && x.CheckoutRequestId == requestId, ct);
        if (existing is not null)
        {
            await transaction.CommitAsync(ct);
            return Describe(existing, true);
        }

        ValidatePayment(request.PaymentMethod);
        if (request.CompleteNow && request.PaymentMethod == PaymentMethod.BankTransfer && !request.PaymentReceived)
            throw Check.Invalid("Vui lòng xác nhận đã nhận chuyển khoản trước khi hoàn thành đơn.");
        if (request.Items is not { Count: > 0 } items)
            throw Check.Invalid("Vui lòng chọn ít nhất một món.");
        if (items.Count > 100)
            throw Check.Invalid("Một đơn chỉ có tối đa 100 dòng món.");

        var note = Check.Optional(request.Note, 500, "ghi chú");
        var orderItems = await BuildStaffItemsAsync(items, ct);
        var (quote, coupon) = await CalculateQuoteAsync(orderItems.Sum(x => x.LineTotalVnd),
            request.CouponCode, 0, ct);
        if (request.ExpectedTotalVnd.HasValue && request.ExpectedTotalVnd.Value != quote.TotalVnd)
            throw Check.Conflict("Tổng tiền đã thay đổi. Vui lòng tính lại và xác nhận đơn.");

        var customer = request.CustomerId.HasValue
            ? await data.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == request.CustomerId
                && x.Role.Name == RoleName.Customer && x.IsActive && x.NormalizedEmail != WalkInEmail.ToUpperInvariant(), ct)
                ?? throw Check.Invalid("Khách hàng được chọn không tồn tại hoặc đã ngừng hoạt động.")
            : await GetOrCreateWalkInCustomerAsync(ct);
        var recipient = Check.Optional(request.RecipientName, 120, "tên khách")
            ?? (request.CustomerId.HasValue ? customer.FullName : "Khách lẻ");
        var phone = Check.Optional(request.Phone, 24, "số điện thoại")
            ?? customer.Phone ?? "Không có";
        var now = time.GetUtcNow().UtcDateTime;
        if (coupon is not null)
        {
            var reserved = await EligibleCoupons(coupon.Code, quote.SubtotalVnd, now)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedCount, x => x.UsedCount + 1), ct);
            if (reserved == 0)
                throw Check.Conflict("Mã giảm giá vừa thay đổi. Vui lòng tính lại đơn.");
        }

        var status = request.CompleteNow ? OrderStatus.Completed : OrderStatus.Preparing;
        var order = new Order
        {
            UserId = customer.Id,
            Channel = OrderChannel.InStore,
            CreatedByStaffId = actorId,
            CheckoutRequestId = requestId,
            OrderNumber = $"TC{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            CouponId = coupon?.Id,
            AppliedCouponCode = quote.CouponCode,
            ShippingRecipientName = recipient,
            ShippingPhone = phone,
            ShippingAddress = "Tại quầy",
            SubtotalVnd = quote.SubtotalVnd,
            DiscountVnd = quote.DiscountVnd,
            ShippingFeeVnd = 0,
            TotalVnd = quote.TotalVnd,
            Status = status,
            PlacedAtUtc = now,
            CompletedAtUtc = request.CompleteNow ? now : null,
            Payment = new Payment
            {
                Method = request.PaymentMethod,
                AmountVnd = quote.TotalVnd,
                Status = request.CompleteNow ? PaymentStatus.Paid : PaymentStatus.Unpaid,
                ConfirmedAtUtc = request.CompleteNow ? now : null,
                ConfirmedByUserId = request.CompleteNow ? actorId : null
            }
        };
        foreach (var item in orderItems)
            order.Items.Add(item);
        order.StatusHistory.Add(new OrderStatusHistory
        {
            NewStatus = status,
            ChangedAtUtc = now,
            ChangedByUserId = actorId,
            Note = request.CompleteNow ? "Bán tại quầy" : note ?? "Bán tại quầy"
        });
        data.Orders.Add(order);
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(await FindAsync(order.Id, null, ct), true);
    }

    public async Task<PageResult<OrderSummary>> ListAsync(int? userId, OrderStatus? status, string? search,
        int? page, int? pageSize, CancellationToken ct, DateOnly? from = null, DateOnly? to = null,
        PaymentMethod? paymentMethod = null, PaymentStatus? paymentStatus = null, bool oldestFirst = false,
        OrderChannel? channel = null)
    {
        var pagination = Check.Page(page, pageSize);
        var term = Check.Optional(search, 120, "từ khóa");
        if ((from.HasValue && from < new DateOnly(2000, 1, 1)) || to >= DateOnly.MaxValue
            || (from.HasValue && to.HasValue && from > to))
            throw Check.Invalid("Khoảng ngày không hợp lệ.");
        if (paymentMethod.HasValue && !Enum.IsDefined(paymentMethod.Value))
            throw Check.Invalid("Phương thức thanh toán không hợp lệ.");
        if (paymentStatus.HasValue && !Enum.IsDefined(paymentStatus.Value))
            throw Check.Invalid("Trạng thái thanh toán không hợp lệ.");
        if (channel.HasValue && !Enum.IsDefined(channel.Value))
            throw Check.Invalid("Kênh đơn hàng không hợp lệ.");
        var query = data.Orders.AsNoTracking().Where(x => userId == null || x.UserId == userId);
        if (status.HasValue)
            query = query.Where(x => x.Status == status);
        if (channel.HasValue)
            query = query.Where(x => x.Channel == channel);
        if (from.HasValue)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
            query = query.Where(x => x.PlacedAtUtc >= start);
        }
        if (to.HasValue)
        {
            var end = to.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(17);
            query = query.Where(x => x.PlacedAtUtc < end);
        }
        if (paymentMethod.HasValue)
            query = query.Where(x => x.Payment.Method == paymentMethod.Value);
        if (paymentStatus.HasValue)
            query = query.Where(x => x.Payment.Status == paymentStatus.Value);
        if (term is not null)
        {
            var code = term.ToUpperInvariant();
            var recipient = VietnameseSearch.Normalize(term);
            query = query.Where(x => x.OrderNumber.Contains(code) || VietnameseSearch.Contains(x.ShippingRecipientName, recipient)
                || x.ShippingPhone.Contains(term));
        }
        var count = await query.CountAsync(ct);
        var ordered = oldestFirst
            ? query.OrderBy(x => x.PlacedAtUtc).ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.PlacedAtUtc).ThenByDescending(x => x.Id);
        var rows = await ordered
            .Skip((pagination.Page - 1) * pagination.Size).Take(pagination.Size)
            .Select(x => new OrderSummary(x.Id, x.OrderNumber, x.Status, x.Payment.Status, x.TotalVnd,
                x.PlacedAtUtc, x.ShippingRecipientName, x.Channel)).ToListAsync(ct);
        return new PageResult<OrderSummary>(rows, count, pagination.Page, pagination.Size);
    }

    public async Task<PageResult<OrderSummary>> CustomerOrdersAsync(int customerId, int? page, int? pageSize,
        CancellationToken ct)
    {
        if (!await data.Users.AnyAsync(x => x.Id == customerId && x.Role.Name == RoleName.Customer
                && x.NormalizedEmail != WalkInEmail.ToUpperInvariant(), ct))
            throw Check.Missing("Không tìm thấy khách hàng.");
        return await ListAsync(customerId, null, null, page, pageSize, ct);
    }

    public async Task<OrderDetails> GetAsync(int id, int? userId, CancellationToken ct) =>
        Describe(await FindAsync(id, userId, ct), userId is null);

    public Task<OrderDetails> CancelAsync(int id, int userId, VersionRequest request, CancellationToken ct) =>
        TransitionAsync(id, userId, false, new OrderStatusRequest(request.Version, OrderStatus.Cancelled, null), ct);

    public Task<OrderDetails> UpdateStatusAsync(int id, int actorId, OrderStatusRequest request, CancellationToken ct) =>
        TransitionAsync(id, actorId, true, request, ct);

    private async Task<OrderDetails> TransitionAsync(int id, int actorId, bool staff, OrderStatusRequest request,
        CancellationToken ct)
    {
        var note = Check.Optional(request.Note, 500, "ghi chú");
        if (staff && request.Status == OrderStatus.Cancelled && (note is null || note.Length < 5))
            throw Check.Invalid("Vui lòng nhập lý do hủy đơn.");
        await using var transaction = await data.BeginWriteAsync(ct);
        var order = await FindAsync(id, staff ? null : actorId, ct);
        ValidateVersion(order, request.Version);
        var now = time.GetUtcNow().UtcDateTime;
        var allowed = (order.Status, request.Status) switch
        {
            (OrderStatus.Pending, OrderStatus.Cancelled) => staff
                ? order.Payment.Status is PaymentStatus.Unpaid or PaymentStatus.Paid
                : order.Payment.Status == PaymentStatus.Unpaid,
            (OrderStatus.Preparing, OrderStatus.Cancelled) => staff
                && order.Payment.Status is PaymentStatus.Unpaid or PaymentStatus.Paid,
            (OrderStatus.Pending, OrderStatus.Preparing) => staff,
            (OrderStatus.Preparing, OrderStatus.Completed) => staff && order.Channel == OrderChannel.InStore
                && (order.Payment.Method == PaymentMethod.CashOnDelivery || order.Payment.Status == PaymentStatus.Paid),
            (OrderStatus.Preparing, OrderStatus.Delivering) => staff &&
                order.Channel == OrderChannel.Online &&
                (order.Payment.Method == PaymentMethod.CashOnDelivery || order.Payment.Status == PaymentStatus.Paid),
            (OrderStatus.Delivering, OrderStatus.Completed) => staff && order.Channel == OrderChannel.Online,
            _ => false
        };
        if (!allowed)
            throw Check.Conflict("Không thể đổi sang trạng thái này. Vui lòng kiểm tra đơn và thanh toán.");
        if (request.Status == OrderStatus.Cancelled)
        {
            if (order.Payment.Status == PaymentStatus.Paid)
            {
                order.Payment.Status = PaymentStatus.Refunded;
                order.Payment.RefundedAtUtc = now;
                order.Payment.RefundedByUserId = actorId;
            }
            else
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
        return Describe(await FindAsync(id, staff ? null : actorId, ct), staff);
    }

    public async Task<OrderDetails> ConfirmTransferAsync(int id, int actorId, ConfirmTransferRequest request, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var order = await FindAsync(id, null, ct);
        ValidateVersion(order, request.Version);
        if (order.Payment.Method != PaymentMethod.BankTransfer || order.Payment.Status != PaymentStatus.Unpaid
            || order.Status is not (OrderStatus.Pending or OrderStatus.Preparing))
            throw Check.Conflict("Đơn này không có khoản chuyển khoản đang chờ xác nhận.");
        if (request.CompleteNow && (order.Channel != OrderChannel.InStore || order.Status != OrderStatus.Preparing))
            throw Check.Conflict("Chỉ có thể hoàn thành ngay đơn tại quầy đang chuẩn bị.");
        var now = time.GetUtcNow().UtcDateTime;
        MarkPaid(order.Payment, actorId, now);
        if (request.CompleteNow)
        {
            order.StatusHistory.Add(new OrderStatusHistory
            {
                PreviousStatus = order.Status,
                NewStatus = OrderStatus.Completed,
                ChangedAtUtc = now,
                ChangedByUserId = actorId,
                Note = "Đã xác nhận chuyển khoản tại quầy"
            });
            order.Status = OrderStatus.Completed;
            order.CompletedAtUtc = now;
        }
        order.Version = Guid.NewGuid();
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(order, true);
    }

    public async Task<OrderDetails> AddInternalNoteAsync(int orderId, int actorId, OrderNoteRequest request,
        CancellationToken ct)
    {
        var text = Check.Text(request.Text, 500, "ghi chú nội bộ");
        await using var transaction = await data.BeginWriteAsync(ct);
        var order = await FindAsync(orderId, null, ct);
        data.OrderNotes.Add(new OrderNote
        {
            OrderId = order.Id,
            Text = text,
            CreatedByUserId = actorId,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        });
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(await FindAsync(orderId, null, ct), true);
    }

    private async Task<List<OrderItem>> BuildStaffItemsAsync(IReadOnlyList<StaffOrderLineRequest> items,
        CancellationToken ct)
    {
        var sizeIds = items.Select(x => x.SizeId).Distinct().ToArray();
        var sizes = await data.ProductSizes.AsNoTracking()
            .Include(x => x.Product).ThenInclude(x => x.Category)
            .Include(x => x.Product).ThenInclude(x => x.AllowedToppings).ThenInclude(x => x.Topping)
            .AsSplitQuery().Where(x => sizeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var orderItems = new List<OrderItem>(items.Count);
        foreach (var requestItem in items)
        {
            if (requestItem.Quantity is < 1 or > 99)
                throw Check.Invalid("Số lượng mỗi món phải từ 1 đến 99.");
            if (!sizes.TryGetValue(requestItem.SizeId, out var size))
                throw Check.Invalid("Có món hoặc size không còn bán. Vui lòng chọn lại.");
            var toppingIds = requestItem.ToppingIds?.Distinct().Order().ToArray() ?? [];
            if (toppingIds.Length > 30)
                throw Check.Invalid("Mỗi món có thể chọn tối đa 30 loại topping.");
            if (!size.IsActive || !size.Product.IsActive || !size.Product.Category.IsActive)
                throw Check.Invalid($"{size.Product.Name} ({size.Name}) hiện không bán.");
            var toppings = toppingIds.Select(id => size.Product.AllowedToppings.SingleOrDefault(x =>
                    x.ToppingId == id && x.Topping.IsActive)?.Topping
                ?? throw Check.Invalid($"{size.Product.Name} ({size.Name}) có topping không còn bán.")).ToArray();
            var line = new OrderItem
            {
                ProductSizeId = size.Id,
                ProductName = size.Product.Name,
                SizeName = size.Name,
                UnitPriceVnd = size.PriceVnd,
                Quantity = requestItem.Quantity,
                LineTotalVnd = checked((size.PriceVnd + toppings.Sum(x => x.PriceVnd)) * requestItem.Quantity)
            };
            foreach (var topping in toppings)
                line.Toppings.Add(new OrderItemTopping
                {
                    ToppingId = topping.Id,
                    ToppingName = topping.Name,
                    UnitPriceVnd = topping.PriceVnd
                });
            orderItems.Add(line);
        }
        return orderItems;
    }

    private async Task<User> GetOrCreateWalkInCustomerAsync(CancellationToken ct)
    {
        var normalizedEmail = WalkInEmail.ToUpperInvariant();
        var customer = await data.Users.Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);
        if (customer is not null)
        {
            if (customer.Role.Name != RoleName.Customer)
                throw Check.Conflict("Tài khoản khách vãng lai không hợp lệ.");
            if (customer.IsActive)
            {
                customer.IsActive = false;
                customer.SecurityStamp = Guid.NewGuid();
                await data.SaveChangesAsync(ct);
            }
            return customer;
        }

        var role = await data.Roles.SingleAsync(x => x.Name == RoleName.Customer, ct);
        customer = new User
        {
            Email = WalkInEmail,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwords.Hash(Guid.NewGuid().ToString("N")),
            FullName = "Khách vãng lai",
            Role = role,
            IsActive = false
        };
        data.Users.Add(customer);
        await data.SaveChangesAsync(ct);
        return customer;
    }

    private async Task<(CheckoutQuote Quote, Coupon? Coupon)> CalculateAsync(Cart cart, string? rawCode,
        CancellationToken ct)
    {
        if (cart.Items.Count == 0)
            throw Check.Invalid("Giỏ hàng đang trống.");
        if (cart.Items.Any(x => !CartService.IsAvailable(x)))
            throw Check.Conflict("Giỏ có món hoặc topping đã ngừng bán. Vui lòng kiểm tra lại.");
        var subtotal = cart.Items.Sum(CartService.LineTotal);
        return await CalculateQuoteAsync(subtotal, rawCode, settings.ShippingFeeVnd, ct);
    }

    private async Task<(CheckoutQuote Quote, Coupon? Coupon)> CalculateQuoteAsync(long subtotal, string? rawCode,
        long shippingFee, CancellationToken ct)
    {
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
        var shipping = Check.Price(shippingFee);
        return (new CheckoutQuote(subtotal, discount, shipping, checked(subtotal - discount + shipping), code), coupon);
    }

    private IQueryable<Coupon> EligibleCoupons(string code, long subtotal, DateTime now) =>
        data.Coupons.Where(x => x.Code == code && x.IsActive && x.StartsAtUtc <= now
            && (x.EndsAtUtc == null || x.EndsAtUtc > now) && x.MinSubtotalVnd <= subtotal
            && (x.UsageLimit == null || x.UsedCount < x.UsageLimit));

    private IQueryable<Order> DetailedOrders() => data.Orders
        .Include(x => x.Payment).Include(x => x.CreatedByStaff)
        .Include(x => x.StatusHistory).ThenInclude(x => x.ChangedByUser)
        .Include(x => x.InternalNotes).ThenInclude(x => x.CreatedByUser)
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

    private OrderDetails Describe(Order order, bool operatorView)
    {
        var payment = order.Payment;
        BankTransferDto? transfer = payment.Method == PaymentMethod.BankTransfer && settings.AllowsBankTransfer
            ? new BankTransferDto(settings.BankName!, settings.AccountNumber!, settings.AccountName!, order.OrderNumber,
                settings.QrImageUrl(payment.AmountVnd, order.OrderNumber))
            : null;
        return new OrderDetails(order.Id, order.OrderNumber, order.Version, order.Status, order.PlacedAtUtc,
            order.ShippingRecipientName, order.ShippingPhone, order.ShippingAddress,
            new CheckoutQuote(order.SubtotalVnd, order.DiscountVnd, order.ShippingFeeVnd, order.TotalVnd, order.AppliedCouponCode),
            new PaymentDto(payment.Method, payment.Status, payment.AmountVnd, payment.ConfirmedAtUtc, transfer),
            order.Items.OrderBy(x => x.Id).Select(x => new OrderItemDto(x.Id, x.ProductSize.ProductId, x.ProductName,
                x.SizeName, x.UnitPriceVnd, x.Quantity, x.LineTotalVnd,
                x.Toppings.OrderBy(t => t.ToppingId).Select(t => new OrderToppingDto(t.ToppingName, t.UnitPriceVnd)).ToArray())).ToArray(),
            order.StatusHistory.OrderBy(x => x.ChangedAtUtc).ThenBy(x => x.Id)
                .Select(x => new OrderHistoryDto(x.PreviousStatus, x.NewStatus, x.ChangedAtUtc,
                    operatorView || x.NewStatus == OrderStatus.Cancelled ? x.Note : null,
                    operatorView ? x.ChangedByUser?.FullName : null)).ToArray(), order.Channel,
            operatorView ? order.CreatedByStaff?.FullName : null,
            operatorView ? order.InternalNotes.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
                .Select(x => new OrderInternalNoteDto(x.Text, x.CreatedByUser.FullName, x.CreatedAtUtc)).ToArray()
                : null);
    }
}
