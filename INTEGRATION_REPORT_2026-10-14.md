# Báo cáo tích hợp MinhBE và master

Ngày: 14/10/2026.

## 1. Phạm vi và trạng thái Git

- Nhánh làm việc: `codex/integrate-minhbe-master`, tạo từ `origin/master` tại `79d8147`.
- Hợp nhất `MinhBE` tại `c0d9125`; commit chung là `993f01d`.
- Dùng `git merge --no-commit --no-ff MinhBE`; giải quyết 6 file conflict.
- Chưa commit/push. Git giữ trạng thái merge chờ commit; không chuyển nhánh khi chưa lưu kết quả tích hợp.
- Không sửa schema, không scaffold, không chạy migration/import hoặc thay đổi SQL Server.
- Không thêm hoặc nâng cấp NuGet package.

## 2. Các chức năng giữ từ master

- Member management, soft delete, quick-search phân trang và membership-status.
- Coach, Receptionist và Discipline management; quan hệ CoachDiscipline.
- Tạo Member tại quầy với account, pending subscription và invoice trong transaction.
- Email welcome sau commit; lỗi email không hủy đơn đã lưu.
- AuditLog service/controller, account unlock/status và profile theo role.
- AuthFilter chung kiểm tra blacklist, account tồn tại, inactive/deleted/locked.
- Room, ClassSession, ClassRegistration, DTO, service/controller và test query Sprint 2 của Phúc.

Code ScheduleQuery của Phúc và các entity/mapping trên master không bị chỉnh trong lượt tích hợp này.

## 3. Các chức năng đưa từ MinhBE sang

| Chức năng | API/hành vi |
|---|---|
| OTP đăng ký email | `POST /api/Account/request-register-email-verification`; đăng ký kiểm mã |
| OTP đăng nhập | `POST /api/Auth/request-login-email-verification`; login chung và login legacy đều kiểm mã |
| Đọc profile tương thích MinhBE | `GET /api/Account/profile`, map từ cùng truy vấn profile master |
| Quyền Receptionist | Xem danh sách/chi tiết Member và dùng legacy Create_member |
| Danh sách hóa đơn | `GET /api/MembershipInvoice`, lọc member/status/search/paymentMethod |
| Receipt của Member | Chỉ cho xem receipt thuộc Member hiện tại |
| Hủy hóa đơn pending | `POST /api/MembershipInvoice/{id}/cancel`, hủy cùng pending subscription |
| Danh sách gói | Bổ sung `benefits`, `subscriberCount` |
| Đơn membership | Bổ sung `invoiceId` trong response register/renew |

OTP dùng IMemoryCache, hết hạn sau 5 phút, cooldown 60 giây, tối đa 5 lần sai. Giữ demoCode chỉ ở Development khi gửi SMTP lỗi; Production trả lỗi giao thư.

SubscriberCount giữ nghĩa của MinhBE: số Member khác nhau có subscription của gói, không chỉ subscription đang active.

## 4. Xử lý conflict và điều chỉnh nghiệp vụ

### Authentication

- Cả 5 route login yêu cầu email verification sau khi credentials hợp lệ.
- 4 route legacy tiếp tục trả JWT dạng chuỗi; login chung tiếp tục trả AuthSession với fullName/createdAt.
- Dùng chung helper kiểm OTP trong AuthController, không lặp một bộ kiểm tra khác cho từng role.
- Giữ khả năng dùng mã REGISTER còn hiệu lực để đăng nhập sau đăng ký như MinhBE.
- JWT blacklist vẫn process-local và mất khi restart; chưa thay đổi kiến trúc session.

### Profile

- Giữ `GET /api/Auth/me` và `PATCH /api/Account/profile` của master.
- Thêm `GET /api/Account/profile` theo DTO đọc của MinhBE bằng adapter gọi service profile hiện có.
- Chỉ giữ một engine cập nhật profile: `UpdateCurrentProfileAsync`.
- PATCH giữ contract master: request cập nhật từng phần, thành công `200` không body. Receptionist cập nhật `workShift`; Coach không tự sửa specialization vốn được quản lý qua Discipline.
- FE MinhBE nếu đang chờ object profile sau PATCH cần gọi GET lại. Đây là khác biệt contract cần nhóm tích hợp lưu ý.

### Email và dependency injection

- IEmailService/EmailService có đủ OTP đổi mật khẩu, OTP xác minh email và welcome email.
- Autofac giữ mọi đăng ký của master và thêm IEmailVerificationService.
- Cập nhật fake email service trong test để tương thích interface hợp nhất.

### Hóa đơn

- Member query luôn scope theo identity, bỏ qua memberId khác trong filter; receipt/cancel kiểm ownership.
- Endpoint pay vẫn chỉ Receptionist/CenterManager.
- Cancel mở transaction trên relational provider và khóa Member bằng UPDLOCK/HOLDLOCK trên SQL Server trước khi đọc lại invoice/subscription.
- Pay và cancel dùng cùng điểm khóa Member để tuần tự hóa thao tác trên cùng hội viên.
- Kiểm cặp invoice/subscription còn pending và cùng Member trước khi hủy; retry không đổi bản ghi lần nữa.
- Không tự hoàn tiền, không hủy invoice PAID hoặc subscription CONFIRMED.

## 5. Kiểm thử

Lệnh build:

```text
dotnet build SportsCenterManagement.slnx --no-restore --verbosity quiet -m:1 -p:UseSharedCompilation=false
```

Lệnh test:

```text
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --no-build --no-restore --verbosity quiet
```

Kết quả: build thành công; 115/115 test pass, 0 fail, 0 skipped.

- Thêm 10 test cho cả 5 route login: thiếu OTP bị chặn và OTP được xác minh giữ đúng response shape.
- Giữ 4 test email verification từ MinhBE.
- Thêm 4 test hóa đơn: hủy lặp, ownership, invoice đã trả tiền và query/receipt ngoài quyền.
- Cập nhật HTTP integration test: Receptionist được đọc Member nhưng không được gọi các route quản lý chỉ dành Manager.
- Bộ test dùng InMemory/SQLite và EF metadata; không chứng minh race pay/cancel trên SQL Server thực tế.
- Test host trong sandbox bị treo; chạy ngoài sandbox hoàn tất bình thường.
- Build còn NU1902/NU1903 cho dependency bắc cầu System.Security.Cryptography.Pkcs 10.0.0 trong test project. Chưa thay package trong phạm vi tích hợp.
- Không có var trên các dòng C# thêm/sửa; không còn conflict marker.

## 6. Việc Sprint 2 còn lại

Chưa coi tích hợp này là hoàn thành Sprint 2.

1. Chốt ownership UC18/20/31 vì prompt cũ và Unified giao khác nhau.
2. Thống nhất Discipline hiện có với Sport trong Unified; không tạo hai danh mục trùng nghĩa.
3. Phúc điều chỉnh query routes và payload theo Unified; bổ sung filter sportId, class/sport mapping, pagination và quyền truy cập.
4. Chốt DTO Coach (master dùng disciplines[]) và audit filter (master dùng from/to/accountId), đang khác mô tả Unified.
5. Hoàn thiện SQL/schema cho Class, entitlement, Room.roomType, FK/index/check constraint của Session/Registration và notification.
6. Thống nhất cơ chế khóa scheduling/assignment/booking; triển khai command theo thứ tự danh mục → lớp → session → registration → cancellation.
7. Chạy SQL Server integration/concurrency và SMTP/FE end-to-end trên môi trường riêng trước nghiệm thu.

Các điểm của Phúc còn nguyên để phối hợp, không tự sửa controller/service do Phúc sở hữu.
