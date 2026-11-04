using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Shopping;
using TiemChe.Wpf.Services;
using TiemChe.Wpf.ViewModels.Auth;

namespace TiemChe.Wpf.ViewModels.Customer;

public sealed partial class HomeViewModel(ShopApi api, Navigator navigation) : Screen
{
    public override string Title => "Trang chủ";
    [ObservableProperty] private IReadOnlyList<CategoryDto> categories = [];
    [ObservableProperty] private IReadOnlyList<ProductCard> popular = [];
    [ObservableProperty] private IReadOnlyList<ProductCard> newest = [];
    [ObservableProperty] private IReadOnlyList<BannerDto> banners = [];

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var home = await api.GetAsync<HomeDto>("catalog/home", ct);
        Categories = home.Categories;
        Popular = home.Popular;
        Newest = home.Newest;
        Banners = home.Banners;
    }

    [RelayCommand] private Task BrowseAsync() => navigation.ShowAsync<CatalogViewModel>();
    [RelayCommand] private Task BrowseCategoryAsync(CategoryDto? category) =>
        navigation.ShowAsync<CatalogViewModel>(x => x.CategoryId = category?.Id ?? 0);
    [RelayCommand] private Task OpenProductAsync(ProductCard? item) =>
        item is null ? Task.CompletedTask : navigation.ShowAsync<ProductViewModel>(x => x.ProductId = item.Id);
    [RelayCommand] private Task OpenBannerAsync(BannerDto? banner) => banner?.ProductId is int id
        ? navigation.ShowAsync<ProductViewModel>(x => x.ProductId = id)
        : BrowseAsync();
}

public sealed partial class CatalogViewModel(ShopApi api, Navigator navigation) : Screen
{
    private const int PageSize = 12;
    public override string Title => "Thực đơn";
    public IReadOnlyList<Choice<ProductSort>> Sorts { get; } = Enum.GetValues<ProductSort>()
        .Select(x => new Choice<ProductSort>(Display.Label(x), x)).ToArray();
    [ObservableProperty] private ProductSort sort = ProductSort.Newest;
    [ObservableProperty] private IReadOnlyList<CategoryDto> categories = [];
    [ObservableProperty] private int categoryId;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string minPrice = "";
    [ObservableProperty] private string maxPrice = "";
    [ObservableProperty] private IReadOnlyList<ProductCard> items = [];
    [ObservableProperty] private int page = 1;
    [ObservableProperty] private int totalCount;
    public string PageLabel => $"Trang {Page}/{Math.Max(1, (TotalCount + PageSize - 1) / PageSize)} · {TotalCount} món";

    partial void OnPageChanged(int value) => OnPropertyChanged(nameof(PageLabel));
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(PageLabel));

    protected override async Task LoadAsync(CancellationToken ct)
    {
        if (Categories.Count == 0)
        {
            var selected = CategoryId;
            var rows = await api.GetAsync<CategoryDto[]>("catalog/categories", ct);
            Categories = [new CategoryDto(0, "Tất cả danh mục", 0, true), .. rows];
            CategoryId = Categories.Any(x => x.Id == selected) ? selected : 0;
        }
        await FetchAsync(Page, ct);
    }

    private async Task FetchAsync(int page, CancellationToken ct)
    {
        var min = string.IsNullOrWhiteSpace(MinPrice) ? (long?)null : Amount(MinPrice, "Giá thấp nhất");
        var max = string.IsNullOrWhiteSpace(MaxPrice) ? (long?)null : Amount(MaxPrice, "Giá cao nhất");
        if (min > max)
            throw new InvalidOperationException("Giá thấp nhất phải nhỏ hơn hoặc bằng giá cao nhất.");
        var result = await api.GetAsync<PageResult<ProductCard>>(ShopApi.Query("catalog/products",
            ("search", Search), ("categoryId", CategoryId == 0 ? null : CategoryId),
            ("minPrice", min), ("maxPrice", max), ("sort", Sort), ("page", page), ("pageSize", PageSize)), ct);
        Items = result.Items;
        TotalCount = result.TotalCount;
        Page = result.Page;
        if (Items.Count == 0)
            Report("Không tìm thấy món phù hợp.", false);
    }

    [RelayCommand] private Task FilterAsync() => RunAsync(ct => FetchAsync(1, ct));
    [RelayCommand] private Task NextAsync() => RunAsync(ct => (long)Page * PageSize < TotalCount
        ? FetchAsync(Page + 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task PreviousAsync() => RunAsync(ct => Page > 1 ? FetchAsync(Page - 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task OpenProductAsync(ProductCard? item) =>
        item is null ? Task.CompletedTask : navigation.ShowAsync<ProductViewModel>(x => x.ProductId = item.Id);
}

public sealed partial class ToppingChoice(ToppingDto topping) : ObservableObject
{
    public ToppingDto Topping { get; } = topping;
    [ObservableProperty] private bool selected;
}

public sealed partial class ProductViewModel(ShopApi api, Session session, Navigator navigation) : Screen
{
    public override string Title => "Chi tiết món";
    public int ProductId { get; set; }
    private int? preferredSizeId;
    private IReadOnlySet<int>? preferredToppings;
    [ObservableProperty] private ProductDetails? product;
    [ObservableProperty] private SizeDto? selectedSize;
    [ObservableProperty] private IReadOnlyList<ToppingChoice> toppings = [];
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private bool isFavorite;
    public string FavoriteLabel => IsFavorite ? "Bỏ yêu thích" : "Yêu thích";
    public string PriceLabel => Display.Money(((SelectedSize?.PriceVnd ?? 0) +
        Toppings.Where(x => x.Selected).Sum(x => x.Topping.PriceVnd)) *
        (int.TryParse(Quantity, out var count) && count is >= 1 and <= 99 ? count : 1));

    partial void OnSelectedSizeChanged(SizeDto? value) => OnPropertyChanged(nameof(PriceLabel));
    partial void OnQuantityChanged(string value) => OnPropertyChanged(nameof(PriceLabel));
    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteLabel));

    protected override async Task LoadAsync(CancellationToken ct)
    {
        Product = await api.GetAsync<ProductDetails>($"catalog/products/{ProductId}", ct);
        SelectedSize = Product.Sizes.FirstOrDefault(x => x.Id == preferredSizeId) ?? Product.Sizes.FirstOrDefault();
        Toppings = Product.Toppings.Select(x => new ToppingChoice(x)
            { Selected = preferredToppings?.Contains(x.Id) == true }).ToArray();
        foreach (var topping in Toppings)
            topping.PropertyChanged += (_, _) => OnPropertyChanged(nameof(PriceLabel));
        OnPropertyChanged(nameof(PriceLabel));
        if (session.IsCustomer)
        {
            var items = await api.GetAsync<ProductCard[]>("customer/wishlist", ct);
            IsFavorite = items.Any(x => x.Id == ProductId);
        }
    }

    [RelayCommand] private Task BackAsync() => navigation.ShowAsync<CatalogViewModel>();
    [RelayCommand] private Task OpenCartAsync() => session.IsCustomer
        ? navigation.ShowAsync<CartViewModel>() : PromptLoginAsync();

    [RelayCommand] private Task AddToCartAsync() => RunAsync(async ct =>
    {
        if (!session.IsCustomer)
        {
            await PromptLoginAsync();
            return;
        }
        var size = SelectedSize ?? throw new InvalidOperationException("Hãy chọn size.");
        var count = WholeNumber(Quantity, "Số lượng", 1, 99);
        var cart = await api.GetAsync<CartDto>("customer/cart", ct);
        await api.PostAsync<CartDto>("customer/cart/items", new AddCartItemRequest(size.Id, count,
            Toppings.Where(x => x.Selected).Select(x => x.Topping.Id).ToArray(), cart.Version), ct);
        Report("Đã thêm món vào giỏ.", false);
    });

    [RelayCommand] private Task FavoriteAsync() => RunAsync(async ct =>
    {
        if (!session.IsCustomer)
        {
            await PromptLoginAsync();
            return;
        }
        if (IsFavorite)
            await api.DeleteAsync($"customer/wishlist/{ProductId}", ct);
        else
            await api.PutAsync($"customer/wishlist/{ProductId}", new { }, ct);
        IsFavorite = !IsFavorite;
    });

    private Task PromptLoginAsync()
    {
        var id = ProductId;
        var sizeId = SelectedSize?.Id;
        var toppingIds = Toppings.Where(x => x.Selected).Select(x => x.Topping.Id).ToHashSet();
        var count = Quantity;
        return navigation.ShowAsync<AuthViewModel>(x =>
        {
            x.InitialMessage = "Đăng nhập tài khoản khách hàng để tiếp tục.";
            x.AfterCustomerLogin = () => navigation.ShowAsync<ProductViewModel>(p =>
            {
                p.ProductId = id;
                p.preferredSizeId = sizeId;
                p.preferredToppings = toppingIds;
                p.Quantity = count;
            });
        });
    }
}

public sealed partial class WishlistViewModel(ShopApi api, Navigator navigation) : Screen
{
    public override string Title => "Món yêu thích";
    [ObservableProperty] private IReadOnlyList<ProductCard> items = [];

    protected override async Task LoadAsync(CancellationToken ct)
    {
        Items = await api.GetAsync<ProductCard[]>("customer/wishlist", ct);
        if (Items.Count == 0)
            Report("Bạn chưa lưu món yêu thích.", false);
    }

    [RelayCommand] private Task OpenProductAsync(ProductCard? item) =>
        item is null ? Task.CompletedTask : navigation.ShowAsync<ProductViewModel>(x => x.ProductId = item.Id);
    [RelayCommand] private Task BrowseAsync() => navigation.ShowAsync<CatalogViewModel>();
}
