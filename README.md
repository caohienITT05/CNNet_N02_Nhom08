# SaigonChe

Website đặt món và quản lý Quán Chè Sài Gòn, được xây dựng cho bài tập lớn học phần Công nghệ .NET.

## Kiến trúc hiện tại

- `SaigonChe.Api`: ASP.NET Core Web API, Entity Framework Core và SQL Server.
- `SaigonChe.Web`: ASP.NET Core MVC, gọi API bằng `HttpClient`.
- `SaigonChe.slnx`: solution chứa hai project.

```text
Trình duyệt → SaigonChe.Web → HTTP API → SaigonChe.Api → SQL Server
```

## Trạng thái chức năng

Đã có:

- CRUD danh mục và món chè.
- Liên kết món chè với danh mục.
- Upload, hiển thị và thay ảnh món chè.
- Migration tạo bảng `Categories`, `Products` và cột `ImageUrl`.

Chưa có:

- Đăng ký, đăng nhập, hồ sơ cá nhân và phân quyền.
- Trang thực đơn dành cho khách, tìm kiếm, lọc và chi tiết món.
- Topping, giỏ hàng, checkout và đơn hàng.
- Theo dõi đơn, lịch sử mua hàng và đánh giá.
- Khuyến mãi, thanh toán điện tử và dashboard doanh thu.
- Kiểm thử tự động và tài liệu API.

Lộ trình chi tiết và thứ tự triển khai nằm trong [ROADMAP.md](ROADMAP.md).

## Yêu cầu môi trường

- .NET 10 SDK.
- SQL Server Express hoặc SQL Server tương thích.
- EF Core CLI nếu cần tạo/chạy migration.

```powershell
dotnet --version
dotnet ef --version
```

Nếu chưa có `dotnet-ef`:

```powershell
dotnet tool install --global dotnet-ef
```

## Cấu hình database

Connection string hiện dùng SQL Server Express với database `SaigonCheDb`. Nên chuyển cấu hình riêng của từng máy sang User Secrets trước khi làm việc nhóm hoặc triển khai.

Áp dụng migration:

```powershell
dotnet ef database update --project .\SaigonChe.Api\SaigonChe.Api.csproj
```

## Khôi phục và build

```powershell
dotnet restore .\SaigonChe.slnx
dotnet build .\SaigonChe.slnx
```

## Chạy dự án

Mở hai terminal và chạy đồng thời API và Web.

Terminal 1:

```powershell
dotnet run --project .\SaigonChe.Api\SaigonChe.Api.csproj --launch-profile http
```

Terminal 2:

```powershell
dotnet run --project .\SaigonChe.Web\SaigonChe.Web.csproj --launch-profile http
```

Địa chỉ mặc định:

- API: `http://localhost:5267`
- Web: `http://localhost:5140`

`SaigonChe.Web/appsettings.json` phải trỏ `ApiSettings:BaseUrl` tới đúng địa chỉ API.

## Quy ước làm việc

- Không commit `.vs/`, `bin/`, `obj`, secret hoặc cấu hình cá nhân.
- Mỗi chức năng làm trên một branch riêng, ví dụ `feature/cart`.
- Commit nhỏ, rõ nghĩa; pull request ghi cách kiểm thử.
- Chỉ xem một chức năng là hoàn thành khi build thành công, có validation, xử lý lỗi và kiểm thử phù hợp.

## Nguồn đặc tả

- [Thư mục tài liệu dự án trên Google Drive](https://drive.google.com/drive/folders/1cpvRw17V_vcU1bbQwkhicA3P75coMmXU)
- Tài liệu chính: `Sơ bộ dự án`.
- Mẫu báo cáo: `Copy of Huong dan trinh bay bao cao BTL.docx`.
