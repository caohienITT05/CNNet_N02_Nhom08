using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TiemChe.Contracts.Auth;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Management;
using TiemChe.Wpf.Services;

namespace TiemChe.Wpf.ViewModels.Admin;

public abstract partial class AdminListScreen<T> : Screen
{
    protected const int PageSize = 20;
    protected int Page { get; private set; } = 1;
    private int total;
    [ObservableProperty] private IReadOnlyList<T> items = [];
    [ObservableProperty] private string pageLabel = "";
    protected abstract Task<PageResult<T>> FetchAsync(int page, CancellationToken ct);

    protected override Task LoadAsync(CancellationToken ct) => LoadPageAsync(Page, ct);

    protected async Task LoadPageAsync(int page, CancellationToken ct)
    {
        var result = await FetchAsync(page, ct);
        if (result.Items.Count == 0 && page > 1)
        {
            var last = Math.Max(1, (result.TotalCount + PageSize - 1) / PageSize);
            result = await FetchAsync(last, ct);
        }
        Items = result.Items;
        Page = result.Page;
        total = result.TotalCount;
        PageLabel = $"Trang {Page}/{Math.Max(1, (total + PageSize - 1) / PageSize)} · {total} mục";
    }

    [RelayCommand] private Task SearchAsync() => RunAsync(ct => LoadPageAsync(1, ct));
    [RelayCommand] private Task NextAsync() => RunAsync(ct => (long)Page * PageSize < total
        ? LoadPageAsync(Page + 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task PreviousAsync() => RunAsync(ct => Page > 1 ? LoadPageAsync(Page - 1, ct) : Task.CompletedTask);
}

public sealed partial class AccountsAdminViewModel(ShopApi api, DesktopDialogs dialogs) : AdminListScreen<UserProfile>
{
    public bool Staff { get; set; }
    public override string Title => Staff ? "Tài khoản nhân viên" : "Khách hàng";
    [ObservableProperty] private UserProfile? selected;
    [ObservableProperty] private string keyword = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string phone = "";

    protected override Task<PageResult<UserProfile>> FetchAsync(int page, CancellationToken ct) =>
        api.GetAsync<PageResult<UserProfile>>(ShopApi.Query(Staff ? "admin/staff" : "admin/customers",
            ("search", Keyword), ("page", page), ("pageSize", PageSize)), ct);

    [RelayCommand] private Task ToggleAsync() => RunAsync(async ct =>
    {
        var account = Selected ?? throw new InvalidOperationException("Hãy chọn tài khoản.");
        var activate = !account.IsActive;
        if (!dialogs.Confirm($"{(activate ? "Mở khóa" : "Khóa")} tài khoản {account.Email}?"))
            return;
        await api.PutAsync($"admin/accounts/{account.Id}/status", new AccountStatusRequest(activate), ct);
        await LoadPageAsync(Page, ct);
        Selected = Items.FirstOrDefault(x => x.Id == account.Id);
        Report("Đã cập nhật trạng thái tài khoản.", false);
    });

    [RelayCommand] private Task CreateStaffAsync() => RunAsync(async ct =>
    {
        await api.PostAsync("admin/staff", new RegisterRequest(Email, Password, Name, Phone), ct);
        Email = Password = Name = Phone = "";
        Keyword = "";
        await LoadPageAsync(1, ct);
        Report("Đã tạo tài khoản nhân viên.", false);
    });
}

public sealed partial class ReviewsAdminViewModel(ShopApi api, DesktopDialogs dialogs) : AdminListScreen<ReviewDto>
{
    public bool Operations { get; set; }
    public override string Title => Operations ? "Đánh giá" : "Quản lý đánh giá";
    public IReadOnlyList<Choice<bool?>> Filters { get; } = [new("Tất cả đánh giá", null), new("Đang hiển thị", true), new("Đã ẩn", false)];
    [ObservableProperty] private bool? visible;
    [ObservableProperty] private ReviewDto? selected;

    protected override Task<PageResult<ReviewDto>> FetchAsync(int page, CancellationToken ct) =>
        api.GetAsync<PageResult<ReviewDto>>(ShopApi.Query(Operations ? "operations/reviews" : "admin/reviews", ("visible", Visible),
            ("page", page), ("pageSize", PageSize)), ct);

    [RelayCommand] private Task ToggleAsync() => RunAsync(async ct =>
    {
        var review = Selected ?? throw new InvalidOperationException("Hãy chọn một đánh giá.");
        await api.PutAsync($"{(Operations ? "operations" : "admin")}/reviews/{review.Id}/visibility",
            new ReviewVisibilityRequest(!review.IsVisible), ct);
        await LoadPageAsync(Page, ct);
        Selected = Items.FirstOrDefault(x => x.Id == review.Id);
        Report("Đã cập nhật đánh giá.", false);
    });

    [RelayCommand] private Task DeleteAsync() => RunAsync(async ct =>
    {
        if (Operations)
            throw new InvalidOperationException("Nhân viên không có quyền xóa đánh giá.");
        var review = Selected ?? throw new InvalidOperationException("Hãy chọn một đánh giá.");
        if (!dialogs.Confirm($"Xóa đánh giá của {review.Author}?"))
            return;
        await api.DeleteAsync($"admin/reviews/{review.Id}", ct);
        await LoadPageAsync(Page, ct);
        Report("Đã xóa đánh giá.", false);
    });
}

public sealed partial class DashboardViewModel(ShopApi api, DesktopDialogs dialogs) : Screen
{
    public override string Title => "Tổng quan cửa hàng";
    [ObservableProperty] private DateTime? from = Display.LocalTime(DateTime.UtcNow).Date.AddDays(-29);
    [ObservableProperty] private DateTime? to = Display.LocalTime(DateTime.UtcNow).Date;
    [ObservableProperty] private bool byMonth;
    [ObservableProperty] private DashboardDto? dashboard;
    [ObservableProperty] private ISeries[] series = [];
    [ObservableProperty] private Axis[] xAxes = [];
    public Axis[] YAxes { get; } = [new Axis { MinLimit = 0, Labeler = value => Display.Money((long)value) }];

    protected override async Task LoadAsync(CancellationToken ct)
    {
        Dashboard = await api.GetAsync<DashboardDto>(Route("admin/dashboard"), ct);
        Series = [new ColumnSeries<long>
        {
            Name = "Doanh thu", Values = Dashboard.Revenue.Select(x => x.RevenueVnd).ToArray(),
            Fill = new SolidColorPaint(SKColor.Parse("#25724B")), Stroke = null, MaxBarWidth = 32
        }];
        XAxes = [new Axis
        {
            Labels = Dashboard.Revenue.Select(x => x.Period).ToArray(),
            LabelsRotation = Dashboard.Revenue.Count > 10 ? -35 : 0
        }];
    }

    [RelayCommand] private Task ExportAsync() => RunAsync(async ct =>
    {
        var file = await api.DownloadAsync(Route("admin/reports/revenue.xlsx"), ct);
        if (await dialogs.SaveExcelAsync(file, ct))
            Report("Đã lưu báo cáo Excel.", false);
    });

    private string Route(string path)
    {
        if (From is not DateTime start || To is not DateTime end || start.Date > end.Date)
            throw new InvalidOperationException("Hãy chọn khoảng ngày báo cáo hợp lệ.");
        return ShopApi.Query(path, ("from", start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            ("to", end.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), ("byMonth", ByMonth));
    }
}
