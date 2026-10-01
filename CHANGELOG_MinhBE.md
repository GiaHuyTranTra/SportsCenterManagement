# Báo Cáo Thay Đổi Backend - Nhánh MinhBE

Tài liệu này tổng hợp các tính năng, cải tiến và thay đổi đã thực hiện trên Backend (`SportsCenterManagement`) so với nhánh chính (`master`/`main`).

---

## 1. Tính Năng & Thay Đổi Chính

### 1.1. Xác Thực Email OTP (Email Verification OTP)
- **Luồng hoạt động**:
  - Hỗ trợ gửi mã xác nhận 6 số ngẫu nhiên qua email khi:
    - Yêu cầu xác thực đăng nhập (`POST /api/Auth/request-login-email-verification`).
    - Yêu cầu xác thực đăng ký hội viên (`POST /api/Account/request-register-email-verification`).
  - Hạn chế spam (Rate limit / Cooldown): Mỗi yêu cầu cách nhau 60 giây (`CooldownSeconds = 60`).
  - Hạn sử dụng mã OTP: 5 phút (`ExpirationMinutes = 5`), tối đa 5 lần nhập sai trước khi bị hủy.
  - Bảo mật mã OTP: Mã được băm (`HashPassword`) trước khi lưu vào `IMemoryCache`.
  - **Cơ chế Dev Fallback**: Trong môi trường `Development`, nếu chưa cấu hình SMTP hoặc gửi thư thất bại, API sẽ trả về `demoCode` để hỗ trợ kiểm thử giao diện cục bộ.

### 1.2. Dịch Vụ Quản Lý Hồ Sơ Tài Khoản (Account Profile)
- Thêm model và DTO quản lý hồ sơ: [AccountProfileAPIViewModel.cs](file:///d:/SWP/SportsCenterManagement/APIViewModel/AccountProfile/AccountProfileAPIViewModel.cs) và `UpdateAccountProfileAPIViewModel`.
- Thêm API xem và cập nhật thông tin cá nhân:
  - `GET /api/Account/profile`
  - `PUT /api/Account/profile`
- Hỗ trợ xem thông tin chi tiết của các vai trò (`CenterManager`, `Coach`, `Member`, `Receptionist`).

### 1.3. Mở Rộng Quyền Hạn Cho Nhân Viên Lễ Tân (Receptionist)
- Cập nhật [MemberController.cs](file:///d:/SWP/SportsCenterManagement/SportsCenterManagement/Controllers/MemberController.cs):
  - Cho phép cả vai trò `Receptionist` truy cập các API xem danh sách hội viên (`GET /api/Member`) và chi tiết hội viên (`GET /api/Member/{accountId}`).

### 1.4. Quản Lý Hoá Đơn Thành Viên (Membership Invoices)
- Cập nhật [MembershipInvoiceController.cs](file:///d:/SWP/SportsCenterManagement/SportsCenterManagement/Controllers/MembershipInvoiceController.cs) và [MembershipInvoiceService.cs](file:///d:/SWP/SportsCenterManagement/Services/MembershipInvoiceService/MembershipInvoiceService.cs):
  - Thêm `GET /api/MembershipInvoice`: Hỗ trợ lọc theo `memberId`, `status`, `search`, `paymentMethod`.
  - Thêm `POST /api/MembershipInvoice/{invoiceId}/cancel`: Cho phép hủy hóa đơn đang ở trạng thái `PENDING_PAYMENT` và hoàn trả gói dịch vụ tương ứng.
  - Phân quyền cho `Member` được phép xem/thao tác hóa đơn của chính mình.

### 1.5. Cấu Hình Email Bảo Mật (Zero Secret Leak)
- Tất cả thông tin nhạy cảm (Host, Username, Password, FromEmail) được cấu hình thông qua **.NET Secret Manager (`dotnet user-secrets`)** hoặc biến môi trường, tuyệt đối **không lưu trực tiếp vào repository/appsettings.json**.
- Mẫu cấu hình trong `appsettings.json` chỉ chứa các giá trị trống (`""`) làm placeholder.

---

## 2. Danh Sách Các File Thay Đổi & Thêm Mới

### Thêm mới:
1. `APIViewModel/AccountProfile/AccountProfileAPIViewModel.cs` - DTO xem và cập nhật hồ sơ cá nhân.
2. `APIViewModel/Auth/EmailVerificationAPIViewModel.cs` - DTO yêu cầu gửi mã và phản hồi mã OTP.
3. `Services/EmailVerificationService/IEmailVerificationService.cs` - Interface dịch vụ xác thực OTP.
4. `Services/EmailVerificationService/EmailVerificationService.cs` - Xử lý cache OTP, cooldown, băm và đối soát mã.
5. `SportsCenterManagement.Tests/EmailVerificationServiceTests.cs` - Bộ kiểm thử tự động (Unit Tests) cho dịch vụ email verification.
6. `CHANGELOG_MinhBE.md` - Báo cáo tổng hợp thay đổi.

### Chỉnh sửa:
1. `SportsCenterManagement/Controllers/AuthController.cs` - Tích hợp endpoint yêu cầu OTP đăng nhập và kiểm tra mã OTP khi đăng nhập.
2. `SportsCenterManagement/Controllers/AccountController.cs` - Tích hợp endpoint yêu cầu OTP đăng ký, xem và cập nhật profile.
3. `SportsCenterManagement/Controllers/MemberController.cs` - Mở rộng quyền cho `Receptionist`.
4. `SportsCenterManagement/Controllers/MembershipInvoiceController.cs` - Thêm API lấy danh sách và hủy hóa đơn.
5. `Services/AccountService/AccountService.cs` & `IAccountService.cs` - Thêm logic quản lý hồ sơ và xác thực mã đăng ký.
6. `Services/EmailService/EmailService.cs` & `IEmailService.cs` - Cải tiến gửi OTP qua SMTP.
7. `Services/MembershipInvoiceService/MembershipInvoiceService.cs` & `IMembershipInvoiceService.cs` - Bổ sung lọc danh sách và hủy hóa đơn.
8. `Services/MemberSubscriptionService/MemberSubscriptionService.cs` - Cập nhật trạng thái subscription khi thanh toán/hủy.
9. `SportsCenterManagement/Program.cs` - Đăng ký DI cho `IEmailVerificationService`.
10. `SportsCenterManagement/appsettings.json` - Bổ sung mục cấu hình `Email` sạch không lộ thông tin bí mật.

---

## 3. Hướng Dẫn Cấu Hình SMTP Cục Bộ (Local Development)

Để hệ thống gửi email thật trên máy cục bộ, lập trình viên cấu hình qua lệnh bí mật (User Secrets) mà không ảnh hưởng git:

```bash
cd SportsCenterManagement
dotnet user-secrets set "Email:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:Port" "587"
dotnet user-secrets set "Email:EnableSsl" "true"
dotnet user-secrets set "Email:Username" "<EMAIL_CUA_BAN>"
dotnet user-secrets set "Email:Password" "<MAT_KHAU_UNG_DUNG_16_KY_TU>"
dotnet user-secrets set "Email:FromEmail" "<EMAIL_CUA_BAN>"
dotnet user-secrets set "Email:FromName" "Sports Center Management"
```

---

## 4. Kết Quả Kiểm Thử (Tests & Build)
- **Build Status**: Thành công (0 Error).
- **Unit Tests**: 20/20 test cases vượt qua thành công (`SportsCenterManagement.Tests`).
