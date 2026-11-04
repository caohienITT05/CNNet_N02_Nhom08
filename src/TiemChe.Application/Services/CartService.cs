using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class CartService(ITiemCheData data)
{
    private const int MaxQuantity = 99;
    private const int MaxLines = 100;

    public async Task<CartDto> GetAsync(int userId, CancellationToken ct) =>
        Describe(await LoadAsync(userId, ct));

    public async Task<CartDto> AddAsync(int userId, AddCartItemRequest request, CancellationToken ct)
    {
        ValidateQuantity(request.Quantity);
        var ids = request.ToppingIds?.Distinct().Order().ToArray() ?? [];
        if (ids.Length > 30)
            throw Check.Invalid("Bạn có thể chọn tối đa 30 loại topping.");
        await using var transaction = await data.BeginWriteAsync(ct);
        var cart = await LoadAsync(userId, ct);
        ValidateVersion(cart, request.Version);
        var size = await FindSizeAsync(request.SizeId, ct);
        if (size is null || !IsAvailable(size, ids))
            throw Check.Invalid("Size hoặc topping đã chọn hiện không bán.");
        cart ??= Create(userId);
        if (!TryAdd(cart, size, ids, request.Quantity))
            throw Check.Invalid("Giỏ hàng đã đạt giới hạn số món hoặc số lượng.");
        cart.Version = Guid.NewGuid();
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(cart);
    }

    public async Task<CartDto> SetQuantityAsync(int userId, int itemId, CartQuantityRequest request, CancellationToken ct)
    {
        ValidateQuantity(request.Quantity);
        await using var transaction = await data.BeginWriteAsync(ct);
        var cart = await RequireAsync(userId, request.Version, ct);
        var item = FindItem(cart, itemId);
        item.Quantity = request.Quantity;
        cart.Version = Guid.NewGuid();
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(cart);
    }

    public async Task<CartDto> RemoveAsync(int userId, int itemId, Guid version, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var cart = await RequireAsync(userId, version, ct);
        var item = FindItem(cart, itemId);
        data.CartItems.Remove(item);
        cart.Items.Remove(item);
        cart.Version = Guid.NewGuid();
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Describe(cart);
    }

    public async Task<ReorderResult> ReorderAsync(int userId, int orderId, ReorderRequest request, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var order = await data.Orders.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Toppings)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, ct)
            ?? throw Check.Missing("Không tìm thấy đơn hàng.");
        var cart = await LoadAsync(userId, ct);
        ValidateVersion(cart, request.CartVersion);
        var skipped = new List<string>();
        var added = false;
        foreach (var item in order.Items.OrderBy(x => x.Id))
        {
            var ids = item.Toppings.Select(x => x.ToppingId).Order().ToArray();
            var size = await FindSizeAsync(item.ProductSizeId, ct);
            if (size is null || !IsAvailable(size, ids) || item.Quantity is < 1 or > MaxQuantity)
            {
                skipped.Add($"{item.ProductName} ({item.SizeName})");
                continue;
            }
            cart ??= Create(userId);
            if (TryAdd(cart, size, ids, item.Quantity))
                added = true;
            else
                skipped.Add($"{item.ProductName} ({item.SizeName})");
        }
        if (added)
        {
            cart!.Version = Guid.NewGuid();
            await data.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new ReorderResult(Describe(cart), skipped);
    }

    internal Task<Cart?> LoadAsync(int userId, CancellationToken ct) =>
        data.Carts.Include(x => x.Items).ThenInclude(x => x.ProductSize).ThenInclude(x => x.Product)
            .ThenInclude(x => x.Category)
            .Include(x => x.Items).ThenInclude(x => x.ProductSize).ThenInclude(x => x.Product)
            .ThenInclude(x => x.AllowedToppings).ThenInclude(x => x.Topping)
            .Include(x => x.Items).ThenInclude(x => x.Toppings).ThenInclude(x => x.Topping)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.UserId == userId, ct);

    internal async Task<Cart> RequireAsync(int userId, Guid version, CancellationToken ct)
    {
        var cart = await LoadAsync(userId, ct);
        ValidateVersion(cart, version);
        return cart ?? throw Check.Invalid("Giỏ hàng đang trống.");
    }

    internal static bool IsAvailable(CartItem item) =>
        IsAvailable(item.ProductSize, item.Toppings.Select(x => x.ToppingId));

    internal static long LineTotal(CartItem item) =>
        checked((item.ProductSize.PriceVnd + item.Toppings.Sum(x => x.Topping.PriceVnd)) * item.Quantity);

    private static bool IsAvailable(ProductSize size, IEnumerable<int> ids) =>
        size.IsActive && size.Product.IsActive && size.Product.Category.IsActive
        && ids.All(id => size.Product.AllowedToppings.Any(x => x.ToppingId == id && x.Topping.IsActive));

    private static void ValidateVersion(Cart? cart, Guid? version)
    {
        if (cart?.Version != version)
            throw Check.Conflict("Giỏ hàng đã thay đổi. Vui lòng tải lại.");
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity is < 1 or > MaxQuantity)
            throw Check.Invalid("Số lượng phải từ 1 đến 99.");
    }

    private static CartItem FindItem(Cart cart, int itemId) =>
        cart.Items.SingleOrDefault(x => x.Id == itemId) ?? throw Check.Missing("Không tìm thấy món trong giỏ.");

    private Task<ProductSize?> FindSizeAsync(int id, CancellationToken ct) =>
        data.ProductSizes.Include(x => x.Product).ThenInclude(x => x.Category)
            .Include(x => x.Product).ThenInclude(x => x.AllowedToppings).ThenInclude(x => x.Topping)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    private Cart Create(int userId)
    {
        var cart = new Cart { UserId = userId };
        data.Carts.Add(cart);
        return cart;
    }

    private static bool TryAdd(Cart cart, ProductSize size, int[] ids, int quantity)
    {
        var existing = cart.Items.FirstOrDefault(x => x.ProductSizeId == size.Id
            && x.Toppings.Select(t => t.ToppingId).Order().SequenceEqual(ids));
        if (existing is not null)
        {
            if (existing.Quantity + quantity > MaxQuantity)
                return false;
            existing.Quantity += quantity;
            return true;
        }
        if (cart.Items.Count >= MaxLines)
            return false;
        var item = new CartItem { ProductSizeId = size.Id, ProductSize = size, Quantity = quantity };
        foreach (var link in size.Product.AllowedToppings.Where(x => ids.Contains(x.ToppingId)))
            item.Toppings.Add(new CartItemTopping { ToppingId = link.ToppingId, Topping = link.Topping });
        cart.Items.Add(item);
        return true;
    }

    private static CartDto Describe(Cart? cart)
    {
        if (cart is null)
            return new CartDto(null, [], 0);
        var items = cart.Items.OrderBy(x => x.Id).Select(x => new CartItemDto(x.Id, x.ProductSize.ProductId,
            x.ProductSizeId, x.ProductSize.Product.Name, x.ProductSize.Name, x.ProductSize.PriceVnd, x.Quantity,
            x.Toppings.OrderBy(t => t.ToppingId).Select(t => new CartToppingDto(t.ToppingId, t.Topping.Name,
                t.Topping.PriceVnd)).ToArray(), LineTotal(x), IsAvailable(x))).ToArray();
        return new CartDto(cart.Version, items, items.Sum(x => x.LineTotalVnd));
    }
}
