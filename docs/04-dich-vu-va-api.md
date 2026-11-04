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
$adminSecret = Read-Host 'Admin123!' -AsSecureString
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
| `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__Username`, `Smtp__Password`, `Smtp__EnableSsl` | Máy chủ email đặt lại mật khẩu. Mặc định dùng cổng 587 và STARTTLS.                   |
| `Images__Directory`                                                                                       | Thư mục lưu ảnh tải lên. Mặc định là `.tiemche/images` dưới thư mục nội dung API. |

## Điểm cuối chính cho WPF

| Tiền tố                  | Thao tác                                                                                                                                                                      |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `/api/auth`              | `register`, `login`, `forgot-password`, `reset-password`.                                                                                                              |
| `/api/profile`           | Đọc và sửa hồ sơ, đổi mật khẩu qua `POST /password`.                                                                                                               |
| `/api/catalog`           | `home`, `categories`, `products` và `products/{id}`. Danh sách nhận `search`, `categoryId`, `minPrice`, `maxPrice`, `sort`, `page` và `pageSize`.    |
| `/api/customer`          | `addresses`, `wishlist`, `cart`, `checkout`, `orders` và `products/{id}/reviews`. Chi tiết đơn có dữ liệu để in hóa đơn trong WPF.                     |
| `/api/operations/orders` | Nhân viên và quản trị xem đơn, cập nhật trạng thái qua `PUT /{id}/status` và xác nhận chuyển khoản qua `POST /{id}/confirm-transfer`.                      |
| `/api/admin`             | Quản lý danh mục, món, topping, coupon, banner, đánh giá, khách và nhân viên. Biểu đồ lấy dữ liệu từ `dashboard`. Tải Excel từ `reports/revenue.xlsx`. |

- `POST /api/auth/login` trả `accessToken` có hiệu lực 2 giờ. Gửi `Authorization: Bearer <accessToken>` vào các điểm cuối có phân quyền. Khi phiên hết hạn, WPF đưa người dùng về màn hình đăng nhập. API trả lỗi dạng Problem Details với thông báo trong `detail`.
- Khi thêm hoặc sửa giỏ, gửi `version` từ giỏ gần nhất. Gọi `POST /api/customer/checkout/quote` để tính tiền. Khi khách xác nhận, gửi `expectedTotalVnd` từ kết quả tính tiền cùng `cartVersion` và một `requestId` mới đến `POST /api/customer/checkout`. Nếu gửi lại cùng lần đặt do lỗi mạng, giữ nguyên `requestId`. Khi API trả `409` vì dữ liệu thay đổi, tải lại dữ liệu và xác nhận lại.
- Tải ảnh bằng `POST /api/admin/images` với dữ liệu nhị phân và `Content-Length`. API nhận PNG hoặc JPEG tối đa 5 MB và trả đường dẫn `/images/…` để lưu cùng món, topping hoặc banner.
- Báo cáo nhận ngày `from` và `to` dạng `yyyy-MM-dd` theo giờ Việt Nam. Truyền `byMonth=true` để nhóm theo tháng. Doanh thu là tổng thanh toán của các đơn đã hoàn thành và đã thu tiền trong kỳ.
