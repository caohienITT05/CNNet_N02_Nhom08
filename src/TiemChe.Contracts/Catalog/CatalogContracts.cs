namespace TiemChe.Contracts.Catalog;

public enum ProductSort { Newest, Popular, PriceAscending, PriceDescending, Name }

public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record CategoryDto(int Id, string Name, int DisplayOrder, bool IsActive);
public sealed record SizeDto(int Id, string Name, long PriceVnd, bool IsActive);
public sealed record ToppingDto(int Id, string Name, long PriceVnd, string? ImagePath, bool IsActive);
public sealed record ProductCard(int Id, int CategoryId, string Name, string? ImagePath, long FromPriceVnd);
public sealed record ReviewDto(int Id, int ProductId, string Author, int Rating, string Comment,
    bool IsVisible, DateTime CreatedAtUtc);
public sealed record BannerDto(int Id, string Title, string ImagePath, int? ProductId,
    int DisplayOrder, DateTime StartsAtUtc, DateTime? EndsAtUtc, bool IsActive);
public sealed record ProductDetails(int Id, int CategoryId, string Name, string Description, string? ImagePath,
    bool IsActive, IReadOnlyList<SizeDto> Sizes, IReadOnlyList<ToppingDto> Toppings,
    IReadOnlyList<ReviewDto> Reviews, double? AverageRating, int ReviewCount);
public sealed record HomeDto(IReadOnlyList<BannerDto> Banners, IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<ProductCard> Popular, IReadOnlyList<ProductCard> Newest);
public sealed record ReviewRequest(int OrderItemId, int Rating, string? Comment);
public sealed record ReviewVisibilityRequest(bool IsVisible);
public sealed record CategoryRequest(string? Name, int DisplayOrder, bool IsActive);
public sealed record SizeRequest(int? Id, string? Name, long PriceVnd, bool IsActive);
public sealed record ToppingRequest(string? Name, long PriceVnd, string? ImagePath, bool IsActive);
public sealed record ProductRequest(int CategoryId, string? Name, string? Description, string? ImagePath,
    bool IsActive, IReadOnlyList<SizeRequest>? Sizes, IReadOnlyList<int>? ToppingIds);
public sealed record BannerRequest(string? Title, string? ImagePath, int? ProductId, int DisplayOrder,
    DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool IsActive);
public sealed record UploadedImage(string Path);
