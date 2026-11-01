# SaigonChe

Website đặt món và quản lý Quán Chè Sài Gòn, được xây dựng cho bài tập lớn học phần Công nghệ .NET. Hệ thống có khu vực khách hàng, vận hành nhân viên và quản trị viên.

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
- Vai trò `Customer`, `Staff`, `Admin`; đăng ký công khai chỉ tạo tài khoản `Customer`; Admin quản lý tài khoản Staff.
- Nhân viên tiếp nhận và cập nhật trạng thái đơn; lịch sử trạng thái được lưu.
- POS cho đơn tại bàn/mang về, ghi chú món, voucher, tiền mặt hoặc VietQR xác nhận thủ công, trừ tồn và in lại bill.
- Quản trị tồn kho theo số phần, bàn và voucher.
- Khách gửi/theo dõi yêu cầu đặt tiệc; Staff/Admin tư vấn, báo giá, xác nhận và hoàn tất.
- Dashboard Admin lọc doanh thu theo ngày và loại đơn, biểu đồ ngày/tháng, món bán chạy và xuất Excel; có thể in/lưu PDF từ trình duyệt.

Các giới hạn hiện tại:
- VietQR chỉ hiển thị yêu cầu chuyển khoản; nhân viên xác nhận đã nhận tiền, chưa có cổng thanh toán tự động.
- Tồn kho tính theo số phần món, chưa trừ nguyên liệu.
- Chưa hỗ trợ hoàn tiền, ghép/tách bàn, đặt cọc tiệc hoặc hóa đơn điện tử.
- PDF được tạo bằng chức năng in/lưu PDF của trình duyệt; file Excel được xuất trực tiếp.
- Migration mới đã tạo và kiểm tra tính đồng bộ với model, nhưng cần chạy trên SQL Server trước khi dùng.
- Kiểm thử tự động và tài liệu API vẫn còn trong lộ trình chung.

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

Đặt mật khẩu seed và thông tin nhận VietQR bằng User Secrets (chạy tại thư mục repository, thay giá trị mẫu bằng thông tin của môi trường):

```powershell
dotnet user-secrets set "Seed:AdminPassword" "<mat-khau-admin-manh>" --project .\SaigonChe.Web\SaigonChe.Web.csproj
dotnet user-secrets set "Payment:BankId" "<ma-ngan-hang-VietQR>" --project .\SaigonChe.Web\SaigonChe.Web.csproj
dotnet user-secrets set "Payment:AccountNumber" "<so-tai-khoan-nhan>" --project .\SaigonChe.Web\SaigonChe.Web.csproj
dotnet user-secrets set "Payment:AccountName" "<ten-chu-tai-khoan>" --project .\SaigonChe.Web\SaigonChe.Web.csproj
```

Thiếu cấu hình VietQR thì lựa chọn này sẽ bị vô hiệu hóa. Có thể đặt `Payment:QrTemplate` nếu cần chọn mẫu QR khác.

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

## Luồng Thành viên 3

- POS: `/Pos`.
- Hàng đợi xử lý đơn online: `/OrderOperations`.
- Tạo và theo dõi yêu cầu tiệc: `/Party/Create`, `/Party/MyRequests`.
- Dashboard báo cáo Admin: `/Reports`.
- Quản lý nhân viên: `/Staff`.
- Quản lý bàn, tồn kho và voucher: `/OperationsAdmin/DiningTables`, `/OperationsAdmin/Inventory`, `/OperationsAdmin/Vouchers`.

Database cần được cập nhật migration trước khi chạy các luồng mới. Các API ghi dữ liệu danh mục thử nghiệm cũ đã bị khóa; dùng giao diện Admin có bảo vệ quyền để quản lý.

## Quy ước làm việc

- Không commit `.vs/`, `bin/`, `obj`, secret hoặc cấu hình cá nhân.
- Mỗi chức năng làm trên một branch riêng, ví dụ `feature/cart`.
- Commit nhỏ, rõ nghĩa; pull request ghi cách kiểm thử.
- Chỉ xem một chức năng là hoàn thành khi build thành công, có validation, xử lý lỗi và kiểm thử phù hợp.

## Nguồn đặc tả

- [Thư mục tài liệu dự án trên Google Drive](https://drive.google.com/drive/folders/1cpvRw17V_vcU1bbQwkhicA3P75coMmXU)
- Tài liệu chính: `Sơ bộ dự án`.
- Mẫu báo cáo: `Copy of Huong dan trinh bay bao cao BTL.docx`.
