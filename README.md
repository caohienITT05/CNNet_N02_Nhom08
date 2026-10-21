# SaigonChe

Website đặt món và quản lý Quán Chè Sài Gòn, được xây dựng cho bài tập lớn học phần Công nghệ .NET. Hệ thống hiện có khu vực khách hàng và quản trị viên.

## Kiến trúc hiện tại

- `SaigonChe.Api`: ASP.NET Core Web API và Entity Framework Core.
- `SaigonChe.Web`: ASP.NET Core MVC, ASP.NET Core Identity, Entity Framework Core và SQL Server.
- `SaigonChe.slnx`: solution chứa hai project.

```text
Trình duyệt → SaigonChe.Web → SQL Server
```

## Trạng thái chức năng

Đã có:

- CRUD danh mục và món chè.
- Liên kết món chè với danh mục.
- Upload, hiển thị và thay ảnh món chè.
- Đăng ký, đăng nhập có giao diện đồng bộ với dashboard, hồ sơ tài khoản và phân quyền quản trị bằng ASP.NET Core Identity.
- Quản trị sản phẩm, danh mục, đơn hàng, trạng thái đơn và hộp thư liên hệ.
- Tìm kiếm thực đơn theo tên, mô tả hoặc danh mục; lọc theo loại chè và món bán chạy.
- Giỏ hàng và checkout, xác thực lại giá và tình trạng món trước khi lưu đơn.
- Lưu liên hệ khách hàng vào database và quản lý trong khu vực quản trị.

Chưa có:
- Khu vực nhân viên để tiếp nhận, pha chế, giao và hoàn tất đơn.
- Quản lý tài khoản nhân viên và lịch sử người xử lý đơn.
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

Mặc định, `SaigonChe.Web` sử dụng SQL Server LocalDB với database `SaigonCheDb`. Có thể ghi đè `ConnectionStrings:DefaultConnection` bằng User Secrets hoặc biến môi trường `ConnectionStrings__DefaultConnection`.

Khi database chưa có tài khoản quản trị, cần cấp `Seed:AdminPassword` qua User Secrets hoặc biến môi trường `Seed__AdminPassword`. Không lưu mật khẩu quản trị trong source code hoặc file cấu hình được commit. Khi tài khoản quản trị đã tồn tại, ứng dụng giữ nguyên mật khẩu hiện tại.

Áp dụng migration:

```powershell
dotnet ef database update --project .\SaigonChe.Web\SaigonChe.Web.csproj
```

## Khôi phục và build

```powershell
dotnet restore .\SaigonChe.slnx
dotnet build .\SaigonChe.slnx
```

## Chạy website

Chạy website:

```powershell
dotnet run --project .\SaigonChe.Web\SaigonChe.Web.csproj --launch-profile http
```

Địa chỉ mặc định:

- Web: `http://localhost:5141`

## Quy ước làm việc

- Không commit `.vs/`, `bin/`, `obj`, secret hoặc cấu hình cá nhân.
- Mỗi chức năng làm trên một branch riêng, ví dụ `feature/cart`.
- Commit nhỏ, rõ nghĩa; pull request ghi cách kiểm thử.
- Chỉ xem một chức năng là hoàn thành khi build thành công, có validation, xử lý lỗi và kiểm thử phù hợp.

## Nguồn đặc tả

- [Thư mục tài liệu dự án trên Google Drive](https://drive.google.com/drive/folders/1cpvRw17V_vcU1bbQwkhicA3P75coMmXU)
- Tài liệu chính: `Sơ bộ dự án`.
- Mẫu báo cáo: `Copy of Huong dan trinh bay bao cao BTL.docx`.
