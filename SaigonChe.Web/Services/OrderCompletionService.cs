using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.Services;

/// <summary>
/// Shared completion path for POS, party and COD orders. Callers start a
/// database transaction before invoking this service and commit with their
/// other changes.
/// </summary>
public sealed class OrderCompletionService(ApplicationDbContext context)
{
    public async Task CompleteAsync(
        Order order,
        PaymentMethod method,
        string? actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (order.IsPaid) return;
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed)
            throw new OrderCompletionException("Đơn đã kết thúc và không thể hoàn tất lần nữa.");
        if (order.Items.Count == 0)
            throw new OrderCompletionException("Đơn hàng chưa có món.");
        if (!Enum.IsDefined(method) || order.Items.Any(item => item.Quantity <= 0))
            throw new OrderCompletionException("Phương thức thanh toán hoặc số lượng món không hợp lệ.");

        var required = order.Items.GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var existingMovements = await context.InventoryMovements.AsNoTracking()
            .Where(movement => movement.OrderId == order.Id && required.Keys.Contains(movement.ProductId))
            .ToDictionaryAsync(movement => movement.ProductId, cancellationToken);

        if (existingMovements.Count > 0 && required.Any(pair =>
                !existingMovements.TryGetValue(pair.Key, out var movement) || movement.QuantityChange != -pair.Value))
            throw new OrderCompletionException("Lịch sử tồn kho của đơn không đầy đủ; vui lòng đối soát với Admin.");

        if (existingMovements.Count == 0)
        {
            var products = await context.Products
                .Where(product => required.Keys.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            foreach (var (productId, quantity) in required)
            {
                if (!products.TryGetValue(productId, out var product) || !product.IsAvailable)
                    throw new OrderCompletionException("Một hoặc nhiều món trong đơn đã ngừng bán.");
                if (product.Stock is null)
                    throw new OrderCompletionException($"Chưa thiết lập tồn kho cho món {product.Name}.");
                if (product.Stock < quantity)
                    throw new OrderCompletionException($"Tồn kho món {product.Name} không đủ; hiện còn {product.Stock} phần.");

                product.Stock -= quantity;
                context.InventoryMovements.Add(new InventoryMovement
                {
                    ProductId = product.Id,
                    OrderId = order.Id,
                    ActorId = actorId,
                    QuantityChange = -quantity,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        var completedAt = DateTime.UtcNow;
        var previousStatus = order.Status;
        order.IsPaid = true;
        order.PaymentMethod = method;
        order.PaidAt = completedAt;
        order.CompletedAt = completedAt;
        order.Status = OrderStatus.Completed;
        order.AssignedStaffId ??= actorId;
        context.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            Status = OrderStatus.Completed,
            ActorId = actorId,
            Note = reason,
            CreatedAt = completedAt
        });
    }
}
