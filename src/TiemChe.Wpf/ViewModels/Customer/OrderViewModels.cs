using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Wpf.Services;

namespace TiemChe.Wpf.ViewModels.Customer;

public sealed partial class OrdersViewModel(ShopApi api, Navigator navigation) : Screen
{
    private const int PageSize = 20;
    public bool Operations { get; set; }
    public override string Title => Operations ? "Quản lý đơn hàng" : "Đơn của tôi";
    public IReadOnlyList<Choice<OrderStatus?>> Statuses { get; } =
        [new("Tất cả trạng thái", null), .. Enum.GetValues<OrderStatus>().Select(x => new Choice<OrderStatus?>(Display.Label(x), x))];
    [ObservableProperty] private OrderStatus? status;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private IReadOnlyList<OrderSummary> orders = [];
    [ObservableProperty] private OrderSummary? selected;
    [ObservableProperty] private string pageLabel = "";
    private int page = 1;
    private int totalCount;

    protected override Task LoadAsync(CancellationToken ct) => FetchAsync(page, ct);

    private async Task FetchAsync(int number, CancellationToken ct)
    {
        var route = Operations ? "operations/orders" : "customer/orders";
        var result = await api.GetAsync<PageResult<OrderSummary>>(ShopApi.Query(route, ("status", Status),
            ("search", Search), ("page", number), ("pageSize", PageSize)), ct);
        Orders = result.Items;
        Selected = Orders.FirstOrDefault();
        page = result.Page;
        totalCount = result.TotalCount;
        PageLabel = $"Trang {page}/{Math.Max(1, (totalCount + PageSize - 1) / PageSize)} · {totalCount} đơn";
        if (Orders.Count == 0)
            Report("Chưa có đơn hàng phù hợp.", false);
    }

    [RelayCommand] private Task SearchOrdersAsync() => RunAsync(ct => FetchAsync(1, ct));
    [RelayCommand] private Task NextAsync() => RunAsync(ct => (long)page * PageSize < totalCount
        ? FetchAsync(page + 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task PreviousAsync() => RunAsync(ct => page > 1 ? FetchAsync(page - 1, ct) : Task.CompletedTask);
    [RelayCommand] private Task OpenAsync() => RunAsync(async _ =>
    {
        var id = Selected?.Id ?? throw new InvalidOperationException("Hãy chọn một đơn hàng.");
        await navigation.ShowAsync<OrderViewModel>(x =>
        {
            x.OrderId = id;
            x.Operations = Operations;
        });
    });
}

public sealed partial class OrderViewModel(ShopApi api, Navigator navigation, DesktopDialogs dialogs) : Screen
{
    public int OrderId { get; set; }
    public bool Operations { get; set; }
    public bool NewlyPlaced { get; set; }
    public bool Customer => !Operations;
    public override string Title => "Chi tiết đơn";
    private string Route => $"{(Operations ? "operations" : "customer")}/orders/{OrderId}";
    [ObservableProperty] private OrderDetails? order;
    [ObservableProperty] private IReadOnlyList<Choice<OrderStatus>> nextStatuses = [];
    [ObservableProperty] private OrderStatus? nextStatus;
    [ObservableProperty] private string note = "";
    [ObservableProperty] private OrderItemDto? reviewItem;
    [ObservableProperty] private string rating = "5";
    [ObservableProperty] private string comment = "";
    public bool CanCancel => Customer && Order is { Status: OrderStatus.Pending, Payment.Status: PaymentStatus.Unpaid };
    public bool CanReview => Customer && Order?.Status == OrderStatus.Completed;
    public bool CanConfirmTransfer => Operations && Order is
        { Status: OrderStatus.Pending or OrderStatus.Preparing, Payment.Method: PaymentMethod.BankTransfer,
          Payment.Status: PaymentStatus.Unpaid };
    public bool CanUpdateStatus => Operations && NextStatuses.Count != 0;
    public bool ShowBankTransfer => Order is { Payment.BankTransfer: not null, Payment.Status: PaymentStatus.Unpaid };

    partial void OnOrderChanged(OrderDetails? value)
    {
        var available = value?.Status switch
        {
            OrderStatus.Pending => value.Payment.Status == PaymentStatus.Unpaid
                ? new[] { OrderStatus.Preparing, OrderStatus.Cancelled } : [OrderStatus.Preparing],
            OrderStatus.Preparing when value.Payment.Method == PaymentMethod.CashOnDelivery
                => [OrderStatus.Delivering, OrderStatus.Cancelled],
            OrderStatus.Preparing when value.Payment.Status == PaymentStatus.Paid => [OrderStatus.Delivering],
            OrderStatus.Preparing => [OrderStatus.Cancelled],
            OrderStatus.Delivering => [OrderStatus.Completed],
            _ => Array.Empty<OrderStatus>()
        };
        NextStatuses = available.Select(x => new Choice<OrderStatus>(Display.Label(x), x)).ToArray();
        NextStatus = available.Length == 0 ? null : available[0];
        ReviewItem = value?.Items.FirstOrDefault();
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanReview));
        OnPropertyChanged(nameof(CanConfirmTransfer));
        OnPropertyChanged(nameof(CanUpdateStatus));
        OnPropertyChanged(nameof(ShowBankTransfer));
    }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        Order = await api.GetAsync<OrderDetails>(Route, ct);
        if (NewlyPlaced)
        {
            NewlyPlaced = false;
            Report("Đặt hàng thành công. Bạn có thể theo dõi trạng thái hoặc in hóa đơn tại đây.", false);
        }
    }

    [RelayCommand] private Task BackAsync() => navigation.ShowAsync<OrdersViewModel>(x => x.Operations = Operations);
    [RelayCommand] private Task PrintAsync() => RunAsync(_ =>
    {
        if (Order is not null)
            dialogs.Print(Order);
        return Task.CompletedTask;
    });

    [RelayCommand] private Task CancelAsync() => RunAsync(async ct =>
    {
        if (Order is null || !CanCancel || !dialogs.Confirm($"Hủy đơn {Order.OrderNumber}?"))
            return;
        Order = await api.PostAsync<OrderDetails>(Route + "/cancel", new VersionRequest(Order.Version), ct);
        Report("Đã hủy đơn.", false);
    });

    [RelayCommand] private Task ReorderAsync() => RunAsync(async ct =>
    {
        var cart = await api.GetAsync<CartDto>("customer/cart", ct);
        var result = await api.PostAsync<ReorderResult>(Route + "/reorder", new ReorderRequest(cart.Version), ct);
        if (result.SkippedItems.Count == 0)
        {
            await navigation.ShowAsync<CartViewModel>();
            return;
        }
        Report("Món không thể thêm: " + string.Join(", ", result.SkippedItems));
    });

    [RelayCommand] private Task UpdateStatusAsync() => RunAsync(async ct =>
    {
        if (Order is null || NextStatus is not OrderStatus status)
            throw new InvalidOperationException("Hãy chọn trạng thái tiếp theo.");
        if (status == OrderStatus.Cancelled && !dialogs.Confirm($"Hủy đơn {Order.OrderNumber}?"))
            return;
        Order = await api.PutAsync<OrderDetails>(Route + "/status", new OrderStatusRequest(Order.Version, status, Note), ct);
        Note = "";
        Report("Đã cập nhật trạng thái.", false);
    });

    [RelayCommand] private Task ConfirmTransferAsync() => RunAsync(async ct =>
    {
        if (Order is null || !CanConfirmTransfer || !dialogs.Confirm($"Xác nhận đã nhận {Display.Money(Order.Payment.AmountVnd)}?"))
            return;
        Order = await api.PostAsync<OrderDetails>(Route + "/confirm-transfer", new VersionRequest(Order.Version), ct);
        Report("Đã ghi nhận thanh toán.", false);
    });

    [RelayCommand] private Task ReviewAsync() => RunAsync(async ct =>
    {
        var item = ReviewItem ?? throw new InvalidOperationException("Hãy chọn món đã mua.");
        var stars = WholeNumber(Rating, "Số sao", 1, 5);
        await api.PostAsync<ReviewDto>($"customer/products/{item.ProductId}/reviews",
            new ReviewRequest(item.Id, stars, Comment), ct);
        Comment = "";
        Report("Đã gửi đánh giá.", false);
    });
}
