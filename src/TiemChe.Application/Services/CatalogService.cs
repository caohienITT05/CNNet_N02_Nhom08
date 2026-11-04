using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Catalog;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class CatalogService(ITiemCheData data, TimeProvider time)
{
    public async Task<HomeDto> HomeAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var banners = await data.Banners.AsNoTracking().Where(x => x.IsActive && x.StartsAtUtc <= now
                && (x.EndsAtUtc == null || x.EndsAtUtc > now)
                && (x.Product == null || (x.Product.IsActive && x.Product.Category.IsActive
                    && x.Product.Sizes.Any(s => s.IsActive))))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new BannerDto(x.Id, x.Title, x.ImagePath, x.ProductId, x.DisplayOrder,
                x.StartsAtUtc, x.EndsAtUtc, x.IsActive)).ToListAsync(ct);
        var popular = await ListAsync(null, null, null, null, ProductSort.Popular, 1, 6, ct);
        var newest = await ListAsync(null, null, null, null, ProductSort.Newest, 1, 6, ct);
        return new HomeDto(banners, await CategoriesAsync(false, ct), popular.Items, newest.Items);
    }

    public async Task<IReadOnlyList<CategoryDto>> CategoriesAsync(bool includeInactive, CancellationToken ct) =>
        await data.Categories.AsNoTracking().Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new CategoryDto(x.Id, x.Name, x.DisplayOrder, x.IsActive)).ToListAsync(ct);

    public async Task<PageResult<ProductCard>> ListAsync(string? search, int? categoryId, long? minPrice,
        long? maxPrice, ProductSort? sort, int? page, int? pageSize, CancellationToken ct)
    {
        var pagination = Check.Page(page, pageSize);
        var term = Check.Optional(search, 120, "từ khóa");
        if (minPrice < 0 || maxPrice < 0 || minPrice > maxPrice)
            throw Check.Invalid("Khoảng giá không hợp lệ.");
        var products = data.Products.AsNoTracking()
            .Where(x => x.IsActive && x.Category.IsActive && x.Sizes.Any(s => s.IsActive));
        if (categoryId.HasValue)
            products = products.Where(x => x.CategoryId == categoryId);
        if (term is not null)
        {
            var normalized = VietnameseSearch.Normalize(term);
            products = products.Where(x => VietnameseSearch.Contains(x.Name, normalized)
                || VietnameseSearch.Contains(x.Description, normalized)
                || VietnameseSearch.Contains(x.Category.Name, normalized));
        }
        var query = products.Select(x => new
        {
            Product = x,
            Price = x.Sizes.Where(s => s.IsActive).Min(s => s.PriceVnd)
        });
        if (minPrice.HasValue)
            query = query.Where(x => x.Price >= minPrice);
        if (maxPrice.HasValue)
            query = query.Where(x => x.Price <= maxPrice);
        var count = await query.CountAsync(ct);
        var ordered = sort switch
        {
            ProductSort.PriceAscending => query.OrderBy(x => x.Price),
            ProductSort.PriceDescending => query.OrderByDescending(x => x.Price),
            ProductSort.Name => query.OrderBy(x => x.Product.Name),
            ProductSort.Popular => query.OrderByDescending(x => data.OrderItems.Where(i =>
                i.ProductSize.ProductId == x.Product.Id && i.Order.Status == OrderStatus.Completed)
                .Sum(i => (long)i.Quantity)),
            null or ProductSort.Newest => query.OrderByDescending(x => x.Product.CreatedAtUtc),
            _ => throw Check.Invalid("Kiểu sắp xếp không hợp lệ.")
        };
        var items = await ordered.ThenByDescending(x => x.Product.Id)
            .Skip((pagination.Page - 1) * pagination.Size).Take(pagination.Size)
            .Select(x => new ProductCard(x.Product.Id, x.Product.CategoryId, x.Product.Name,
                x.Product.ImagePath, x.Price)).ToListAsync(ct);
        return new PageResult<ProductCard>(items, count, pagination.Page, pagination.Size);
    }

    public async Task<ProductDetails> DetailsAsync(int id, bool management, CancellationToken ct)
    {
        var product = await data.Products.AsNoTracking().Include(x => x.Sizes)
            .Include(x => x.AllowedToppings).ThenInclude(x => x.Topping).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id && (management ||
                (x.IsActive && x.Category.IsActive && x.Sizes.Any(s => s.IsActive))), ct)
            ?? throw Check.Missing("Không tìm thấy món.");
        var reviews = data.Reviews.AsNoTracking().Where(x => x.ProductId == id && x.IsVisible);
        var stats = await reviews.GroupBy(x => x.ProductId)
            .Select(group => new { Count = group.Count(), Average = group.Average(x => x.Rating) })
            .SingleOrDefaultAsync(ct);
        var comments = await reviews.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new ReviewDto(x.Id, x.ProductId, x.User.FullName, x.Rating, x.Comment,
                x.IsVisible, x.CreatedAtUtc)).ToListAsync(ct);
        return new ProductDetails(product.Id, product.CategoryId, product.Name, product.Description,
            product.ImagePath, product.IsActive,
            product.Sizes.Where(x => management || x.IsActive).OrderBy(x => x.PriceVnd).ThenBy(x => x.Id)
                .Select(x => new SizeDto(x.Id, x.Name, x.PriceVnd, x.IsActive)).ToArray(),
            product.AllowedToppings.Select(x => x.Topping).Where(x => management || x.IsActive)
                .OrderBy(x => x.Id).Select(x => new ToppingDto(x.Id, x.Name, x.PriceVnd, x.ImagePath, x.IsActive)).ToArray(),
            comments, stats?.Average, stats?.Count ?? 0);
    }

    public async Task<ReviewDto> ReviewAsync(int userId, int productId, ReviewRequest request, CancellationToken ct)
    {
        if (request.Rating is < 1 or > 5)
            throw Check.Invalid("Điểm đánh giá phải từ 1 đến 5 sao.");
        var comment = Check.Text(request.Comment, 2000, "nội dung đánh giá");
        var item = await data.OrderItems.Include(x => x.Order).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == request.OrderItemId && x.ProductSize.ProductId == productId
                && x.Order.UserId == userId && x.Order.Status == OrderStatus.Completed, ct)
            ?? throw Check.Invalid("Bạn có thể đánh giá món đã mua trong đơn hoàn thành.");
        if (await data.Reviews.AnyAsync(x => x.UserId == userId && x.ProductId == productId, ct))
            throw Check.Conflict("Bạn đã đánh giá món này.");
        var review = new Review
        {
            UserId = userId,
            ProductId = productId,
            OrderItemId = item.Id,
            Rating = request.Rating,
            Comment = comment,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        };
        data.Reviews.Add(review);
        await data.SaveChangesAsync(ct);
        return new ReviewDto(review.Id, productId, item.Order.User.FullName, review.Rating,
            review.Comment, review.IsVisible, review.CreatedAtUtc);
    }
}
