# Lộ trình xây dựng SaigonChe

Tài liệu này hợp nhất đặc tả trong [thư mục Google Drive của dự án](https://drive.google.com/drive/folders/1cpvRw17V_vcU1bbQwkhicA3P75coMmXU) với trạng thái thực tế của repository ngày 12/10/2026.

## 1. Mục tiêu sản phẩm

Hệ thống phục vụ ba nhóm người dùng:

- **Khách hàng:** đăng ký, xem và tìm món, chọn topping, quản lý giỏ hàng, đặt hàng, theo dõi đơn, xem lịch sử và đánh giá món.
- **Nhân viên:** đăng nhập vào khu vực vận hành, tiếp nhận đơn, cập nhật tiến độ pha chế/giao hàng, xem thông tin cần thiết của đơn và cập nhật tình trạng còn hàng.
- **Quản trị viên:** quản lý danh mục, món, hình ảnh, nhân viên, khách hàng, khuyến mãi, toàn bộ đơn hàng và dashboard doanh thu.

Công nghệ theo đặc tả:

- ASP.NET Core MVC và Web API trên .NET 10.
- Entity Framework Core và SQL Server.
- ASP.NET Core Identity cho người dùng và vai trò.
- HTML, CSS, Bootstrap và JavaScript/jQuery.

## 2. Hiện trạng repository

### Đã hoàn thành một phần

| Hạng mục | Hiện trạng |
|---|---|
| Solution | Có `SaigonChe.Api` và `SaigonChe.Web` |
| Database | Có `Categories`, `Products` và migration ảnh |
| API danh mục | Có CRUD cơ bản |
| API sản phẩm | Có CRUD cơ bản và trả tên danh mục |
| MVC quản trị | Có màn hình CRUD danh mục và sản phẩm |
| Ảnh sản phẩm | Có tạo, hiển thị và thay ảnh trong `wwwroot/uploads/products` |

### Khoảng trống cần xử lý

- API đang nhận/trả trực tiếp Entity, chưa có DTO, Service và validation.
- `CategoriesController.Update` gọi `FindAsync()` nhưng không truyền `id`, khiến sửa danh mục không hoạt động đúng.
- Chưa có xử lý lỗi tập trung; Web dễ lỗi khi API dừng hoặc trả response không hợp lệ.
- Xóa sản phẩm chưa xóa ảnh; xóa danh mục có thể xung đột khi còn sản phẩm.
- Chỉ kiểm tra đuôi file ảnh, chưa kiểm tra MIME/signature và chưa gom logic ảnh thành service.
- Chưa có Swagger/OpenAPI, authentication, authorization theo ba vai trò, pagination hoặc test.
- Giao diện hiện là CRUD quản trị, chưa phải trải nghiệm mua hàng cho khách.
- Chưa có màn hình vận hành đơn dành riêng cho nhân viên.

## 3. Phạm vi ưu tiên

### MVP bắt buộc

1. Đăng ký, đăng nhập và phân quyền `Customer`/`Staff`/`Admin`.
2. Admin quản lý danh mục, sản phẩm và trạng thái còn hàng.
3. Khách xem, tìm kiếm, lọc và xem chi tiết món.
4. Giỏ hàng bằng Session.
5. Checkout COD, tạo đơn và chi tiết đơn trong một transaction.
6. Khách xem lịch sử/trạng thái đơn; nhân viên tiếp nhận và xử lý đơn; Admin giám sát toàn bộ.
7. Admin quản lý tài khoản nhân viên và phân công trạng thái hoạt động.
8. Dashboard cơ bản: số đơn, doanh thu, khách hàng và món bán chạy.
9. Kiểm thử luồng chính và hoàn thiện báo cáo.

### Sau MVP

- Topping linh hoạt theo từng món.
- Quên/đổi mật khẩu qua email.
- Đánh giá và bình luận.
- Mã giảm giá.
- Ví điện tử.
- Thông báo thời gian thực.

## 4. Kiến trúc mục tiêu vừa đủ

Không cần chuyển ngay sang Clean Architecture nhiều project. Giữ hai project hiện tại nhưng tách trách nhiệm rõ ràng:

```text
SaigonChe.Api/
├── Controllers/
├── Data/
├── DTOs/
├── Models/
├── Services/
├── Enums/
├── Middleware/
└── Migrations/

SaigonChe.Web/
├── Controllers/
├── Services/
├── ViewModels/
├── Views/
└── wwwroot/
```

Luồng phụ thuộc:

```text
Web Controller → API Client Service → HTTP API
API Controller → Application Service → AppDbContext → SQL Server
```

Quy tắc:

- Web không truy cập trực tiếp database.
- API không trả Entity trực tiếp; dùng request/response DTO.
- Nghiệp vụ tính tiền và chuyển trạng thái nằm trong Service.
- Tiền dùng `decimal`; thời gian lưu UTC.
- Dữ liệu đã xuất hiện trong đơn không hard-delete tùy tiện.

## 5. Mô hình dữ liệu mục tiêu

### Nền tảng

- `ApplicationUser`: kế thừa Identity, bổ sung họ tên, địa chỉ, trạng thái tài khoản.
- Vai trò Identity: `Customer`, `Staff`, `Admin`; tài khoản nhân viên chỉ do Admin tạo hoặc gán quyền.
- `Category`: thêm `IsActive`.
- `Product`: giữ `ImageUrl`, thêm thời gian tạo/cập nhật và nên dùng trạng thái thay vì hard delete.

### Giao dịch

- `Order`: khách hàng, nhân viên đang xử lý, người nhận, SĐT, địa chỉ, ghi chú, trạng thái, tổng tiền, thời gian.
- `OrderItem`: sản phẩm, tên/giá chụp tại thời điểm mua, số lượng và thành tiền.
- `OrderStatus`: `Pending`, `Confirmed`, `Preparing`, `Delivering`, `Completed`, `Cancelled`.
- `OrderStatusHistory`: đơn hàng, trạng thái cũ/mới, người cập nhật, thời gian và ghi chú/lý do hủy.

### Mở rộng

- `Topping` và bảng nối `ProductTopping`.
- `OrderItemTopping` để lưu topping thực tế đã mua.
- `Review` chỉ cho phép khi đơn hoàn thành.
- `Promotion` với mã, loại giảm, giá trị, giới hạn và thời gian hiệu lực.
- `Payment` nếu tích hợp ví điện tử.

## 6. Lộ trình triển khai

### Giai đoạn 0 — Ổn định phần hiện có

- [ ] Sửa lỗi `CategoriesController.Update`.
- [ ] Thêm validation cho Category/Product và hiển thị lỗi trên Web.
- [ ] Không cho xóa danh mục đang có sản phẩm; ưu tiên `IsActive`.
- [ ] Gom xử lý upload/xóa ảnh vào một service dùng chung.
- [ ] Kiểm tra MIME, giới hạn 5 MB, tên file ngẫu nhiên và đường dẫn an toàn.
- [ ] Xóa ảnh khi xóa sản phẩm thành công.
- [ ] Dùng typed/named `HttpClient` và xử lý API không khả dụng.
- [ ] Bật Swagger/OpenAPI cho API.

**Hoàn thành khi:** Category/Product CRUD chạy trọn vẹn, lỗi được hiển thị rõ, build sạch và kiểm thử thủ công đầy đủ.

### Giai đoạn 1 — Chuẩn hóa API và database

- [ ] Tạo DTO cho Category và Product.
- [ ] Tạo Service; Controller chỉ nhận request và trả response.
- [ ] Thêm `IsActive`, `CreatedAt`, `UpdatedAt` và cấu hình Fluent API.
- [ ] Seed danh mục, món mẫu và dữ liệu demo.
- [ ] Thêm middleware trả `ProblemDetails` thống nhất.
- [ ] Viết migration mới, không sửa migration đã áp dụng.

**Hoàn thành khi:** API không còn nhận/trả trực tiếp Entity và database có dữ liệu demo tái lập được.

### Giai đoạn 2 — Identity, đăng nhập và phân quyền

- [ ] Tích hợp ASP.NET Core Identity vào API.
- [ ] Tạo role `Customer`, `Staff`, `Admin`; seed tài khoản Admin và một Staff demo.
- [ ] Làm đăng ký, đăng nhập, đăng xuất và hồ sơ cá nhân.
- [ ] API phát hành token; Web lưu phiên đăng nhập bằng cookie an toàn và gắn token khi gọi API.
- [ ] Tài khoản đăng ký công khai luôn nhận role `Customer`; không cho client tự gửi role.
- [ ] Bảo vệ endpoint theo policy; Customer không thấy khu vực nội bộ, Staff không vào trang quản trị hệ thống.
- [ ] Thêm khóa/mở khóa tài khoản.
- [ ] Admin tạo, sửa, khóa/mở khóa tài khoản nhân viên; không hard-delete tài khoản đã xử lý đơn.

**Hoàn thành khi:** Customer không gọi được API nội bộ; Staff chỉ truy cập nghiệp vụ vận hành; Admin quản lý được catalog và nhân viên; mật khẩu không lưu dạng rõ.

### Giai đoạn 3 — Trải nghiệm xem món

- [ ] Thiết kế lại layout, navbar, footer và trang chủ.
- [ ] Hiển thị món mới, nổi bật và đang bán.
- [ ] Tìm kiếm theo tên.
- [ ] Lọc theo danh mục và sắp xếp giá.
- [ ] Thêm phân trang.
- [ ] Làm trang chi tiết món.
- [ ] Thiết kế dữ liệu topping; chỉ triển khai topping sau khi luồng sản phẩm ổn định.

**Hoàn thành khi:** khách có thể tìm được món phù hợp trên desktop/mobile mà không thấy màn hình CRUD Admin.

### Giai đoạn 4 — Giỏ hàng

- [ ] Bật Session trong Web.
- [ ] Tạo `CartItemViewModel` và `CartService`.
- [ ] Thêm món/topping vào giỏ.
- [ ] Tăng, giảm, xóa và tính tạm tính.
- [ ] Kiểm tra lại giá/trạng thái món từ API trước checkout.
- [ ] Thêm phí giao hàng theo quy tắc thống nhất.

**Hoàn thành khi:** giỏ giữ được dữ liệu trong phiên và không tin giá gửi từ trình duyệt.

### Giai đoạn 5 — Checkout và đơn hàng

- [ ] Tạo `Order`, `OrderItem` và migration.
- [ ] Checkout gồm họ tên, SĐT, địa chỉ, ghi chú và COD.
- [ ] API lấy giá từ database và tạo toàn bộ đơn trong transaction.
- [ ] Xóa giỏ chỉ sau khi tạo đơn thành công.
- [ ] Khách xem chi tiết, trạng thái và lịch sử đơn của chính mình.
- [ ] Tạo màn hình `Staff/Orders` tối ưu cho thao tác nhanh: đơn mới, đang pha chế, đang giao và đã xong.
- [ ] Nhân viên nhận đơn và hệ thống lưu `AssignedStaffId`; tránh hai nhân viên xử lý trùng.
- [ ] Staff cập nhật trạng thái theo luồng hợp lệ; chỉ thấy thông tin khách hàng cần để hoàn thành đơn.
- [ ] Admin lọc đơn theo trạng thái, thời gian, nhân viên và xem lịch sử xử lý.
- [ ] Lưu mọi lần đổi trạng thái vào `OrderStatusHistory`, bao gồm người thực hiện và lý do hủy.
- [ ] Dùng kiểm soát đồng thời để cảnh báo khi đơn vừa được người khác cập nhật.

**Hoàn thành khi:** chạy trọn luồng `xem món → giỏ → checkout → Staff tiếp nhận/pha chế/giao → hoàn thành`, Admin theo dõi được người xử lý và lịch sử trạng thái.

### Giai đoạn 6 — Dashboard và nghiệp vụ mở rộng

- [ ] Tổng số đơn, doanh thu, khách hàng và sản phẩm.
- [ ] Biểu đồ doanh thu ngày/tháng.
- [ ] Danh sách món bán chạy.
- [ ] Dashboard Staff trong ca: số đơn mới, đang xử lý, hoàn thành và đơn quá thời gian dự kiến.
- [ ] Staff báo món hết/tạm hết; Admin quyết định thay đổi thông tin hoặc ngừng bán dài hạn.
- [ ] Đánh giá món sau khi đơn hoàn thành.
- [ ] Mã giảm giá có thời gian hiệu lực và giới hạn sử dụng.
- [ ] Chỉ tích hợp ví điện tử sau khi COD ổn định.

**Hoàn thành khi:** số liệu dashboard khớp dữ liệu đơn hoàn thành và các quy tắc mở rộng có test.

### Giai đoạn 7 — Kiểm thử, bảo mật và chất lượng

- [ ] Tạo project test.
- [ ] Unit test tính tiền, giảm giá và chuyển trạng thái.
- [ ] Integration test đăng nhập, CRUD sản phẩm và tạo đơn.
- [ ] Authorization test cho ma trận quyền Customer/Staff/Admin.
- [ ] Test hai nhân viên cùng nhận hoặc cập nhật một đơn.
- [ ] Kiểm tra IDOR: khách không xem được đơn của người khác.
- [ ] Kiểm tra Staff không xem/sửa được tài khoản, khuyến mãi hoặc báo cáo quản trị.
- [ ] Kiểm tra upload file giả, file lớn và path traversal.
- [ ] Thêm logging, trang lỗi thân thiện và health check.
- [ ] Kiểm tra responsive, accessibility và các trường hợp dữ liệu rỗng.

**Hoàn thành khi:** `dotnet build` và `dotnet test` đều thành công; luồng chính có kiểm thử tự động.

### Giai đoạn 8 — Báo cáo và bảo vệ

- [ ] Viết báo cáo theo khung: yêu cầu → phân tích thiết kế → triển khai → kiểm thử → kết luận.
- [ ] Bổ sung Use Case diagram, ERD, sơ đồ kiến trúc và luồng đặt hàng.
- [ ] Chụp màn hình các chức năng đã hoàn thành, không đưa mockup chưa chạy vào phần kết quả.
- [ ] Tạo dữ liệu demo ổn định.
- [ ] Làm slide và kịch bản demo 5–10 phút.
- [ ] Gắn tag phiên bản dùng để bảo vệ.

**Hoàn thành khi:** một máy mới có thể làm theo README để chạy và nhóm demo được toàn bộ luồng không cần sửa dữ liệu thủ công.

## 7. Use case bổ sung cho nhân viên

Đặc tả Drive ban đầu có 19 use case cho Customer và Admin. Khi bổ sung Staff, thêm các use case sau:

| Use case mới | Tác nhân | Giai đoạn |
|---|---|---|
| UC20: Nhân viên đăng nhập và xem màn hình ca làm | Staff | 2, 5 |
| UC21: Xem/lọc danh sách đơn cần xử lý | Staff | 5 |
| UC22: Tiếp nhận đơn | Staff | 5 |
| UC23: Cập nhật trạng thái pha chế/giao hàng | Staff | 5 |
| UC24: Hủy/từ chối đơn kèm lý do theo quyền được cấp | Staff, Admin | 5 |
| UC25: Báo món tạm hết | Staff | 6 |
| UC26: Quản lý tài khoản nhân viên | Admin | 2 |
| UC27: Theo dõi hiệu quả xử lý đơn theo nhân viên | Admin | 6 |

### Ma trận quyền tối thiểu

| Chức năng | Customer | Staff | Admin |
|---|:---:|:---:|:---:|
| Xem thực đơn, đặt hàng, xem đơn của mình | Có | Không | Không bắt buộc |
| Xem hàng đợi và xử lý đơn | Không | Có | Có |
| Báo món tạm hết | Không | Có | Có |
| Quản lý danh mục/sản phẩm | Không | Không | Có |
| Quản lý nhân viên/khách hàng/khuyến mãi | Không | Không | Có |
| Xem báo cáo doanh thu đầy đủ | Không | Không | Có |

Nguyên tắc: API kiểm tra quyền ở server; việc ẩn nút trên giao diện không được xem là cơ chế bảo mật.

## 8. Ánh xạ 19 use case ban đầu

| Use case | Giai đoạn |
|---|---|
| UC01–UC04: đăng nhập, đăng ký, hồ sơ, mật khẩu | 2 |
| UC05–UC08: trang chủ, tìm/lọc, danh sách, chi tiết | 3 |
| UC09: giỏ hàng | 4 |
| UC10–UC12: checkout, theo dõi, lịch sử | 5 |
| UC13: đánh giá/bình luận | 6 |
| UC14: dashboard | 6 |
| UC15–UC16: danh mục và sản phẩm | 0–1 |
| UC17: quản lý đơn | 5 (Admin giám sát, Staff vận hành) |
| UC18: quản lý khách hàng | 2 |
| UC19: khuyến mãi | 6 |

## 9. Thứ tự công việc cho mỗi tính năng

1. Viết user story và tiêu chí chấp nhận.
2. Thiết kế Entity/DTO và quyền truy cập.
3. Viết migration nếu schema thay đổi.
4. Viết Service và quy tắc nghiệp vụ.
5. Viết API Controller.
6. Viết API client, MVC Controller, ViewModel và View.
7. Test đường thành công và các trường hợp lỗi.
8. Build toàn solution, review rồi tạo pull request nhỏ.

## 10. Definition of Done

Một chức năng chỉ hoàn thành khi:

- Đúng tiêu chí chấp nhận và phân quyền.
- Đã kiểm tra cả ba vai trò `Customer`, `Staff`, `Admin` nếu chức năng liên quan quyền truy cập.
- Có validation phía server; lỗi hiển thị được trên giao diện.
- Trả đúng HTTP status code và không lộ stack trace/secret.
- Không tin giá, role hoặc user ID do client tự gửi.
- Có test cho nghiệp vụ quan trọng.
- Migration có thể áp dụng trên database mới.
- `dotnet build` và `dotnet test` thành công.
- README/API docs được cập nhật nếu cách chạy hoặc endpoint thay đổi.

## 11. Việc nên làm ngay tiếp theo

Theo đúng phụ thuộc, thứ tự 5 pull request tiếp theo nên là:

1. `fix/category-product-crud`: sửa Category Update, validation, xóa ảnh và lỗi API.
2. `refactor/catalog-dtos-services`: DTO, Service, `ProblemDetails`, Swagger.
3. `feature/identity-auth`: Identity, ba role, seed Admin/Staff, đăng nhập Web/API.
4. `feature/customer-menu`: trang chủ, tìm kiếm, lọc, chi tiết món.
5. `feature/cart`: Session và toàn bộ thao tác giỏ hàng.

Sau năm bước trên, triển khai `feature/staff-order-workflow` cùng mô hình Order; không dồn màn hình vận hành của Staff vào khu vực Admin.
