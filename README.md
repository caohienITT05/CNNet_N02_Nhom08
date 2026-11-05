# Tiệm Chè

Ứng dụng bán chè gồm giao diện WPF trên Windows, backend ASP.NET Core và cơ sở dữ liệu SQLite.

## Yêu cầu

- Windows và Git để tải mã nguồn và chạy ứng dụng WPF.
- .NET 10 SDK để build solution và chạy API.
- .NET Desktop Runtime 8 để chạy `TiemChe.Wpf`. Có thể kiểm tra bằng `dotnet --list-runtimes` và tìm dòng `Microsoft.WindowsDesktop.App 8.0.x`.

## Chạy trên máy phát triển

### 1. Clone nhánh `wpf-nhanvien`

Mở PowerShell tại thư mục bạn muốn lưu dự án rồi chạy các lệnh sau.

```powershell
git clone --branch wpf-nhanvien https://github.com/caohienITT05/CNNet_N02_Nhom08.git
cd .\CNNet_N02_Nhom08
```

Chạy các lệnh tiếp theo từ thư mục này, nơi có tệp `TiemChe.sln`.

### 2. Khởi tạo cơ sở dữ liệu

```powershell
dotnet tool restore
dotnet build .\TiemChe.sln
dotnet tool run dotnet-ef database update --project .\src\TiemChe.Infrastructure --startup-project .\src\TiemChe.Api --no-build
```

Migration tạo tệp `src/TiemChe.Api/tiemche.db` theo cấu hình mặc định và nạp dữ liệu mẫu.

### 3. Tạo quản trị viên lần đầu

Chạy bước này một lần sau khi đã áp dụng migration. Thay email mẫu bằng email bạn muốn dùng để đăng nhập. Khi PowerShell yêu cầu mật khẩu, nhập mật khẩu riêng có ít nhất 10 ký tự và tối đa 72 byte UTF-8.

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

### 4. Khởi động API

Nếu muốn dùng VietQR và giữ cấu hình qua các lần mở API, tạo tệp `src/TiemChe.Api/appsettings.Development.json` trên máy. Thay các giá trị mẫu bằng thông tin của cửa hàng. Git tự bỏ qua tệp này.

```json
{
  "Checkout": {
    "BankName": "VCB",
    "AccountNumber": "SO_TAI_KHOAN_CUA_HANG",
    "AccountName": "TEN_CHU_TAI_KHOAN"
  }
}
```

`VCB` là tên viết tắt được nhận diện sẵn cho Vietcombank. Với ngân hàng chưa được dự án nhận diện, thêm trường `BankBin` vào mục `Checkout`. Khởi động lại API sau khi đổi cấu hình. Các biến môi trường `Checkout__*` của phiên chạy API có thể ghi đè giá trị trong tệp. [Cấu hình ngân hàng](docs/04-dich-vu-va-api.md#chạy-api-trên-máy-phát-triển)

Chạy API từ thư mục dự án. Nếu Windows hỏi có tin cậy chứng chỉ HTTPS dành cho phát triển hay không, xác nhận để WPF kết nối được với API cục bộ.

```powershell
dotnet dev-certs https --trust
dotnet run --project .\src\TiemChe.Api --launch-profile TiemChe.Api
```

Giữ cửa sổ PowerShell này mở. Khi API báo `Now listening on: https://localhost:7048`, bạn có thể chạy WPF.

### 5. Mở ứng dụng WPF

Mở cửa sổ PowerShell thứ hai tại thư mục `CNNet_N02_Nhom08` rồi chạy lệnh sau.

```powershell
dotnet run --project .\src\TiemChe.Wpf
```

WPF kết nối mặc định tới `https://localhost:7048/`. Nếu API dùng địa chỉ HTTPS khác, đặt biến môi trường `TIEMCHE_API_URL` trong cửa sổ chạy WPF trước khi khởi động ứng dụng.

Nếu dùng Visual Studio, mở `TiemChe.sln`. Solution có sẵn cả `TiemChe.Api` và `TiemChe.Wpf`.

### 6. Đăng nhập và thử bán hàng tại quầy

1. Đăng nhập WPF bằng email và mật khẩu Admin đã tạo ở bước 3.
2. Vào **Nhân viên → Tạo tài khoản nhân viên**. Điền họ tên, email và mật khẩu, rồi đăng xuất và đăng nhập bằng tài khoản vừa tạo.
3. Vào **Đặt món tại quầy**, thêm món và tạo đơn. Nếu đã cấu hình VietQR, chọn **Chuyển khoản** trước khi tạo đơn để hiện mã có sẵn số tiền và mã đơn.
4. Sau khi đối chiếu khoản tiền đã vào tài khoản, bấm **Xác nhận đã nhận chuyển khoản**. Nếu đã chọn hoàn thành ngay, đơn sẽ hoàn thành trong cùng bước xác nhận.

Khi đã có cơ sở dữ liệu và tài khoản, các lần mở tiếp theo bắt đầu từ bước 4. Sau khi kéo mã nguồn mới có migration, chạy lại lệnh cập nhật cơ sở dữ liệu ở bước 2.

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
