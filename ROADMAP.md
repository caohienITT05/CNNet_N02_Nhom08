# Lộ trình SaigonChe

Cập nhật 02/11/2026, branch Nhom3_Hoang. Trạng thái này thay cho các checklist khảo sát cũ; bằng chứng chi tiết nằm trong [AUDIT_REPORT.md](AUDIT_REPORT.md).

## Phạm vi đợt hiện tại

Sửa và kiểm chứng chức năng đã có, giữ .NET 10/MVC/Identity/EF Core/SQL Server. Các bản sửa ở working tree tại D:/CNNet_N02_Nhom08 để review; không commit, push hoặc tạo PR trong đợt audit.

Chỉ số 91,21% M1–M6 trước đây là ghi nhận triển khai lịch sử, chưa phải nghiệm thu. Không tiếp tục cộng tỷ lệ đó vào kết quả audit. Nhánh chưa được merge vào main; không mở PR theo yêu cầu hiện tại.

## Kiến trúc và dữ liệu hiện tại

- SaigonChe.Web: MVC/Razor, Identity cookie, Session giỏ hàng, service và DbContext trực tiếp SQL Server.
- SaigonChe.Api: catalog thử nghiệm chỉ đọc; không phải đường ghi dữ liệu của Web.
- SaigonChe.Web.Tests: unit test, HTTP/Kestrel với SQL Server thật và Chromium/Playwright.
- SaigonChe.slnx chứa cả ba project.

Browser -> Web MVC -> service/DbContext -> SQL Server.
API GET catalog -> AppDbContext -> SQL Server.
Tests -> host riêng + database SaigonChe_Audit_<RunId> + browser riêng.

Dữ liệu gồm Category/Product, Identity, Order/OrderItem/OrderStatusHistory, ContactMessage, DiningTable, Voucher, InventoryMovement và PartyRequest. Đơn giữ tên/giá món tại thời điểm mua. Thời gian lưu UTC, nghiệp vụ dùng ngày Việt Nam. Giữ nguyên giá trị số enum đang có.

Migration audit chỉ thêm PartyRequest.RowVersion và Orders.CheckoutToken với unique filtered index; không sửa migration đã áp dụng. Migration này chưa áp dụng vào database cá nhân trong đợt audit.

## Checklist nghiệm thu audit

Mỗi dòng có trọng số bằng nhau. Kết quả cuối: 10/11 nhóm đạt (90,91%); build 0 warning/0 error, 45/45 test đạt. Mục 11 chưa kiểm chứng và không tính hoàn thành. Chi tiết và bằng chứng ở mục 13 của AUDIT_REPORT.

| STT | Nhóm nghiệm thu | Bằng chứng |
|---|---|---|
| 1 | Bản đồ source, route/form/action/entity và phân loại chức năng | Báo cáo mục 1–2 |
| 2 | Khởi động SQL, database test riêng, migration giữ dữ liệu | Fresh/legacy/custom-category SQL tests |
| 3 | Dependency, restore/build sạch | NuGet audit và build toàn solution |
| 4 | CRUD, validation, ảnh và rollback file/DB | HTTP + SQL + filesystem |
| 5 | Thanh toán, idempotency, version, tồn/voucher/bàn đồng thời | Request trên connection độc lập |
| 6 | Role, CSRF, IDOR, Staff, khóa/reset/thu hồi cookie, last Admin | HTTP + Identity + SQL |
| 7 | UTC/VN, voucher và báo cáo/Excel/PDF | Dataset biên ngày/tháng/năm; PDF thật |
| 8 | Desktop/tablet/mobile, theme, menu, các form chính và bill | Chromium + screenshot + PDF render |
| 9 | Regression tự động toàn solution | TRX giữ theo từng lượt chạy |
| 10 | README, báo cáo, kịch bản chạy lại và backlog | Tài liệu tại gốc và Run-Audit.ps1 |
| 11 | Identity nâng cao/khôi phục qua email end-to-end | Chưa kiểm chứng: chưa có SMTP/provider tích hợp |

Các luồng chính đã kiểm chứng và giới hạn của từng nhóm xem AUDIT_REPORT. Không tính mục 11 là hoàn thành.

## Các chức năng đang có

- Catalog và ảnh: quản trị CRUD, tìm/lọc thực đơn, empty state và ảnh dự phòng.
- Giỏ Session và checkout COD: server lấy giá, token chống gửi lặp, giữ giỏ khi lưu thất bại.
- Customer/Staff/Admin: đăng ký công khai Customer; Admin tạo/sửa/khóa/reset Staff; hồ sơ và đổi mật khẩu.
- Đơn online: hàng đợi Staff/Admin, chuyển trạng thái hợp lệ, lịch sử, thanh toán COD và trừ tồn dùng chung.
- POS: bàn/mang về, món/notes, nháp, voucher, cash/QR xác nhận tay, trừ tồn một lần, hủy nháp, in lại.
- Đặt tiệc: yêu cầu khách, owner isolation, tư vấn/báo giá, version, xác nhận một đơn Party, hoàn tất/hủy.
- Quản trị bàn, tồn theo phần và voucher có hiệu lực/giới hạn.
- Báo cáo Admin: paid + completed theo CompletedAt giờ Việt Nam, lọc loại/ngày, biểu đồ ngày/tháng, Excel và in PDF.
- Liên hệ và hộp thư quản trị.
- API GET catalog giữ response cũ; mọi verb ghi tiếp tục bị chặn.

## Backlog sau audit

Chưa triển khai trong đợt này; không coi nội dung landing hoặc roadmap là nghiệp vụ đã hoạt động:

1. Lịch sử/chi tiết đơn online riêng của Customer.
2. Chi tiết món, topping, sắp xếp/phân trang nâng cao theo đặc tả được duyệt.
3. Review sau mua và quản lý Customer riêng.
4. SMTP/quên mật khẩu email, 2FA và nghiệm thu Identity nâng cao.
5. Ví/cổng thanh toán tự động, hoàn tiền, đặt cọc, ghép/tách bàn, nguyên liệu, hóa đơn điện tử.
6. Realtime, dashboard ca làm và đo tải lớn.
7. Hồ sơ báo cáo môn học, ERD/Use Case/slide theo tài liệu Drive khi có quyền truy cập.

Voucher hiện có áp dụng cho luồng vận hành đã kiểm chứng; không tự coi đó là khuyến mãi online đầy đủ.

## Cách kiểm chứng và bước tiếp theo

Chạy bằng PowerShell từ gốc repo:

    pwsh -NoProfile -File .\scripts\Run-Audit.ps1 -InstallBrowser

Lần sau bỏ InstallBrowser. LocalDB/SQL Server cần sẵn sàng; cache và artifacts nằm trên ổ chứa repository. Test chỉ xóa database đúng tên riêng của fixture. Cấu hình thật và tài khoản thật không được dùng cho test.

Sau audit: review working tree và cấu hình local; sao lưu trước khi chạy ứng dụng có auto-migration trên database thật; chạy kịch bản demo trong README. Các thay đổi Git tiếp theo thực hiện khi người dùng yêu cầu, không tự đưa lên main.

## Quy tắc cho công việc tiếp theo

- Chốt tiêu chí chấp nhận, route, quyền và ranh giới dữ liệu trước khi thêm tính năng.
- Giữ nghiệp vụ giao dịch trong Web/service hiện tại; không mặc định chuyển sang API/token.
- Không tin giá/role/user ID từ client; mọi form ghi có validation và anti-forgery.
- Giữ snapshot giá/tên, enum cũ và migration cộng thêm.
- Build và test đúng hành vi bị ảnh hưởng; chỉ đánh dấu hoàn thành khi có bằng chứng.
