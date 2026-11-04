# Bước 3 — Models, DbContext, migration và dữ liệu mẫu

Mở `TiemChe.sln` để xem ba project đã triển khai ở bước này. `TiemChe.Domain/Models` chứa 21 thực thể theo bốn nhóm tài khoản, danh mục, mua sắm và đơn hàng. `TiemChe.Infrastructure/Data` chứa DbContext, cấu hình ánh xạ, seed và migration. `TiemChe.Api/Program.cs` đăng ký DbContext với SQLite qua DI và bật kiểm tra khóa ngoại trên mỗi kết nối.

## Tạo cơ sở dữ liệu

Dùng .NET 10 SDK cho solution hiện tại và mở PowerShell tại thư mục chứa `TiemChe.sln`. WPF dùng .NET Desktop Runtime 8 khi chạy.

```powershell
dotnet tool restore
dotnet build .\TiemChe.sln
dotnet tool run dotnet-ef database update --project .\src\TiemChe.Infrastructure --startup-project .\src\TiemChe.Api
```

Theo mặc định, tệp `tiemche.db` nằm trong thư mục nội dung của project API. Có thể trỏ đến tệp khác bằng biến môi trường `ConnectionStrings__TiemChe`. Ví dụ dưới đây dùng thư mục `data` trong solution.

```powershell
$dataDir = New-Item -ItemType Directory -Force -Path .\data
$databasePath = Join-Path $dataDir.FullName 'tiemche.db'
$env:ConnectionStrings__TiemChe = "Data Source=$databasePath"
dotnet tool run dotnet-ef database update --project .\src\TiemChe.Infrastructure --startup-project .\src\TiemChe.Api
```

Tệp SQLite và các tệp phụ của nó được bỏ qua bởi Git. Cần áp dụng migration trước khi chạy API.

## Dữ liệu sau migration

- Ba vai trò `Customer`, `Staff`, `Admin`.
- Ba danh mục, bảy món chè và trà sữa, mười hai lựa chọn size và ba topping.
- Bảy liên kết giữa món và topping. Tài khoản sẽ được tạo qua luồng tài khoản ở bước 4.

Các quan hệ, chỉ mục và ràng buộc được định nghĩa trong `Data/Configurations`. Chẳng hạn, một khách chỉ có một giỏ hàng và tối đa một địa chỉ mặc định. Khóa duy nhất `(UserId, CheckoutRequestId)` giúp nhận diện yêu cầu đặt hàng gửi lại. Dịch vụ ở bước 4 sẽ dùng giao dịch để tạo đơn cùng dòng hàng, lịch sử và thanh toán. Dịch vụ cũng chịu trách nhiệm kiểm tra quy tắc sở hữu, chuẩn hóa email và mã giảm giá, và cập nhật `Version` khi sửa giỏ, đơn hoặc thanh toán.

## Kiểm tra schema

```powershell
dotnet tool run dotnet-ef migrations has-pending-model-changes --project .\src\TiemChe.Infrastructure --startup-project .\src\TiemChe.Api
```

Các lệnh sử dụng công cụ `dotnet-ef` đã ghim phiên bản trong manifest của solution. [Tài liệu EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
