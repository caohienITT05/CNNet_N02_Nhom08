using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Management;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class ManagementService(ITiemCheData data, IImageStore images, TimeProvider time)
{
    public async Task<CategoryDto> SaveCategoryAsync(int? id, CategoryRequest request, CancellationToken ct)
    {
        var name = Check.Text(request.Name, 120, "tên danh mục");
        var category = id.HasValue
            ? await data.Categories.FindAsync([id.Value], ct) ?? throw Check.Missing("Không tìm thấy danh mục.")
            : new Category { Name = name };
        category.Name = name;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        if (!id.HasValue)
            data.Categories.Add(category);
        await data.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.DisplayOrder, category.IsActive);
    }

    public async Task DisableCategoryAsync(int id, CancellationToken ct)
    {
        if (await data.Categories.Where(x => x.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false), ct) == 0)
            throw Check.Missing("Không tìm thấy danh mục.");
    }

    public async Task<PageResult<ProductCard>> ProductsAsync(int? page, int? pageSize, string? search, CancellationToken ct)
    {
        var pagination = Check.Page(page, pageSize);
        var term = Check.Optional(search, 120, "từ khóa");
        var query = data.Products.AsNoTracking();
        if (term is not null)
        {
            var normalized = VietnameseSearch.Normalize(term);
            query = query.Where(x => VietnameseSearch.Contains(x.Name, normalized));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Id).Skip((pagination.Page - 1) * pagination.Size)
            .Take(pagination.Size)
            .Select(x => new ProductCard(x.Id, x.CategoryId, x.Name, x.ImagePath,
                x.Sizes.Where(s => s.IsActive).Min(s => (long?)s.PriceVnd) ?? 0)).ToListAsync(ct);
        return new PageResult<ProductCard>(rows, count, pagination.Page, pagination.Size);
    }

    public async Task<int> SaveProductAsync(int? id, ProductRequest request, CancellationToken ct)
    {
        var name = Check.Text(request.Name, 160, "tên món");
        var description = Check.Text(request.Description, 4000, "mô tả");
        var image = ValidateImage(request.ImagePath);
        if (request.Sizes is not { Count: >= 1 and <= 20 } ||
            (request.IsActive && !request.Sizes.Any(s => s is { IsActive: true })))
            throw Check.Invalid("Hãy nhập từ 1 đến 20 size và bật ít nhất một size cho món đang bán.");
        var sizes = request.Sizes.Select(x =>
        {
            if (x is null)
                throw Check.Invalid("Size không hợp lệ.");
            return (x.Id, Name: Check.Text(x.Name, 40, "tên size"), Price: Check.Price(x.PriceVnd), x.IsActive);
        }).ToArray();
        if (sizes.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sizes.Length
            || sizes.Where(x => x.Id.HasValue).Select(x => x.Id).Distinct().Count() != sizes.Count(x => x.Id.HasValue))
            throw Check.Invalid("Danh sách size bị trùng.");
        var toppingIds = request.ToppingIds?.Distinct().ToArray() ?? [];
        if (toppingIds.Length > 30)
            throw Check.Invalid("Một món có tối đa 30 topping.");

        await using var transaction = await data.BeginWriteAsync(ct);
        if (!await data.Categories.AnyAsync(x => x.Id == request.CategoryId, ct))
            throw Check.Invalid("Danh mục không hợp lệ.");
        if (await data.Toppings.CountAsync(x => toppingIds.Contains(x.Id), ct) != toppingIds.Length)
            throw Check.Invalid("Danh sách topping có lựa chọn không hợp lệ.");

        var product = id.HasValue
            ? await data.Products.Include(x => x.Sizes).Include(x => x.AllowedToppings).AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Check.Missing("Không tìm thấy món.")
            : new Product { Name = name, Description = description, CreatedAtUtc = time.GetUtcNow().UtcDateTime };
        product.Name = name;
        product.Description = description;
        product.CategoryId = request.CategoryId;
        product.ImagePath = image;
        product.IsActive = request.IsActive;
        if (!id.HasValue)
            data.Products.Add(product);

        var current = product.Sizes.ToArray();
        var matched = sizes.Select(size =>
        {
            var existing = size.Id.HasValue
                ? current.SingleOrDefault(x => x.Id == size.Id) ?? throw Check.Invalid("Size không thuộc món này.")
                : current.SingleOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, size.Name));
            return (Size: size, Entity: existing);
        }).ToArray();
        var selected = matched.Where(x => x.Entity is not null).Select(x => x.Entity!).ToArray();
        if (selected.Distinct().Count() != selected.Length)
            throw Check.Invalid("Danh sách size bị trùng.");
        foreach (var existing in current)
        {
            if (!selected.Contains(existing))
            {
                if (sizes.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, existing.Name)))
                    throw Check.Invalid("Tên size đã được sử dụng. Hãy chỉnh sửa size hiện có.");
                existing.IsActive = false;
            }
        }
        // Đổi tạm tên trong giao dịch để có thể hoán đổi tên hai size có chỉ mục duy nhất.
        var renamed = matched.Where(x => x.Entity is not null && x.Entity.Name != x.Size.Name).ToArray();
        if (renamed.Length != 0)
        {
            foreach (var change in renamed)
                change.Entity!.Name = Guid.NewGuid().ToString("N");
            await data.SaveChangesAsync(ct);
        }
        foreach (var (size, entity) in matched)
        {
            var target = entity ?? new ProductSize { Name = size.Name };
            target.Name = size.Name;
            target.PriceVnd = size.Price;
            target.IsActive = size.IsActive;
            if (entity is null)
                product.Sizes.Add(target);
        }
        var links = product.AllowedToppings.ToArray();
        foreach (var link in links.Where(x => !toppingIds.Contains(x.ToppingId)))
            product.AllowedToppings.Remove(link);
        foreach (var toppingId in toppingIds.Where(toppingId => links.All(x => x.ToppingId != toppingId)))
            product.AllowedToppings.Add(new ProductTopping { ToppingId = toppingId });

        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return product.Id;
    }

    public async Task DisableProductAsync(int id, CancellationToken ct)
    {
        if (await data.Products.Where(x => x.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false), ct) == 0)
            throw Check.Missing("Không tìm thấy món.");
    }

    public async Task<IReadOnlyList<ToppingDto>> ToppingsAsync(CancellationToken ct) =>
        await data.Toppings.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ToppingDto(x.Id, x.Name, x.PriceVnd, x.ImagePath, x.IsActive)).ToListAsync(ct);

    public async Task<ToppingDto> SaveToppingAsync(int? id, ToppingRequest request, CancellationToken ct)
    {
        var name = Check.Text(request.Name, 120, "tên topping");
        var image = ValidateImage(request.ImagePath);
        var price = Check.Price(request.PriceVnd);
        var topping = id.HasValue
            ? await data.Toppings.FindAsync([id.Value], ct) ?? throw Check.Missing("Không tìm thấy topping.")
            : new Topping { Name = name };
        topping.Name = name;
        topping.PriceVnd = price;
        topping.IsActive = request.IsActive;
        topping.ImagePath = image;
        if (!id.HasValue)
            data.Toppings.Add(topping);
        await data.SaveChangesAsync(ct);
        return new ToppingDto(topping.Id, name, price, image, topping.IsActive);
    }

    public async Task DisableToppingAsync(int id, CancellationToken ct)
    {
        if (await data.Toppings.Where(x => x.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false), ct) == 0)
            throw Check.Missing("Không tìm thấy topping.");
    }

    public async Task<IReadOnlyList<BannerDto>> BannersAsync(CancellationToken ct) =>
        await data.Banners.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new BannerDto(x.Id, x.Title, x.ImagePath, x.ProductId, x.DisplayOrder,
                x.StartsAtUtc, x.EndsAtUtc, x.IsActive)).ToListAsync(ct);

    public async Task<BannerDto> SaveBannerAsync(int? id, BannerRequest request, CancellationToken ct)
    {
        var title = Check.Text(request.Title, 160, "tiêu đề");
        var image = ValidateImage(request.ImagePath) ?? throw Check.Invalid("Vui lòng chọn ảnh banner.");
        if (request.StartsAt == default || request.EndsAt <= request.StartsAt)
            throw Check.Invalid("Thời gian hiển thị không hợp lệ.");
        if (request.ProductId.HasValue && !await data.Products.AnyAsync(x => x.Id == request.ProductId, ct))
            throw Check.Invalid("Món liên kết không tồn tại.");
        var banner = id.HasValue
            ? await data.Banners.FindAsync([id.Value], ct) ?? throw Check.Missing("Không tìm thấy banner.")
            : new Banner { Title = title, ImagePath = image };
        banner.Title = title;
        banner.ImagePath = image;
        banner.ProductId = request.ProductId;
        banner.DisplayOrder = request.DisplayOrder;
        banner.StartsAtUtc = request.StartsAt.UtcDateTime;
        banner.EndsAtUtc = request.EndsAt?.UtcDateTime;
        banner.IsActive = request.IsActive;
        if (!id.HasValue)
            data.Banners.Add(banner);
        await data.SaveChangesAsync(ct);
        return new BannerDto(banner.Id, title, image, banner.ProductId, banner.DisplayOrder,
            banner.StartsAtUtc, banner.EndsAtUtc, banner.IsActive);
    }

    public async Task DeleteBannerAsync(int id, CancellationToken ct)
    {
        if (await data.Banners.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0)
            throw Check.Missing("Không tìm thấy banner.");
    }

    public async Task<IReadOnlyList<CouponDto>> CouponsAsync(CancellationToken ct) =>
        await data.Coupons.AsNoTracking().OrderByDescending(x => x.Id)
            .Select(x => new CouponDto(x.Id, x.Code, x.Kind, x.DiscountValue, x.MaxDiscountVnd,
                x.MinSubtotalVnd, x.StartsAtUtc, x.EndsAtUtc, x.UsageLimit, x.UsedCount, x.IsActive)).ToListAsync(ct);

    public async Task<CouponDto> SaveCouponAsync(int? id, CouponRequest request, CancellationToken ct)
    {
        var code = Check.Text(request.Code, 40, "mã giảm giá").ToUpperInvariant();
        if (code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw Check.Invalid("Mã chỉ gồm chữ không dấu, chữ số, dấu gạch nối và gạch dưới.");
        var max = request.Kind switch
        {
            CouponKind.FixedAmount => Check.MaxPrice,
            CouponKind.Percentage => 10_000,
            _ => throw Check.Invalid("Loại mã giảm giá không hợp lệ.")
        };
        if (request.DiscountValue <= 0 || request.DiscountValue > max
            || request.MaxDiscountVnd is <= 0 or > Check.MaxPrice
            || request.MinSubtotalVnd is < 0 or > Check.MaxPrice
            || request.UsageLimit is < 1 || request.StartsAt == default || request.EndsAt <= request.StartsAt)
            throw Check.Invalid("Giá trị, điều kiện hoặc thời hạn mã giảm giá không hợp lệ.");
        await using var transaction = await data.BeginWriteAsync(ct);
        if (await data.Coupons.AnyAsync(x => x.Code == code && x.Id != id, ct))
            throw Check.Conflict("Mã giảm giá đã tồn tại.");
        var coupon = id.HasValue
            ? await data.Coupons.FindAsync([id.Value], ct) ?? throw Check.Missing("Không tìm thấy mã giảm giá.")
            : new Coupon { Code = code };
        if (request.UsageLimit < coupon.UsedCount)
            throw Check.Invalid("Giới hạn thấp hơn số lượt đã sử dụng.");
        coupon.Code = code;
        coupon.Kind = request.Kind;
        coupon.DiscountValue = request.DiscountValue;
        coupon.MaxDiscountVnd = request.MaxDiscountVnd;
        coupon.MinSubtotalVnd = request.MinSubtotalVnd;
        coupon.StartsAtUtc = request.StartsAt.UtcDateTime;
        coupon.EndsAtUtc = request.EndsAt?.UtcDateTime;
        coupon.UsageLimit = request.UsageLimit;
        coupon.IsActive = request.IsActive;
        if (!id.HasValue)
            data.Coupons.Add(coupon);
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new CouponDto(coupon.Id, coupon.Code, coupon.Kind, coupon.DiscountValue, coupon.MaxDiscountVnd,
            coupon.MinSubtotalVnd, coupon.StartsAtUtc, coupon.EndsAtUtc, coupon.UsageLimit, coupon.UsedCount, coupon.IsActive);
    }

    public async Task DisableCouponAsync(int id, CancellationToken ct)
    {
        if (await data.Coupons.Where(x => x.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false), ct) == 0)
            throw Check.Missing("Không tìm thấy mã giảm giá.");
    }

    public async Task<PageResult<ReviewDto>> ReviewsAsync(int? page, int? pageSize, bool? visible, CancellationToken ct)
    {
        var pagination = Check.Page(page, pageSize);
        var query = data.Reviews.AsNoTracking().Where(x => visible == null || x.IsVisible == visible);
        var count = await query.CountAsync(ct);
        var reviews = await query.OrderByDescending(x => x.Id).Skip((pagination.Page - 1) * pagination.Size)
            .Take(pagination.Size).Select(x => new ReviewDto(x.Id, x.ProductId, x.User.FullName, x.Rating,
                x.Comment, x.IsVisible, x.CreatedAtUtc)).ToListAsync(ct);
        return new PageResult<ReviewDto>(reviews, count, pagination.Page, pagination.Size);
    }

    public async Task SetReviewVisibilityAsync(int id, bool visible, CancellationToken ct)
    {
        if (await data.Reviews.Where(x => x.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsVisible, visible), ct) == 0)
            throw Check.Missing("Không tìm thấy đánh giá.");
    }

    public async Task DeleteReviewAsync(int id, CancellationToken ct)
    {
        if (await data.Reviews.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0)
            throw Check.Missing("Không tìm thấy đánh giá.");
    }

    private string? ValidateImage(string? input)
    {
        var image = Check.Optional(input, 500, "đường dẫn ảnh");
        if (image is not null && !images.Exists(image))
            throw Check.Invalid("Vui lòng tải ảnh lên trước khi lưu.");
        return image;
    }
}
