using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Management;
using TiemChe.Domain.Enums;
using TiemChe.Wpf.Services;

namespace TiemChe.Wpf.ViewModels.Admin;

public sealed record EditorRow<T>(int Id, string Label, T Value);

public abstract partial class EditorScreen<T>(ShopApi api, DesktopDialogs dialogs) : Screen where T : class
{
    [ObservableProperty] private IReadOnlyList<EditorRow<T>> rows = [];
    [ObservableProperty] private EditorRow<T>? selected;
    [ObservableProperty] private bool isActive = true;
    protected abstract string Route { get; }
    protected ShopApi Api => api;

    partial void OnSelectedChanged(EditorRow<T>? value) => Populate(value?.Value);

    protected abstract EditorRow<T> Row(T entity);
    protected abstract void Populate(T? entity);
    protected abstract object Request();

    protected override Task LoadAsync(CancellationToken ct) => FetchAsync(Selected?.Id, ct);

    private async Task FetchAsync(int? id, CancellationToken ct)
    {
        Rows = (await api.GetAsync<T[]>(Route, ct)).Select(Row).ToArray();
        Selected = Rows.FirstOrDefault(x => x.Id == id);
        if (Selected is null)
            Populate(null);
    }

    [RelayCommand] private void Clear()
    {
        Selected = null;
        Populate(null);
    }

    [RelayCommand] private Task SaveAsync() => RunAsync(async ct =>
    {
        var body = Request();
        var value = Selected is { } item
            ? await api.PutAsync<T>($"{Route}/{item.Id}", body, ct)
            : await api.PostAsync<T>(Route, body, ct);
        await FetchAsync(Row(value).Id, ct);
        Report("Đã lưu thay đổi.", false);
    });

    [RelayCommand] private Task RemoveAsync() => RunAsync(async ct =>
    {
        var item = Selected ?? throw new InvalidOperationException("Hãy chọn một mục trong danh sách.");
        if (!dialogs.Confirm($"Xóa hoặc ngừng hiển thị {item.Label}?"))
            return;
        await api.DeleteAsync($"{Route}/{item.Id}", ct);
        await FetchAsync(null, ct);
        Report("Đã cập nhật danh sách.", false);
    });

    protected async Task<string?> PickImageAsync(CancellationToken ct)
    {
        var file = dialogs.PickImage();
        return file is null ? null : await api.UploadImageAsync(file, ct);
    }
}

public sealed partial class CategoriesAdminViewModel(ShopApi api, DesktopDialogs dialogs)
    : EditorScreen<CategoryDto>(api, dialogs)
{
    public override string Title => "Danh mục";
    protected override string Route => "admin/categories";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string displayOrder = "0";
    protected override EditorRow<CategoryDto> Row(CategoryDto value) =>
        new(value.Id, value.IsActive ? value.Name : $"{value.Name} · Đã ẩn", value);

    protected override void Populate(CategoryDto? value)
    {
        Name = value?.Name ?? "";
        DisplayOrder = (value?.DisplayOrder ?? 0).ToString();
        IsActive = value?.IsActive ?? true;
    }

    protected override object Request() => new CategoryRequest(Name,
        WholeNumber(DisplayOrder, "Thứ tự", 0, int.MaxValue), IsActive);
}

public sealed partial class ToppingsAdminViewModel(ShopApi api, DesktopDialogs dialogs)
    : EditorScreen<ToppingDto>(api, dialogs)
{
    public override string Title => "Topping";
    protected override string Route => "admin/toppings";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string price = "";
    [ObservableProperty] private string? imagePath;
    protected override EditorRow<ToppingDto> Row(ToppingDto value) =>
        new(value.Id, $"{value.Name} · {Display.Money(value.PriceVnd)}{(value.IsActive ? "" : " · Đã ẩn")}", value);

    protected override void Populate(ToppingDto? value)
    {
        Name = value?.Name ?? "";
        Price = value?.PriceVnd.ToString() ?? "";
        ImagePath = value?.ImagePath;
        IsActive = value?.IsActive ?? true;
    }

    protected override object Request() => new ToppingRequest(Name, Amount(Price, "Giá"), ImagePath, IsActive);

    [RelayCommand] private Task ImageAsync() => RunAsync(async ct => ImagePath = await PickImageAsync(ct) ?? ImagePath);
    [RelayCommand] private void ClearImage() => ImagePath = null;
}

public sealed partial class CouponsAdminViewModel(ShopApi api, DesktopDialogs dialogs)
    : EditorScreen<CouponDto>(api, dialogs)
{
    public override string Title => "Mã giảm giá";
    protected override string Route => "admin/coupons";
    public IReadOnlyList<Choice<CouponKind>> Kinds { get; } =
        Enum.GetValues<CouponKind>().Select(x => new Choice<CouponKind>(Display.Label(x), x)).ToArray();
    [ObservableProperty] private string code = "";
    [ObservableProperty] private CouponKind kind = CouponKind.FixedAmount;
    [ObservableProperty] private string discount = "";
    [ObservableProperty] private string minSubtotal = "0";
    [ObservableProperty] private string maxDiscount = "";
    [ObservableProperty] private string usageLimit = "";
    [ObservableProperty] private DateTime? startsAt = Display.LocalTime(DateTime.UtcNow).Date;
    [ObservableProperty] private DateTime? endsAt;
    public string ValueHint => Kind == CouponKind.Percentage ? "Mức giảm (%)" : "Mức giảm (đồng)";
    partial void OnKindChanged(CouponKind value) => OnPropertyChanged(nameof(ValueHint));

    protected override EditorRow<CouponDto> Row(CouponDto value) => new(value.Id,
        $"{value.Code} · Đã dùng {value.UsedCount}{(value.IsActive ? "" : " · Đã ẩn")}", value);

    protected override void Populate(CouponDto? value)
    {
        Code = value?.Code ?? "";
        Kind = value?.Kind ?? CouponKind.FixedAmount;
        Discount = value is null ? "" : value.Kind == CouponKind.Percentage
            ? (value.DiscountValue / 100m).ToString("0.##", Display.Vietnamese)
            : value.DiscountValue.ToString();
        MinSubtotal = (value?.MinSubtotalVnd ?? 0).ToString();
        MaxDiscount = value?.MaxDiscountVnd?.ToString() ?? "";
        UsageLimit = value?.UsageLimit?.ToString() ?? "";
        StartsAt = value is null ? Display.LocalTime(DateTime.UtcNow).Date : Display.LocalTime(value.StartsAtUtc).Date;
        EndsAt = value?.EndsAtUtc is DateTime end ? Display.LocalTime(end).AddTicks(-1).Date : null;
        IsActive = value?.IsActive ?? true;
    }

    protected override object Request()
    {
        var discountValue = Kind == CouponKind.FixedAmount ? Amount(Discount, "Mức giảm") : ParsePercent();
        return new CouponRequest(Code, Kind, discountValue,
            string.IsNullOrWhiteSpace(MaxDiscount) ? null : Amount(MaxDiscount, "Mức giảm tối đa"),
            Amount(MinSubtotal, "Giá trị đơn tối thiểu"),
            EditorDates.Start(StartsAt),
            EditorDates.End(EndsAt),
            string.IsNullOrWhiteSpace(UsageLimit) ? null : WholeNumber(UsageLimit, "Giới hạn lượt dùng", 1, int.MaxValue),
            IsActive);
    }

    private long ParsePercent()
    {
        if (!decimal.TryParse(Discount.Replace('.', ','), System.Globalization.NumberStyles.AllowDecimalPoint,
                Display.Vietnamese, out var percent) || percent is <= 0 or > 100 || percent * 100 != decimal.Truncate(percent * 100))
            throw new InvalidOperationException("Phần trăm giảm phải lớn hơn 0, tối đa 100 và có nhiều nhất hai chữ số thập phân.");
        return (long)(percent * 100);
    }
}

public sealed partial class BannersAdminViewModel(ShopApi api, DesktopDialogs dialogs)
    : EditorScreen<BannerDto>(api, dialogs)
{
    public override string Title => "Banner khuyến mãi";
    protected override string Route => "admin/banners";
    [ObservableProperty] private string bannerTitle = "";
    [ObservableProperty] private string? imagePath;
    [ObservableProperty] private string productId = "";
    [ObservableProperty] private string displayOrder = "0";
    [ObservableProperty] private DateTime? startsAt = Display.LocalTime(DateTime.UtcNow).Date;
    [ObservableProperty] private DateTime? endsAt;

    protected override EditorRow<BannerDto> Row(BannerDto value) =>
        new(value.Id, value.IsActive ? value.Title : $"{value.Title} · Đã ẩn", value);

    protected override void Populate(BannerDto? value)
    {
        BannerTitle = value?.Title ?? "";
        ImagePath = value?.ImagePath;
        ProductId = value?.ProductId?.ToString() ?? "";
        DisplayOrder = (value?.DisplayOrder ?? 0).ToString();
        StartsAt = value is null ? Display.LocalTime(DateTime.UtcNow).Date : Display.LocalTime(value.StartsAtUtc).Date;
        EndsAt = value?.EndsAtUtc is DateTime end ? Display.LocalTime(end).AddTicks(-1).Date : null;
        IsActive = value?.IsActive ?? true;
    }

    protected override object Request() => new BannerRequest(BannerTitle, ImagePath,
        string.IsNullOrWhiteSpace(ProductId) ? null : WholeNumber(ProductId, "Mã món", 1, int.MaxValue),
        WholeNumber(DisplayOrder, "Thứ tự", 0, int.MaxValue), EditorDates.Start(StartsAt),
        EditorDates.End(EndsAt), IsActive);

    [RelayCommand] private Task ImageAsync() => RunAsync(async ct => ImagePath = await PickImageAsync(ct) ?? ImagePath);
}

internal static class EditorDates
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);
    internal static DateTimeOffset Start(DateTime? date) => ToLocal(
        date ?? throw new InvalidOperationException("Hãy chọn ngày bắt đầu."));
    internal static DateTimeOffset? End(DateTime? date) => date is DateTime value ? ToLocal(value.AddDays(1)) : null;
    private static DateTimeOffset ToLocal(DateTime date) => new(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), Offset);
}
