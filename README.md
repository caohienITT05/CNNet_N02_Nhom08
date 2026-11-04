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

### 2. Khởi động API

```powershell
dotnet dev-certs https --trust
dotnet run --project .\src\TiemChe.Api --launch-profile TiemChe.Api
```

Giữ cửa sổ PowerShell này mở. Khi API báo `Now listening on: https://localhost:7048`, bạn có thể chạy WPF.

### 3. Mở ứng dụng WPF

Mở cửa sổ PowerShell thứ hai tại thư mục chứa solution:

```powershell
dotnet run --project .\src\TiemChe.Wpf
```

WPF kết nối mặc định tới `https://localhost:7048/`. Nếu API dùng địa chỉ HTTPS khác, đặt biến môi trường `TIEMCHE_API_URL` trong cửa sổ chạy WPF trước khi khởi động ứng dụng.

Nếu dùng Visual Studio, mở `TiemChe.sln`. Solution có sẵn cả `TiemChe.Api` và `TiemChe.Wpf`.

## Tài liệu

- [Cơ sở dữ liệu SQLite và migration](docs/03-du-lieu-sqlite.md)
- [Cấu hình API và tạo tài khoản quản trị đầu tiên](docs/04-dich-vu-va-api.md)
