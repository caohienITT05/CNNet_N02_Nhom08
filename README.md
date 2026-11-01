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
- Đăng ký, đăng nhập, hồ sơ tài khoản; quản trị và nhân viên dùng các vai trò riêng của ASP.NET Core Identity.
- Quản trị sản phẩm, danh mục, đơn hàng, tài khoản nhân viên và hộp thư liên hệ.
- Nhân viên nhận đơn, cập nhật tuần tự từ xác nhận đến chuẩn bị, giao hàng và hoàn thành; mỗi lần đổi trạng thái có lịch sử, đơn nhận đồng thời được bảo vệ bằng rowversion.
- Tìm kiếm thực đơn theo tên, mô tả hoặc danh mục; lọc theo loại chè và món bán chạy.
- Gợi ý ba món chè theo thời tiết khách tự chọn (nắng nóng, trời mát hoặc trời mưa); không thu thập vị trí hay gọi dịch vụ thời tiết bên ngoài.
- Giỏ hàng và checkout, xác thực lại giá và tình trạng món trước khi lưu đơn.
- Lưu liên hệ khách hàng vào database và quản lý trong khu vực quản trị.
- Dashboard admin lọc theo ngày, thống kê doanh thu từ đơn hoàn thành, đơn đang xử lý và top món bán chạy.
- Ảnh minh họa món chè được lưu cục bộ trong `SaigonChe.Web/wwwroot/images/menu`; giấy phép và nguồn ảnh hiển thị ở cuối thực đơn.

Chưa có:
- Theo dõi đơn, lịch sử mua hàng và đánh giá.
- Khuyến mãi và thanh toán điện tử.
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

Trong môi trường Development, có thể seed ba tài khoản khách hàng và đánh giá mẫu bằng cách cấp `Seed:SampleCustomerPassword` qua User Secrets hoặc biến môi trường `Seed__SampleCustomerPassword`. Nếu chưa cấu hình mật khẩu này, dữ liệu mẫu sẽ được bỏ qua; không dùng các tài khoản mẫu trong môi trường khác.

Đăng nhập bằng tài khoản Admin để tạo tài khoản Staff trong mục **Nhân viên**. Nhân viên đăng nhập bằng tài khoản được cấp và dùng mục **Đơn cần xử lý** để nhận đơn, chuyển trạng thái và ghi chú.

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
