# AUDIT_REPORT - SaigonChe

Ngày bàn giao: 02/11/2026 (Asia/Saigon). Branch Nhom3_Hoang.
Phạm vi: sửa và kiểm chứng chức năng hiện có; phần thiếu giữ backlog.
Kết quả ở working tree để review; không tạo commit, push hoặc PR trong đợt audit.

## 1. Tổng quan kiến trúc

- SDK .NET 10.0.401, C# 14; ASP.NET Core MVC/Razor, Identity cookie, EF Core 10.0.13, SQL Server.
- SaigonChe.Web: UI khách/Staff/Admin, nghiệp vụ và truy cập SQL trực tiếp.
- SaigonChe.Api: API catalog thử nghiệm chỉ đọc, giữ URL/JSON đọc.
- SaigonChe.Web.Tests: xUnit, SQL Server LocalDB 17, host Kestrel thật và Playwright .NET 1.64.1.
- Luồng: browser -> MVC action/ViewModel -> service/DbContext -> transaction SQL -> redirect/view.
- Dùng chung OrderCompletionService, OrderWorkflowRules, ProductImageService, VietQrPayment.
- Đã khảo sát controller/service/model/ViewModel/Razor/action binding/JS/CSS/config/test; migration và snapshot đối chiếu với EF model và chạy nâng cấp. Không viết lại thư viện bên thứ ba.

### Bản đồ route → form → action → dữ liệu → kết quả

| Route/giao diện | Form/action và nghiệp vụ | Entity/transaction | Kết quả hiển thị |
|---|---|---|---|
| / và Home/Contact | Tìm/lọc JS; Contact POST validation | Product/Category read; ContactMessage SaveChanges | Thực đơn/empty state; thông báo liên hệ |
| /Cart, /Cart/Checkout | AddToCart/UpdateQuantity/RemoveItem; Checkout GET/POST | Session; server giá; Order + Items + History trong SaveChanges transaction; CheckoutToken unique | Giỏ; lỗi form hoặc thông báo mã đơn |
| /Account/Login, Register, Logout | AccountController; Identity validation/Customer role | AspNetUsers/Roles/cookie/security stamp | Validation; redirect; đăng xuất |
| /Identity/Account/Manage | Razor Identity profile/change password | UserManager, SecurityStamp | Cập nhật hồ sơ; cookie/password |
| /Admin/Categories | CreateCategory/EditCategory/DeleteCategory | Category; Serializable query+write; kiểm tra Product | Form lỗi hoặc danh sách sau redirect |
| /Admin/Products | CreateProduct/EditProduct/DeleteProduct | ProductImageService + Product; SaveChanges, cleanup file; OrderItem/InventoryMovement history guard | Form lỗi; ảnh/món cập nhật hoặc ẩn |
| /Admin/ContactMessages | MarkContactMessageRead/DeleteContactMessage | ContactMessage SaveChanges | Trạng thái đã đọc/danh sách |
| /Admin/Orders, /OrderOperations | UpdateOrderStatus/Update + OrderWorkflowRules/OrderCompletionService | Order/Items/History/InventoryMovement/Voucher; Serializable + RowVersion | Hàng đợi, trạng thái hoặc conflict |
| /Pos, /Pos/Order/{id} | Start/AddItem/UpdateItem/RemoveItem/Customer/Voucher/Pay/Cancel | Order/Items/Table/Voucher/InventoryMovement/History; Serializable + version | Nháp, lỗi tồn/cash/config, đơn paid hoặc hủy |
| /Pos/Receipt/{id} | Receipt GET chỉ đọc | Order + snapshot Items | Bill; in lại không ghi giao dịch |
| /Party/Create, MyRequests, Details | Create/Update; owner/role guard, version và transition | PartyRequest + History/Order/Items/InventoryMovement; Serializable | Trạng thái/tư vấn/báo giá; một đơn Party |
| /OperationsAdmin/* | AdjustStock/SaveDiningTable/ToggleDiningTable/SaveVoucher | Product/InventoryMovement/Table/Voucher; UTC conversion, stock transaction | Form và bảng validation |
| /Staff/* | Create/Edit/ResetPassword/SetLocked | UserManager/IdentityResult; Serializable; stamp; last Admin guard | Danh sách/form hoặc lỗi có thể sửa |
| /Reports | Index/ExportExcel cùng BuildReportAsync | Order paid+completed/Items, AsNoTracking, UTC range | Dashboard, Excel; browser print tạo PDF |
| /api/categories, /api/products | GET list/id | AppDbContext catalog read | JSON 200/404; ghi 404; SQL lỗi 503 |

Các action ghi MVC áp dụng anti-forgery; quyền được kiểm tại server. Kết quả test cụ thể ở mục 10.

## 2. Danh sách chức năng và trạng thái

Quy ước: Đúng và đã kiểm chứng; UI chưa có nghiệp vụ; nghiệp vụ chưa nối UI; có lỗi; chưa triển khai; chưa thể kiểm chứng.
Chỉ đánh dấu đã kiểm chứng khi có bằng chứng hành vi.

| STT | Chức năng | Trạng thái sau sửa | Bug phát hiện/ghi nhận | Ưu tiên | Hướng xử lý/bằng chứng |
|---|---|---|---|---|---|
| 1 | Startup/migration/seed | Đúng và đã kiểm chứng trên DB audit | Seed lỗi khi có danh mục riêng và chưa có món | HIGH | Fresh/legacy/custom-category tests; giữ DB người dùng |
| 2 | Thực đơn/tìm/lọc | Đúng và đã kiểm chứng | Empty catalog hiển thị món mẫu ID cố định | HIGH | Empty state thật; tìm có/không kết quả và lọc trên browser |
| 3 | Giỏ/checkout COD | Đúng và đã kiểm chứng | Gửi lặp có thể tạo nhiều đơn | HIGH | Session token + unique key; hai POST tạo một order; server repricing |
| 4 | Liên hệ/hộp thư | Đúng và đã kiểm chứng | Thiếu bằng chứng luồng lưu/read/delete | MEDIUM | HTTP -> SQL xác nhận ghi, đọc, xóa và input rỗng |
| 5 | Danh mục | Đúng và đã kiểm chứng | Optional description bị bắt buộc; race trùng/xóa | HIGH | Nullable VM, Serializable; CRUD và tạo trùng đồng thời |
| 6 | Sản phẩm/ảnh | Đúng và đã kiểm chứng | Thiếu bounds/FK; giả ảnh; nguy cơ file rác | HIGH | Save/replace/delete, invalid MIME/size/path/FK, IO failure |
| 7 | Đăng ký/login/logout | Đúng và đã kiểm chứng các đường ứng dụng | Route Identity mặc định song song bypass đường custom | HIGH | Hợp nhất login/register; Customer role/CSRF/direct URL tests |
| 8 | Hồ sơ/đổi mật khẩu | Đúng và đã kiểm chứng | Dùng Identity Razor UI | MEDIUM | HTTP đổi phone/password, đăng nhập lại |
| 9 | Quản lý Staff | Đúng và đã kiểm chứng | Trim trước validation; bỏ IdentityResult; last Admin đa role | HIGH | Create/edit/reset/lock, duplicate email, cookie cũ, last Admin |
| 10 | POS nháp/món/notes | Đúng và đã kiểm chứng | Child-only edit không tăng version cha | HIGH | Aggregate UPDATE; stale submission bị từ chối |
| 11 | Cash/QR manual | Đúng và đã kiểm chứng | Thiếu guard QR phía server ở tiệc | HIGH | Tiền thừa/thiếu, config thiếu/giả, payment/time/actor/stock SQL |
| 12 | Tồn/bàn/voucher | Đúng và đã kiểm chứng | UTC edit sai ngày, enum/null/overflow, bypass ngừng bàn | HIGH/MEDIUM | Tồn âm/hợp lệ; voucher round-trip; final stock/use và bàn đồng thời |
| 13 | Đặt tiệc | Đúng và đã kiểm chứng | Thiếu version; nguy cơ update cũ | HIGH | Owner isolation, tư vấn/chốt một order, stale/complete/cancel |
| 14 | Báo cáo/Excel | Đúng và đã kiểm chứng | OOXML namespace gây HTTP 500, date/enum biên | HIGH | ZIP/XML hợp lệ; row/revenue theo ranh giới UTC/VN |
| 15 | PDF/bill | Đúng và đã kiểm chứng với dữ liệu audit | Chữ trắng, grid ép cột, chart méo/tràn, trang trắng | MEDIUM | Print CSS riêng, chart bitmap; render PDF bằng Poppler |
| 16 | Responsive/theme/navigation | Đúng và đã kiểm chứng theo ma trận | POS trắng trên trắng; Staff tới Admin; resize khóa scroll | MEDIUM | 120 screenshot, menu/resize và form thật ba vai trò |
| 17 | API/route cũ | Đúng và đã kiểm chứng | Dead handler update FindAsync thiếu key | LOW | Bỏ handler ghi, GET 200/404, verb ghi 404, mất SQL -> 503 |
| 18 | Testimonial/ưu đãi landing | UI chưa có nghiệp vụ review/promotion online | Nội dung landing tĩnh | Backlog | Không coi là tính năng đánh giá/khuyến mãi đã hoàn thành |
| 19 | Lịch sử đơn online của khách | Chưa triển khai | Chưa có controller/view | Backlog | Không bổ sung theo lựa chọn phạm vi |
| 20 | Topping, ví, realtime, quản lý Customer riêng | Chưa triển khai | Chỉ nằm trong roadmap | Backlog | Ngoài đợt sửa hiện tại |\n| 21 | SMTP/quên mật khẩu email | UI chưa có tích hợp email | Chưa có SMTP/provider nghiệp vụ | Backlog | Không giả lập việc gửi thư thành công |\n| 22 | Identity nâng cao/2FA | Chưa thể kiểm chứng end-to-end | Chưa có môi trường tích hợp | Chưa đánh giá | Không tính hoàn thành |

## 3. Những chức năng đã hoàn thành

Có kiểm tra phản hồi HTTP và trạng thái SQL cho catalog, giỏ/checkout, contact inbox, Staff, POS, bàn/tồn/voucher, tiệc, báo cáo và quyền.
Browser thực hiện click tìm/lọc/thêm món/checkout/đặt tiệc, tạo danh mục, Staff vào vận hành/tạo POS/thêm món/confirm cash/in.
Test không chỉ kiểm tra file code hoặc HTML tồn tại.

## 4. Những chức năng còn thiếu

Giữ backlog mục 2. Chưa bổ sung lịch sử/chi tiết đơn online của khách, topping, đánh giá thật, quản lý Customer riêng, ví và realtime.
Không bổ sung hoàn tiền, đặt cọc, ghép/tách bàn, nguyên liệu, hóa đơn điện tử hoặc quy tắc phí giao hàng mới.
VietQR là nhân viên đối chiếu và xác nhận thủ công; không có giao dịch ngân hàng trong test.
SMTP/quên mật khẩu email, 2FA và các trang Identity nâng cao chưa nghiệm thu end-to-end.
Drive đặc tả đầy đủ chưa truy cập được, nên không tuyên bố hoàn thành toàn bộ đặc tả môn học.

## 5. Danh sách bug và cách sửa

| ID | Mức độ | Nguyên nhân | Sửa đổi |
|---|---|---|---|
| B01 | HIGH | Pkcs transitive 9.0.11 có advisory | Pin 9.0.21, đồng bộ EF 10.0.13 |
| B02 | HIGH | Product VM không khớp Entity/FK | Bounds, category-exists, lỗi lưu trên form |
| B03 | HIGH | Upload chỉ kiểm extension, logic lặp | MIME/signature/5 MiB/path; cleanup/old-file ordering |
| B04 | HIGH | Catalog fallback dựng món giả | Empty state thật; ảnh lỗi dùng placeholder nội bộ |
| B05 | HIGH | Sửa OrderItems không đổi Order rowversion | Force aggregate UPDATE trong SaveChanges |
| B06 | HIGH | EF bọc SqlException deadlock | Dò inner exception; request thua nhận 409, không ghi lặp |
| B07 | HIGH | Checkout không có idempotency key | Token Session và unique filtered index |
| B08 | HIGH | Party không kiểm form cũ | RowVersion additive + hidden token + transaction |
| B09 | HIGH | Identity route mặc định bypass custom | GET về route custom; POST cũ bị chặn; giữ Manage |
| B10 | HIGH | IdentityResult bị bỏ qua/last Admin đa role | Transaction, kiểm tra result, security stamp/guard |
| B11 | MEDIUM | UTC điền vào date input Việt Nam | Round-trip VN/UTC; enum/ngày biên/null/overflow checks |
| B12 | HIGH | xmlns gắn vào XElement chưa qualified | Namespace đúng cho OOXML |
| B13 | MEDIUM | Màu cố định, grid/viewport in | Theme tokens, print CSS, bitmap chart, bỏ min-height |
| B14 | HIGH | Seed First tên mặc định trên danh mục riêng | Đảm bảo danh mục cần thiết, giữ custom category |
| B15 | MEDIUM | Link Staff tới trang Admin/thiếu entrypoint | Navigation theo role, ẩn kênh thử nghiệm cho Staff |
| B16 | MEDIUM | Implicit required trên mô tả tùy chọn | Nullable VM, null-safe xử lý |
| B17 | LOW | Clipboard lỗi vẫn báo copy thành công | Hiển thị failure khi browser từ chối |
| B18 | LOW | Handler ghi API đã vô hiệu còn lỗi | Xóa dead handlers; giữ API chỉ đọc |
| B19 | HIGH | Check danh mục rồi ghi ngoài transaction | Serializable bao query/save/delete, tránh duplicate/cascade race |
| B20 | MEDIUM | Form không hiện đủ validation, mất input | Summary/attempted values/empty states/panel tokens |

## 6. Nguyên nhân gốc của bug quan trọng

Serializable có thể chọn deadlock victim khi hai request nâng shared lock thành update lock. EF bọc lỗi 1205 trong InvalidOperationException nên catch SqlException trực tiếp không đủ. Request thua được báo conflict; transaction SQL giữ bất biến, không tự retry thanh toán.

Rowversion chỉ tăng khi UPDATE bảng cha; sửa notes trong OrderItems không tự đổi token Order. Mọi sửa POS nay cập nhật aggregate trong transaction SaveChanges.

File và SQL không chung transaction: giữ ảnh cũ đến khi DB lưu thành công; dọn ảnh mới khi thất bại. Lỗi xóa ảnh cũ được log.

Giá trị enum cũ được giữ. Đơn cũ thiếu payment metadata tiếp tục bị loại khỏi doanh thu. CheckoutToken nullable giữ dữ liệu cũ; Party rowversion do SQL sinh.

Xuất Excel trước đó đặt xmlns trên element không có namespace, khiến XmlWriter báo prefix conflict. PDF còn dùng màu/theme và kích thước canvas màn hình; nay dùng bố cục in riêng và snapshot chart đúng tỷ lệ.

## 7. File chỉnh sửa/thêm

- AUDIT_REPORT.md
- README.md
- ROADMAP.md
- SaigonChe.Api/Controllers/CategoriesController.cs
- SaigonChe.Api/Controllers/ProductsController.cs
- SaigonChe.Api/Program.cs
- SaigonChe.Api/SaigonChe.Api.csproj
- SaigonChe.Web.Tests/Integration/AuditFlowTests.cs
- SaigonChe.Web.Tests/Integration/AuditHost.cs
- SaigonChe.Web.Tests/SaigonChe.Web.Tests.csproj
- SaigonChe.Web.Tests/Services/ProductImageServiceTests.cs
- SaigonChe.Web/Controllers/AdminController.cs
- SaigonChe.Web/Controllers/CartController.cs
- SaigonChe.Web/Controllers/HomeController.cs
- SaigonChe.Web/Controllers/OperationsAdminController.cs
- SaigonChe.Web/Controllers/OrderOperationsController.cs
- SaigonChe.Web/Controllers/PartyController.cs
- SaigonChe.Web/Controllers/PosController.cs
- SaigonChe.Web/Controllers/ReportsController.cs
- SaigonChe.Web/Controllers/StaffController.cs
- SaigonChe.Web/Data/ApplicationDbContext.cs
- SaigonChe.Web/Data/DbSeeder.cs
- SaigonChe.Web/Data/Migrations/20261101143018_AuditConcurrencyAndCheckout.Designer.cs
- SaigonChe.Web/Data/Migrations/20261101143018_AuditConcurrencyAndCheckout.cs
- SaigonChe.Web/Data/Migrations/ApplicationDbContextModelSnapshot.cs
- SaigonChe.Web/Models/Order.cs
- SaigonChe.Web/Models/PartyRequest.cs
- SaigonChe.Web/Program.cs
- SaigonChe.Web/SaigonChe.Web.csproj
- SaigonChe.Web/Services/DatabaseErrors.cs
- SaigonChe.Web/Services/OrderCompletionService.cs
- SaigonChe.Web/Services/OrderWorkflowRules.cs
- SaigonChe.Web/Services/ProductImageService.cs
- SaigonChe.Web/Services/VietQrPayment.cs
- SaigonChe.Web/ViewModels/CategoryFormViewModel.cs
- SaigonChe.Web/ViewModels/CheckoutViewModel.cs
- SaigonChe.Web/ViewModels/PartyRequestViewModels.cs
- SaigonChe.Web/ViewModels/ProductFormViewModel.cs
- SaigonChe.Web/ViewModels/UpdateOrderStatusViewModel.cs
- SaigonChe.Web/Views/Admin/Orders.cshtml
- SaigonChe.Web/Views/Cart/Checkout.cshtml
- SaigonChe.Web/Views/OperationsAdmin/DiningTables.cshtml
- SaigonChe.Web/Views/OperationsAdmin/VoucherForm.cshtml
- SaigonChe.Web/Views/Party/Details.cshtml
- SaigonChe.Web/Views/Pos/Index.cshtml
- SaigonChe.Web/Views/Pos/Order.cshtml
- SaigonChe.Web/Views/Pos/Receipt.cshtml
- SaigonChe.Web/Views/Reports/Index.cshtml
- SaigonChe.Web/Views/Shared/_Layout.cshtml
- SaigonChe.Web/Views/Shared/_LoginPartial.cshtml
- SaigonChe.Web/wwwroot/css/site.css
- SaigonChe.Web/wwwroot/images/product-placeholder.svg
- SaigonChe.Web/wwwroot/js/site.js
- SaigonChe.Web/wwwroot/lib/chartjs/LICENSE.md
- SaigonChe.Web/wwwroot/lib/chartjs/chart.umd.min.js
- scripts/Run-Audit.ps1

Các thay đổi local trước audit được giữ: appsettings.json, appsettings.Development.json, NuGet.Config và UserSecretsId trong csproj. Không ghi secret vào báo cáo.
Chỉ tạo migration mới 20261101143018_AuditConcurrencyAndCheckout; không sửa migration cũ.

## 8. Nội dung triển khai và cách chạy lại

- Giữ URL/JSON API đọc và kiến trúc Web trực tiếp SQL. API không mở ghi.
- Bổ sung trường form RowVersion/CheckoutToken; không đổi số enum.
- DB error request trả 409/503 an toàn, không raw exception/connection string.
- Chart.js 4.4.7 lưu nội bộ cùng license; ảnh lỗi có SVG placeholder.
- Chạy: pwsh -NoProfile -File ./scripts/Run-Audit.ps1 -InstallBrowser
- Lần sau bỏ InstallBrowser. Script restore/build/test, browser cache và temp trên ổ chứa repo, phục hồi environment của shell khi xong.
- Test tạo SaigonChe_Audit_<32 hex>, file database nằm trong artifacts trên D; chỉ dọn DB đúng tên fixture. Có thể chọn server qua SAIGONCHE_TEST_SQL_CONNECTION.
- Cấu hình test chỉ nằm trong process; không ghi đè cấu hình cá nhân.
- Database thật chưa được audit áp dụng migration mới. Sao lưu dữ liệu quan trọng trước khi chạy bản mới có auto-migrate.

## 9. Kết quả build/dependency

Baseline: build 3 project 0 lỗi, còn advisory Pkcs, 19 test InMemory đạt.
Sau sửa: restore thành công; build SaigonChe.slnx --no-restore -p:UseSharedCompilation=false đạt 0 warning, 0 error.
EF has-pending-model-changes xác nhận model đồng bộ. CLI dotnet-ef máy đang 10.0.12, package runtime 10.0.13; cảnh báo phiên bản tool không làm migration/test thất bại.
Dependency graph: EF SqlServer -> SqlClient 6.1.7 -> Pkcs 9.0.21.
NuGet vulnerability scan cả direct/transitive: không có package bị cảnh báo trong cả ba project tại thời điểm audit.\n[Advisory Microsoft](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx).

## 10. Kết quả Unit/Integration Test

Regression cuối: **45/45 PASS, 0 failed, 0 skipped**; gồm 24 unit và 21 SQL/HTTP/UI test (một test browser chứa ma trận 120 ảnh và các luồng form). Thời lượng test: 1 phút 8 giây.
Lệnh: pwsh -NoProfile -File ./scripts/Run-Audit.ps1.
TRX: [audit-20261101-174008-3b5486.trx](artifacts/audit-results/audit-20261101-174008-3b5486.trx).
Build trước test: cả ba project đạt 0 warning, 0 error. Không dùng filter loại bỏ test trong lượt regression này.

| Test case | Input | Mong đợi | Thực tế | Trạng thái |
|---|---|---|---|---|
| Giá/voucher/cash | Hết hạn/hết lượt/minimum/>100%/tiền thiếu-thừa | Tính đúng hoặc từ chối | Đúng giá trị/điều kiện | PASS |
| Ảnh | PNG hợp lệ, giả đuôi/MIME, >5 MiB, traversal | Chỉ lưu file hợp lệ trong root | Save/replace/delete/containment đúng | PASS |
| IO/FK/validation | Upload dir bị chặn, FK sai, field rỗng/dài | Không lưu row/file rác | DB/file xác nhận | PASS |
| Category CRUD | Optional description, duplicate, hai POST cùng tên | Optional hợp lệ; tối đa một tên | Row/update/delete đúng | PASS |
| Checkout | Giá DB thay đổi; cùng token gửi đồng thời | Snapshot giá mới, một order | Total/line/count đúng | PASS |
| POS version | Hai form, sửa notes không đổi tiền | Version tăng, form cũ không ghi | Notes/version đúng | PASS |
| Final stock | Hai connection, stock 1 | Một paid, stock 0, một movement | SQL xác nhận | PASS |
| Bàn/voucher cuối | Hai request cùng bàn/lượt cuối | Một open, một use | Count/useCount/stock đúng | PASS |
| Online COD | Chuỗi trạng thái hợp lệ, Admin stale | Thu tiền/trừ tồn một lần | SQL payment/history đúng | PASS |
| QR manual | Thiếu config; config nhận giả | Chặn thiếu, lưu xác nhận hợp lệ | Method/time/actor/stock đúng | PASS |
| Tiệc | Khác owner, stale, confirm lặp, cancel/complete | 404 IDOR; một order; reason/history | SQL xác nhận | PASS |
| Identity | 3 role, legacy route, CSRF, lock/reset/last Admin | Đúng quyền, thu hồi cookie cũ | HTTP/DB đúng | PASS |
| Hồ sơ/password | Phone mới, password mới | Lưu và login lại | Identity/HTTP đúng | PASS |
| Staff CRUD | Tạo, sửa, duplicate email | Chỉ Staff, từ chối trùng | Role/email/cookie đúng | PASS |
| Contact | Valid/empty, read/delete | Đọc/ghi/xóa đúng | SQL đúng | PASS |
| Báo cáo | 16:59:59Z/17:00:00Z, paid/unpaid, date/type biên | Chia ngày VN, Excel khớp | Row/revenue đúng | PASS |
| Legacy migration | Order/line snapshot trước POS migration | Giữ total/name/price/enum | Fresh/upgrade đều đạt | PASS |
| Seed | Custom category, chưa có món | Không crash, giữ category | Seed products + custom còn | PASS |
| DB unavailable | OFFLINE riêng DB audit rồi ONLINE | Web/API 503 rồi phục hồi | Không lộ exception/config | PASS |
| UI | 5 viewport x 2 theme x 12 route; form thật | Không vỡ shell/menu/submit | Screenshot + DB assertions | PASS |
| PDF | In từ theme tối | Chữ rõ, chart đúng tỷ lệ, không blank | Render ảnh kiểm tra | PASS |

Đây là bảng nhóm assertion, không cộng số dòng thành số test xUnit.
Lịch sử kiểm chứng gồm 25/28 (deadlock/Excel/fixture model), 32/33 (selector role), 40/41 (optional description), 44/45 (POS detail tràn mobile), và một lượt 44/45 do assertion test giả định thư mục ảnh rỗng thay vì so sánh trước/sau. Assertion PDF riêng từng sai do dấu phân nhóm số theo culture; đã sửa test so sánh số độc lập culture. Không bỏ qua case hoặc thay SQL bằng InMemory.
Runner ban đầu dùng lại audit.trx nên một số TRX sớm bị ghi đè; không coi đó là bằng chứng đầy đủ được lưu. Các lượt mới dùng tên duy nhất, giữ cả kết quả thất bại còn có và regression cuối. Host log/artifacts của các lượt vẫn được giữ.
PDF được kiểm cả dữ liệu rỗng và dữ liệu cố định độc lập gồm hai đơn/hai món, tổng 75.000 đồng tại ranh giới năm 2031/2032. Assertion đếm một trang cho dataset nhỏ; report lớn được phép có nhiều trang.

## 11. Kiểm chứng UI thực tế

Kestrel + database SQL riêng + Chromium headless; ảnh từ ứng dụng chạy thật.
- 1920/1366/1024/768/390, light/dark; 12 trang Admin/vận hành, 120 screenshot.
- Customer click tìm/lọc/thêm món/checkout/gửi tiệc; SQL xác nhận sau submit.
- Staff click menu vận hành/tạo POS/thêm món/confirm cash/dialog/receipt.
- PDF báo cáo/bill sinh từ trang thật, render Poppler và xem ảnh.
- Poppler có cảnh báo Type 3 glyph bounding box của font Chromium; ảnh cuối vẫn đọc đầy đủ, không thấy ô vuông/chồng/cắt chữ.
- Cốc Cốc cá nhân không được công cụ Windows hỗ trợ URL policy; dùng Chromium test riêng, không dùng phiên/mật khẩu cá nhân.

Bằng chứng cuối (được gitignore, giữ tại máy):
- [Thư mục lượt chạy](artifacts/audit/20261101-174009-549d79/).
- [Trace Chromium](artifacts/audit/20261101-174009-549d79/browser-trace.zip).
- [Excel báo cáo](artifacts/audit/20261101-174009-549d79/report.xlsx).
- [PDF báo cáo](artifacts/audit/20261101-174009-549d79/report.pdf) và [bill](artifacts/audit/20261101-174009-549d79/receipt.pdf).
- [POS mobile](artifacts/audit/20261101-174009-549d79/staff-pos-order-mobile.png), [desktop](artifacts/audit/20261101-174009-549d79/staff-pos-order-desktop.png), [bill trên UI](artifacts/audit/20261101-174009-549d79/staff-receipt.png).
- Host log và QR host log cùng thư mục.
- PDF fixed-data độc lập đã render/xem tại artifacts/audit/20261101-173914-fa661a/report-review-1.png; file một trang đủ hai đơn, hai món và tổng 75.000 đồng.

## 12. Vấn đề còn mở và giới hạn

- Backlog mục 4 không được triển khai theo lựa chọn của người dùng.
- Không reset/khóa Admin thật, xóa hoặc sửa dữ liệu thật.
- Chưa chứng minh nguyên nhân mọi lỗi startup LocalDB trong lịch sử. Lần này instance khởi động và DB audit hoạt động; không sửa registry/xóa instance.
- QR chỉ kiểm URL và xác nhận tay trên dữ liệu giả; không ngân hàng thật/hóa đơn điện tử.
- Test tập trung correctness/concurrency, chưa là benchmark tải lớn.
- Clipboard hệ điều hành, SMTP và Identity nâng cao chưa nghiệm thu tích hợp ngoài.
- Các bản sửa chưa commit/push; cần review working tree và cấu hình local trước khi đưa vào repo.

## 13. Đánh giá cuối cùng

**Đạt 10/11 nhóm nghiệm thu audit = 90,91%.** Mỗi nhóm có trọng số 1/11; phần chưa kiểm chứng không tính đạt. Đây là checklist riêng của đợt audit, không phải tỷ lệ M1–M6 lịch sử.

| STT | Tiêu chí nghiệm thu | Kết quả |
|---|---|---|
| 1 | Khảo sát và bản đồ route/form/action/entity; trạng thái chức năng | Đạt — mục 1–2 |
| 2 | SQL host thật, DB audit cách ly, migration fresh/legacy và seed | Đạt — SQL tests; DB thật giữ migration cũ |
| 3 | Vá dependency, restore/build, quét direct/transitive | Đạt — 0 warning/error; không package bị cảnh báo |
| 4 | CRUD/validation/ảnh; lỗi IO và rollback khi DB từ chối | Đạt — unit + HTTP/SQL/filesystem |
| 5 | Version/idempotency/transition; tồn/voucher/bàn đồng thời | Đạt — request trên connection độc lập |
| 6 | Role/CSRF/IDOR/Staff/khóa/reset/cookie/last Admin | Đạt — HTTP/Identity/SQL |
| 7 | UTC/VN, voucher, báo cáo/Excel/PDF theo dataset cố định | Đạt — biên ngày/tháng/năm, giá trị file khớp |
| 8 | Viewport/theme/navigation/form chính/bill/print | Đạt — Chromium thật, screenshot và PDF render |
| 9 | Regression toàn solution, không bỏ qua test | Đạt — 45/45, 0 skipped |
| 10 | Báo cáo, README, ROADMAP và runner tái lập | Đạt — file bàn giao trong working tree |
| 11 | Identity nâng cao và khôi phục mật khẩu qua email end-to-end | Chưa kiểm chứng/blocked — chưa có SMTP/provider tích hợp |

Không còn lỗi CRITICAL/HIGH đã xác nhận chư    a xử lý trong các luồng đã kiểm chứng. Có thể demo các luồng catalog → giỏ/COD, POS → cash/bill, tiệc → hoàn tất, báo cáo và quyền với dữ liệu demo phù hợp; **chưa đánh dấu toàn bộ sản phẩm sẵn sàng demo** vì mục 11 và backlog chưa nghiệm thu.

Kiểm tra cuối: git diff --check sạch; model không có pending changes; không còn database SaigonChe_Audit_*; HEAD vẫn c86ea82 trên Nhom3_Hoang. Không commit/push/PR. Database SaigonCheDb không nhận migration audit mới và tài khoản thật không bị reset.
Tỷ lệ test đạt chỉ phản ánh bộ test đã nêu; không thay cho mức hoàn thành toàn bộ roadmap môn học.
Chỉ nghiệm thu chức năng có bằng chứng; phần thiếu/chưa kiểm chứng được ghi riêng.
