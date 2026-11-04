using System.Globalization;
using System.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Wpf.Services;

namespace TiemChe.Wpf.ViewModels.Customer;

public sealed partial class OrdersViewModel(ShopApi api, Navigator navigation, OperationsMonitor monitor) : Screen
{
    private const int PageSize = 20;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private int page = 1;
    private int totalCount;
    private bool initialized;
    private bool subscribed;
    public bool Operations { get; set; }
    public override string Title => Operations ? "Quản lý đơn hàng" : "Đơn của tôi";
    public IReadOnlyList<Choice<OrderStatus?>> Statuses { get; } =
        [new("Tất cả trạng thái", null), .. Enum.GetValues<OrderStatus>().Select(x => new Choice<OrderStatus?>(Display.Label(x), x))];
    public IReadOnlyList<Choice<PaymentMethod?>> PaymentMethods { get; } =
        [new("Tất cả phương thức", null), .. Enum.GetValues<PaymentMethod>().Select(x => new Choice<PaymentMethod?>(Display.Label(x), x))];
    public IReadOnlyList<Choice<PaymentStatus?>> PaymentStatuses { get; } =
        [new("Tất cả thanh toán", null), .. Enum.GetValues<PaymentStatus>().Select(x => new Choice<PaymentStatus?>(Display.Label(x), x))];
    public IReadOnlyList<Choice<OrderChannel?>> Channels { get; } =
        [new("Tất cả kênh", null), .. Enum.GetValues<OrderChannel>().Select(x => new Choice<OrderChannel?>(Display.Label(x), x))];
    [ObservableProperty] private OrderStatus? status;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private IReadOnlyList<OrderSummary> orders = [];
    [ObservableProperty] private OrderSummary? selected;
    [ObservableProperty] private string pageLabel = "";
    [ObservableProperty] private DateTime? from;
    [ObservableProperty] private DateTime? to;
    [ObservableProperty] private PaymentMethod? paymentMethod;
    [ObservableProperty] private PaymentStatus? paymentStatus;
    [ObservableProperty] private OrderChannel? channel;
    [ObservableProperty] private bool oldestFirst;
    [ObservableProperty] private bool soundEnabled = true;

    protected override async Task LoadAsync(CancellationToken ct)
    {
        if (Operations && !initialized)
        {
            initialized = true;
            ApplyToday();
        }
        await FetchAsync(page, ct);
        if (Operations)
            StartTimer();
    }

    protected override void OnDeactivated()
    {
        timer.Stop();
        if (subscribed)
        {
            monitor.PendingIncreased -= OnPendingIncreased;
            subscribed = false;
        }
    }

    private void ApplyToday()
    {
        var today = Display.LocalTime(DateTime.UtcNow).Date;
        From = today;
        To = today;
    }

    private void StartTimer()
    {
        if (!subscribed)
        {
            monitor.PendingIncreased += OnPendingIncreased;
            subscribed = true;
        }
        timer.Tick -= OnTimerTick;
        timer.Tick += OnTimerTick;
        timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs args)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try { await FetchAsync(page, LifetimeToken, true); }
        catch (OperationCanceledException) when (LifetimeToken.IsCancellationRequested) { }
        catch { Report("Không thể tự làm mới danh sách. Sẽ thử lại sau.", false); }
        finally { IsBusy = false; }
    }

    private void OnPendingIncreased(int count)
    {
        if (SoundEnabled)
            SystemSounds.Asterisk.Play();
        Report("Có đơn mới", false);
    }

    private async Task FetchAsync(int number, CancellationToken ct, bool automatic = false)
    {
        var selectedId = Selected?.Id;
        var route = Operations
            ? ShopApi.Query("operations/orders", ("status", Status), ("search", Search), ("page", number),
                ("pageSize", PageSize), ("from", DateText(From)), ("to", DateText(To)),
                ("paymentMethod", PaymentMethod), ("paymentStatus", PaymentStatus), ("oldestFirst", OldestFirst),
                ("channel", Channel))
            : ShopApi.Query("customer/orders", ("status", Status), ("search", Search), ("page", number),
                ("pageSize", PageSize));
        var result = await api.GetAsync<PageResult<OrderSummary>>(route, ct);
        Orders = result.Items;
        Selected = Orders.FirstOrDefault(x => x.Id == selectedId) ?? Orders.FirstOrDefault();
        page = result.Page;
        totalCount = result.TotalCount;
        PageLabel = $"Trang {page}/{Math.Max(1, (totalCount + PageSize - 1) / PageSize)} · {totalCount} đơn";
        if (Orders.Count == 0 && !automatic)
            Report("Chưa có đơn hàng phù hợp.", false);
    }

    private static string? DateText(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [RelayCommand] private Task SearchOrdersAsync() => RunAsync(ct => FetchAsync(1, ct));
    [RelayCommand] private Task QuickRangeAsync(string? range) => RunAsync(async ct =>
    {
        var today = Display.LocalTime(DateTime.UtcNow).Date;
        (From, To) = range switch
        {
            "today" => (today, today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "week" => (today.AddDays(-6), today),
            _ => ((DateTime?)null, (DateTime?)null)
        };
        await FetchAsync(1, ct);
    });
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
    public bool IsInStore => Order?.Channel == OrderChannel.InStore;
    public bool IsOnline => Order?.Channel == OrderChannel.Online;
    public bool ShowShippingAddress => !IsInStore;
    public bool ShowCreatedByStaff => Operations && !string.IsNullOrWhiteSpace(Order?.CreatedByStaffName);
    public override string Title => "Chi tiết đơn";
    private string Route => $"{(Operations ? "operations" : "customer")}/orders/{OrderId}";
    [ObservableProperty] private OrderDetails? order;
    [ObservableProperty] private IReadOnlyList<Choice<OrderStatus>> nextStatuses = [];
    [ObservableProperty] private OrderStatus? nextStatus;
    [ObservableProperty] private string note = "";
    [ObservableProperty] private string internalNoteText = "";
    public string NoteHint => Operations && NextStatus == OrderStatus.Cancelled
        ? "Lý do hủy (bắt buộc, tối thiểu 5 ký tự)" : "Ghi chú (tùy chọn)";
    [ObservableProperty] private OrderItemDto? reviewItem;
    [ObservableProperty] private string rating = "5";
    [ObservableProperty] private string comment = "";
    public bool CanCancel => Customer && Order is { Status: OrderStatus.Pending, Payment.Status: PaymentStatus.Unpaid };
    public bool CanReview => Customer && Order?.Status == OrderStatus.Completed;
    public bool CanCancelAndRefund => Operations && Order is
        { Status: OrderStatus.Pending or OrderStatus.Preparing, Payment.Status: PaymentStatus.Paid };
    public bool CanConfirmTransfer => Operations && Order is
        { Status: OrderStatus.Pending or OrderStatus.Preparing, Payment.Method: PaymentMethod.BankTransfer,
          Payment.Status: PaymentStatus.Unpaid };
    public bool CanUpdateStatus => Operations && NextStatuses.Count != 0;
    public bool ShowInternalNotes => Operations;
    public bool ShowBankTransfer => Order is { Payment.BankTransfer: not null, Payment.Status: PaymentStatus.Unpaid };
    public bool ShowQr => ShowBankTransfer && !string.IsNullOrWhiteSpace(Order?.Payment.BankTransfer?.QrImageUrl);
    partial void OnNextStatusChanged(OrderStatus? value) => OnPropertyChanged(nameof(NoteHint));

    partial void OnOrderChanged(OrderDetails? value)
    {
        OrderStatus[] available = value?.Status switch
        {
            OrderStatus.Pending when Operations => [OrderStatus.Preparing, OrderStatus.Cancelled],
            OrderStatus.Pending => value!.Payment.Status == PaymentStatus.Unpaid
                ? [OrderStatus.Preparing, OrderStatus.Cancelled] : [OrderStatus.Preparing],
            OrderStatus.Preparing when Operations && value!.Channel == OrderChannel.InStore =>
                [OrderStatus.Completed, OrderStatus.Cancelled],
            OrderStatus.Preparing when value!.Channel == OrderChannel.InStore => [],
            OrderStatus.Preparing when Operations && value!.Payment.Method == PaymentMethod.CashOnDelivery
                => [OrderStatus.Delivering, OrderStatus.Cancelled],
            OrderStatus.Preparing when Operations && value!.Payment.Status == PaymentStatus.Paid
                => [OrderStatus.Delivering, OrderStatus.Cancelled],
            OrderStatus.Preparing when Operations => [OrderStatus.Cancelled],
            OrderStatus.Preparing when value!.Payment.Method == PaymentMethod.CashOnDelivery
                => [OrderStatus.Delivering, OrderStatus.Cancelled],
            OrderStatus.Preparing when value!.Payment.Status == PaymentStatus.Paid => [OrderStatus.Delivering],
            OrderStatus.Preparing => [OrderStatus.Cancelled],
            OrderStatus.Delivering when value!.Channel == OrderChannel.Online => [OrderStatus.Completed],
            _ => Array.Empty<OrderStatus>()
        };
        NextStatuses = available.Select(x => new Choice<OrderStatus>(Display.Label(x), x)).ToArray();
        NextStatus = available.Length == 0 ? null : available[0];
        ReviewItem = value?.Items.FirstOrDefault();
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanCancelAndRefund));
        OnPropertyChanged(nameof(CanReview));
        OnPropertyChanged(nameof(CanConfirmTransfer));
        OnPropertyChanged(nameof(CanUpdateStatus));
        OnPropertyChanged(nameof(ShowBankTransfer));
        OnPropertyChanged(nameof(ShowQr));
        OnPropertyChanged(nameof(IsInStore));
        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(ShowShippingAddress));
        OnPropertyChanged(nameof(ShowCreatedByStaff));
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

    [RelayCommand] private Task PrintKitchenAsync() => RunAsync(_ =>
    {
        if (Order is not null)
            dialogs.PrintKitchenTicket(Order);
        return Task.CompletedTask;
    });

    [RelayCommand] private Task PrintCupsAsync() => RunAsync(_ =>
    {
        if (Order is not null)
            dialogs.PrintCupLabels(Order);
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
        if (status == OrderStatus.Cancelled)
        {
            if (Operations && Note.Trim().Length < 5)
                throw new InvalidOperationException("Vui lòng nhập lý do hủy đơn.");
            if (!dialogs.Confirm($"Hủy đơn {Order.OrderNumber}?"))
                return;
        }
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

    [RelayCommand] private Task CancelAndRefundAsync() => RunAsync(async ct =>
    {
        if (Order is null || !CanCancelAndRefund)
            return;
        if (Note.Trim().Length < 5)
            throw new InvalidOperationException("Vui lòng nhập lý do hủy đơn, tối thiểu 5 ký tự.");
        if (!dialogs.Confirm($"Hủy đơn {Order.OrderNumber} và hoàn {Display.Money(Order.Payment.AmountVnd)} cho khách?"))
            return;
        Order = await api.PutAsync<OrderDetails>(Route + "/status",
            new OrderStatusRequest(Order.Version, OrderStatus.Cancelled, Note), ct);
        Note = "";
        Report("Đã hủy đơn và ghi nhận hoàn tiền.", false);
    });

    [RelayCommand] private Task AddInternalNoteAsync() => RunAsync(async ct =>
    {
        if (!Operations || Order is null)
            return;
        Order = await api.PostAsync<OrderDetails>(Route + "/notes", new OrderNoteRequest(InternalNoteText), ct);
        InternalNoteText = "";
        Report("Đã thêm ghi chú nội bộ.", false);
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
