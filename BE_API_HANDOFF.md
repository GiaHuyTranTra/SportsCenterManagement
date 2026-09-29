# Công việc BE và danh sách API cần cung cấp

Ngày đối chiếu: 25/09/2026. Tài liệu dành cho nhóm BE.

- **Cần cho chức năng hiện tại (mục 4–8):** SCMS-1, 2, 3, 9, 11, 15 và các chức năng phụ thuộc đã có giao diện. FE hiện dùng mock, chưa gọi các endpoint đề xuất.
- **Có thể dùng trong tương lai (mục 9):** API cho các chức năng mở rộng đã nêu trong mô tả dự án; chưa bắt buộc triển khai trong phạm vi 6 mục trên.
- **FE giữ nguyên:** chỉ xem xét kết nối sau khi BE bàn giao API chính thức. Tài liệu này không giao công việc sửa FE.

Đây là contract đề xuất dựa trên mã nguồn hiện tại, chưa phải các endpoint đã triển khai. Phần 2 ghi riêng API thực sự tìm thấy trong source BE. Chưa kiểm thử HTTP hoặc database BE đang chạy. Không thay đổi file Excel hay code ứng dụng trong lần phân tích này.

## 1. Chức năng hiện tại BE cần hỗ trợ

Các yêu cầu đã bổ sung sau file sprint và đang có trong FE:

- Không phân hạng Basic/Premium. Gói gồm tên, giá, quyền lợi và thời hạn 1/3/12 tháng.
- Nâng gói theo giá: khấu trừ giá trị ngày chưa dùng và bắt đầu đủ kỳ hạn mới từ ngày thanh toán.
- Có trang `/payments/cash` cho Receptionist và Center Manager xác nhận tiền mặt.
- Có tạo Member tại quầy bắt buộc chọn gói. Chức năng này giao với SCMS-13 trong Excel, cần phối hợp Phuc; không coi toàn bộ SCMS-13 là phạm vi gốc của SCMS-15.
- Cần danh sách thành viên để lễ tân chọn người; phần tìm kiếm dùng chung với SCMS-12. Danh mục active dùng chung với SCMS-10.
- AI, lớp học, lịch dạy, điểm danh và báo cáo tổng thể không nằm trong 6 mục này.

## 2. API BE hiện có và điểm chưa khớp

Đã đọc source ở `D:/SWP/SportsCenterManagement`.

| API tìm thấy | Hiện trạng | Việc cần làm để nối FE |
| --- | --- | --- |
| `POST /api/Account/Create_member` | Nhận Email, Password, FullName, Phone, MemberCode; có DateOfBirth/AvatarUrl tùy chọn. Trả chuỗi thông báo | FE public không có Phone/MemberCode, FullName tùy chọn và có Username. BE cần DTO đăng ký public và dữ liệu Username phù hợp các đầu vào này. MemberCode nên do BE sinh. Trả user và phiên đăng nhập để giữ hành vi tự đăng nhập sau đăng ký |
| `POST /api/Auth/Login_center_manager` | Login riêng Manager, trả chuỗi JWT | Cần login chung bằng email/password vì FE không chọn role |
| `POST /api/Auth/Login_coach` | Login riêng Coach, trả chuỗi JWT | Dùng chung service xác thực, trả role và user |
| `POST /api/Auth/Login_member` | Login riêng Member, trả chuỗi JWT | Như trên |
| `POST /api/Auth/Login_receptionist` | Login riêng Receptionist, trả chuỗi JWT | Như trên |
| `POST /api/Auth/Logout` | Đưa token vào IMemoryCache trong 60 phút, trả OK | Thời gian thu hồi phải tới lúc token hết hạn; kiểm tra thu hồi trên mọi API bảo vệ, không chỉ check-token |
| `POST /api/Auth/check-token` | Trả AccountId, Email, Role; có AuthFilter | Chưa đủ fullName/username và thông tin user FE dùng. Bổ sung `/api/auth/me` hoặc mở rộng response này |

Các điểm cụ thể cần sửa:

1. `JwtOptions` và `appsettings.json` đang đặt 60 phút; sprint và FE dùng 24 giờ. Thống nhất thành 1440 phút nếu giữ yêu cầu hiện tại. BE công bố rõ `exp`/`iat` theo giây và trả `expiresAt` dạng ISO-8601.
2. BE đã dùng BCrypt và đếm sai tới 5 lần để khóa, nhưng các nhánh login thành công hiện chưa reset `FailedLoginCount`. FE cần lỗi có `isLocked`, `failedAttemptsRemaining`, thay vì mọi trường hợp cùng trả BadRequest chuỗi.
3. Login chung phải tự xác định role từ DB; không gọi thử lần lượt 4 endpoint vì vừa sai luồng vừa có thể tác động bộ đếm đăng nhập.
4. Role BE là `CenterManager`, `Coach`, `Member`, `Receptionist`; FE dùng `CENTER_MANAGER`, `COACH`, `MEMBER`, `RECEPTIONIST`. BE cần công bố enum role chính thức; đề xuất trả các giá trị uppercase trên trong DTO.
5. `Account` chưa có Username. Cần thêm cột/unique index nếu giữ form hiện tại. Tách DTO đăng ký public khỏi DTO hồ sơ đầy đủ; không điền Phone/MemberCode giả để vượt validation.
6. Các action tạo Manager/Coach/Receptionist trong `AccountController` hiện chưa có `[Authorize]`; `Program.cs` không cấu hình fallback policy. Phải bảo vệ trước khi dùng thực tế, public chỉ được tạo MEMBER.
7. AuthFilter kiểm tra blacklist mới gắn vào `check-token`; chuyển kiểm tra token bị thu hồi vào pipeline chung. IMemoryCache mất khi restart và không chia sẻ giữa nhiều instance: dùng kho phiên/blacklist bền vững hoặc distributed cache phù hợp triển khai.
8. `Program.cs` chưa có AddCors/UseCors. Cần cho phép origin FE cấu hình, gồm origin dev đang dùng; signing key chỉ nằm trong cấu hình bí mật phía server.
9. Trong các controller/entities đã kiểm tra, chưa có MembershipPackage, Subscription, Invoice hoặc API thu tiền. Đây là phần cần xây mới.

Nguồn BE: `SportsCenterManagement/Controllers/AuthController.cs`, `AccountController.cs`, `Program.cs`; `Services/AuthService/AuthService.cs`, `Services/AccessTokenService/AccessTokenService.cs`, `Services/Utils/JwtOptions.cs`, `Services/PasswordHashService/PasswordHashService.cs`; `APIViewModel/Member/CreateMemberAPIViewModel.cs`; `DataAccess/Entities/Account.cs`, `Member.cs`.

## 3. Quy ước contract đề xuất

- Prefix `/api`; JSON camelCase; ID chuỗi. Để khớp FE hiện tại, `user.id` và `memberId` dùng Account.Id của Member, không lẫn MemberCode. MemberCode chỉ là mã hiển thị/tìm kiếm.
- API bảo vệ nhận `Authorization: Bearer <token>`. BE lấy người thao tác và role từ token/DB, không nhận `actor`, `createdBy`, `paidBy` do client quyết định.
- Thành công: `{ "data": ... }`; tạo mới 201, đọc/sửa 200, xóa/logout 204. Đây là quy ước mới đề xuất, không phải response mock hiện tại.
- Danh sách: `{ "data": { "items": [], "page": 1, "pageSize": 20, "total": 0 } }`. BE công bố page bắt đầu từ 1, giới hạn pageSize, thứ tự sắp xếp ổn định và tổng số bản ghi.
- Ngày quyền sử dụng: `YYYY-MM-DD`, có tính cả ngày bắt đầu/kết thúc. BE dùng ngày nghiệp vụ `Asia/Ho_Chi_Minh`; timestamp sự kiện dùng ISO-8601 UTC.
- Tiền VND số nguyên, không gửi chuỗi đã định dạng. Database dùng kiểu tiền chính xác; không dùng float để tính khấu trừ.
- Các request tạo đơn, tạo Member tại quầy và xác nhận thu tiền dùng `Idempotency-Key`. Cùng key/cùng body trả lại kết quả đã lưu; cùng key/khác body trả 409.

Ví dụ lỗi thống nhất:

```json
{
  "code": "ACCOUNT_LOCKED",
  "message": "Tài khoản đã bị khóa sau 5 lần nhập sai liên tiếp.",
  "fieldErrors": {},
  "isLocked": true,
  "failedAttemptsRemaining": 0
}
```

Mã HTTP: 401 sai thông tin/hết phiên; 403 thiếu quyền; 404 không tìm thấy; 409 trùng dữ liệu/đơn đã xử lý/giá đã đổi/báo giá cũ; 422 dữ liệu nhập không hợp lệ; 423 tài khoản bị khóa. BE chuẩn hóa lỗi validation ASP.NET theo contract đã công bố.

## 4. SCMS-1, 2, 3: xác thực

| API đề xuất | Request | Response data | Quyền |
| --- | --- | --- | --- |
| `POST /api/auth/register` | `username, email, password, fullName?` | `AuthSession` | Public; BE luôn gán MEMBER |
| `POST /api/auth/login` | `email, password, rememberMe` | `AuthSession` | Public |
| `GET /api/auth/me` | Không body | `PublicUser` | Đã đăng nhập, token còn hiệu lực/chưa thu hồi |
| `POST /api/auth/logout` | Bearer token, không body | 204 | Phiên hiện tại |

`confirmPassword` hiện được FE dùng để kiểm tra nhập lại; không cần lưu DB hoặc gửi sang BE nếu contract chỉ nhận password. BE vẫn kiểm tra chính password. Public register tạo account + member profile nguyên tử; email trim/lowercase, email và username duy nhất không phân biệt hoa thường. Giữ validation FE: username tối thiểu 3 ký tự, password tối thiểu 8 ký tự/tối đa 72 byte UTF-8 theo giới hạn đang áp dụng với BCrypt; fullName trống dùng username.

```json
{
  "data": {
    "token": "<jwt>",
    "expiresAt": "2026-09-26T03:00:00Z",
    "user": {
      "id": "account-member-123",
      "username": "minh_member",
      "email": "minh@example.com",
      "fullName": "Nguyễn Minh",
      "role": "MEMBER",
      "isLocked": false,
      "createdAt": "2026-09-25T03:00:00Z"
    }
  }
}
```

PublicUser còn có `phone?`, `dateOfBirth?`, `avatar?`; tuyệt đối không trả password/passwordHash. Không cần trả bộ đếm đăng nhập trong PublicUser; lỗi login trả số lần thử còn lại. `rememberMe` quyết định cách lưu phiên trên FE; nếu theo sprint thì cả hai lựa chọn vẫn có hạn tối đa 24 giờ. Chưa cần refresh-token để đáp ứng phạm vi này.

Logout phải thu hồi token ở BE tới hết hạn của token. Sau khi logout thành công, mọi API bảo vệ phải từ chối token đó. Việc xóa phiên cục bộ không thay thế trách nhiệm thu hồi phía server.

## 5. SCMS-9: quản lý gói

| API đề xuất | Dữ liệu | Quyền |
| --- | --- | --- |
| `GET /api/membership-packages?includeHidden=false&search=&page=1&pageSize=20` | Danh sách Package; chỉ gói active mặc định | Member, Receptionist, Manager; `includeHidden=true` chỉ Manager |
| `POST /api/membership-packages` | PackageInput → Package | Manager |
| `PUT /api/membership-packages/{id}` | PackageInput → Package; có version/If-Match để tránh ghi đè thay đổi mới | Manager |
| `PATCH /api/membership-packages/{id}/visibility` | `{ "isActive": false }` → Package | Manager |
| `DELETE /api/membership-packages/{id}` | 204 nếu chưa có lịch sử; 409 `PACKAGE_IN_USE` nếu đã được tham chiếu | Manager |

PackageInput:

```json
{
  "name": "Gói Năm",
  "price": 4200000,
  "durationMonths": 12,
  "benefits": ["Sử dụng phòng gym", "Tham gia lớp tập nhóm"]
}
```

Package trả thêm `id, isActive, createdAt, updatedAt`. Nên thêm `subscriberCount, canDelete, version` để màn hình quản lý không phải tải toàn bộ lịch sử đăng ký chỉ để đếm. `subscriberCount` hiện FE tính theo số Member khác nhau từng có đăng ký, kể cả pending/canceled; BE cần ghi rõ định nghĩa này trong response schema.

Validation theo service hiện tại: name trim, 2–80 ký tự, không trùng không phân biệt hoa thường; price nguyên 1–1.000.000.000; durationMonths chỉ 1/3/12; benefits 1–12 mục, mỗi mục tối đa 200 ký tự, trim và loại trùng. Không có `tier`.

Xóa bị chặn khi có bất kỳ subscription hoặc invoice tham chiếu, không chỉ khi gói đang active. Ẩn gói chỉ chặn yêu cầu mới; không sửa lịch sử đã mua. Sửa tên/giá/quyền lợi không thay đổi snapshot trên hóa đơn cũ.

Excel SCMS-10 yêu cầu danh mục public; FE hiện bảo vệ màn hình chọn gói bằng đăng nhập. Endpoint active có thể cho phép anonymous khi nhóm triển khai SCMS-10, nhưng không mở dữ liệu quản trị/ẩn gói ra public.

## 6. SCMS-11 và SCMS-15: chọn thành viên, báo giá, đăng ký

| API đề xuất | Request/query | Response data | Quyền |
| --- | --- | --- | --- |
| `GET /api/members?search=&page=1&pageSize=20` | Tìm tên/email/phone/memberCode | Danh sách PublicMember | Receptionist, Manager |
| `GET /api/membership-subscriptions?memberId=...&page=1&pageSize=20` | Lịch sử đăng ký; có thể thêm status | Danh sách Subscription | Member chỉ chính mình; staff được theo memberId |
| `POST /api/membership-quotes` | `memberId, packageId, paymentMethod` | Quote do BE tính | Member chính mình; Receptionist, Manager |
| `POST /api/membership-orders` | `quoteId` + Idempotency-Key | `{ subscription, invoice }` | Như báo giá |
| `POST /api/invoices/{id}/cancel` | Không body | `{ subscription, invoice }` đã canceled | Chủ sở hữu hoặc staff; chỉ pending |

PublicMember cần `id, memberCode?, fullName, username, email, phone?, isLocked`; không trả hash. API lịch sử nếu Member không truyền memberId thì mặc định chính họ, nếu truyền ID người khác phải từ chối. Staff có thể xem tất cả theo quyền, tránh để query thiếu memberId vô tình trả toàn bộ dữ liệu cho Member.

Quote trả toàn bộ trường `MembershipQuote` trong FE: `memberId, memberName, memberEmail, packageId, packageName, durationMonths, benefits, paymentMethod, kind, amount, packagePrice, startDate, endDate`, và khi nâng gói: `previousSubscriptionId, previousPackagePrice, creditAmount, remainingDays, previousPeriodDays`.

BE nên bổ sung `quoteId, expiresAt, packageVersion` và version dữ liệu đăng ký liên quan. Đây là phần đề xuất mới; mock hiện chưa có quoteId. Lưu báo giá hoặc ký nội dung cùng kiểm tra version để xác nhận quote là do BE phát hành. Tạo đơn phải tính/kiểm tra lại, không tin amount/dates/kind client gửi. Nếu catalog hoặc lịch sử đã đổi, trả 409 `QUOTE_CHANGED`; trả báo giá mới hoặc thông tin đủ để yêu cầu lập lại báo giá, không tự thu giá khác.

Ví dụ response báo giá minh họa: kỳ cũ 01/09/2026–30/09/2026, lập báo giá ngày 16/09/2026:

```json
{
  "data": {
    "quoteId": "quote-123",
    "expiresAt": "2026-09-16T17:00:00Z",
    "memberId": "account-member-123",
    "memberName": "Nguyễn Minh",
    "memberEmail": "minh@example.com",
    "packageId": "pkg-yearly",
    "packageName": "Gói Năm",
    "durationMonths": 12,
    "benefits": ["Sử dụng phòng gym"],
    "paymentMethod": "CASH",
    "kind": "UPGRADE",
    "packagePrice": 4200000,
    "previousSubscriptionId": "sub-monthly",
    "previousPackagePrice": 450000,
    "remainingDays": 15,
    "previousPeriodDays": 30,
    "creditAmount": 225000,
    "amount": 3975000,
    "startDate": "2026-09-16",
    "endDate": "2027-09-15",
    "packageVersion": 1
  }
}
```

### Quy tắc BE phải thực thi

1. Một Member chỉ có một yêu cầu pending tại một thời điểm; kiểm tra cả invoice và subscription. Gói được chọn phải active, Member không bị khóa.
2. Loại yêu cầu do BE tự xác định, không tin nút FE. Với các kỳ CONFIRMED chưa replaced: tham chiếu kỳ ACTIVE, nếu không có thì kỳ tương lai kết thúc muộn nhất. Không có kỳ còn hạn thì REGISTER nếu chưa có lịch sử confirmed chưa replaced, còn lại RENEW.
3. Cùng packageId hoặc cùng tổng giá với kỳ tham chiếu → RENEW. Kỳ mới bắt đầu sau ngày hết hạn muộn nhất đã trả tiền, hoặc hôm nay nếu đã hết hạn. Giá đổi ở cùng packageId vẫn là gia hạn.
4. Gói khác có tổng giá thấp hơn giá snapshot tham chiếu → DOWNGRADE, xếp sau toàn bộ kỳ đã trả tiền. Không hoàn tiền và không đổi quyền ngay.
5. Gói khác có tổng giá cao hơn, có kỳ ACTIVE và chưa trả trước kỳ tương lai → UPGRADE ngay sau thanh toán. Nếu đã trả trước kỳ tương lai hoặc chưa có kỳ ACTIVE → RENEW nối tiếp, thu đủ giá gói mới, giữ nguyên các kỳ đã mua.
6. So sánh tổng giá gói, không so giá bình quân mỗi tháng. Không suy ra nâng/hạ chỉ từ kỳ hạn hoặc từ tên gói.
7. Nâng gói: `previousPeriodDays = endDateCũ - startDateCũ + 1`; `remainingDays = endDateCũ - ngàyThanhToán + 1`; `creditAmount = round(giáSnapshotCũ × remainingDays / previousPeriodDays)`; `amount = giáGóiMới - creditAmount`. Làm tròn một lần tới đồng; .NET dùng quy tắc midpoint phù hợp Math.round cho số dương, không mặc định banker's rounding.
8. Nâng lên năm có đủ 12 tháng từ ngày thu tiền. `endDate = addMonthsClamped(startDate, durationMonths) - 1 ngày`. Ví dụ 31/01/2026 + 1 tháng → 28/02/2026, ngày cuối sử dụng là 27/02/2026; đây là quy ước code hiện tại, cần giữ nhất quán hai phía.
9. Báo giá nâng gói chỉ có hiệu lực trong ngày nghiệp vụ. Qua ngày khác, hoặc kỳ cũ không còn ACTIVE, từ chối thu; người dùng hủy yêu cầu và lập lại. Không âm thầm đổi tiền trên invoice đã lập.
10. Tạo đơn sinh invoice PENDING_PAYMENT và subscription PENDING_PAYMENT trong một transaction; chưa cấp quyền tập. Chỉ khi thu tiền thành công mới CONFIRMED/PAID. Với upgrade, đánh dấu kỳ cũ `replacedOn`, giữ nguyên hóa đơn cũ.
11. Đăng ký/gia hạn/hạ gói trả tiền trễ sau startDate dự kiến: tính lại kỳ đủ tháng từ ngày thu. Nếu startDate vẫn ở tương lai thì giữ lịch. Kiểm tra không trùng kỳ trước khi commit.

## 7. Tiền mặt, hóa đơn và đăng ký tại quầy

| API đề xuất | Request | Response data | Quyền |
| --- | --- | --- | --- |
| `GET /api/invoices?memberId=&status=&paymentMethod=&search=&page=1&pageSize=20` | Bộ lọc, riêng trang thu tiền dùng CASH/PENDING_PAYMENT | Danh sách Invoice | Member chỉ của mình; staff theo quyền |
| `GET /api/invoices/{id}` | ID | Invoice đầy đủ để xem/in | Chủ sở hữu hoặc staff |
| `POST /api/invoices/{id}/confirm-cash` | `{ "receivedAmount": 3975000 }` + Idempotency-Key | `{ subscription, invoice }` | Receptionist, Center Manager |
| `POST /api/members/counter-registration` | CounterRegistrationInput bên dưới | `{ member, order: { subscription, invoice } }` | Receptionist, Center Manager |

Thu tiền phải kiểm tra CASH, invoice và subscription còn pending, người thu hợp lệ, Member không khóa, số tiền nguyên đúng invoice.amount, báo giá nâng gói chưa cũ và kỳ không trùng. Ghi `paidAt, paidBy, paidByName` từ server. Transaction gồm invoice PAID + subscription CONFIRMED + thay thế kỳ cũ nếu có. Dùng khóa/version để hai lễ tân không thu cùng một hóa đơn hai lần. Retry cùng Idempotency-Key trả kết quả trước; request mới cho hóa đơn đã trả trả 409 `INVOICE_ALREADY_PROCESSED`.

FE đang in bằng `window.print()`/Save as PDF, nên API file PDF chưa bắt buộc. BE phải trả đủ dữ liệu hóa đơn. Nếu cần PDF chính thức do server sinh, thêm endpoint xuất PDF sau khi chốt mẫu.

`BANK_TRANSFER` và `CARD` hiện chỉ ghi nhận phương thức dự kiến, chưa có luồng kích hoạt/đối soát. Không cho endpoint tiền mặt xác nhận hai phương thức này. Tích hợp cổng thanh toán/webhook là công việc riêng, không coi chọn phương thức là đã trả tiền.

CounterRegistrationInput để giữ FE hiện tại:

```json
{
  "fullName": "Nguyễn Minh",
  "email": "minh@example.com",
  "phone": "0901234567",
  "username": "minh_member",
  "password": "<mat-khau-khoi-tao>",
  "packageId": "pkg-monthly",
  "paymentMethod": "CASH",
  "expectedPrice": 450000
}
```

Tạo account + profile + pending subscription + invoice phải cùng transaction; lỗi bất kỳ bước nào rollback toàn bộ, giữ phiên nhân viên hiện tại. MemberCode do BE sinh. expectedPrice chỉ để phát hiện thay đổi, giá thực lấy từ DB; nếu không khớp trả 409 trước khi tạo tài khoản.

Validation hiện tại: fullName 2–80 ký tự; email hợp lệ tối đa 254; username 3–30 ký tự chữ/số/gạch dưới; phone sau bỏ dấu cách/chấm/gạch ngang là 10 chữ số bắt đầu 0 hoặc +84 theo sau 9 số; password 8 ký tự–72 byte; gói bắt buộc và active. Cả email/username duy nhất.

Khác Excel SCMS-13: file yêu cầu ngày sinh, BE tự sinh mật khẩu và gửi email; FE hiện yêu cầu nhân viên nhập username/password, chưa có ngày sinh và không gửi email. Contract trên giữ FE hiện tại. Nếu triển khai đầy đủ SCMS-13 nguyên bản, BE cần contract riêng cho ngày sinh, cơ chế cấp mật khẩu ban đầu an toàn và email service. Đây là phần mở rộng cần chốt trước khi triển khai, chưa nằm trong luồng hiện tại.

## 8. DTO và bảng dữ liệu cần có

| DTO/bảng | Dữ liệu cần lưu/trả |
| --- | --- |
| Account/Member | ID, username, email, passwordHash chỉ nội bộ, role, fullName, phone, memberCode, isLocked, failedLoginCount, createdAt, updatedAt; ngày sinh/avatar nếu dùng |
| MembershipPackage | id, name, price, durationMonths, benefits, isActive, createdAt, updatedAt, version |
| MemberSubscription | id, memberId, packageId, packageName snapshot, durationMonths snapshot, benefits snapshot, amount thực trả, packagePrice tổng giá snapshot, startDate, endDate, kind, status, invoiceId, previousSubscriptionId?, replacedOn?, createdAt |
| MembershipInvoice | id, number duy nhất, subscriptionId, toàn bộ trường Quote, status, createdAt, createdBy, paidAt?, paidBy?, paidByName?, canceledAt?, canceledBy? |
| MembershipQuote | quoteId, dữ liệu báo giá/version liên quan, expiresAt, trạng thái đã sử dụng nếu lưu server-side |
| Session/revoked token | token identifier hoặc hash, accountId, expiresAt, revokedAt; không lưu token thô vào log |
| Idempotency record | key, actor, operation, request hash, trạng thái/kết quả để retry không tạo/thu trùng |

`Subscription.status`: PENDING_PAYMENT / CONFIRMED / CANCELED. `Invoice.status`: PENDING_PAYMENT / PAID / CANCELED. Đừng dùng một enum chung cho cả hai.

Trạng thái hiển thị được suy ra theo thứ tự hiện tại: canceled → pending → replacedOn đã tới → startDate tương lai (SCHEDULED_DOWNGRADE nếu DOWNGRADE, còn lại UPCOMING) → hết hạn → ACTIVE. BE nên trả thêm `displayStatus` theo ngày server để tránh lệch đồng hồ client. `SUSPENDED` của SCMS-14 chưa có luồng trong FE này, cần làm riêng nếu dùng.

Ràng buộc DB: unique email/username/memberCode/invoice number; foreign key; không cascade-delete lịch sử tài chính; unique pending theo Member hoặc cơ chế khóa tương đương; transaction/row version cho quote/order/payment; kiểm tra chồng kỳ trong transaction. Audit ghi tạo/sửa/ẩn/xóa gói, tạo/hủy đơn, thu tiền và người thực hiện, nhưng API xem audit log thuộc SCMS-16.

## 9. API có thể sử dụng trong tương lai

Các đường dẫn sau là đề xuất định hướng theo mô tả dự án, chưa phải API đang được FE hiện tại gọi hoặc yêu cầu phải hoàn thành ngay. BE chỉ triển khai khi chức năng tương ứng được đưa vào kế hoạch và đã chốt nghiệp vụ. Dùng lại API mục 4–7 khi chức năng trùng, không tạo endpoint khác chỉ vì khác màn hình.

| Nhóm | API có thể cần | Dữ liệu/chức năng BE cần hỗ trợ | Quyền dự kiến |
| --- | --- | --- | --- |
| Hồ sơ cá nhân | GET /api/profile/me; PATCH /api/profile/me; POST /api/profile/me/avatar | Họ tên, số điện thoại, ngày sinh, avatar; không cho đổi email qua API cập nhật hồ sơ | Người dùng cập nhật chính mình |
| Khôi phục tài khoản | POST /api/auth/forgot-password; POST /api/auth/reset-password; PUT /api/auth/password | Token đặt lại mật khẩu một lần, có hạn; kiểm tra mật khẩu hiện tại khi đổi; không tiết lộ tài khoản tồn tại qua forgot-password | Chủ tài khoản; reset qua token hợp lệ |
| Quản lý thành viên | GET /api/members/{id}; PATCH /api/members/{id}; PATCH /api/members/{id}/status | Chi tiết hồ sơ, active/inactive; giữ lịch sử gói và hóa đơn; danh sách/tìm kiếm dùng lại GET /api/members | Manager; lễ tân chỉ quyền xem phù hợp |
| Mở khóa và phân quyền | POST /api/accounts/{id}/unlock; GET /api/roles; PATCH /api/accounts/{id}/role | Mở khóa đăng nhập, reset bộ đếm; kiểm tra quyền cấp role, bảo vệ Manager cuối cùng, cập nhật hiệu lực phiên khi đổi quyền | Manager |
| Huấn luyện viên và nhân viên | GET/POST /api/coaches; GET/PATCH/DELETE /api/coaches/{id}; GET/POST /api/staff; GET/PATCH/DELETE /api/staff/{id} | Hồ sơ, chuyên môn, bộ môn, ca làm/lịch làm; chặn xóa khi còn tham chiếu cần bảo toàn | Manager |
| Danh mục và chi tiết gói | GET /api/membership-packages/{id}; GET /api/public/membership-packages | Xem/so sánh gói active, quyền lợi, tổng giá, kỳ hạn; không trả dữ liệu quản trị | Public nếu triển khai SCMS-10 |
| Trạng thái/bảo lưu gói | GET /api/members/{id}/membership-status; POST /api/membership-subscriptions/{id}/suspend; POST /api/membership-subscriptions/{id}/resume | Gói hiện tại, số ngày còn lại, cảnh báo dưới 7 ngày; chính sách bảo lưu/phí/ngày hết hạn phải chốt riêng | Member xem chính mình; staff theo quyền; quyền bảo lưu cần chốt |
| PDF hóa đơn | GET /api/invoices/{id}/pdf | PDF từ dữ liệu hóa đơn đã lưu, trả application/pdf; kiểm tra quyền sở hữu | Chủ hóa đơn, Receptionist, Manager |
| Thanh toán trực tuyến | POST /api/payments; GET /api/payments/{id}; POST /api/payments/webhooks/{provider} | Tạo giao dịch từ invoice, trạng thái đối soát, xác minh webhook, chống callback trùng; chỉ kích hoạt sau xác nhận đáng tin cậy | Chủ hóa đơn/staff; webhook xác thực theo nhà cung cấp |
| Bộ môn và phòng tập | GET/POST /api/sports; PATCH/DELETE /api/sports/{id}; GET/POST /api/rooms; PATCH/DELETE /api/rooms/{id} | Bộ môn, sức chứa, tình trạng phòng và liên kết lớp học | Manager ghi; người dùng liên quan được xem |
| Lớp học và lịch | GET/POST /api/classes; GET/PATCH/DELETE /api/classes/{id}; GET/POST /api/class-sessions; PATCH/DELETE /api/class-sessions/{id} | Lớp, bộ môn, sức chứa, từng buổi, phòng, thời gian; kiểm tra trùng phòng và trùng lịch HLV | Manager quản lý; Member/Coach/Receptionist xem theo quyền |
| Phân công HLV | PUT /api/classes/{id}/coach; GET /api/coaches/me/schedule; GET /api/classes/{id}/members | HLV phụ trách, lịch dạy, danh sách học viên; kiểm tra lịch/chuyên môn và quyền xem lớp được giao | Manager phân công; Coach xem lớp phụ trách |
| Đăng ký lớp | GET /api/class-bookings; POST /api/class-bookings; POST /api/class-bookings/{id}/cancel; GET /api/members/me/schedule | Member, buổi/lớp, trạng thái; kiểm tra gói hợp lệ, còn chỗ, trùng lịch, hạn hủy và chống đặt trùng | Member của mình; Receptionist hỗ trợ |
| Điểm danh | POST /api/check-ins; GET /api/check-ins; PUT /api/class-sessions/{id}/attendance; GET /api/members/{id}/attendance | Check-in trung tâm và điểm danh lớp tách biệt; thời gian, người ghi nhận, trạng thái | Receptionist check-in; Coach lớp phụ trách; Member xem của mình |
| Mục tiêu và kế hoạch tập | GET/PATCH /api/members/{id}/fitness-profile; GET/POST /api/training-plans; GET/PATCH /api/training-plans/{id} | Mục tiêu, trình độ, kế hoạch cá nhân/lớp và bài tập; kiểm tra quan hệ HLV–học viên | Coach được phân công; Member theo quyền |
| Kết quả và nhận xét | GET/POST /api/training-results; PATCH /api/training-results/{id}; GET/POST /api/member-assessments | Kết quả mỗi buổi, tiến độ, đánh giá, người tạo và thời gian | Coach phụ trách ghi; Member xem của mình |
| Thông báo và bài tập | GET/POST /api/notifications; PATCH /api/notifications/{id}/read; GET/POST /api/assignments | Thông báo lịch, đổi lịch, hết hạn gói, bài tập; tác vụ nền gửi đúng đối tượng, trạng thái đã đọc | Người nhận xem; Coach/Manager gửi trong phạm vi quyền |
| Hỗ trợ thành viên | GET/POST /api/support-requests; GET/PATCH /api/support-requests/{id}; POST /api/support-requests/{id}/messages | Nội dung, người gửi, người phụ trách, trạng thái xử lý và trao đổi | Member của mình; Receptionist/Manager xử lý |
| Báo cáo | GET /api/reports/revenue; GET /api/reports/members; GET /api/reports/class-registrations | Lọc khoảng ngày/múi giờ; doanh thu từ giao dịch đã thu, không cộng hóa đơn pending; định nghĩa rõ tổng thành viên/đang hoạt động | Manager |
| Nhật ký thao tác | GET /api/audit-logs | Ai, làm gì, lúc nào, đối tượng; lọc người dùng/thời gian/hành động; không có API xóa log | Manager |
| AI hỗ trợ | POST /api/ai/workout-suggestions; POST /api/ai/conversations; GET /api/ai/conversations/{id}; POST /api/ai/conversations/{id}/messages | Gợi ý tập cho Coach, hỏi đáp cho Member; giới hạn dữ liệu theo quyền, lưu ngữ cảnh phù hợp, quota và lỗi nhà cung cấp | Coach/Member theo chức năng |

Các nhóm tương lai cần thiết kế thêm bảng và migration khi chốt chức năng. API AI không tự thay đổi đăng ký, thanh toán hoặc kế hoạch tập chính thức nếu chưa có thao tác xác nhận tương ứng. API refresh token chỉ bổ sung nếu nhóm đổi sang mô hình access token ngắn hạn + refresh token; không bắt buộc cho yêu cầu JWT 24 giờ hiện tại.

## 10. Thứ tự BE triển khai và bàn giao

1. Chốt DTO đăng ký, username/memberCode, role mapping, envelope lỗi và login chung. BE giao Swagger/OpenAPI cùng base URL, CORS, tài khoản test 4 role và database migration/seed.
2. Hoàn thiện và kiểm thử register/login/me/logout; kiểm tra khóa sau 5 lần sai liên tiếp, đăng nhập đúng reset bộ đếm, token 24h và token logout không gọi được bất kỳ API bảo vệ nào.
3. Xây dựng và kiểm thử danh mục, CRUD/ẩn gói; Member không thấy gói ẩn; xóa gói có lịch sử bị chặn; sửa giá không sửa invoice cũ.
4. Xây dựng báo giá/tạo đơn/lịch sử/hóa đơn; kiểm tra gia hạn còn hạn, hết hạn, cuối tháng/năm nhuận, ngày cuối kỳ và nâng gói năm theo khấu trừ.
5. Xây dựng transaction tại quầy và thu tiền; kiểm tra lỗi rollback, Member không thu được tiền, 2 request đồng thời không thu trùng, pending không có quyền tập, báo giá qua ngày bị chặn.

Các test FE hiện có trong `src/services/authService.test.ts` và `src/services/membershipService.test.ts` là nguồn ca nghiệm thu nghiệp vụ cho BE; không thay thế integration test HTTP/DB của BE.

BE bàn giao API chính thức kèm Swagger/OpenAPI, request/response mẫu, bảng mã lỗi, phân quyền từng endpoint, cách chạy/migration/seed, base URL và tài khoản kiểm thử. Ưu tiên hoàn thành các API phục vụ chức năng hiện tại; mục 9 là danh sách mở rộng để lập kế hoạch sau.
