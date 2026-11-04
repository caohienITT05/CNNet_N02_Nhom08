using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Auth;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Operations;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Wpf.Services;
using TiemChe.Wpf.ViewModels.Customer;

namespace TiemChe.Wpf.ViewModels.Staff;

public sealed partial class ShiftSummaryViewModel(Navigator navigation, OperationsMonitor monitor, ShopApi api,
    DesktopDialogs dialogs) : Screen
{
    public override string Title => "Tổng quan ca";
    [ObservableProperty] private ShiftSummary? summary;
    [ObservableProperty] private CashShiftDto? currentShift;
    [ObservableProperty] private CashShiftDto? lastClosedShift;
    [ObservableProperty] private string openingCash = "0";
    [ObservableProperty] private string countedCash = "";
    [ObservableProperty] private string closingNote = "";
    public bool HasOpenShift => CurrentShift is not null;
    public bool ShowOpenShiftForm => CurrentShift is null;
    public bool HasClosedShift => LastClosedShift is not null;

    protected override async Task LoadAsync(CancellationToken ct)
    {
        await monitor.RefreshAsync(ct);
        Summary = monitor.Summary;
        CurrentShift = (await api.GetAsync<CashShiftStateDto>("operations/shifts/current", ct)).Shift;
    }

    protected override void OnActivated() => monitor.Updated += OnUpdated;
    protected override void OnDeactivated() => monitor.Updated -= OnUpdated;
    private void OnUpdated() => Summary = monitor.Summary;

    partial void OnCurrentShiftChanged(CashShiftDto? value)
    {
        OnPropertyChanged(nameof(HasOpenShift));
        OnPropertyChanged(nameof(ShowOpenShiftForm));
    }
    partial void OnLastClosedShiftChanged(CashShiftDto? value) => OnPropertyChanged(nameof(HasClosedShift));

    [RelayCommand] private Task OpenStatusAsync(OrderStatus status) =>
        navigation.ShowAsync<OrdersViewModel>(x =>
        {
            x.Operations = true;
            x.Status = status;
        });

    [RelayCommand] private Task OpenShiftAsync() => RunAsync(async ct =>
    {
        var amount = Amount(OpeningCash, "Tiền đầu ca");
        CurrentShift = await api.PostAsync<CashShiftDto>("operations/shifts/open",
            new OpenCashShiftRequest(amount), ct);
        LastClosedShift = null;
        OpeningCash = "";
        Report("Đã mở ca làm việc.", false);
    });

    [RelayCommand] private Task CloseShiftAsync() => RunAsync(async ct =>
    {
        var shift = CurrentShift ?? throw new InvalidOperationException("Bạn chưa mở ca làm việc.");
        var amount = Amount(CountedCash, "Tiền kiểm đếm");
        if (amount != shift.ExpectedCashVnd && string.IsNullOrWhiteSpace(ClosingNote))
            throw new InvalidOperationException("Vui lòng ghi chú khi tiền kiểm đếm có chênh lệch.");
        if (!dialogs.Confirm($"Đóng ca với tiền dự kiến {Display.Money(shift.ExpectedCashVnd ?? 0)} và tiền kiểm đếm {Display.Money(amount)}?"))
            return;
        LastClosedShift = await api.PostAsync<CashShiftDto>("operations/shifts/close",
            new CloseCashShiftRequest(amount, ClosingNote), ct);
        CurrentShift = null;
        CountedCash = ClosingNote = "";
        Report("Đã đóng ca và lưu biên bản đối soát.", false);
    });

    [RelayCommand] private Task PrintShiftAsync() => RunAsync(_ =>
    {
        var shift = LastClosedShift ?? CurrentShift
            ?? throw new InvalidOperationException("Chưa có ca để in biên bản.");
        dialogs.PrintCashShift(shift);
        return Task.CompletedTask;
    });

    [RelayCommand] private Task OpenHistoryAsync() => navigation.ShowAsync<CashShiftsViewModel>();
}

public sealed partial class CashShiftsViewModel(ShopApi api, Session session, DesktopDialogs dialogs) : Screen
{
    private const int PageSize = 20;
    private int page = 1;
    private int totalCount;
    public override string Title => "Lịch sử ca";
    public bool IsAdmin => session.IsAdmin;
    public IReadOnlyList<Choice<int?>> StaffFilters { get; private set; } = [new("Tất cả nhân viên", null)];
    [ObservableProperty] private int? staffId;
    [ObservableProperty] private IReadOnlyList<CashShiftDto> shifts = [];
    [ObservableProperty] private CashShiftDto? selected;
    [ObservableProperty] private string pageLabel = "";

    protected override async Task LoadAsync(CancellationToken ct)
    {
        if (IsAdmin)
        {
            var staff = await api.GetAsync<PageResult<UserProfile>>(
                ShopApi.Query("admin/staff", ("page", 1), ("pageSize", 100)), ct);
            StaffFilters = [new("Tất cả nhân viên", null),
                .. staff.Items.Select(x => new Choice<int?>(x.FullName, x.Id))];
            OnPropertyChanged(nameof(StaffFilters));
        }
        await FetchAsync(1, ct);
    }

    private async Task FetchAsync(int number, CancellationToken ct)
    {
        var route = ShopApi.Query(IsAdmin ? "admin/shifts" : "operations/shifts",
            ("staffId", IsAdmin ? StaffId : null), ("page", number), ("pageSize", PageSize));
        var result = await api.GetAsync<PageResult<CashShiftDto>>(route, ct);
        Shifts = result.Items;
        Selected = Shifts.FirstOrDefault(x => x.Id == Selected?.Id) ?? Shifts.FirstOrDefault();
        page = result.Page;
        totalCount = result.TotalCount;
        PageLabel = $"Trang {page}/{Math.Max(1, (totalCount + PageSize - 1) / PageSize)} · {totalCount} ca";
    }

    [RelayCommand] private Task SearchAsync() => RunAsync(ct => FetchAsync(1, ct));
    [RelayCommand] private Task NextAsync() => RunAsync(ct => (long)page * PageSize < totalCount
        ? FetchAsync(page + 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task PreviousAsync() => RunAsync(ct => page > 1
        ? FetchAsync(page - 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task PrintAsync() => RunAsync(_ =>
    {
        var shift = Selected ?? throw new InvalidOperationException("Hãy chọn một ca làm việc.");
        dialogs.PrintCashShift(shift);
        return Task.CompletedTask;
    });
}

public sealed partial class CustomerLookupViewModel(ShopApi api, Navigator navigation) : Screen
{
    private const int PageSize = 20;
    private int page = 1;
    private int totalCount;
    public override string Title => "Tra cứu khách";
    [ObservableProperty] private string search = "";
    [ObservableProperty] private IReadOnlyList<StaffCustomerDto> customers = [];
    [ObservableProperty] private StaffCustomerDto? selectedCustomer;
    [ObservableProperty] private IReadOnlyList<OrderSummary> orders = [];
    [ObservableProperty] private OrderSummary? selectedOrder;
    [ObservableProperty] private string pageLabel = "";

    [RelayCommand] private Task SearchCustomersAsync() => RunAsync(async ct =>
    {
        Customers = await api.GetAsync<StaffCustomerDto[]>(ShopApi.Query("operations/customers",
            ("search", Search)), ct);
        SelectedCustomer = Customers.FirstOrDefault();
        if (Customers.Count == 0)
        {
            Orders = [];
            Report("Không tìm thấy khách hàng phù hợp.", false);
        }
        else
            await FetchOrdersAsync(1, ct);
    });

    partial void OnSelectedCustomerChanged(StaffCustomerDto? value)
    {
        page = 1;
        if (value is not null)
            _ = RunAsync(ct => FetchOrdersAsync(1, ct));
        else
            Orders = [];
    }

    private async Task FetchOrdersAsync(int number, CancellationToken ct)
    {
        var customer = SelectedCustomer ?? throw new InvalidOperationException("Hãy chọn một khách hàng.");
        var result = await api.GetAsync<PageResult<OrderSummary>>(ShopApi.Query(
            $"operations/customers/{customer.Id}/orders", ("page", number), ("pageSize", PageSize)), ct);
        Orders = result.Items;
        SelectedOrder = Orders.FirstOrDefault(x => x.Id == SelectedOrder?.Id) ?? Orders.FirstOrDefault();
        page = result.Page;
        totalCount = result.TotalCount;
        PageLabel = $"Trang {page}/{Math.Max(1, (totalCount + PageSize - 1) / PageSize)} · {totalCount} đơn";
    }

    [RelayCommand] private Task NextAsync() => RunAsync(ct => (long)page * PageSize < totalCount
        ? FetchOrdersAsync(page + 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task PreviousAsync() => RunAsync(ct => page > 1
        ? FetchOrdersAsync(page - 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task OpenOrderAsync() => RunAsync(async _ =>
    {
        var order = SelectedOrder ?? throw new InvalidOperationException("Hãy chọn một đơn hàng.");
        await navigation.ShowAsync<OrderViewModel>(x =>
        {
            x.OrderId = order.Id;
            x.Operations = true;
        });
    });
}

public enum AvailabilityTarget { Product, Size, Topping }

public sealed partial class AvailabilityRow : ObservableObject
{
    public int Id { get; init; }
    public AvailabilityTarget Target { get; init; }
    public string Name { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Category { get; init; } = "";
    [ObservableProperty] private bool isActive;
    internal Action<AvailabilityRow, bool>? ChangeRequested;
    private bool suppress;

    partial void OnIsActiveChanged(bool value)
    {
        if (!suppress)
            ChangeRequested?.Invoke(this, value);
    }

    internal void Set(bool value)
    {
        suppress = true;
        IsActive = value;
        suppress = false;
    }
}

public sealed record AvailabilityGroup(string Title, IReadOnlyList<AvailabilityRow> Rows);

public sealed partial class AvailabilityViewModel(ShopApi api) : Screen
{
    private sealed record ProductNode(string Category, AvailabilityRow Product, IReadOnlyList<AvailabilityRow> Sizes);
    private readonly List<ProductNode> products = [];
    private readonly List<AvailabilityRow> toppings = [];
    [ObservableProperty] private string search = "";
    [ObservableProperty] private IReadOnlyList<AvailabilityGroup> groups = [];
    [ObservableProperty] private IReadOnlyList<AvailabilityLogDto> logs = [];
    public override string Title => "Tình trạng món";

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var data = await api.GetAsync<AvailabilityCatalog>("operations/availability", ct);
        products.Clear();
        foreach (var product in data.Products)
        {
            var productRow = Row(product.Id, AvailabilityTarget.Product, product.Name,
                product.IsActive ? "Đang bán" : "Tạm ngừng bán", product.IsActive, product.CategoryName);
            var sizes = product.Sizes.Select(size => Row(size.Id, AvailabilityTarget.Size, size.Name,
                $"{Display.Money(size.PriceVnd)} · {(size.IsActive ? "Đang bán" : "Tạm ngừng bán")}",
                size.IsActive, product.CategoryName)).ToArray();
            products.Add(new ProductNode(product.CategoryName, productRow, sizes));
        }
        toppings.Clear();
        toppings.AddRange(data.Toppings.Select(topping => Row(topping.Id, AvailabilityTarget.Topping, topping.Name,
            $"{Display.Money(topping.PriceVnd)} · {(topping.IsActive ? "Đang bán" : "Tạm ngừng bán")}",
            topping.IsActive, "Topping")));
        Logs = (await api.GetAsync<PageResult<AvailabilityLogDto>>(
            ShopApi.Query("operations/availability/log", ("page", 1)), ct)).Items;
        ApplyFilter();
    }

    [RelayCommand] private void ApplySearch() => ApplyFilter();

    private void ApplyFilter()
    {
        var term = VietnameseText.Normalize(Search.Trim());
        var result = new List<AvailabilityGroup>();
        foreach (var category in products.GroupBy(x => x.Category))
        {
            var rows = new List<AvailabilityRow>();
            foreach (var node in category)
            {
                var productMatch = term.Length == 0 || VietnameseText.Normalize(node.Product.Name).Contains(term)
                    || VietnameseText.Normalize(node.Category).Contains(term);
                var sizes = node.Sizes.Where(x => productMatch || VietnameseText.Normalize(x.Name).Contains(term)).ToArray();
                if (!productMatch && sizes.Length == 0)
                    continue;
                rows.Add(node.Product);
                rows.AddRange(sizes);
            }
            if (rows.Count != 0)
                result.Add(new AvailabilityGroup(category.Key, rows));
        }
        var toppingRows = toppings.Where(x => term.Length == 0 || VietnameseText.Normalize(x.Name).Contains(term)).ToArray();
        if (toppingRows.Length != 0)
            result.Add(new AvailabilityGroup("Topping", toppingRows));
        Groups = result;
    }

    private AvailabilityRow Row(int id, AvailabilityTarget target, string name, string detail, bool active,
        string category)
    {
        var row = new AvailabilityRow
        {
            Id = id,
            Target = target,
            Name = name,
            Detail = detail,
            Category = category,
            IsActive = active
        };
        row.ChangeRequested = OnChangeRequested;
        return row;
    }

    private void OnChangeRequested(AvailabilityRow row, bool value) => _ = ChangeAsync(row, value);

    private async Task ChangeAsync(AvailabilityRow row, bool value)
    {
        var succeeded = false;
        await RunAsync(async ct =>
        {
            await api.PutAsync(Route(row), new AvailabilityRequest(value), ct);
            Logs = (await api.GetAsync<PageResult<AvailabilityLogDto>>(
                ShopApi.Query("operations/availability/log", ("page", 1)), ct)).Items;
            succeeded = true;
        });
        if (!succeeded)
            row.Set(!value);
        else
            Report("Đã cập nhật tình trạng món.", false);
    }

    private static string Route(AvailabilityRow row) => row.Target switch
    {
        AvailabilityTarget.Product => $"operations/products/{row.Id}/availability",
        AvailabilityTarget.Size => $"operations/product-sizes/{row.Id}/availability",
        _ => $"operations/toppings/{row.Id}/availability"
    };
}
