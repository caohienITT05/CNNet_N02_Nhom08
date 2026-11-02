# SaigonChe

Website đặt món và quản lý Quán Chè Sài Gòn, được xây dựng cho bài tập lớn học phần Công nghệ .NET. Hệ thống có khu vực khách hàng, vận hành nhân viên và quản trị viên.

## Kiến trúc hiện tại

- `SaigonChe.Api`: ASP.NET Core Web API và Entity Framework Core.
- `SaigonChe.Web`: ASP.NET Core MVC, ASP.NET Core Identity, Entity Framework Core và SQL Server.
- `SaigonChe.Web.Tests`: xUnit, SQL/HTTP integration và Playwright UI tests.
- `SaigonChe.slnx`: solution chứa ba project.

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
- Migration đã kiểm tra trên SQL Server mới và có dữ liệu cũ trong môi trường audit; database cá nhân cần sao lưu trước khi cập nhật.
- Kết quả kiểm toán, test SQL/HTTP/UI và phần còn thiếu nằm trong [AUDIT_REPORT.md](AUDIT_REPORT.md).

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

## Chạy kiểm toán tái lập

Dùng PowerShell tại gốc repo, sau khi LocalDB/SQL Server sẵn sàng:

```powershell
pwsh -NoProfile -File .\scripts\Run-Audit.ps1 -InstallBrowser
```

Lần tiếp theo bỏ `-InstallBrowser`. Script restore/build/test, dùng Chromium/cache và artifacts trên ổ chứa repository. Test tạo database `SaigonChe_Audit_<RunId>` và tài khoản thử riêng, chỉ dọn DB của fixture. Không trỏ fixture vào DB thật. Có thể đặt `SAIGONCHE_TEST_SQL_CONNECTION` chọn server SQL khác; tên database luôn được fixture thay bằng tên audit.

Kết quả audit 02/11/2026: build 0 warning/0 error, 45/45 test đạt, 0 skipped. Checklist audit đạt 10/11 nhóm (90,91%); Identity nâng cao/khôi phục qua email chưa kiểm chứng. Đây không phải tỷ lệ hoàn thành toàn bộ đặc tả môn học.

Ảnh UI, trace, Excel/PDF và host log nằm trong `artifacts/audit`; TRX trong `artifacts/audit-results`, đều đã gitignore. Nếu Visual Studio giữ khóa DLL, dừng Debug trước khi chạy script. Kết quả/giới hạn: [AUDIT_REPORT.md](AUDIT_REPORT.md).

## Kiểm thử và ma trận quyền

Chạy kiểm thử tự động trên toàn solution:

```powershell
dotnet build .\SaigonChe.slnx
dotnet test .\SaigonChe.slnx
```

Bộ test gồm nghiệp vụ đơn vị, SQL Server thật, HTTP ba vai trò và browser UI: gửi lặp checkout, cạnh tranh tồn/voucher/bàn, migration giữ snapshot cũ, upload, thu hồi phiên, IDOR, báo cáo/bản in. Kết quả từng lượt trong AUDIT_REPORT.md.

| Quyền | Được phép | Bị chặn |
|---|---|---|
| `Customer` | Thực đơn, giỏ hàng/checkout, yêu cầu tiệc và lịch sử của mình (`/Party/Create`, `/Party/MyRequests`) | POS, hàng đợi nhân viên, quản trị và báo cáo |
| `Staff` | POS (`/Pos`), hàng đợi đơn (`/OrderOperations`), xử lý yêu cầu tiệc (`/Party`) | Quản lý nhân viên/danh mục/tồn kho/voucher và báo cáo doanh thu |
| `Admin` | Toàn bộ chức năng quản trị, vận hành, báo cáo và quản lý Staff | Không được khóa hoặc hạ quyền Admin cuối cùng |

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
