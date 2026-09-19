# SaigonChe — Hệ thống đặt món và quản lý quán chè Sài Gòn

SaigonChe là dự án Web API phục vụ hai nhóm người dùng:

- **Khách hàng:** xem thực đơn, chọn món, đặt hàng, theo dõi trạng thái và xem lịch sử đơn hàng.
- **Nhân viên/quản trị viên:** quản lý món chè, danh mục, đơn hàng, người dùng và xem báo cáo doanh thu cơ bản.

> Mục tiêu học tập: xây dựng một backend hoàn chỉnh bằng ASP.NET Core Web API theo hướng phân lớp, sử dụng Entity Framework Core, SQL Server, JWT và kiểm thử API.

## 1. Phạm vi MVP

Phiên bản đầu tiên nên hoàn thành các chức năng sau:

1. Đăng ký, đăng nhập và phân quyền bằng JWT.
2. Xem danh sách/danh mục/chi tiết món chè.
3. Tạo đơn hàng gồm nhiều món và địa chỉ giao hàng.
4. Khách hàng xem đơn của chính mình.
5. Nhân viên cập nhật trạng thái đơn hàng.
6. Quản trị viên thêm, sửa, ẩn món chè và quản lý danh mục.
7. Thống kê doanh thu, số đơn theo ngày ở mức cơ bản.

Các tính năng như thanh toán online, khuyến mãi, đánh giá, thông báo thời gian thực và quản lý nhiều chi nhánh nên để sau khi MVP hoạt động ổn định.

## 2. Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core |
| Cơ sở dữ liệu | SQL Server |
| Xác thực | JWT Bearer Authentication |
| Tài liệu API | OpenAPI/Swagger |
| Kiểm thử API | Postman hoặc Bruno |
| Unit/Integration Test | xUnit (thêm ở giai đoạn kiểm thử) |
| Quản lý mã nguồn | Git/GitHub |

Frontend có thể được làm sau bằng React, Angular hoặc ASP.NET Core MVC. Trong giai đoạn đầu, hãy dùng Swagger/Postman làm giao diện kiểm thử backend.

## 3. Vai trò và quyền hạn

| Vai trò | Quyền chính |
|---|---|
| Customer | Xem món, đặt hàng, xem và hủy đơn của mình khi đơn chưa được xác nhận |
| Staff | Xem các đơn, xác nhận đơn, cập nhật trạng thái chế biến/giao hàng |
| Admin | Toàn quyền quản lý món, danh mục, tài khoản và báo cáo |

Luồng trạng thái đề xuất:

```text
Pending → Confirmed → Preparing → Delivering → Completed
    └──────────────────────────────→ Cancelled
```

Không cho phép chuyển trạng thái ngược hoặc chuyển từ `Completed`/`Cancelled` sang trạng thái khác.

## 4. Mô hình dữ liệu đề xuất

### User

- `Id`: Guid
- `FullName`, `Email`, `PhoneNumber`
- `PasswordHash`
- `Role`: Customer, Staff hoặc Admin
- `IsActive`, `CreatedAt`

### Category

- `Id`: int
- `Name`, `Description`
- `IsActive`

### Product

- `Id`: int
- `CategoryId`
- `Name`, `Description`, `ImageUrl`
- `Price`: decimal
- `IsAvailable`, `CreatedAt`, `UpdatedAt`

### Order

- `Id`: Guid
- `UserId`
- `ReceiverName`, `PhoneNumber`, `DeliveryAddress`, `Note`
- `Status`
- `TotalAmount`: decimal
- `CreatedAt`, `UpdatedAt`

### OrderItem

- `Id`: long
- `OrderId`, `ProductId`
- `ProductName`, `UnitPrice`, `Quantity`, `Subtotal`

Quan hệ chính:

```text
Category 1 ─── n Product
User     1 ─── n Order
Order    1 ─── n OrderItem
Product  1 ─── n OrderItem
```

`ProductName` và `UnitPrice` trong `OrderItem` là dữ liệu chụp tại thời điểm mua. Nhờ vậy, đơn cũ không bị thay đổi khi tên hoặc giá sản phẩm được cập nhật.

## 5. API dự kiến

### Authentication

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Đăng ký khách hàng |
| POST | `/api/auth/login` | Public | Đăng nhập và nhận JWT |
| GET | `/api/auth/me` | Đã đăng nhập | Lấy thông tin cá nhân |

### Categories và Products

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/categories` | Public | Danh sách danh mục đang hoạt động |
| GET | `/api/products` | Public | Tìm kiếm, lọc và phân trang món |
| GET | `/api/products/{id}` | Public | Chi tiết món |
| POST | `/api/categories` | Admin | Thêm danh mục |
| PUT | `/api/categories/{id}` | Admin | Sửa danh mục |
| POST | `/api/products` | Admin | Thêm món |
| PUT | `/api/products/{id}` | Admin | Sửa món |
| PATCH | `/api/products/{id}/availability` | Admin | Bật/tắt bán món |

### Orders

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/orders` | Customer | Tạo đơn |
| GET | `/api/orders/my-orders` | Customer | Xem đơn của chính mình |
| GET | `/api/orders/{id}` | Chủ đơn/Staff/Admin | Xem chi tiết đơn |
| PATCH | `/api/orders/{id}/cancel` | Customer | Hủy đơn hợp lệ |
| GET | `/api/admin/orders` | Staff/Admin | Lọc và phân trang đơn hàng |
| PATCH | `/api/admin/orders/{id}/status` | Staff/Admin | Cập nhật trạng thái |

### Reports

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/admin/reports/revenue?from=&to=` | Admin | Doanh thu theo khoảng ngày |
| GET | `/api/admin/reports/top-products?from=&to=` | Admin | Các món bán chạy |

## 6. Kiến trúc thư mục mục tiêu

Giai đoạn đầu có thể dùng một project và phân chia rõ trách nhiệm:

```text
SaigonChe.Api/
├── Controllers/
├── Data/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   └── SeedData.cs
├── DTOs/
│   ├── Auth/
│   ├── Products/
│   └── Orders/
├── Entities/
├── Enums/
├── Exceptions/
├── Extensions/
├── Middleware/
├── Repositories/
│   ├── Interfaces/
│   └── Implementations/
├── Services/
│   ├── Interfaces/
│   └── Implementations/
├── Validators/
├── Program.cs
└── appsettings.json
```

Luồng xử lý:

```text
HTTP Request → Controller → Service → Repository/DbContext → SQL Server
                         ↓
                       DTO
```

- **Controller** nhận request, kiểm tra quyền và trả HTTP response.
- **Service** chứa nghiệp vụ như tính tổng tiền và kiểm tra chuyển trạng thái.
- **Repository/DbContext** truy xuất dữ liệu.
- **DTO** là dữ liệu vào/ra API; không trả trực tiếp Entity cho client.

Không cần tách thành nhiều project Clean Architecture ngay từ đầu. Hãy hoàn thành một kiến trúc phân lớp rõ ràng trước, rồi tái cấu trúc khi dự án đủ lớn.

## 7. Cài đặt môi trường

Cần chuẩn bị:

- .NET 10 SDK
- SQL Server hoặc SQL Server chạy bằng Docker
- `dotnet-ef` CLI
- Postman/Bruno hoặc công cụ hỗ trợ OpenAPI

Kiểm tra môi trường:

```bash
dotnet --version
dotnet tool install --global dotnet-ef
dotnet ef --version
```

Clone và chạy project hiện tại:

```bash
git clone <repository-url>
cd CNNet_N02_Nhom08/SaigonChe.Api
dotnet restore
dotnet run
```

Project hiện tại là template ban đầu. Endpoint `weatherforecast` sẽ được xóa khi bắt đầu triển khai các module nghiệp vụ.

## 8. Cấu hình cơ sở dữ liệu an toàn

Không commit mật khẩu hoặc JWT secret thật vào Git. Với môi trường local, dùng User Secrets:

```bash
cd SaigonChe.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=SaigonCheDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "mot-khoa-bi-mat-dai-it-nhat-32-ky-tu"
dotnet user-secrets set "Jwt:Issuer" "SaigonChe.Api"
dotnet user-secrets set "Jwt:Audience" "SaigonChe.Client"
```

Nếu dùng tài khoản SQL Server, thay connection string cho phù hợp nhưng vẫn lưu bằng User Secrets.

Khi đã tạo `AppDbContext` và Entity, chạy migration:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 9. Quy tắc nghiệp vụ quan trọng

- Giá và tổng tiền phải tính ở backend; không tin giá do frontend gửi lên.
- Khi tạo đơn, đọc giá hiện tại của sản phẩm từ database.
- Dùng `decimal` cho tiền, không dùng `float` hoặc `double`.
- Chỉ cho đặt sản phẩm có `IsAvailable = true` và số lượng lớn hơn 0.
- Khách hàng chỉ được xem/cập nhật dữ liệu thuộc về mình.
- Mật khẩu phải được băm, tuyệt đối không lưu dạng văn bản thuần.
- Email nên được chuẩn hóa và đặt unique index.
- Danh sách sản phẩm/đơn hàng phải có phân trang.
- Nên dùng soft delete (`IsActive`, `IsAvailable`) cho dữ liệu đã xuất hiện trong đơn cũ.
- Thời gian lưu trong database nên dùng UTC; chỉ đổi múi giờ khi hiển thị.

## 10. Chuẩn response và lỗi

Dùng đúng HTTP status code:

- `200 OK`: lấy/cập nhật thành công.
- `201 Created`: tạo tài nguyên thành công.
- `204 No Content`: thao tác thành công, không cần response body.
- `400 Bad Request`: dữ liệu đầu vào hoặc nghiệp vụ không hợp lệ.
- `401 Unauthorized`: chưa đăng nhập/token không hợp lệ.
- `403 Forbidden`: đã đăng nhập nhưng không có quyền.
- `404 Not Found`: không tìm thấy tài nguyên.
- `409 Conflict`: dữ liệu xung đột, ví dụ email đã tồn tại.

Nên dùng `ProblemDetails` và middleware xử lý exception toàn cục để lỗi có cấu trúc thống nhất. Không trả stack trace cho client ở production.

## 11. Lộ trình thực hiện

### Sprint 1 — Nền tảng và dữ liệu

- [ ] Xóa API mẫu `weatherforecast`.
- [ ] Tạo cấu trúc thư mục.
- [ ] Cài `Microsoft.EntityFrameworkCore.Design` nếu project chưa có.
- [ ] Tạo Entity, Enum và `AppDbContext`.
- [ ] Tạo migration và database.
- [ ] Seed danh mục, món mẫu và tài khoản Admin.
- [ ] Bật Swagger/OpenAPI để kiểm thử.

**Hoàn thành khi:** ứng dụng chạy, kết nối được SQL Server và migration tạo đúng bảng.

### Sprint 2 — Đăng nhập và phân quyền

- [ ] Tạo DTO đăng ký/đăng nhập.
- [ ] Băm và kiểm tra mật khẩu.
- [ ] Sinh và xác thực JWT.
- [ ] Tạo role Customer, Staff, Admin.
- [ ] Bảo vệ endpoint bằng `[Authorize]` và role.

**Hoàn thành khi:** đăng nhập nhận được token và mỗi role chỉ gọi được API đúng quyền.

### Sprint 3 — Quản lý thực đơn

- [ ] CRUD danh mục và sản phẩm.
- [ ] Tìm kiếm theo tên, lọc theo danh mục/trạng thái.
- [ ] Thêm phân trang và sắp xếp.
- [ ] Validate tên, giá, mô tả và URL ảnh.

**Hoàn thành khi:** Admin quản lý được thực đơn; khách chỉ thấy món đang bán.

### Sprint 4 — Đặt hàng

- [ ] Tạo đơn và các `OrderItem` trong một transaction.
- [ ] Tính tổng tiền hoàn toàn ở server.
- [ ] Xem lịch sử và chi tiết đơn.
- [ ] Cập nhật/hủy đơn theo đúng quy tắc trạng thái.

**Hoàn thành khi:** một khách hàng có thể đi trọn luồng từ xem món đến hoàn tất đơn.

### Sprint 5 — Báo cáo và kiểm thử

- [ ] Báo cáo doanh thu và món bán chạy.
- [ ] Unit test nghiệp vụ tính tiền và chuyển trạng thái.
- [ ] Integration test cho đăng nhập và đặt hàng.
- [ ] Hoàn thiện collection Postman/Bruno.
- [ ] Viết hướng dẫn chạy và dữ liệu demo.

**Hoàn thành khi:** luồng chính có test, API trả lỗi nhất quán và người khác clone về chạy được.

## 12. Thứ tự làm việc cho mỗi chức năng

Khi làm một chức năng, hãy đi theo vòng lặp nhỏ sau:

1. Viết user story và tiêu chí chấp nhận.
2. Xác định Entity/DTO cần dùng.
3. Viết Service và quy tắc nghiệp vụ.
4. Viết Controller/endpoint.
5. Chạy migration nếu schema thay đổi.
6. Kiểm thử đường đi đúng và các trường hợp lỗi.
7. Commit một thay đổi nhỏ, có ý nghĩa.

Ví dụ user story:

> Là khách hàng, tôi muốn đặt nhiều món chè trong một đơn để cửa hàng chuẩn bị và giao đến địa chỉ của tôi.

Tiêu chí chấp nhận:

- Phải đăng nhập với role Customer.
- Đơn phải có ít nhất một sản phẩm.
- Số lượng mỗi món từ 1 đến 100.
- Sản phẩm phải tồn tại và đang được bán.
- Server tự lấy đơn giá, tính subtotal và tổng tiền.
- Tạo `Order` và toàn bộ `OrderItem` thành công hoặc rollback tất cả.
- API trả `201 Created` cùng mã đơn.

## 13. Git workflow cho nhóm

- Nhánh chính: `main`.
- Mỗi chức năng dùng nhánh riêng, ví dụ `feature/authentication`, `feature/product-management`.
- Commit nhỏ với thông điệp rõ ràng, ví dụ `feat: add product creation endpoint`.
- Pull request phải mô tả thay đổi, cách kiểm thử và ảnh Swagger/Postman nếu cần.
- Không commit `bin/`, `obj/`, secret, connection string có mật khẩu hoặc file cấu hình cá nhân.

## 14. Definition of Done

Một chức năng chỉ được xem là hoàn thành khi:

- Đúng tiêu chí chấp nhận và quyền truy cập.
- Validate đầy đủ dữ liệu đầu vào.
- Trả đúng HTTP status code và lỗi có cấu trúc.
- Không lộ Entity, mật khẩu, secret hoặc stack trace.
- Có kiểm thử cho nghiệp vụ quan trọng.
- Chạy `dotnet build` không lỗi.
- API được cập nhật trong OpenAPI và tài liệu kiểm thử.

## 15. Bước tiếp theo

Bắt đầu với **Sprint 1** theo thứ tự: thiết kế Entity → tạo `AppDbContext` → cấu hình SQL Server → migration đầu tiên → seed dữ liệu → kiểm tra bằng OpenAPI. Chỉ chuyển sang đăng nhập sau khi tầng dữ liệu chạy ổn định.

Trong quá trình làm, mỗi lần nên triển khai một phần nhỏ (ví dụ chỉ `Category`) rồi build và kiểm thử ngay. Cách này giúp phát hiện lỗi sớm và dễ hiểu vai trò của từng lớp trong ASP.NET Core Web API.
