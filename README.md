# Tiệm Chè

Ứng dụng bán chè gồm giao diện WPF trên Windows, backend ASP.NET Core và cơ sở dữ liệu SQLite.

## Yêu cầu

- Windows để chạy ứng dụng WPF.
- .NET 10 SDK để build solution và chạy API.
- .NET Desktop Runtime 8 để chạy `TiemChe.Wpf`. Có thể kiểm tra bằng `dotnet --list-runtimes` và tìm dòng `Microsoft.WindowsDesktop.App 8.0.x`.

## Chạy trên máy phát triển

Chạy các lệnh PowerShell dưới đây từ thư mục chứa `TiemChe.sln`.

### 1. Khởi tạo cơ sở dữ liệu

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project .\src\TiemChe.Infrastructure --startup-project .\src\TiemChe.Api
```

Migration tạo tệp `src/TiemChe.Api/tiemche.db` theo cấu hình mặc định và nạp dữ liệu mẫu.

### 2. Tạo quản trị viên lần đầu

Chỉ chạy bước này một lần sau khi đã áp dụng migration. Mật khẩu phải có ít nhất 10 ký tự; ví dụ hợp lệ là `ViDu-Admin2026!`. Hãy nhập mật khẩu riêng của bạn khi được nhắc:

```powershell
$env:Seed__AdminEmail = 'admin@example.com'
$env:Seed__AdminName = 'Quản trị viên'
$adminSecret = Read-Host 'Nhập mật khẩu quản trị (ít nhất 10 ký tự)' -AsSecureString
$env:Seed__AdminPassword = [System.Net.NetworkCredential]::new('', $adminSecret).Password
try {
    dotnet run --project .\src\TiemChe.Api -- --bootstrap-admin
} finally {
    Remove-Item Env:Seed__AdminPassword
    $adminSecret.Dispose()
}
```

Sau lần tạo đầu tiên, không chạy lại `--bootstrap-admin`. Các lần khởi động tiếp theo dùng lệnh chạy API bình thường.

### 3. Khởi động API

```powershell
dotnet dev-certs https --trust
dotnet run --project .\src\TiemChe.Api --launch-profile TiemChe.Api
```

Giữ cửa sổ PowerShell này mở. Khi API báo `Now listening on: https://localhost:7048`, bạn có thể chạy WPF.

### 4. Mở ứng dụng WPF

Mở cửa sổ PowerShell thứ hai tại thư mục chứa solution:

```powershell
dotnet run --project .\src\TiemChe.Wpf
```

WPF kết nối mặc định tới `https://localhost:7048/`. Nếu API dùng địa chỉ HTTPS khác, đặt biến môi trường `TIEMCHE_API_URL` trong cửa sổ chạy WPF trước khi khởi động ứng dụng.

Nếu dùng Visual Studio, mở `TiemChe.sln`. Solution có sẵn cả `TiemChe.Api` và `TiemChe.Wpf`.

## Tài liệu

- [Cơ sở dữ liệu SQLite và migration](docs/03-du-lieu-sqlite.md)
- [Cấu hình API và tạo tài khoản quản trị đầu tiên](docs/04-dich-vu-va-api.md)
- [Giao diện WPF và luồng kiểm tra](docs/05-giao-dien-wpf.md)

## Chức năng nhân viên

- Bán hàng tại quầy cho khách vãng lai hoặc khách có tài khoản; tính giá, áp dụng coupon và xác nhận tổng trước khi tạo đơn.
- Hoàn thành đơn và thu tiền ngay, hoặc gửi bếp để thu tiền sau; hỗ trợ VietQR khi cửa hàng cấu hình chuyển khoản.
- Theo dõi/lọc đơn theo kênh, trạng thái và thanh toán; xác nhận chuyển khoản, hủy/hoàn tiền theo điều kiện.
- Mở/đóng ca, đối soát tiền mặt, xem và in lịch sử ca; tra cứu khách và lịch sử đơn.
- Cập nhật tình trạng món kèm nhật ký, ẩn/hiện đánh giá và thêm ghi chú nội bộ trên đơn.

Để dùng VietQR tại quầy, cấu hình `Checkout__BankName`, `Checkout__AccountNumber`, `Checkout__AccountName` và `Checkout__BankBin` nếu cần, rồi khởi động lại API. Chọn **Chuyển khoản** và tạo đơn để hiện mã có sẵn số tiền và mã đơn. Sau khi đối chiếu khoản tiền vào tài khoản, bấm **Xác nhận đã nhận chuyển khoản**. Nếu đã chọn thu tiền và hoàn thành ngay, đơn sẽ được hoàn thành trong cùng bước xác nhận. [Cấu hình ngân hàng](docs/04-dich-vu-va-api.md#chạy-api-trên-máy-phát-triển)
