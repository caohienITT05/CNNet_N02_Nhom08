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

lần đầu chạy :

```
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=SaigonCheDb_Demo;Trusted_Connection=True;TrustServerCertificate=True;'
dotnet run --project .\SaigonChe.Web\SaigonChe.Web.csproj --launch-profile http
```

```
dotnet user-secrets init --project .\SaigonChe.Web\SaigonChe.Web.csproj
dotnet user-secrets set 'Seed:AdminPassword' 'THAY_BANG_MAT_KHAU_MOI' --project .\SaigonChe.Web\SaigonChe.Web.csproj
dotnet run --project .\SaigonChe.Web\SaigonChe.Web.csproj --launch-profile http
```

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
- Vai trò `Customer`, `Staff`, `Admin`; đăng ký công khai chỉ tạo tài khoản `Customer`; Admin quản lý tài khoản Staff.
- Nhân viên tiếp nhận và cập nhật trạng thái đơn; lịch sử trạng thái được lưu.
- POS cho đơn tại bàn/mang về, ghi chú món, voucher, tiền mặt hoặc VietQR xác nhận thủ công, trừ tồn và in lại bill.
- Quản trị tồn kho theo số phần, bàn và voucher.
- Khách gửi/theo dõi yêu cầu đặt tiệc; Staff/Admin tư vấn, báo giá, xác nhận và hoàn tất.
- Dashboard Admin lọc doanh thu theo ngày, thống kê đơn hoàn thành và đang xử lý, món bán chạy; báo cáo có thể xuất Excel hoặc in/lưu PDF từ trình duyệt.
- Khách có thể theo dõi đơn, xem lịch sử mua hàng và gửi đánh giá; Admin quản lý đánh giá và khách hàng.
- Ảnh minh họa thực đơn được lưu cục bộ trong `SaigonChe.Web/wwwroot/images/menu`; nguồn và giấy phép ảnh được liệt kê ở cuối thực đơn.

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

## Luồng Thành viên 3

- POS: `/Pos`.
- Hàng đợi xử lý đơn online: `/OrderOperations`.
- Tạo và theo dõi yêu cầu tiệc: `/Party/Create`, `/Party/MyRequests`.
- Dashboard báo cáo Admin: `/Reports`.
- Quản lý nhân viên: `/Staff`.
- Quản lý bàn, tồn kho và voucher: `/OperationsAdmin/DiningTables`, `/OperationsAdmin/Inventory`, `/OperationsAdmin/Vouchers`.

Database cần được cập nhật migration trước khi chạy các luồng mới. Các API ghi dữ liệu danh mục thử nghiệm cũ đã bị khóa; dùng giao diện Admin có bảo vệ quyền để quản lý.

## Kiểm thử và ma trận quyền

Chạy kiểm thử tự động trên toàn solution:

```powershell
dotnet build .\SaigonChe.slnx
dotnet test .\SaigonChe.slnx
```

Bộ kiểm thử hiện bao phủ tính tổng/giảm giá, voucher hết hạn hoặc hết lượt, tiền thừa và thiếu tiền, thiếu tồn, bán phần cuối theo thứ tự, xác nhận thanh toán lặp và từ chối đơn ở trạng thái kết thúc. Kiểm thử chưa thay thế xác minh cạnh tranh giao dịch trên SQL Server hoặc kiểm thử tích hợp đủ ba vai trò.

| Quyền       | Được phép                                                                                                         | Bị chặn                                                                |
| ------------ | --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `Customer` | Thực đơn, giỏ hàng/checkout, yêu cầu tiệc và lịch sử của mình (`/Party/Create`, `/Party/MyRequests`) | POS, hàng đợi nhân viên, quản trị và báo cáo                   |
| `Staff`    | POS (`/Pos`), hàng đợi đơn (`/OrderOperations`), xử lý yêu cầu tiệc (`/Party`)                        | Quản lý nhân viên/danh mục/tồn kho/voucher và báo cáo doanh thu |
| `Admin`    | Toàn bộ chức năng quản trị, vận hành, báo cáo và quản lý Staff                                           | Không được khóa hoặc hạ quyền Admin cuối cùng                  |

## Kịch bản demo Thành viên 3 (5–10 phút)

1. Đăng nhập Admin; tạo một bàn, thiết lập tồn kho ban đầu và tạo voucher còn hiệu lực tại `/OperationsAdmin/DiningTables`, `/OperationsAdmin/Inventory`, `/OperationsAdmin/Vouchers`.
2. Đăng nhập Customer; gửi yêu cầu tiệc tại `/Party/Create`, sau đó kiểm tra trạng thái tại `/Party/MyRequests`.
3. Đăng nhập Staff; mở `/Pos`, chọn bàn hoặc mang về, thêm món và ghi chú, áp dụng voucher, thanh toán tiền mặt hoặc xác nhận VietQR rồi mở `/Pos/Receipt/{id}` để in/lưu PDF.
4. Ở vai trò Staff, mở `/Party` để tư vấn, báo giá và cập nhật yêu cầu; xác nhận yêu cầu để tạo đơn tiệc.
5. Đăng nhập Admin; mở `/Reports`, lọc thời gian và loại đơn, đối chiếu doanh thu/món bán chạy, tải Excel và dùng chức năng in của trình duyệt để lưu PDF.
6. Đăng xuất và thử truy cập `/Reports` bằng Customer, `/Pos` bằng Customer và `/Staff` bằng Staff; xác nhận hệ thống yêu cầu đăng nhập hoặc từ chối quyền.

## Quy ước làm việc

- Không commit `.vs/`, `bin/`, `obj`, secret hoặc cấu hình cá nhân.
- Mỗi chức năng làm trên một branch riêng, ví dụ `feature/cart`.
- Commit nhỏ, rõ nghĩa; pull request ghi cách kiểm thử.
- Chỉ xem một chức năng là hoàn thành khi build thành công, có validation, xử lý lỗi và kiểm thử phù hợp.

## Nguồn đặc tả

- [Thư mục tài liệu dự án trên Google Drive](https://drive.google.com/drive/folders/1cpvRw17V_vcU1bbQwkhicA3P75coMmXU)
- Tài liệu chính: `Sơ bộ dự án`.
- Mẫu báo cáo: `Copy of Huong dan trinh bay bao cao BTL.docx`.
