# Bước 4 — Services, Repository và API

## Các module

| Module                                             | Trách nhiệm                                                                                                                                          |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `TiemChe.Contracts`                              | DTO dùng chung giữa API và WPF cho xác thực, danh mục, giỏ, đơn và quản trị.                                                               |
| `TiemChe.Application/Services`                   | Xử lý tài khoản, danh mục, đánh giá, địa chỉ, yêu thích, giỏ, đơn hàng, quản trị và báo cáo.                                     |
| `TiemChe.Application/Interfaces/ITiemCheData.cs` | Hợp đồng truy cập dữ liệu và giao dịch. `TiemCheDbContext` triển khai hợp đồng, các dịch vụ dùng truy vấn EF Core theo nghiệp vụ. |
| `TiemChe.Infrastructure/Services`                | Hash BCrypt, gửi email SMTP, lưu ảnh PNG/JPEG và tạo báo cáo Excel.                                                                             |
| `TiemChe.Api/Endpoints`                          | Điểm cuối HTTP và chính sách Customer, Staff và Admin.                                                                                          |

Migration `AddUserSecurityStamp` thêm mã phiên vào tài khoản. API thu hồi phiên cũ khi đổi hoặc đặt lại mật khẩu, khóa hoặc mở lại tài khoản.

## Chạy API trên máy phát triển

Từ thư mục solution, áp dụng [migration SQLite](03-du-lieu-sqlite.md) trước. Dùng cùng cấu hình `ConnectionStrings__TiemChe` khi chạy migration, tạo admin và khởi động API.

```powershell
$env:Seed__AdminEmail = 'admin@example.com'
$adminSecret = Read-Host 'Nhập mật khẩu quản trị (ít nhất 10 ký tự)' -AsSecureString
$env:Seed__AdminPassword = [System.Net.NetworkCredential]::new('', $adminSecret).Password
try {
    dotnet run --project .\src\TiemChe.Api -- --bootstrap-admin
} finally {
    Remove-Item Env:Seed__AdminPassword
    $adminSecret.Dispose()
}

dotnet dev-certs https --trust
dotnet run --project .\src\TiemChe.Api -- --urls https://localhost:7048
```

Lệnh bootstrap chỉ tạo quản trị viên đầu tiên. Đăng nhập bằng tài khoản này để cấp tài khoản nhân viên. Tài khoản khách được tạo qua `POST /api/auth/register`.

| Cấu hình                                                                                                  | Mục đích                                                                                        |
| ----------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `ConnectionStrings__TiemChe`                                                                              | Chuỗi kết nối SQLite.                                                                           |
| `Checkout__ShippingFeeVnd`                                                                                | Phí giao theo đồng. Mặc định là 15.000 ₫.                                                  |
| `Checkout__BankName`, `Checkout__AccountNumber`, `Checkout__AccountName`                              | Điền đủ để bật chuyển khoản và hiển thị thông tin nhận tiền trên đơn.            |
| `Checkout__BankBin`                                                                                       | Mã BIN ngân hàng dùng cho VietQR. Có thể bỏ trống nếu `BankName` là tên ngân hàng phổ biến đã được hỗ trợ sẵn. |
| `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__Username`, `Smtp__Password`, `Smtp__EnableSsl` | Máy chủ email đặt lại mật khẩu. Mặc định dùng cổng 587 và STARTTLS.                   |
| `Images__Directory`                                                                                       | Thư mục lưu ảnh tải lên. Mặc định là `.tiemche/images` dưới thư mục nội dung API. |

## Điểm cuối chính cho WPF

| Tiền tố                  | Thao tác                                                                                                                                                                      |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `/api/auth`              | `register`, `login`, `forgot-password`, `reset-password`.                                                                                                              |
| `/api/profile`           | Đọc và sửa hồ sơ, đổi mật khẩu qua `POST /password`.                                                                                                               |
| `/api/catalog`           | `home`, `categories`, `products` và `products/{id}`. Danh sách nhận `search`, `categoryId`, `minPrice`, `maxPrice`, `sort`, `page` và `pageSize`.    |
| `/api/customer`          | `addresses`, `wishlist`, `cart`, `checkout`, `orders` và `products/{id}/reviews`. Chi tiết đơn có dữ liệu để in hóa đơn trong WPF.                     |
| `/api/operations`        | Nhân viên và quản trị xem tổng quan, tùy chọn thanh toán, khách đang hoạt động, đánh giá, nhật ký tình trạng món và ca của mình. Đặt dưới `Endpoints/` theo Minimal API. |
| `/api/operations/orders` | `POST /quote`, `POST /`, `GET /`, `GET /{id}`, `PUT /{id}/status`, `POST /{id}/confirm-transfer`, `POST /{id}/notes`; quote/đặt quầy hỗ trợ coupon, khách, idempotency và xác nhận tổng. |
| `/api/operations/customers` | Tìm tối đa 10 khách; `GET /{id}/orders` trả lịch sử đơn phân trang. |
| `/api/operations/shifts` | `GET /current`, `POST /open`, `POST /close`, `GET /` cho lịch sử ca của nhân viên hiện tại. |
| `/api/operations/availability` | Đọc tình trạng món; `GET /log` xem lịch sử; các `PUT` sản phẩm/size/topping ghi audit trong cùng giao dịch. |
| `/api/operations/reviews` | Đọc đánh giá và `PUT /{id}/visibility` để ẩn/hiện; nhân viên không có quyền xóa. |
| `/api/admin/shifts`       | Quản trị viên xem toàn bộ lịch sử ca, có thể lọc theo `staffId`. |
| `/api/admin`             | Quản lý danh mục, món, topping, coupon, banner, đánh giá, khách và nhân viên. Biểu đồ lấy dữ liệu từ `dashboard`. Tải Excel từ `reports/revenue.xlsx`. |

- `POST /api/auth/login` trả `accessToken` có hiệu lực 2 giờ. Gửi `Authorization: Bearer <accessToken>` vào các điểm cuối có phân quyền. Khi phiên hết hạn, WPF đưa người dùng về màn hình đăng nhập. API trả lỗi dạng Problem Details với thông báo trong `detail`.
- Khi thêm hoặc sửa giỏ, gửi `version` từ giỏ gần nhất. Gọi `POST /api/customer/checkout/quote` để tính tiền. Khi khách xác nhận, gửi `expectedTotalVnd` từ kết quả tính tiền cùng `cartVersion` và một `requestId` mới đến `POST /api/customer/checkout`. Nếu gửi lại cùng lần đặt do lỗi mạng, giữ nguyên `requestId`. Khi API trả `409` vì dữ liệu thay đổi, tải lại dữ liệu và xác nhận lại.
- Tải ảnh bằng `POST /api/admin/images` với dữ liệu nhị phân và `Content-Length`. API nhận PNG hoặc JPEG tối đa 5 MB và trả đường dẫn `/images/…` để lưu cùng món, topping hoặc banner.
- Báo cáo nhận ngày `from` và `to` dạng `yyyy-MM-dd` theo giờ Việt Nam. Truyền `byMonth=true` để nhóm theo tháng. Doanh thu là tổng thanh toán của các đơn đã hoàn thành và đã thu tiền trong kỳ.
- Đơn chuyển khoản trả thêm `payment.bankTransfer.qrImageUrl` khi cửa hàng đã cấu hình tài khoản và xác định được BIN ngân hàng. WPF hiển thị ảnh VietQR để khách quét bằng ứng dụng ngân hàng.
- `POST /api/operations/orders/quote` tính giá cho POS từ danh sách món và coupon. Khi nhân viên xác nhận, `POST /api/operations/orders` gửi `requestId` ổn định, `expectedTotalVnd`, khách tùy chọn và chế độ hoàn thành. Đơn thuộc khách (hoặc tài khoản nội bộ không đăng nhập cho khách vãng lai) và lưu riêng nhân viên tạo.
- Đơn tại quầy có kênh `InStore`, không có phí giao; hoàn thành ngay ghi nhận thanh toán, còn gửi bếp tạo đơn `Preparing`. Khi hoàn thành từ màn hình chi tiết, hệ thống ghi nhận đã thu tiền.
- Ghi chú nội bộ trên đơn chỉ trả về trong dữ liệu operator/admin; phản hồi `customer/orders` không serialize trường ghi chú.
