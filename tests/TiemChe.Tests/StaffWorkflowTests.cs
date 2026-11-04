using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Services;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Operations;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;
using TiemChe.Infrastructure.Data;
using Xunit;

namespace TiemChe.Tests;

public sealed class StaffWorkflowTests
{
    [Fact]
    public async Task StaffOrder_UsesCurrentPriceAndSelectedToppings()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var request = test.Request(sizeId: 9, toppingIds: [1], expectedTotal: 37_000);

        var order = await test.Orders.CreateStaffOrderAsync(staff.Id, request, CancellationToken.None);

        Assert.Equal(37_000, order.Amounts.TotalVnd);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(PaymentStatus.Paid, order.Payment.Status);
        Assert.Equal(OrderChannel.InStore, order.Channel);
        Assert.Equal(staff.Id, await test.Db.Orders.Where(x => x.Id == order.Id)
            .Select(x => x.CreatedByStaffId).SingleAsync());
        Assert.Equal(RoleName.Customer, await test.Db.Orders.Where(x => x.Id == order.Id)
            .Select(x => x.User.Role.Name).SingleAsync());
    }

    [Fact]
    public async Task StaffOrder_RejectsToppingNotAllowedForProduct()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");

        var error = await Assert.ThrowsAsync<BusinessException>(() => test.Orders.CreateStaffOrderAsync(
            staff.Id, test.Request(sizeId: 1, toppingIds: [1]), CancellationToken.None));

        Assert.Equal(400, error.Status);
        Assert.Empty(await test.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task StaffOrder_RejectsDisabledSize()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var size = await test.Db.ProductSizes.SingleAsync(x => x.Id == 9);
        size.IsActive = false;
        await test.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<BusinessException>(() => test.Orders.CreateStaffOrderAsync(
            staff.Id, test.Request(sizeId: 9), CancellationToken.None));

        Assert.Equal(400, error.Status);
        Assert.Empty(await test.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task StaffOrder_RejectsChangedExpectedTotalWithConflict()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");

        var error = await Assert.ThrowsAsync<BusinessException>(() => test.Orders.CreateStaffOrderAsync(
            staff.Id, test.Request(sizeId: 9, expectedTotal: 1), CancellationToken.None));

        Assert.Equal(409, error.Status);
        Assert.Empty(await test.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task StaffOrder_IsIdempotentForSameStaffAndRequestId()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var request = test.Request(sizeId: 9);

        var first = await test.Orders.CreateStaffOrderAsync(staff.Id, request, CancellationToken.None);
        var retry = await test.Orders.CreateStaffOrderAsync(staff.Id, request, CancellationToken.None);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, await test.Db.Orders.CountAsync());
    }

    [Fact]
    public async Task Coupon_IsQuotedReservedOnceAndRestoredWhenOrderIsCancelled()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var coupon = new Coupon
        {
            Code = "SAVE5",
            Kind = CouponKind.FixedAmount,
            DiscountValue = 5_000,
            MinSubtotalVnd = 30_000,
            StartsAtUtc = test.Now.UtcDateTime.AddDays(-1),
            UsageLimit = 1
        };
        test.Db.Coupons.Add(coupon);
        await test.Db.SaveChangesAsync();
        var quoteRequest = test.Request(sizeId: 9, toppingIds: [1], couponCode: "SAVE5");

        var quote = await test.Orders.StaffQuoteAsync(quoteRequest, CancellationToken.None);
        Assert.Equal(32_000, quote.TotalVnd);
        var order = await test.Orders.CreateStaffOrderAsync(staff.Id,
            quoteRequest with { CompleteNow = false, ExpectedTotalVnd = quote.TotalVnd }, CancellationToken.None);
        Assert.Equal(1, await test.Db.Coupons.Where(x => x.Id == coupon.Id).Select(x => x.UsedCount).SingleAsync());
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() =>
            test.Orders.StaffQuoteAsync(quoteRequest, CancellationToken.None))).Status);

        await test.Orders.UpdateStatusAsync(order.Id, staff.Id,
            new OrderStatusRequest(order.Version, OrderStatus.Cancelled, "Khách đổi ý"), CancellationToken.None);

        Assert.Equal(0, await test.Db.Coupons.Where(x => x.Id == coupon.Id).Select(x => x.UsedCount).SingleAsync());
    }

    [Fact]
    public async Task InStoreOrderCanSkipDeliveryWhileOnlineOrderKeepsDeliveryFlow()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var inStore = await test.Orders.CreateStaffOrderAsync(staff.Id,
            test.Request(sizeId: 9, completeNow: false), CancellationToken.None);

        var completed = await test.Orders.UpdateStatusAsync(inStore.Id, staff.Id,
            new OrderStatusRequest(inStore.Version, OrderStatus.Completed, null), CancellationToken.None);
        Assert.Equal(PaymentStatus.Paid, completed.Payment.Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => test.Orders.UpdateStatusAsync(
            completed.Id, staff.Id, new OrderStatusRequest(completed.Version, OrderStatus.Delivering, null),
            CancellationToken.None))).Status);

        var customer = await test.AddUserAsync(RoleName.Customer, "customer@example.com");
        var online = await test.AddManualOrderAsync(customer.Id, OrderChannel.Online, OrderStatus.Preparing,
            PaymentStatus.Unpaid);
        var delivering = await test.Orders.UpdateStatusAsync(online.Id, staff.Id,
            new OrderStatusRequest(online.Version, OrderStatus.Delivering, null), CancellationToken.None);
        Assert.Equal(OrderStatus.Delivering, delivering.Status);
    }

    [Fact]
    public async Task PaidCancellationRequiresReasonRefundsAndDoesNotCountAsRevenue()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var customer = await test.AddUserAsync(RoleName.Customer, "customer@example.com");
        var coupon = new Coupon
        {
            Code = "REFUND",
            Kind = CouponKind.FixedAmount,
            DiscountValue = 1_000,
            MinSubtotalVnd = 0,
            StartsAtUtc = test.Now.UtcDateTime.AddDays(-1),
            UsedCount = 1
        };
        test.Db.Coupons.Add(coupon);
        await test.Db.SaveChangesAsync();
        var order = await test.AddManualOrderAsync(customer.Id, OrderChannel.Online, OrderStatus.Preparing,
            PaymentStatus.Paid, coupon.Id);

        Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => test.Orders.CancelAsync(order.Id,
            customer.Id, new VersionRequest(order.Version), CancellationToken.None))).Status);
        var error = await Assert.ThrowsAsync<BusinessException>(() => test.Orders.UpdateStatusAsync(order.Id,
            staff.Id, new OrderStatusRequest(order.Version, OrderStatus.Cancelled, "Sai"), CancellationToken.None));
        Assert.Equal(400, error.Status);
        var cancelled = await test.Orders.UpdateStatusAsync(order.Id, staff.Id,
            new OrderStatusRequest(order.Version, OrderStatus.Cancelled, "Khách yêu cầu hoàn"), CancellationToken.None);
        var payment = await test.Db.Payments.SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal(PaymentStatus.Refunded, cancelled.Payment.Status);
        Assert.Equal(staff.Id, payment.RefundedByUserId);
        Assert.Equal(test.Now.UtcDateTime, payment.RefundedAtUtc);
        Assert.Equal(0, await test.Db.Coupons.Where(x => x.Id == coupon.Id).Select(x => x.UsedCount).SingleAsync());

        await test.AddManualOrderAsync(customer.Id, OrderChannel.Online, OrderStatus.Completed,
            PaymentStatus.Refunded);
        var report = await new ReportService(test.Db, test.Clock)
            .DashboardAsync(null, null, false, CancellationToken.None);
        Assert.Equal(0, report.CompletedOrders);
        Assert.Equal(0, report.RevenueVnd);
    }

    [Fact]
    public async Task BankTransferCompleteNowRequiresStaffAcknowledgement()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var transferSettings = new CheckoutSettings
        {
            BankName = "VCB",
            AccountNumber = "123456789",
            AccountName = "TIEM CHE"
        };
        var orders = new OrderService(test.Db, test.Carts, transferSettings, test.Clock, new TestPasswordHasher());
        var request = test.Request(sizeId: 9) with { PaymentMethod = PaymentMethod.BankTransfer };

        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => orders.CreateStaffOrderAsync(
            staff.Id, request, CancellationToken.None))).Status);
        var order = await orders.CreateStaffOrderAsync(staff.Id, request with { PaymentReceived = true },
            CancellationToken.None);
        Assert.Equal(PaymentStatus.Paid, order.Payment.Status);
        Assert.Equal(staff.Id, await test.Db.Payments.Where(x => x.OrderId == order.Id)
            .Select(x => x.ConfirmedByUserId).SingleAsync());
    }

    [Fact]
    public async Task InternalOrderNotesAreOnlyReturnedForOperators()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var order = await test.Orders.CreateStaffOrderAsync(staff.Id, test.Request(sizeId: 9),
            CancellationToken.None);
        await test.Orders.AddInternalNoteAsync(order.Id, staff.Id, new OrderNoteRequest("Gọi khách khi xong"),
            CancellationToken.None);

        var customerId = await test.Db.Orders.Where(x => x.Id == order.Id).Select(x => x.UserId).SingleAsync();
        var customerView = await test.Orders.GetAsync(order.Id, customerId, CancellationToken.None);
        var operatorView = await test.Orders.GetAsync(order.Id, null, CancellationToken.None);

        Assert.Null(customerView.InternalNotes);
        Assert.Equal("Gọi khách khi xong", Assert.Single(operatorView.InternalNotes!).Text);
    }

    [Fact]
    public async Task CashShift_ComputesExpectedCashAndRequiresNoteForDifference()
    {
        await using var test = await TestDatabase.CreateAsync();
        var staff = await test.AddUserAsync(RoleName.Staff, "staff@example.com");
        var shift = await test.Operations.OpenShiftAsync(staff.Id, 1_000, CancellationToken.None);
        var order = await test.Orders.CreateStaffOrderAsync(staff.Id, test.Request(sizeId: 9),
            CancellationToken.None);
        Assert.Equal(PaymentStatus.Paid, order.Payment.Status);

        var error = await Assert.ThrowsAsync<BusinessException>(() => test.Operations.CloseShiftAsync(staff.Id,
            31_100, null, CancellationToken.None));
        Assert.Equal(400, error.Status);
        var closed = await test.Operations.CloseShiftAsync(staff.Id, 31_100, "Thừa 100 đồng",
            CancellationToken.None);

        Assert.Equal(31_000, closed.ExpectedCashVnd);
        Assert.Equal(31_100, closed.CountedCashVnd);
        Assert.Equal(100, closed.DifferenceVnd);
        Assert.Equal(1, closed.HandledOrderCount);
        Assert.NotNull(shift);
    }

    private sealed class TestDatabase(SqliteConnection connection, TiemCheDbContext db, FixedTimeProvider clock)
        : IAsyncDisposable
    {
        public readonly DateTimeOffset Now = new(2026, 11, 4, 7, 0, 0, TimeSpan.Zero);
        public TiemCheDbContext Db { get; } = db;
        public FixedTimeProvider Clock { get; } = clock;
        public CartService Carts { get; } = new(db);
        public OrderService Orders { get; } = new(db, new CartService(db), new CheckoutSettings(), clock,
            new TestPasswordHasher());
        public OperationsService Operations { get; } = new(db, clock, new CheckoutSettings());

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<TiemCheDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new SqliteSearchInterceptor())
                .Options;
            var db = new TiemCheDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, db, new FixedTimeProvider(
                new DateTimeOffset(2026, 11, 4, 7, 0, 0, TimeSpan.Zero)));
        }

        public StaffOrderRequest Request(int sizeId, IReadOnlyList<int>? toppingIds = null,
            long? expectedTotal = null, string? couponCode = null, bool completeNow = true) =>
            new("Khách lẻ", null, null, PaymentMethod.CashOnDelivery,
                [new StaffOrderLineRequest(sizeId, 1, toppingIds)], Guid.NewGuid(), null, couponCode,
                completeNow, false, expectedTotal);

        public async Task<User> AddUserAsync(RoleName roleName, string email)
        {
            var role = await Db.Roles.SingleAsync(x => x.Name == roleName);
            var user = new User
            {
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = "test-hash",
                FullName = roleName == RoleName.Staff ? "Nhân viên thử nghiệm" : "Khách thử nghiệm",
                Role = role
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user;
        }

        public async Task<OrderDetails> AddManualOrderAsync(int userId, OrderChannel channel, OrderStatus status,
            PaymentStatus paymentStatus, int? couponId = null)
        {
            var order = new Order
            {
                UserId = userId,
                Channel = channel,
                CheckoutRequestId = Guid.NewGuid(),
                OrderNumber = $"TC{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                CouponId = couponId,
                ShippingRecipientName = "Khách thử nghiệm",
                ShippingPhone = "0123456789",
                ShippingAddress = "Địa chỉ thử nghiệm",
                SubtotalVnd = 10_000,
                DiscountVnd = couponId.HasValue ? 1_000 : 0,
                TotalVnd = couponId.HasValue ? 9_000 : 10_000,
                Status = status,
                PlacedAtUtc = Clock.GetUtcNow().UtcDateTime,
                Payment = new Payment
                {
                    Method = PaymentMethod.CashOnDelivery,
                    Status = paymentStatus,
                    AmountVnd = couponId.HasValue ? 9_000 : 10_000,
                    ConfirmedByUserId = paymentStatus == PaymentStatus.Paid ? userId : null,
                    ConfirmedAtUtc = paymentStatus == PaymentStatus.Paid ? Clock.GetUtcNow().UtcDateTime : null
                }
            };
            if (status == OrderStatus.Completed)
                order.CompletedAtUtc = Clock.GetUtcNow().UtcDateTime;
            Db.Orders.Add(order);
            await Db.SaveChangesAsync();
            return await Orders.GetAsync(order.Id, null, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"test:{password}";
        public bool Verify(string password, string hash) => hash == Hash(password);
    }
}
