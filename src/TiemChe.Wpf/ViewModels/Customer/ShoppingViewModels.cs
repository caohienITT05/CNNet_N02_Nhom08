using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;
using TiemChe.Wpf.Services;

namespace TiemChe.Wpf.ViewModels.Customer;

public sealed partial class CartViewModel(ShopApi api, Navigator navigation) : Screen
{
    public override string Title => "Giỏ hàng";
    [ObservableProperty] private CartDto? cart;
    [ObservableProperty] private CartItemDto? selected;
    [ObservableProperty] private string quantity = "1";
    partial void OnSelectedChanged(CartItemDto? value) => Quantity = value?.Quantity.ToString() ?? "1";

    protected override async Task LoadAsync(CancellationToken ct)
    {
        Cart = await api.GetAsync<CartDto>("customer/cart", ct);
        Selected = Cart.Items.FirstOrDefault();
        if (Selected is null)
            Report("Giỏ đang trống. Hãy chọn món từ thực đơn.", false);
    }

    [RelayCommand] private Task UpdateAsync() => RunAsync(async ct =>
    {
        if (Selected is not { } item || Cart?.Version is not Guid version)
            throw new InvalidOperationException("Hãy chọn một dòng trong giỏ.");
        var count = WholeNumber(Quantity, "Số lượng", 1, 99);
        Cart = await api.PutAsync<CartDto>($"customer/cart/items/{item.Id}", new CartQuantityRequest(count, version), ct);
        Selected = Cart.Items.FirstOrDefault(x => x.Id == item.Id);
        Report("Đã cập nhật giỏ hàng.", false);
    });

    [RelayCommand] private Task RemoveAsync() => RunAsync(async ct =>
    {
        if (Selected is not { } item || Cart?.Version is not Guid version)
            throw new InvalidOperationException("Hãy chọn một dòng trong giỏ.");
        Cart = await api.DeleteAsync<CartDto>(ShopApi.Query($"customer/cart/items/{item.Id}", ("version", version)), ct);
        Selected = Cart.Items.FirstOrDefault();
        Report("Đã xóa món khỏi giỏ.", false);
    });

    [RelayCommand] private Task BrowseAsync() => navigation.ShowAsync<CatalogViewModel>();
    [RelayCommand] private Task CheckoutAsync() => RunAsync(async _ =>
    {
        if (Cart?.Items.Count is not > 0)
            throw new InvalidOperationException("Giỏ hàng đang trống.");
        if (Cart.Items.Any(x => !x.IsAvailable))
            throw new InvalidOperationException("Hãy xóa các món đã ngừng bán trước khi thanh toán.");
        await navigation.ShowAsync<CheckoutViewModel>();
    });
}

public sealed partial class CheckoutViewModel(ShopApi api, Navigator navigation) : Screen
{
    private CheckoutRequest? confirmedRequest;
    public override string Title => "Thanh toán";
    [ObservableProperty] private CartDto? cart;
    [ObservableProperty] private IReadOnlyList<AddressDto> addresses = [];
    [ObservableProperty] private AddressDto? selectedAddress;
    [ObservableProperty] private IReadOnlyList<Choice<PaymentMethod>> methods = [];
    [ObservableProperty] private PaymentMethod method = PaymentMethod.CashOnDelivery;
    [ObservableProperty] private string coupon = "";
    [ObservableProperty] private CheckoutQuote? quote;
    [ObservableProperty] private long shippingFee;
    public bool HasQuote => Quote is not null;

    partial void OnSelectedAddressChanged(AddressDto? value) => ResetQuote();
    partial void OnMethodChanged(PaymentMethod value) => ResetQuote();
    partial void OnCouponChanged(string value) => ResetQuote();
    partial void OnQuoteChanged(CheckoutQuote? value) => OnPropertyChanged(nameof(HasQuote));

    private void ResetQuote()
    {
        confirmedRequest = null;
        Quote = null;
    }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        ResetQuote();
        Cart = await api.GetAsync<CartDto>("customer/cart", ct);
        Addresses = await api.GetAsync<AddressDto[]>("customer/addresses", ct);
        SelectedAddress = Addresses.FirstOrDefault();
        var options = await api.GetAsync<CheckoutOptions>("customer/checkout/options", ct);
        ShippingFee = options.ShippingFeeVnd;
        Methods = Enum.GetValues<PaymentMethod>().Where(x => x == PaymentMethod.CashOnDelivery || options.AllowsBankTransfer)
            .Select(x => new Choice<PaymentMethod>(Display.Label(x), x)).ToArray();
        if (!options.AllowsBankTransfer)
            Method = PaymentMethod.CashOnDelivery;
        if (Addresses.Count == 0)
            Report("Hãy thêm địa chỉ nhận hàng trước khi đặt đơn.", false);
    }

    [RelayCommand] private Task CalculateAsync() => RunAsync(async ct =>
    {
        var address = SelectedAddress ?? throw new InvalidOperationException("Hãy chọn địa chỉ nhận hàng.");
        if (Cart?.Version is not Guid version || Cart.Items.Count == 0)
            throw new InvalidOperationException("Giỏ hàng đang trống.");
        ResetQuote();
        var request = new CheckoutRequest(Guid.NewGuid(), version, address.Id, Method, Coupon);
        Quote = await api.PostAsync<CheckoutQuote>("customer/checkout/quote", request, ct);
        confirmedRequest = request with { ExpectedTotalVnd = Quote.TotalVnd };
    });

    [RelayCommand] private Task PlaceAsync() => RunAsync(async ct =>
    {
        var request = confirmedRequest ?? throw new InvalidOperationException("Hãy tính và kiểm tra tổng tiền trước.");
        var order = await api.PostAsync<OrderDetails>("customer/checkout", request, ct);
        ct.ThrowIfCancellationRequested();
        await navigation.ShowAsync<OrderViewModel>(x =>
        {
            x.OrderId = order.Id;
            x.NewlyPlaced = true;
        });
    });

    [RelayCommand] private Task ManageAddressesAsync() => navigation.ShowAsync<AddressesViewModel>(x => x.FromCheckout = true);
    [RelayCommand] private Task BackAsync() => navigation.ShowAsync<CartViewModel>();

    private sealed record CheckoutOptions(long ShippingFeeVnd, bool AllowsBankTransfer);
}

public sealed partial class AddressesViewModel(ShopApi api, Navigator navigation, DesktopDialogs dialogs) : Screen
{
    public override string Title => "Sổ địa chỉ";
    public bool FromCheckout { get; set; }
    [ObservableProperty] private IReadOnlyList<AddressDto> addresses = [];
    [ObservableProperty] private AddressDto? selected;
    [ObservableProperty] private string recipient = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string line = "";
    [ObservableProperty] private bool isDefault;

    partial void OnSelectedChanged(AddressDto? value)
    {
        Recipient = value?.RecipientName ?? "";
        Phone = value?.Phone ?? "";
        Line = value?.AddressLine ?? "";
        IsDefault = value?.IsDefault ?? Addresses.Count == 0;
    }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var id = Selected?.Id;
        Addresses = await api.GetAsync<AddressDto[]>("customer/addresses", ct);
        Selected = Addresses.FirstOrDefault(x => x.Id == id);
        if (Selected is null)
            Clear();
    }

    [RelayCommand] private void Clear()
    {
        Selected = null;
        Recipient = Phone = Line = "";
        IsDefault = Addresses.Count == 0;
    }

    [RelayCommand] private Task SaveAsync() => RunAsync(async ct =>
    {
        var request = new AddressRequest(Recipient, Phone, Line, IsDefault);
        var saved = Selected is null
            ? await api.PostAsync<AddressDto>("customer/addresses", request, ct)
            : await api.PutAsync<AddressDto>($"customer/addresses/{Selected.Id}", request, ct);
        Addresses = await api.GetAsync<AddressDto[]>("customer/addresses", ct);
        Selected = Addresses.FirstOrDefault(x => x.Id == saved.Id);
        Report("Đã lưu địa chỉ.", false);
    });

    [RelayCommand] private Task DeleteAsync() => RunAsync(async ct =>
    {
        if (Selected is not { } address)
            throw new InvalidOperationException("Hãy chọn địa chỉ cần xóa.");
        if (!dialogs.Confirm($"Xóa địa chỉ {address.AddressLine}?"))
            return;
        await api.DeleteAsync($"customer/addresses/{address.Id}", ct);
        Clear();
        await LoadAsync(ct);
        Report("Đã xóa địa chỉ.", false);
    });

    [RelayCommand] private Task BackAsync() => navigation.ShowAsync<CheckoutViewModel>();
}
