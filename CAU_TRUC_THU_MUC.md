# Giải thích cấu trúc thư mục CheSaiGon

## 1. Nhận xét tổng quan

Cấu trúc đề xuất phù hợp cho một đồ án ASP.NET Core quy mô nhỏ đến trung bình. Việc tách `CheSaiGon.Api` và `CheSaiGon.Web` giúp backend API không phụ thuộc vào giao diện, đồng thời cho phép bổ sung ứng dụng mobile hoặc frontend khác trong tương lai mà vẫn tái sử dụng API.

```text
CheSaiGon/
│
├── CheSaiGon.Api/
│   ├── Controllers/
│   ├── DTOs/
│   ├── Services/
│   ├── Repositories/
│   ├── Models/
│   ├── Data/
│   ├── Middleware/
│   └── Program.cs
│
├── CheSaiGon.Web/
│   ├── Controllers/
│   ├── Views/
│   ├── ViewModels/
│   ├── Services/
│   └── wwwroot/
│
├── CheSaiGon.Tests/
│
└── CheSaiGon.sln
```

Luồng xử lý chính:

```text
Trình duyệt
    ↓
CheSaiGon.Web Controller
    ↓
Web Service gọi HTTP API
    ↓
API Controller → Service → Repository → AppDbContext → SQL Server
                                      ↓
                                    Model
```

Nguyên tắc quan trọng là mỗi tầng chỉ đảm nhận một nhóm trách nhiệm. Không viết truy vấn cơ sở dữ liệu trong Controller và không đặt nghiệp vụ của cửa hàng trong Repository.

## 2. Thư mục gốc `CheSaiGon/`

Đây là thư mục chứa toàn bộ solution. Các project con và tài liệu chung của nhóm được đặt tại đây.

Có thể bổ sung ở thư mục gốc:

- `README.md`: giới thiệu và hướng dẫn chạy dự án.
- `.gitignore`: loại bỏ `.vs`, `bin`, `obj` và file cấu hình cá nhân.
- `docs/`: tài liệu thiết kế database, API và phân công nhóm.
- `docker-compose.yml`: tùy chọn, dùng khi chạy SQL Server bằng Docker.

## 3. Project `CheSaiGon.Api`

Đây là backend ASP.NET Core Web API. Project chịu trách nhiệm xác thực, phân quyền, xử lý nghiệp vụ, truy xuất cơ sở dữ liệu và cung cấp dữ liệu cho `CheSaiGon.Web` hoặc các client khác.

### `Controllers/`

Chứa các API Controller, nhận HTTP request và trả HTTP response.

Ví dụ:

```text
Controllers/
├── AuthController.cs
├── CategoriesController.cs
├── ProductsController.cs
├── OrdersController.cs
└── ReportsController.cs
```

Controller nên:

- Nhận và kiểm tra request cơ bản.
- Gọi Service phù hợp.
- Trả đúng status code như `200`, `201`, `400`, `404`.
- Khai báo xác thực và phân quyền bằng `[Authorize]`.

Controller không nên:

- Truy vấn `DbContext` trực tiếp.
- Chứa công thức tính tiền hoặc quy tắc chuyển trạng thái đơn.
- Trả trực tiếp Entity cho client.

### `DTOs/`

DTO (Data Transfer Object) định nghĩa dữ liệu đi vào và đi ra khỏi API. DTO giúp API không làm lộ cấu trúc Entity và chỉ nhận/trả đúng dữ liệu cần thiết.

Nên chia theo chức năng:

```text
DTOs/
├── Auth/
│   ├── RegisterRequest.cs
│   ├── LoginRequest.cs
│   └── AuthResponse.cs
├── Products/
│   ├── CreateProductRequest.cs
│   ├── UpdateProductRequest.cs
│   └── ProductResponse.cs
└── Orders/
    ├── CreateOrderRequest.cs
    ├── OrderItemRequest.cs
    └── OrderResponse.cs
```

Ví dụ, `CreateProductRequest` không chứa `Id` hoặc `CreatedAt` vì các giá trị này do server tạo.

### `Services/`

Chứa nghiệp vụ chính của hệ thống.

Ví dụ:

- `AuthService`: đăng ký, kiểm tra mật khẩu và tạo JWT.
- `ProductService`: kiểm tra dữ liệu trước khi thêm/sửa món.
- `OrderService`: kiểm tra món đang bán, lấy giá từ database, tính tổng tiền và tạo đơn.
- `ReportService`: tính doanh thu và món bán chạy.

Nên tách interface và implementation:

```text
Services/
├── Interfaces/
│   ├── IAuthService.cs
│   └── IOrderService.cs
└── Implementations/
    ├── AuthService.cs
    └── OrderService.cs
```

Service có thể sử dụng nhiều Repository để thực hiện một nghiệp vụ hoàn chỉnh. Đây cũng là tầng phù hợp để quản lý transaction khi tạo một đơn hàng gồm nhiều chi tiết.

### `Repositories/`

Chứa logic truy xuất dữ liệu, làm việc với Entity Framework Core và `AppDbContext`.

Ví dụ:

- Tìm sản phẩm theo mã.
- Lọc sản phẩm theo danh mục.
- Lấy đơn hàng của một khách hàng.
- Thêm hoặc cập nhật Entity.

Cấu trúc đề xuất:

```text
Repositories/
├── Interfaces/
│   ├── IProductRepository.cs
│   └── IOrderRepository.cs
└── Implementations/
    ├── ProductRepository.cs
    └── OrderRepository.cs
```

Repository không nên chứa nghiệp vụ như quyết định khách hàng có được hủy đơn hay không. Quy tắc đó thuộc về Service.

Với những thao tác CRUD rất đơn giản, có thể cho Service dùng `AppDbContext` trực tiếp để tránh tạo Repository chỉ nhằm bọc lại từng phương thức của Entity Framework Core. Nếu nhóm chọn sử dụng Repository thì cần áp dụng nhất quán.

### `Models/`

Chứa các Entity ánh xạ với bảng trong cơ sở dữ liệu.

Ví dụ:

```text
Models/
├── User.cs
├── Category.cs
├── Product.cs
├── Order.cs
└── OrderItem.cs
```

Model thường chứa:

- Thuộc tính của bảng.
- Khóa chính và khóa ngoại.
- Navigation property thể hiện quan hệ.
- Một số quy tắc bất biến rất gần với Entity nếu cần.

Không dùng Model làm request/response của API. Hãy chuyển đổi giữa Model và DTO trong Service hoặc lớp mapping riêng.

Tên `Entities/` thường rõ nghĩa hơn `Models/` trong project API. Tuy nhiên, `Models/` vẫn hợp lệ nếu cả nhóm thống nhất cách gọi.

### `Data/`

Chứa các thành phần liên quan đến Entity Framework Core và cơ sở dữ liệu.

```text
Data/
├── AppDbContext.cs
├── Configurations/
│   ├── ProductConfiguration.cs
│   └── OrderConfiguration.cs
├── Migrations/
└── SeedData.cs
```

Chức năng:

- `AppDbContext.cs`: khai báo các `DbSet` và kết nối Entity Framework Core.
- `Configurations/`: cấu hình bảng, độ dài cột, kiểu tiền, index và quan hệ bằng Fluent API.
- `Migrations/`: lưu lịch sử thay đổi schema database.
- `SeedData.cs`: tạo danh mục, món mẫu hoặc tài khoản quản trị ban đầu.

Không lưu mật khẩu SQL Server trực tiếp trong source code. Connection string nên được lấy từ User Secrets ở môi trường phát triển hoặc biến môi trường khi triển khai.

### `Middleware/`

Chứa các thành phần xử lý request/response dùng chung cho toàn bộ API.

Ví dụ:

- `ExceptionHandlingMiddleware`: bắt exception và trả `ProblemDetails` thống nhất.
- `RequestLoggingMiddleware`: ghi thông tin request phục vụ theo dõi lỗi.
- `CorrelationIdMiddleware`: gắn mã theo dõi cho từng request nếu dự án cần.

Middleware không thay thế Service. Nó phù hợp với các xử lý cắt ngang áp dụng cho nhiều endpoint.

### `Program.cs`

Là điểm khởi động và nơi cấu hình ứng dụng API.

Các nhiệm vụ chính:

- Đăng ký `AppDbContext`.
- Đăng ký Service và Repository vào Dependency Injection.
- Cấu hình JWT Authentication và Authorization.
- Cấu hình Controllers, OpenAPI/Swagger và CORS.
- Đăng ký middleware theo đúng thứ tự.
- Ánh xạ các Controller bằng `MapControllers()`.

Nên chuyển các nhóm cấu hình dài sang extension method, ví dụ `AddApplicationServices()` hoặc `AddJwtAuthentication()`, để `Program.cs` dễ đọc.

## 4. Project `CheSaiGon.Web`

Đây là ứng dụng giao diện ASP.NET Core MVC. Web nhận thao tác từ trình duyệt, gọi `CheSaiGon.Api` qua HTTP và render HTML.

Web không nên kết nối trực tiếp tới database. Nếu Web truy cập database bỏ qua API, nghiệp vụ và phân quyền có thể bị lặp hoặc không nhất quán.

### `Controllers/`

Chứa MVC Controller điều hướng giao diện.

Ví dụ:

- `HomeController`: trang chủ.
- `MenuController`: trang thực đơn và chi tiết món.
- `CartController`: giỏ hàng.
- `OrderController`: đặt hàng và lịch sử đơn.
- `AccountController`: đăng ký và đăng nhập.
- `AdminController`: các màn hình quản trị.

MVC Controller nhận form từ người dùng, gọi Web Service và chọn View để hiển thị.

### `Views/`

Chứa các file Razor `.cshtml` tạo giao diện HTML.

```text
Views/
├── Home/
├── Menu/
├── Cart/
├── Order/
├── Account/
└── Shared/
```

`Views/Shared/` chứa layout, partial view, thông báo và các thành phần dùng chung. View chỉ nên trình bày dữ liệu; không đặt truy vấn database hoặc nghiệp vụ trong file `.cshtml`.

### `ViewModels/`

Chứa dữ liệu dành riêng cho một màn hình hoặc form của Web.

Ví dụ:

- `LoginViewModel`: dữ liệu form đăng nhập và thông báo validation.
- `MenuViewModel`: danh mục, sản phẩm, từ khóa và thông tin phân trang.
- `CheckoutViewModel`: giỏ hàng, người nhận và địa chỉ giao hàng.

ViewModel có thể kết hợp dữ liệu từ nhiều response DTO để phục vụ một trang. ViewModel không phải Entity và không nhất thiết giống DTO của API.

### `Services/`

Chứa các client gọi HTTP tới `CheSaiGon.Api`.

Ví dụ:

```text
Services/
├── Interfaces/
│   ├── IProductApiService.cs
│   └── IOrderApiService.cs
└── Implementations/
    ├── ProductApiService.cs
    └── OrderApiService.cs
```

Các Service này sử dụng `HttpClient` để:

- Gửi request tới API.
- Gắn JWT vào header khi endpoint yêu cầu đăng nhập.
- Chuyển JSON response thành object.
- Xử lý timeout và lỗi kết nối ở mức phù hợp.

Nên đăng ký bằng `IHttpClientFactory` thay vì tự tạo mới `HttpClient` cho mỗi request.

### `wwwroot/`

Chứa tài nguyên tĩnh được trình duyệt tải trực tiếp:

```text
wwwroot/
├── css/
├── js/
├── images/
└── lib/
```

Không đặt source code C#, connection string hoặc dữ liệu bí mật trong `wwwroot`, vì nội dung trong đây có thể được cung cấp công khai.

### Các file nên có thêm trong `CheSaiGon.Web`

Cấu trúc ban đầu chưa hiển thị một số file bắt buộc hoặc thường dùng:

- `Program.cs`: cấu hình MVC, Session, Cookie và các API Service.
- `appsettings.json`: lưu địa chỉ cơ sở của API và cấu hình không nhạy cảm.
- `CheSaiGon.Web.csproj`: định nghĩa project và package.
- `Properties/launchSettings.json`: cấu hình chạy local.

## 5. Project `CheSaiGon.Tests`

Chứa kiểm thử tự động. Nên chia test theo loại:

```text
CheSaiGon.Tests/
├── Unit/
│   ├── Services/
│   └── Validators/
├── Integration/
│   ├── AuthEndpointsTests.cs
│   └── OrderEndpointsTests.cs
├── Fixtures/
└── Helpers/
```

- **Unit test:** kiểm thử một hàm hoặc một Service độc lập, ví dụ tính tổng tiền và kiểm tra chuyển trạng thái đơn.
- **Integration test:** chạy nhiều thành phần cùng nhau, ví dụ gọi API đăng nhập hoặc tạo đơn với database kiểm thử.
- **Fixtures/Helpers:** tạo dữ liệu dùng chung và hỗ trợ thiết lập môi trường test.

`CheSaiGon.Tests` cần reference tới project được kiểm thử. Khi số lượng test tăng, có thể tách thành `CheSaiGon.Api.UnitTests`, `CheSaiGon.Api.IntegrationTests` và `CheSaiGon.Web.Tests`.

## 6. File `CheSaiGon.sln`

Solution liên kết các project để Visual Studio và .NET CLI có thể restore, build và test cùng lúc.

Các project cần được thêm vào solution:

```bash
dotnet sln CheSaiGon.sln add CheSaiGon.Api/CheSaiGon.Api.csproj
dotnet sln CheSaiGon.sln add CheSaiGon.Web/CheSaiGon.Web.csproj
dotnet sln CheSaiGon.sln add CheSaiGon.Tests/CheSaiGon.Tests.csproj
```

Các lệnh kiểm tra toàn solution:

```bash
dotnet restore CheSaiGon.sln
dotnet build CheSaiGon.sln
dotnet test CheSaiGon.sln
```

Nếu sử dụng định dạng solution mới của .NET/Visual Studio, file có thể mang đuôi `.slnx`. Nhóm nên chọn một định dạng thống nhất.

## 7. Các thư mục nên cân nhắc bổ sung

Không cần tạo tất cả ngay từ đầu. Chỉ bổ sung khi đã có nhu cầu thực tế.

### Trong `CheSaiGon.Api`

- `Enums/`: `UserRole`, `OrderStatus`.
- `Validators/`: validation phức tạp hoặc FluentValidation.
- `Mappings/`: chuyển đổi giữa Entity và DTO.
- `Exceptions/`: exception nghiệp vụ riêng.
- `Extensions/`: extension method cấu hình Dependency Injection.
- `Common/`: kiểu phân trang hoặc hằng số dùng chung, nhưng tránh biến thành nơi chứa mọi thứ.

### Trong `CheSaiGon.Web`

- `Models/Api/` hoặc `Contracts/`: kiểu request/response dùng khi gọi API.
- `Extensions/`: helper đăng ký HttpClient, Session hoặc Authentication.
- `Filters/`: action filter dùng chung cho MVC.

Không nên cho `CheSaiGon.Web` reference trực tiếp tới `CheSaiGon.Api`, vì điều đó dễ khiến Web gọi Service hoặc dùng Entity của API thay vì giao tiếp qua HTTP. Nếu cả hai cần dùng chung contract, có thể tạo project riêng `CheSaiGon.Contracts` chứa các request/response DTO không phụ thuộc hạ tầng.

## 8. Quy tắc phụ thuộc

Các phụ thuộc nên đi theo một chiều:

```text
API Controller → API Service → Repository/Data
Web Controller → Web Service → HTTP API
Tests          → Project đang được kiểm thử
```

Tránh các trường hợp:

- Repository gọi ngược Controller.
- Model phụ thuộc DTO hoặc ViewModel.
- API phụ thuộc Web.
- Web truy cập trực tiếp `AppDbContext` của API.
- Controller vừa xử lý giao diện, vừa truy vấn database, vừa chứa toàn bộ nghiệp vụ.

## 9. Kết luận

Cấu trúc hiện tại là nền tảng tốt và không cần áp dụng Clean Architecture phức tạp ngay. Nên giữ ba project chính, bổ sung các file cấu hình còn thiếu và tổ chức interface/implementation trong `Services` và `Repositories`.

Thứ tự triển khai phù hợp:

1. Tạo solution và ba project.
2. Thiết kế Entity trong API.
3. Tạo `AppDbContext`, migration và database.
4. Hoàn thiện một luồng API nhỏ, ví dụ danh sách sản phẩm.
5. Tạo Web Service gọi API đó.
6. Tạo MVC Controller, ViewModel và View để hiển thị.
7. Viết test cho nghiệp vụ quan trọng trước khi mở rộng module tiếp theo.
