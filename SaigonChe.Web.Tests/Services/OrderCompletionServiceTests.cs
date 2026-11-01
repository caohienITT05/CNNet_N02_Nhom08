using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.Services;
using Xunit;

namespace SaigonChe.Web.Tests.Services;

public sealed class OrderCompletionServiceTests
{
    [Fact]
    public async Task CompleteAsync_MarksPaidAndDeductsStockOnce()
    {
        var databaseName = Guid.NewGuid().ToString();
        int orderId;
        int productId;

        await using (var context = CreateContext(databaseName))
        {
            var (order, product) = CreateOrder(stock: 5, quantity: 2);
            context.Products.Add(product);
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
            productId = product.Id;

            await new OrderCompletionService(context).CompleteAsync(
                order, PaymentMethod.Cash, "staff-1", "POS sale #1");
            await context.SaveChangesAsync();

            Assert.True(order.IsPaid);
            Assert.Equal(OrderStatus.Completed, order.Status);
            Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
            Assert.NotNull(order.PaidAt);
            Assert.NotNull(order.CompletedAt);
            Assert.Equal(3, product.Stock);
            Assert.Single(context.InventoryMovements);
            Assert.Single(context.OrderStatusHistory);
        }

        // A second request must observe the saved paid state and avoid creating a
        // second inventory movement or deducting stock again.
        await using (var context = CreateContext(databaseName))
        {
            var order = await context.Orders.Include(item => item.Items)
                .SingleAsync(item => item.Id == orderId);
            await new OrderCompletionService(context).CompleteAsync(
                order, PaymentMethod.Cash, "staff-2", "duplicate confirmation");
            await context.SaveChangesAsync();

            Assert.Equal(3, await context.Products.Where(item => item.Id == productId)
                .Select(item => item.Stock).SingleAsync());
            Assert.Single(context.InventoryMovements);
            Assert.Single(context.OrderStatusHistory);
        }
    }

    [Fact]
    public async Task CompleteAsync_RejectsInsufficientStockWithoutPayingOrder()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var (order, product) = CreateOrder(stock: 1, quantity: 2);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<OrderCompletionException>(() =>
            new OrderCompletionService(context).CompleteAsync(
                order, PaymentMethod.Cash, "staff-1", "POS sale #2"));

        Assert.Contains("không đủ", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(order.IsPaid);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(1, product.Stock);
        Assert.Empty(context.InventoryMovements);
        Assert.Empty(context.OrderStatusHistory);
    }

    [Fact]
    public async Task CompleteAsync_RejectsUninitializedStock()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var (order, product) = CreateOrder(stock: null, quantity: 1);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<OrderCompletionException>(() =>
            new OrderCompletionService(context).CompleteAsync(
                order, PaymentMethod.Cash, "staff-1", "POS sale #3"));

        Assert.Contains("Chưa thiết lập tồn kho", exception.Message);
        Assert.False(order.IsPaid);
        Assert.Empty(context.InventoryMovements);
    }

    [Fact]
    public async Task CompleteAsync_AllowsFinalPortionButRejectsNextOrder()
    {
        var databaseName = Guid.NewGuid().ToString();
        int productId;

        await using (var context = CreateContext(databaseName))
        {
            var (order, product) = CreateOrder(stock: 1, quantity: 1);
            context.Products.Add(product);
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            productId = product.Id;

            await new OrderCompletionService(context).CompleteAsync(
                order, PaymentMethod.Cash, "staff-1", "POS final portion");
            await context.SaveChangesAsync();
            Assert.Equal(0, product.Stock);
        }

        await using (var context = CreateContext(databaseName))
        {
            var nextOrder = CreateOrderForExistingProduct(productId);
            context.Orders.Add(nextOrder);
            await context.SaveChangesAsync();

            await Assert.ThrowsAsync<OrderCompletionException>(() =>
                new OrderCompletionService(context).CompleteAsync(
                    nextOrder, PaymentMethod.Cash, "staff-2", "POS sold out"));

            Assert.False(nextOrder.IsPaid);
            Assert.Equal(0, await context.Products.Where(item => item.Id == productId)
                .Select(item => item.Stock).SingleAsync());
        }
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed)]
    public async Task CompleteAsync_RejectsTerminalUnpaidOrder(OrderStatus status)
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var (order, product) = CreateOrder(stock: 2, quantity: 1);
        order.Status = status;
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<OrderCompletionException>(() =>
            new OrderCompletionService(context).CompleteAsync(
                order, PaymentMethod.Cash, "staff-1", "invalid terminal order"));

        Assert.False(order.IsPaid);
        Assert.Equal(status, order.Status);
        Assert.Equal(2, product.Stock);
        Assert.Empty(context.InventoryMovements);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CompleteAsync_RejectsInvalidItemQuantity(int quantity)
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var (order, product) = CreateOrder(stock: 5, quantity);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<OrderCompletionException>(() =>
            new OrderCompletionService(context).CompleteAsync(
                order, PaymentMethod.Cash, "staff-1", "POS sale #4"));

        Assert.False(order.IsPaid);
        Assert.Equal(5, product.Stock);
        Assert.Empty(context.InventoryMovements);
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (Order Order, Product Product) CreateOrder(int? stock, int quantity)
    {
        var product = new Product
        {
            Name = "Trà đào",
            Description = "Món kiểm thử",
            Price = 25000,
            CategoryId = 1,
            Stock = stock,
            IsAvailable = true
        };
        var order = new Order
        {
            CustomerName = "Khách POS",
            Phone = "0900000000",
            Address = "Tại quầy",
            Type = OrderType.Pos,
            Total = 25000m * quantity,
            Status = OrderStatus.Pending,
            Items =
            [
                new OrderItem
                {
                    Product = product,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = quantity
                }
            ]
        };
        return (order, product);
    }

    private static Order CreateOrderForExistingProduct(int productId) => new()
    {
        CustomerName = "Khách POS",
        Phone = "0900000001",
        Address = "Tại quầy",
        Type = OrderType.Pos,
        Total = 25000,
        Status = OrderStatus.Pending,
        Items =
        [
            new OrderItem
            {
                ProductId = productId,
                ProductName = "Trà đào",
                Price = 25000,
                Quantity = 1
            }
        ]
    };
}
