# UC34 — Cash Payment Confirmation

Ngày triển khai: 03/11/2026. Nền: master `040848b`, nhánh local `codex/integrate-minhbe-master`.
Phạm vi: backend UC34 trong `Sprint3.md`. Không tích hợp nhánh MinhBE, không đổi schema hoặc sửa FE.

## Quy trình FE

1. Lấy danh sách từ `GET /api/MembershipInvoice/search?status=PENDING_PAYMENT&paymentMethod=CASH&page=1&pageSize=20`. Dùng phân trang UC35 hiện có.
2. Nhân viên kiểm tra invoice, nhập lại số tiền và xác nhận đã nhận tiền thật.
3. Gửi `POST /api/MembershipInvoice/{invoiceId}/pay` với token Receptionist hoặc CenterManager:

```json
{
  "paymentMethod": "CASH",
  "amount": 500000,
  "confirmReceivedCash": true
}
```

`amount` là số tiền invoice nhân viên xác nhận, VND nguyên đồng, không phải số tiền đưa dư để tính tiền thối. Server so sánh chính xác với invoice.Amount; không dùng số này để sửa số tiền hóa đơn.

4. `200 OK` trả `MembershipReceiptAPIViewModel` trực tiếp, gồm invoice/subscription, amount, PaidAt, người thu và kỳ hiệu lực.
5. Sau timeout hoặc lỗi cạnh tranh, tải lại invoice/receipt trước khi thao tác lại. `409 INVOICE_ALREADY_PAID` không ghi thêm lần thu; gọi `GET /api/MembershipInvoice/{id}/receipt` để đọc kết quả đã lưu. Không coi lỗi này là yêu cầu thu thêm tiền.

**Thay đổi contract:** `amount` và `confirmReceivedCash=true` nay bắt buộc. FE hiện tại gọi pay chỉ với paymentMethod phải cập nhật; request thiếu xác nhận/số tiền sẽ bị từ chối. Không có thay đổi route hoặc DTO receipt. Các script HTTP Flow 2 cũ còn dùng request pay cũ và dữ liệu initialPassword đã bị UC33 loại bỏ: không coi chúng là smoke test hiện hành cho UC34; cần sửa luồng cấp tài khoản trước khi chạy lại.

## Quy tắc xử lý

- Chỉ CASH, cả paymentMethod trong request và trên invoice. CARD/BANK_TRANSFER không được tự chuyển sang CASH bằng API này.
- Xác minh actor từ database: role, Active, chưa xóa, không khóa; Member cần đúng role, Active, chưa xóa, không khóa.
- Invoice và subscription phải là một cặp PENDING_PAYMENT cùng Member, không có metadata thu tiền trước đó.
- Subscription không đình chỉ, duration hợp lệ; package tồn tại/active, giá và thời hạn hiện tại còn khớp snapshot. Giá/thời hạn đổi phải lập lại đơn, không tự tính lại tiền.
- REGISTER/RENEW phải có invoice.Amount bằng PackagePrice đã chụp, dương và nguyên đồng.
- Khóa SQL Server Member bằng UPDLOCK/HOLDLOCK trong transaction RepeatableRead, cùng quy tắc khóa của cancel pending và tạo đơn. Re-read invoice/subscription sau khi có khóa; hai context thu cùng invoice chỉ được một lần chuyển PAID.
- Thu tiền cập nhật PAID, CONFIRMED, PaidAt UTC, PaidBy từ token; tính kỳ theo BusinessSettings:TimeZoneId. Gia hạn nối sau kỳ CONFIRMED cuối còn hạn, hoặc bắt đầu hôm nay nếu không còn kỳ hiệu lực.
- Audit `MEMBERSHIP_INVOICE_PAID`, entity MembershipInvoice, actor là người thu. Lưu audit, invoice và subscription trong cùng transaction; lỗi rollback toàn bộ.
- Retry sau thành công trả 409 và không thay PaidAt, kỳ gói hoặc ghi audit lần hai.

## Giới hạn upgrade cần giải quyết khi tích hợp MinhBE

Checkout hiện tại chỉ tạo REGISTER/RENEW; entity chưa có quote expiry, credit, base subscription hoặc metadata xác thực nâng/hạ gói. Không tính công thức credit mới trong UC34 và không biến UPGRADE thành RENEW.

- UPGRADE tạo khác ngày trung tâm hiện tại bị chặn `UPGRADE_QUOTE_EXPIRED` theo quy tắc quote cùng ngày trong Sprint3.md.
- UPGRADE cùng ngày vẫn bị chặn `ORDER_KIND_NOT_SUPPORTED`, vì chưa có dữ liệu xác minh quote và cách thay thế gói cũ. Các kind khác REGISTER/RENEW cũng bị chặn.
- Không tuyên bố đã hỗ trợ thu upgrade hợp lệ. Muốn hoàn tất nhánh này cần đối chiếu contract/schema upgrade được nhóm duyệt, rồi tích hợp riêng và test credit/expiry/base-period. Ngày tạo chỉ giúp từ chối quote cũ, không chứng minh quote mới hợp lệ.

## Mã lỗi pay

Lỗi nghiệp vụ có envelope `{success:false,error:{code,message,details},traceId}`. Auth framework có thể trả 401/403 không body; FE cần fallback theo HTTP status.

| HTTP | code | FE xử lý |
|---:|---|---|
| 400 | VALIDATION_ERROR | Thiếu/sai trường theo model validation |
| 400 | CASH_ONLY | Chọn đúng invoice CASH; không coi phương thức khác đã thu |
| 400 | INVALID_CASH_CONFIRMATION | Nhập số tiền nguyên dương và xác nhận đã nhận tiền |
| 409 | PAYMENT_AMOUNT_MISMATCH | Tải lại invoice, kiểm số tiền |
| 409 | PACKAGE_CHANGED | Hủy đơn pending và lập lại |
| 409 | PACKAGE_INACTIVE | Không thu cho gói inactive |
| 409 | INVALID_INVOICE_STATE / INVALID_SUBSCRIPTION_STATE | Tải lại trạng thái, không tự ép thanh toán |
| 409 | UPGRADE_QUOTE_EXPIRED | Lập lại báo giá/đơn upgrade |
| 409 | ORDER_KIND_NOT_SUPPORTED | Không thu cho loại đơn chưa có contract hỗ trợ |
| 409 | INVOICE_ALREADY_PAID | Lấy receipt, không thu lần hai |
| 409 | PAYMENT_CONCURRENCY_CONFLICT | Đối chiếu trạng thái/receipt trước khi retry |
| 403 | FORBIDDEN / ACCOUNT_INACTIVE | Kiểm quyền/tài khoản |
| 423 | ACCOUNT_LOCKED | Tài khoản bị khóa |
| 404 | INVOICE_NOT_FOUND / MEMBER_NOT_FOUND / PACKAGE_NOT_FOUND | Tải lại dữ liệu |
| 500 | DATA_INTEGRITY_ERROR / INTERNAL_ERROR | Dừng thu và kiểm tra backend |

## Kiểm chứng

- `CashPaymentServiceTests`: số tiền/xác nhận, CASH-only, trạng thái, snapshot giá, tài khoản, subscription đình chỉ, quote cũ/kind không hỗ trợ, kỳ gia hạn, audit, retry và lỗi insert audit rollback trên SQLite.
- `CashPaymentHttpTests`: quyền HTTP, input bắt buộc; thu thực tế qua WebApplicationFactory + SQLite cho hai role, amount mismatch, retry/read receipt và audit một lần. Không phải live HTTP trên DB dev.
- `CashPaymentSqlServerTests`: hai request thu đồng thời; thu đối kháng hủy; lỗi sau SaveChanges nhưng trước commit phải rollback.
- SQL tests chỉ chạy khi `SCMS_SQLSERVER_TEST_CONNECTION` trỏ tới DB riêng đã provision có tên `SCMS_Flow2_Test_*`; không ghi connection string vào repo. Test thêm dữ liệu GUID riêng, không xóa dữ liệu cũ, không create/drop DB.

Lệnh regression:

```powershell
dotnet build SportsCenterManagement.slnx --no-restore --verbosity quiet -m:1 -p:UseSharedCompilation=false
dotnet test SportsCenterManagement.slnx --no-build --no-restore --verbosity minimal
```

SQL riêng, sau khi đặt biến môi trường trong phiên chạy:

```powershell
dotnet test SportsCenterManagement.slnx --no-build --no-restore --filter FullyQualifiedName~CashPaymentSqlServerTests
```

Kết quả trực tiếp ngày 03/11/2026:

- Build cuối: 0 warning, 0 error (incremental). Clean compile trước đó còn CS8620 có sẵn ở ClassRegistrationService, không thuộc UC34; không thêm warning mới.
- UC34 service/HTTP: 36 passed; ba test SQL bị skip khi không có biến môi trường.
- Chạy ba SQL test UC34 riêng trên LocalDB test SCMS_Flow2_TestInst / SCMS_Flow2_Test_Concurrency: 3 passed, 0 failed, 0 skipped.
- Regression solution: 286 total, 275 passed, 0 failed, 11 skipped. Mười một test SQL gồm tám Flow 2 chưa chạy lại và ba UC34 đã chạy riêng; không cộng các test trùng thành tổng mới.
- Lần gọi SQL test đầu bị guard từ chối do cấu hình biến môi trường PowerShell sai, chưa ghi dữ liệu. Đã sửa cấu hình và chạy lại đủ ba ca thành công.
- SQL tests thêm fixture GUID vào DB test, không đụng DB dev. Không thay đổi schema hoặc thêm production dependency. Không commit/push.

Chưa nghiệm thu FE và chưa chạy SMTP/production; không dùng test SQLite để khẳng định concurrency SQL Server.
