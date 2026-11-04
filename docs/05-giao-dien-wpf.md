# Bước 5 — Giao diện WPF và điều hướng

Project `src/TiemChe.Wpf` dùng .NET 8, CommunityToolkit.Mvvm, MaterialDesignInXamlToolkit và LiveCharts2. `App.xaml` ánh xạ mỗi ViewModel sang View tương ứng. `Views/MainWindow.xaml` hiển thị menu theo vai trò và `Navigator` chuyển màn hình. `Services/ShopApi.cs` gọi API qua HTTPS với DTO từ `TiemChe.Contracts`.

## Chạy thử

Chuẩn bị .NET 10 SDK để build solution và .NET Desktop Runtime 8 để chạy WPF. Mở PowerShell tại thư mục chứa `TiemChe.sln`. Áp dụng migration và tạo tài khoản quản trị theo [hướng dẫn bước 4](04-dich-vu-va-api.md).

Chạy API ở cửa sổ PowerShell thứ nhất.

```powershell
dotnet run --project .\src\TiemChe.Api
```

Chạy WPF ở cửa sổ PowerShell thứ hai.

```powershell
dotnet run --project .\src\TiemChe.Wpf
```

Địa chỉ API mặc định là `https://localhost:7048/`. Nếu chạy API ở địa chỉ khác, đặt `$env:TIEMCHE_API_URL = 'https://dia-chi-api:7048/'` trước khi chạy WPF. Máy khách cần tin cậy chứng chỉ HTTPS của API.

## Các màn hình

| Khu vực | Màn hình và luồng chính |
|---|---|
| Tài khoản | Đăng nhập, đăng ký, quên mật khẩu và hồ sơ. Màn hình hồ sơ có form đổi mật khẩu. |
| Khách hàng | Trang chủ có banner, danh mục, món bán chạy và món mới. Thực đơn có bộ lọc và phân trang. Chi tiết món có ảnh, size, topping, số lượng, đánh giá, yêu thích và thêm giỏ. |
| Đặt hàng | Giỏ hàng, sổ địa chỉ, thanh toán, lịch sử đơn và chi tiết đơn. Khách kiểm tra tổng tiền trước khi xác nhận. Chi tiết đơn hỗ trợ hủy đơn hợp lệ, mua lại và đánh giá món trong đơn đã hoàn thành. |
| Nhân viên | Danh sách đơn có tìm kiếm và lọc trạng thái. Chi tiết đơn cho phép cập nhật trạng thái hợp lệ và xác nhận chuyển khoản. |
| Quản trị viên | Tổng quan doanh thu với LiveCharts2 và xuất Excel. Các trang quản lý món/size, danh mục, topping, coupon, banner, đánh giá, khách hàng và nhân viên. |

Nút **In hóa đơn** trong chi tiết đơn mở hộp thoại in của Windows. Máy in được chọn trong hộp thoại. Nút **Xuất Excel** ở trang Tổng quan tải báo cáo từ API rồi mở hộp thoại chọn nơi lưu.

## Kiểm tra nhanh

1. Mở thực đơn và tìm `che buoi`. Kiểm tra tìm kiếm không dấu và giá hiển thị theo VNĐ.
2. Đăng ký hoặc đăng nhập khách hàng, lưu địa chỉ, thêm một món với topping, tính tổng và đặt đơn.
3. Đăng nhập nhân viên để cập nhật trạng thái đơn. Với đơn chuyển khoản, xác nhận thanh toán trước khi chuyển sang **Đang giao**.
4. Đăng nhập quản trị viên, lọc báo cáo và xuất Excel. Mở **Món và size** để thử sửa thực đơn hoặc tải ảnh.
