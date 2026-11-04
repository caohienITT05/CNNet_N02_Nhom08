using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Operations;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Wpf.Services;
using TiemChe.Wpf.ViewModels.Customer;

namespace TiemChe.Wpf.ViewModels.Staff;

public sealed record StaffProductRow(int Id, int CategoryId, string CategoryName, string Name, long FromPriceVnd);

public sealed partial class StaffToppingChoice(ToppingDto topping) : ObservableObject
{
    public ToppingDto Topping { get; } = topping;
    [ObservableProperty] private bool selected;
}

public sealed partial class StaffOrderLine(string productName, SizeDto size, IReadOnlyList<ToppingDto> toppings,
    int quantity) : ObservableObject
{
    public string ProductName { get; } = productName;
    public int SizeId { get; } = size.Id;
    public string SizeName { get; } = size.Name;
    public long UnitPriceVnd { get; } = size.PriceVnd + toppings.Sum(x => x.PriceVnd);
    public IReadOnlyList<int> ToppingIds { get; } = toppings.Select(x => x.Id).Order().ToArray();
    public string ToppingLabel { get; } = toppings.Count == 0
        ? "Không topping" : string.Join(", ", toppings.Select(x => x.Name));
    [ObservableProperty] private int quantity = quantity;
    public long LineTotalVnd => UnitPriceVnd * Quantity;

    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(LineTotalVnd));

    public bool Matches(int sizeId, IReadOnlyList<int> toppingIds) =>
        SizeId == sizeId && ToppingIds.SequenceEqual(toppingIds.Order());
}

public sealed partial class StaffOrderViewModel(ShopApi api, Navigator navigation, DesktopDialogs dialogs) : Screen
{
    private readonly List<StaffProductRow> allProducts = [];
    private CancellationTokenSource? quoteCancellation;
    private int quoteGeneration;
    private Guid requestId = Guid.NewGuid();
    public override string Title => "Đặt món tại quầy";
    public ObservableCollection<StaffOrderLine> Lines { get; } = [];
    public IReadOnlyList<Choice<bool>> CompletionModes { get; } =
        [new("Thu tiền và hoàn thành ngay", true), new("Gửi bếp, thu tiền sau", false)];
    [ObservableProperty] private IReadOnlyList<Choice<int>> categories = [];
    [ObservableProperty] private int categoryId;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private IReadOnlyList<StaffProductRow> products = [];
    [ObservableProperty] private ProductDetails? product;
    [ObservableProperty] private SizeDto? selectedSize;
    [ObservableProperty] private IReadOnlyList<StaffToppingChoice> toppings = [];
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private StaffOrderLine? selectedLine;
    [ObservableProperty] private string recipientName = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string note = "";
    [ObservableProperty] private string customerSearch = "";
    [ObservableProperty] private IReadOnlyList<StaffCustomerDto> customers = [];
    [ObservableProperty] private StaffCustomerDto? selectedCustomer;
    [ObservableProperty] private string couponCode = "";
    [ObservableProperty] private CheckoutQuote? quote;
    [ObservableProperty] private bool isQuoting;
    [ObservableProperty] private bool completeNow = true;
    [ObservableProperty] private bool paymentReceived;
    [ObservableProperty] private CashShiftDto? currentShift;
    [ObservableProperty] private IReadOnlyList<Choice<PaymentMethod>> paymentMethods = [];
    [ObservableProperty] private PaymentMethod paymentMethod = PaymentMethod.CashOnDelivery;
    [ObservableProperty] private StaffCheckoutOptions? checkoutOptions;
    [ObservableProperty] private OrderDetails? createdOrder;
    public long SubtotalVnd => Lines.Sum(x => x.LineTotalVnd);
    public long TotalVnd => Quote?.TotalVnd ?? 0;
    public bool HasLines => Lines.Count != 0;
    public bool ShowForm => CreatedOrder is null;
    public bool HasQr => !string.IsNullOrWhiteSpace(CreatedOrder?.Payment.BankTransfer?.QrImageUrl);
    public bool ShowPaymentReceived => CompleteNow && PaymentMethod == PaymentMethod.BankTransfer;
    public bool ShowShiftWarning => CurrentShift is null;
    public bool CanCreateOrder => !IsQuoting && Quote is not null && HasLines;
    public string PaymentHint => CheckoutOptions switch
    {
        null => "",
        { AllowsBankTransfer: false } => "Cửa hàng chưa cấu hình tài khoản nhận chuyển khoản.",
        { SupportsQr: true } => "Chọn Chuyển khoản để hiện mã VietQR sau khi tạo đơn.",
        _ => "Có thể chuyển khoản nhưng chưa cấu hình mã QR."
    };
    public string ConfiguredPriceLabel => Display.Money(((SelectedSize?.PriceVnd ?? 0) +
        Toppings.Where(x => x.Selected).Sum(x => x.Topping.PriceVnd)) * ParsedQuantity);

    private int ParsedQuantity => int.TryParse(Quantity, out var value) && value is >= 1 and <= 99 ? value : 1;

    partial void OnSelectedSizeChanged(SizeDto? value) => OnPropertyChanged(nameof(ConfiguredPriceLabel));
    partial void OnQuantityChanged(string value) => OnPropertyChanged(nameof(ConfiguredPriceLabel));
    partial void OnCheckoutOptionsChanged(StaffCheckoutOptions? value) => OnPropertyChanged(nameof(PaymentHint));
    partial void OnCreatedOrderChanged(OrderDetails? value)
    {
        OnPropertyChanged(nameof(ShowForm));
        OnPropertyChanged(nameof(HasQr));
    }

    partial void OnQuoteChanged(CheckoutQuote? value)
    {
        OnPropertyChanged(nameof(TotalVnd));
        OnPropertyChanged(nameof(CanCreateOrder));
    }

    partial void OnIsQuotingChanged(bool value) => OnPropertyChanged(nameof(CanCreateOrder));
    partial void OnCompleteNowChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCreateOrder));
        OnPropertyChanged(nameof(ShowPaymentReceived));
    }

    partial void OnPaymentMethodChanged(PaymentMethod value) => OnPropertyChanged(nameof(ShowPaymentReceived));
    partial void OnCurrentShiftChanged(CashShiftDto? value) => OnPropertyChanged(nameof(ShowShiftWarning));

    partial void OnSelectedCustomerChanged(StaffCustomerDto? value)
    {
        if (value is not null)
        {
            RecipientName = value.FullName;
            Phone = value.Phone ?? "";
        }
    }

    protected override void OnDeactivated()
    {
        quoteCancellation?.Cancel();
        quoteCancellation?.Dispose();
        quoteCancellation = null;
    }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var categories = await api.GetAsync<CategoryDto[]>("catalog/categories", ct);
        var availability = await api.GetAsync<AvailabilityCatalog>("operations/availability", ct);
        CheckoutOptions = await api.GetAsync<StaffCheckoutOptions>("operations/checkout-options", ct);
        CurrentShift = (await api.GetAsync<CashShiftStateDto>("operations/shifts/current", ct)).Shift;
        PaymentMethods = Enum.GetValues<PaymentMethod>()
            .Where(x => x == PaymentMethod.CashOnDelivery || CheckoutOptions.AllowsBankTransfer)
            .Select(x => new Choice<PaymentMethod>(Display.Label(x), x)).ToArray();
        if (!CheckoutOptions.AllowsBankTransfer)
            PaymentMethod = PaymentMethod.CashOnDelivery;
        Categories = [new("Tất cả danh mục", 0), .. categories.Select(x => new Choice<int>(x.Name, x.Id))];
        if (!Categories.Any(x => x.Value == CategoryId))
            CategoryId = 0;
        var activeCategories = categories.Select(x => x.Id).ToHashSet();
        allProducts.Clear();
        allProducts.AddRange(availability.Products
            .Where(x => x.IsActive && activeCategories.Contains(x.CategoryId))
            .Select(x =>
            {
                var sizes = x.Sizes.Where(size => size.IsActive).ToArray();
                return sizes.Length == 0 ? null : new StaffProductRow(x.Id, x.CategoryId, x.CategoryName, x.Name,
                    sizes.Min(size => size.PriceVnd));
            })
            .Where(x => x is not null)
            .Select(x => x!));
        ApplyFilter();
        if (CreatedOrder is null && Lines.Count == 0)
            Report("Chọn món, size và topping để bắt đầu đơn tại quầy.", false);
    }

    [RelayCommand] private void ApplyFilter()
    {
        var term = VietnameseText.Normalize(Search.Trim());
        Products = allProducts.Where(x =>
                (CategoryId == 0 || x.CategoryId == CategoryId)
                && (term.Length == 0 || VietnameseText.Normalize(x.Name).Contains(term)
                    || VietnameseText.Normalize(x.CategoryName).Contains(term)))
            .OrderBy(x => x.CategoryName).ThenBy(x => x.Name).ThenBy(x => x.Id).ToArray();
    }

    [RelayCommand] private Task ChooseProductAsync(StaffProductRow? row) => row is null ? Task.CompletedTask :
        RunAsync(async ct =>
        {
            Product = await api.GetAsync<ProductDetails>($"catalog/products/{row.Id}", ct);
            SelectedSize = Product.Sizes.FirstOrDefault();
            Quantity = "1";
            Toppings = Product.Toppings.Select(x => new StaffToppingChoice(x)).ToArray();
            foreach (var topping in Toppings)
                topping.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ConfiguredPriceLabel));
            OnPropertyChanged(nameof(ConfiguredPriceLabel));
        });

    [RelayCommand] private void AddLine()
    {
        if (Product is null || SelectedSize is not { } size)
        {
            Report("Hãy chọn món và size trước khi thêm vào đơn.");
            return;
        }
        if (!int.TryParse(Quantity, out var count) || count is < 1 or > 99)
        {
            Report("Số lượng phải là số từ 1 đến 99.");
            return;
        }
        var selectedToppings = Toppings.Where(x => x.Selected).Select(x => x.Topping).ToArray();
        var toppingIds = selectedToppings.Select(x => x.Id).Order().ToArray();
        var existing = Lines.FirstOrDefault(x => x.Matches(size.Id, toppingIds));
        if (existing is not null)
        {
            if (existing.Quantity + count > 99)
            {
                Report("Tổng số lượng của một món không được vượt quá 99.");
                return;
            }
            existing.Quantity += count;
        }
        else
        {
            if (Lines.Count >= 100)
            {
                Report("Một đơn chỉ có tối đa 100 dòng món.");
                return;
            }
            existing = new StaffOrderLine(Product.Name, size, selectedToppings, count);
            Lines.Add(existing);
        }
        SelectedLine = existing;
        RefreshTotals();
        Report("Đã thêm món vào đơn tại quầy.", false);
    }

    [RelayCommand] private void IncreaseLine(StaffOrderLine? line)
    {
        if (line is null)
            return;
        if (line.Quantity >= 99)
        {
            Report("Số lượng mỗi món tối đa là 99.");
            return;
        }
        line.Quantity++;
        RefreshTotals();
    }

    [RelayCommand] private void DecreaseLine(StaffOrderLine? line)
    {
        if (line is null)
            return;
        if (line.Quantity <= 1)
        {
            RemoveLine(line);
            return;
        }
        line.Quantity--;
        RefreshTotals();
    }

    [RelayCommand] private void RemoveLine(StaffOrderLine? line)
    {
        if (line is null)
            return;
        Lines.Remove(line);
        if (ReferenceEquals(SelectedLine, line))
            SelectedLine = null;
        RefreshTotals();
    }

    [RelayCommand] private Task SearchCustomersAsync() => RunAsync(async ct =>
    {
        Customers = await api.GetAsync<StaffCustomerDto[]>(ShopApi.Query("operations/customers",
            ("search", CustomerSearch)), ct);
        if (Customers.Count == 0)
            Report("Không tìm thấy khách hàng phù hợp.", false);
    });

    [RelayCommand] private void ApplyCoupon()
    {
        RefreshQuote();
        Report("Đang tính lại tổng tiền theo mã giảm giá.", false);
    }

    [RelayCommand] private Task CreateOrderAsync() => RunAsync(async ct =>
    {
        if (Lines.Count == 0)
            throw new InvalidOperationException("Hãy thêm ít nhất một món vào đơn.");
        if (Quote is null || IsQuoting)
            throw new InvalidOperationException("Vui lòng đợi hệ thống tính xong tổng tiền.");
        if (CompleteNow && PaymentMethod == PaymentMethod.BankTransfer && !PaymentReceived)
            throw new InvalidOperationException("Vui lòng xác nhận đã nhận chuyển khoản.");
        if (!dialogs.Confirm($"Xác nhận tạo đơn với tổng tiền {Display.Money(Quote.TotalVnd)}?"))
            return;
        var request = BuildRequest(Quote.TotalVnd);
        try
        {
            CreatedOrder = await api.PostAsync<OrderDetails>("operations/orders", request, ct);
        }
        catch (ApiException error) when (error.Status == System.Net.HttpStatusCode.Conflict)
        {
            RefreshQuote();
            throw;
        }
        Lines.Clear();
        SelectedLine = null;
        RecipientName = Phone = Note = "";
        RefreshTotals();
        Report(CompleteNow ? "Đã thu tiền và hoàn thành đơn tại quầy." : "Đã gửi đơn đến bếp.", false);
    });

    [RelayCommand] private void NewOrder()
    {
        CreatedOrder = null;
        requestId = Guid.NewGuid();
        Product = null;
        SelectedSize = null;
        Toppings = [];
        Quantity = "1";
        RecipientName = Phone = Note = "";
        CustomerSearch = CouponCode = "";
        Customers = [];
        SelectedCustomer = null;
        PaymentReceived = false;
        CompleteNow = true;
        Quote = null;
        if (CheckoutOptions?.AllowsBankTransfer != true)
            PaymentMethod = PaymentMethod.CashOnDelivery;
        Report("Sẵn sàng tạo đơn mới.", false);
    }

    [RelayCommand] private Task PrintInvoiceAsync() => RunAsync(_ =>
    {
        if (CreatedOrder is not null)
            dialogs.Print(CreatedOrder);
        return Task.CompletedTask;
    });

    [RelayCommand] private Task PrintKitchenAsync() => RunAsync(_ =>
    {
        if (CreatedOrder is not null)
            dialogs.PrintKitchenTicket(CreatedOrder);
        return Task.CompletedTask;
    });

    [RelayCommand] private Task PrintCupsAsync() => RunAsync(_ =>
    {
        if (CreatedOrder is not null)
            dialogs.PrintCupLabels(CreatedOrder);
        return Task.CompletedTask;
    });

    [RelayCommand] private Task OpenCreatedOrderAsync() => CreatedOrder is not { } order
        ? Task.CompletedTask
        : navigation.ShowAsync<OrderViewModel>(x =>
        {
            x.OrderId = order.Id;
            x.Operations = true;
        });

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(SubtotalVnd));
        OnPropertyChanged(nameof(TotalVnd));
        OnPropertyChanged(nameof(HasLines));
        RefreshQuote();
    }

    private StaffOrderRequest BuildRequest(long? expectedTotal) => new(RecipientName, Phone, Note, PaymentMethod,
        Lines.Select(x => new StaffOrderLineRequest(x.SizeId, x.Quantity, x.ToppingIds)).ToArray(),
        requestId, SelectedCustomer?.Id, CouponCode, CompleteNow, PaymentReceived, expectedTotal);

    private void RefreshQuote()
    {
        var generation = Interlocked.Increment(ref quoteGeneration);
        quoteCancellation?.Cancel();
        quoteCancellation?.Dispose();
        Quote = null;
        if (Lines.Count == 0)
        {
            IsQuoting = false;
            return;
        }

        quoteCancellation = CancellationTokenSource.CreateLinkedTokenSource(LifetimeToken);
        var cancellationToken = quoteCancellation.Token;
        IsQuoting = true;
        _ = FetchQuoteAsync(generation, cancellationToken);
    }

    private async Task FetchQuoteAsync(int generation, CancellationToken ct)
    {
        try
        {
            var result = await api.PostAsync<CheckoutQuote>("operations/orders/quote", BuildRequest(null), ct);
            if (generation == quoteGeneration)
                Quote = result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ApiException error)
        {
            if (generation == quoteGeneration)
                Report(error.Message);
        }
        catch (HttpRequestException)
        {
            if (generation == quoteGeneration)
                Report("Không kết nối được máy chủ Tiệm Chè. Kiểm tra API và chứng chỉ HTTPS.");
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError(error.ToString());
            if (generation == quoteGeneration)
                Report("Không thể tính tổng tiền. Vui lòng thử lại.");
        }
        finally
        {
            if (generation == quoteGeneration)
            {
                IsQuoting = false;
                OnPropertyChanged(nameof(CanCreateOrder));
            }
        }
    }
}
