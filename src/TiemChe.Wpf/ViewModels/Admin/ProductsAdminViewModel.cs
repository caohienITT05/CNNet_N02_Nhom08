using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Catalog;
using TiemChe.Wpf.Services;
using TiemChe.Wpf.ViewModels.Customer;

namespace TiemChe.Wpf.ViewModels.Admin;

public sealed partial class EditableSize : ObservableObject
{
    public int? Id { get; init; }
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string price = "";
    [ObservableProperty] private bool isActive = true;
}

public sealed partial class ProductsAdminViewModel(ShopApi api, DesktopDialogs dialogs) : Screen
{
    private const int PageSize = 15;
    private int page = 1;
    private int count;
    private int? editingId;
    public override string Title => "Quản lý món và size";
    [ObservableProperty] private IReadOnlyList<ProductCard> products = [];
    [ObservableProperty] private IReadOnlyList<CategoryDto> categories = [];
    [ObservableProperty] private IReadOnlyList<ToppingChoice> toppings = [];
    [ObservableProperty] private ProductCard? selected;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string pageLabel = "";
    [ObservableProperty] private string editorLabel = "Thêm món mới";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string? imagePath;
    [ObservableProperty] private int categoryId;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private EditableSize? selectedSize;
    public ObservableCollection<EditableSize> Sizes { get; } = [];

    protected override async Task LoadAsync(CancellationToken ct)
    {
        Categories = await api.GetAsync<CategoryDto[]>("admin/categories", ct);
        Toppings = (await api.GetAsync<ToppingDto[]>("admin/toppings", ct)).Select(x => new ToppingChoice(x)).ToArray();
        await FetchAsync(page, ct);
        if (editingId.HasValue)
            await EditAsync(editingId.Value, ct);
        else
            New();
    }

    private async Task FetchAsync(int next, CancellationToken ct)
    {
        var result = await api.GetAsync<PageResult<ProductCard>>(ShopApi.Query("admin/products",
            ("search", Search), ("page", next), ("pageSize", PageSize)), ct);
        Products = result.Items;
        count = result.TotalCount;
        page = result.Page;
        PageLabel = $"Trang {page}/{Math.Max(1, (count + PageSize - 1) / PageSize)}";
    }

    [RelayCommand] private Task SearchProductsAsync() => RunAsync(ct => FetchAsync(1, ct));
    [RelayCommand] private Task NextAsync() => RunAsync(ct => (long)page * PageSize < count
        ? FetchAsync(page + 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task PreviousAsync() => RunAsync(ct => page > 1 ? FetchAsync(page - 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task EditSelectedAsync() => RunAsync(ct => EditAsync(
        Selected?.Id ?? throw new InvalidOperationException("Hãy chọn món cần sửa."), ct));

    private async Task EditAsync(int id, CancellationToken ct)
    {
        var product = await api.GetAsync<ProductDetails>($"admin/products/{id}", ct);
        editingId = id;
        EditorLabel = $"Chỉnh sửa món #{id}";
        Name = product.Name;
        CategoryId = product.CategoryId;
        Description = product.Description;
        ImagePath = product.ImagePath;
        IsActive = product.IsActive;
        Sizes.Clear();
        foreach (var size in product.Sizes)
            Sizes.Add(new EditableSize { Id = size.Id, Name = size.Name, Price = size.PriceVnd.ToString(), IsActive = size.IsActive });
        foreach (var topping in Toppings)
            topping.Selected = product.Toppings.Any(x => x.Id == topping.Topping.Id);
    }

    [RelayCommand] private void New()
    {
        Selected = null;
        editingId = null;
        EditorLabel = "Thêm món mới";
        Name = Description = "";
        CategoryId = Categories.FirstOrDefault()?.Id ?? 0;
        ImagePath = null;
        IsActive = true;
        Sizes.Clear();
        Sizes.Add(new EditableSize { Name = "Tiêu chuẩn" });
        foreach (var topping in Toppings)
            topping.Selected = false;
    }

    [RelayCommand] private void AddSize()
    {
        if (Sizes.Count >= 20)
        {
            Report("Tối đa 20 size cho mỗi món.");
            return;
        }
        var size = new EditableSize();
        Sizes.Add(size);
        SelectedSize = size;
    }

    [RelayCommand] private void RemoveSize()
    {
        if (SelectedSize is { } size)
            Sizes.Remove(size);
    }

    [RelayCommand] private Task ImageAsync() => RunAsync(async ct =>
    {
        if (dialogs.PickImage() is { } file)
            ImagePath = await api.UploadImageAsync(file, ct);
    });

    [RelayCommand] private void ClearImage() => ImagePath = null;

    [RelayCommand] private Task SaveAsync() => RunAsync(async ct =>
    {
        var request = new ProductRequest(CategoryId, Name, Description, ImagePath, IsActive,
            Sizes.Select(x => new SizeRequest(x.Id, x.Name, Amount(x.Price, "Giá size"), x.IsActive)).ToArray(),
            Toppings.Where(x => x.Selected).Select(x => x.Topping.Id).ToArray());
        var result = editingId is int id
            ? await api.PutAsync<ProductDetails>($"admin/products/{id}", request, ct)
            : await api.PostAsync<ProductDetails>("admin/products", request, ct);
        await FetchAsync(1, ct);
        await EditAsync(result.Id, ct);
        Selected = Products.FirstOrDefault(x => x.Id == result.Id);
        Report("Đã lưu món.", false);
    });

    [RelayCommand] private Task DisableAsync() => RunAsync(async ct =>
    {
        var id = editingId ?? Selected?.Id ?? throw new InvalidOperationException("Hãy chọn món cần ngừng bán.");
        if (!dialogs.Confirm($"Ngừng bán món #{id}?"))
            return;
        await api.DeleteAsync($"admin/products/{id}", ct);
        await FetchAsync(page, ct);
        await EditAsync(id, ct);
        Report("Đã ngừng bán món.", false);
    });
}
