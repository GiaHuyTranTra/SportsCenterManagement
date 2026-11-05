# Kịch bản kiểm thử Backend end-to-end — Sprint 1 đến Sprint 3

## Trạng thái tài liệu

- Nhánh BE được kiểm tra: `MinhBE`.
- Commit hiện tại: `3448b6b` (`[BE] feat: implement training operations, member progress tracking and business logic improvements`).
- Nhánh `MinhBE` đang ahead `origin/MinhBE` 1 commit; worktree sạch tại thời điểm lập kịch bản.
- Đây là kế hoạch test, **chưa chạy các kịch bản bên dưới** và không khẳng định chức năng đã nghiệm thu.
- Phạm vi lấy từ API/controller hiện tại, `BE_API_HANDOFF.md`, `FE_HANDOFF_SPRINT2_UC17_UC32.md`, `SPRINT2_API_TEST_REPORT_FOR_FE_2026-10-15.md` và tài liệu Sprint 3 trong `docs/`.

## 1. Mục tiêu và cách dùng

Kiểm tra các luồng nghiệp vụ xuyên suốt từ xác thực, thành viên, gói tập và hóa đơn (Sprint 1), lớp/lịch/đặt chỗ (Sprint 2), tới báo cáo và vận hành huấn luyện/quầy (Sprint 3). Mỗi ca cần ghi lại method/path, role, HTTP status, mã lỗi nếu có, ID dữ liệu sinh ra và kết quả kiểm tra trạng thái sau thao tác.

Chạy trên **database test dùng riêng hoặc bản clone có thể bỏ đi**. Không trỏ kịch bản tạo/sửa/xóa dữ liệu vào production hay database dùng chung. Không chạy kiểm thử SQL Server nếu chưa xác nhận rõ server/database đích. Kịch bản không cần và không ghi password, signing key, SMTP credential hoặc token thật vào tài liệu/bằng chứng.

## 2. Chuẩn bị môi trường và dữ liệu

### 2.1. Điều kiện môi trường

1. FE/BE cùng trỏ tới API dev, mặc định `http://localhost:5198`; xác nhận Swagger/OpenAPI mở được.
2. Dùng SQL Server test riêng. Với Flow 2, làm theo `Database/tests/flow2/README.md`: chạy preflight chỉ đọc, kiểm tra baseline, áp dụng schema trên bản test dùng riêng, rồi chạy verify. Đối chiếu các script Sprint 1, Sprint 2 và `Database/2026-11-03-training-operations.sql` với schema nền trước khi áp dụng; không chạy script schema theo suy đoán trên DB hiện có.
3. Cấu hình múi giờ nghiệp vụ là `SE Asia Standard Time`; các ca ranh giới ngày/7 tiếng phải dùng thời gian server và timezone này.
4. Nếu kiểm email, dùng SMTP sandbox/test mailbox. Nếu kiểm cổng thanh toán, chỉ dùng sandbox; không chuyển tiền thật.
5. Sinh token bằng login của từng actor. Token sau logout phải được dùng thử lại để xác nhận bị thu hồi.

### 2.2. Actor/fixture cần có

| Fixture | Vai trò/trạng thái | Dùng cho |
|---|---|---|
| `managerA` | CenterManager, active, không khóa | Quản trị, phân công, báo cáo, phân quyền |
| `receptionA` | Receptionist, active, không khóa | Đăng ký tại quầy, check-in/out, phiếu hỗ trợ |
| `coachA`, `coachB` | Coach active; có bộ môn phù hợp/không phù hợp | Lịch dạy, roster, điểm danh, giáo án, thử truy cập chéo |
| `memberA`, `memberB` | Member active | Đăng ký, đặt/hủy chỗ, hồ sơ tập luyện, ownership |
| `memberInactive`, `memberLocked` | Member lần lượt inactive và locked | Login, quyền, booking, thanh toán/check-in bị chặn |
| `receptionInactive` | Receptionist inactive | Xác nhận actor không hợp lệ bị chặn |

Tạo bộ môn, phòng, lớp, gói, hóa đơn và thành viên với email/memberCode duy nhất theo lần chạy. Lưu ID trả về vào bảng test run; không dùng ID cố định từ máy dev.

### 2.3. Dữ liệu nghiệp vụ tối thiểu

- Một gói active và một gói inactive; một gói tháng, một gói quý và một gói năm với giá khác nhau.
- Subscription của thành viên ở các trạng thái `PENDING_PAYMENT`, `CONFIRMED` đang hiệu lực, `CONFIRMED` tương lai, hết hạn và `SUSPENDED`.
- Invoice `PENDING_PAYMENT`, `PAID`, `CANCELED`; có invoice CASH và phương thức khác để kiểm tra không xác nhận nhầm.
- Một lớp có lịch tương lai còn chỗ, một lớp đầy chỗ, một buổi đã hủy, một buổi đã qua giờ để kiểm tra attendance.
- Hai phiên bản lịch cho ca hủy đúng ngưỡng: giờ bắt đầu cách hiện tại 7 giờ chính xác; ca khác cách 6 giờ 59 phút và 7 giờ 1 phút. Tạo dữ liệu thời gian sát thời điểm chạy hoặc dùng clock fixture hiện có trong test; không sửa giờ hệ thống.
- Member đăng ký hai buổi để kiểm tra số lượt, thành viên khác để kiểm tra ownership/riêng tư.

## 3. Quy ước xác nhận chung

- Với lỗi API, kiểm tra envelope chuẩn có `success=false`, `error.code`, `error.message`, `traceId`; không yêu cầu nội dung `message` cố định nếu contract chỉ cam kết `code`.
- Kiểm tra dữ liệu không đổi sau request thất bại: trạng thái invoice/subscription, BookedCount, registration, attendance, notification và audit log tương ứng.
- Với danh sách, kiểm tra filter và quyền trước, sau đó kiểm `page`, `pageSize`, `totalItems`, `totalPages`; không chỉ xác nhận HTTP 200.
- Với mutation, gọi GET lại để xác nhận trạng thái đã lưu; retry lệnh có rủi ro tạo/thu trùng để kiểm idempotency hoặc lỗi conflict.
- Member không được đọc/sửa dữ liệu Member khác bằng cách thay ID trên URL/query. Coach chỉ được thao tác lớp/học viên/buổi thuộc phạm vi được giao.
- Các trường PII/credential không được lộ trong log, CSV, lỗi, workspace của actor khác hoặc response không thuộc hợp đồng.

## 4. Sprint 1 — tài khoản, thành viên, gói và thanh toán

| ID | Luồng/kịch bản | Các bước chính | Kết quả mong đợi |
|---|---|---|---|
| S1-01 | Đăng ký Member và xác thực email | Gửi yêu cầu OTP đăng ký; xác nhận OTP; đăng ký qua `/api/Account/Register_member`; đăng nhập `/api/Auth/login`; kiểm tra `/api/Auth/check-token`. | OTP sai/hết hạn bị từ chối; email được chuẩn hóa; Member được tạo một lần; response không chứa password hash; token và role khớp. Email trùng bị từ chối. |
| S1-02 | Login, OTP đổi mật khẩu và vòng đời phiên | Login sai mật khẩu; login đúng; yêu cầu OTP đổi mật khẩu; thử OTP sai/hết hạn rồi đổi thành công; xác nhận mật khẩu cũ/mới; logout `/api/Auth/Logout`; dùng token cũ gọi lại API. | Sai thông tin trả 401; OTP chỉ dùng đúng hạn/một lần; mật khẩu mới dùng được và mật khẩu cũ không dùng được; token logout bị thu hồi. Account inactive/locked/deleted không đăng nhập được. |
| S1-03 | RBAC theo bốn vai trò | Dùng token Manager, Receptionist, Coach, Member thử các route quản trị, quầy, lớp và hồ sơ cá nhân. Lặp lại một request không token. | Anonymous nhận 401; role không đủ nhận 403; chỉ role sở hữu/phụ trách đọc được resource; không có response chứa dữ liệu ngoài quyền. |
| S1-04 | Quản lý Member | Manager tạo, đọc chi tiết, tìm kiếm/phân trang, cập nhật, đổi trạng thái và soft-delete tại `/api/Member`. Thử email/phone trùng và account đã xóa mềm. | CRUD đúng; filter/pagination khớp; email/phone trùng bị chặn; soft-delete không xóa lịch sử hóa đơn/đăng ký; Member đã inactive không thực hiện nghiệp vụ bị hạn chế. |
| S1-04a | Hồ sơ cá nhân và khóa tài khoản | Member cập nhật hồ sơ qua `/api/Account/profile`; Manager đổi status/lock/unlock account; thử Member sửa ID của người khác hoặc sửa email nếu endpoint không cho phép. | Hồ sơ chỉ cập nhật field được phép; không đổi email/role bằng request hồ sơ; khóa/inactive chặn login và thao tác bảo vệ; unlock/status change ghi audit. |
| S1-05 | Quản lý Coach và bộ môn | Manager tạo bộ môn; tạo Coach có bộ môn; cập nhật assignment; tắt bộ môn; thử gán mới bộ môn inactive; thử tắt Coach đang có session tương lai. | Chỉ Manager quản lý; quan hệ chuyên môn lưu đúng; bộ môn inactive không được gán mới nhưng lịch sử vẫn giữ; Coach có lịch tương lai bị chặn inactive theo business rule. |
| S1-06 | Quản lý Receptionist | Manager tạo, tìm, xem, sửa, đổi trạng thái Receptionist; thử cập nhật email; thử xóa mềm. | Email không đổi qua endpoint cập nhật; sai role bị 403; thay đổi được ghi audit; actor inactive/locked không thể xác nhận thao tác quầy. |
| S1-07 | Gói tập và quyền bộ môn | Anonymous đọc `/api/MembershipPackage/active`; Manager tạo/sửa/ẩn gói và cấu hình sport tại `/api/MembershipPackage/{id}/sports`; thử xóa gói đã có lịch sử. | Public chỉ thấy gói active; validation giá/thời hạn/quyền lợi hoạt động; gói tham chiếu bởi subscription/invoice không bị xóa; lịch sử giữ snapshot gói cũ. |
| S1-08 | Đăng ký/gia hạn/nâng/hạ gói | Member tạo yêu cầu gói; Receptionist/Manager tạo tại quầy; kiểm quote và các tình huống REGISTER, RENEW, UPGRADE, DOWNGRADE; tạo yêu cầu thứ hai khi còn pending; thanh toán trễ quote. | Loại đơn, số tiền, credit, start/end date do BE tính; yêu cầu pending thứ hai bị chặn; quote thay đổi/hết hạn không được thu; downgrade nối tiếp, upgrade chỉ thay gói sau khi thu thành công; không tin amount từ client. |
| S1-09 | Đăng ký Member tại quầy (UC13/UC33) | Receptionist dùng `/api/Member/counter-registration`; thử dữ liệu hợp lệ, duplicate email/phone, giá gói thay đổi, gói inactive, lỗi email sau commit và lỗi chèn DB giữa transaction. | Tạo account + Member + subscription + invoice nguyên tử; đơn mới ở `PENDING_PAYMENT`; `expectedPrice` sai trả conflict, không tạo bản ghi dở; lỗi email không rollback đơn đã commit; response Sprint 3 không trả `initialPassword`/hash. |
| S1-10 | Thu tiền CASH và hóa đơn | Lễ tân thanh toán invoice CASH pending qua `/api/MembershipInvoice/{id}/pay`; kiểm tra receipt; lặp lại thanh toán; thử invoice canceled/đã paid và BANK_TRANSFER/CARD với endpoint tiền mặt. | Invoice `PAID`, subscription `CONFIRMED` cùng transaction; có collector/time từ server; receipt chỉ có sau khi trả tiền; thu lặp hoặc sai phương thức trả conflict; không kích hoạt gói khi chưa thu. |
| S1-11 | Membership status tại quầy | Gọi `/api/Member/membership-status` với ALL/ACTIVE/EXPIRING/EXPIRED/SUSPENDED/UPCOMING/PENDING_PAYMENT/NONE và search; thử sai role. | Nhóm trạng thái và remainingDays tính theo timezone server; expiring theo khoảng quy định; trạng thái không suy từ FE; chỉ Manager/Receptionist xem được. |

## 5. Sprint 2 — lớp học, lịch và đăng ký

| ID | Luồng/kịch bản | Các bước chính | Kết quả mong đợi |
|---|---|---|---|
| S2-01 | Sports, rooms và classes | Manager tạo/sửa/ẩn sport, room, class; thử tên trùng, ID sai, phòng inactive; thay đổi capacity thấp hơn sĩ số session; đổi sport khi lớp đã có lịch. | Validation/RBAC đúng; phòng inactive không nhận lịch mới nhưng lịch cũ còn lịch sử; không giảm capacity thấp hơn session; sport của lớp đã có session scheduled bị khóa. |
| S2-02 | Lên lịch lặp và phân công Coach | Tạo class DRAFT, sessions đơn/lặp, coachA và room; thử Coach sai sport, Coach trùng lịch, room trùng lịch, class capacity nhỏ hơn room/session và cấu hình recurrence sai. | Tạo đúng số session; Coach phải phù hợp chuyên môn; overlap/room/capacity bị chặn với mã lỗi chính xác; thao tác lỗi không tạo một phần lịch. |
| S2-03 | Mở lớp và danh sách lịch | Chuyển class DRAFT → OPEN; gọi `/api/Class`, `/api/ClassSession`, `/api/ScheduleQuery/available`, `/api/ScheduleQuery/center`; thử Member đọc class chưa OPEN và filter ngày/Coach/room/status. | Member chỉ thấy lớp được mở; danh sách/count/filter và múi giờ đúng; session hủy/đã qua không được hiển thị như chỗ có thể đặt. |
| S2-04 | Member đặt chỗ | Member active có package/sport hợp lệ đặt buổi còn chỗ qua `/api/ClassRegistration/register`; đọc `/api/ClassRegistration/me`, `/api/ClassSession/{id}`; thử đặt lặp, đầy chỗ, ngoài hạn gói, sai sport, trùng giờ. | Registration `BOOKED`, BookedCount tăng đúng một; retry không nhân đôi; case không hợp lệ bị từ chối và giữ nguyên count/lịch cũ. |
| S2-05 | Ngưỡng tự hủy 7 giờ | Dùng member sở hữu booking, hủy buổi cách 7h01; hủy ca đúng 7h; thử ca 6h59; Member khác thử cùng registration; retry sau khi đã hủy. | Cho hủy nếu còn ít nhất 7 tiếng, gồm đúng ranh giới 7h; dưới 7h bị `CANCELLATION_DEADLINE_PASSED`; Member khác bị chặn; hủy thành công giảm BookedCount một lần, giữ lịch sử. |
| S2-06 | Dời lịch và đổi Coach | Manager dời session có booking; thử member hết hạn package, trùng lịch, room/Coach conflict; sau đó thử dời hợp lệ và đổi Coach. | Khi lỗi, giữ session, registration, count và notification nguyên trạng; khi thành công, metadata, Coach mới và thông báo cập nhật chính xác; retry cùng Coach không tạo event trùng. |
| S2-07 | Receptionist đặt/hủy hộ | Receptionist tìm Member bằng `/api/Member/quick-search`; counter-register/counter-cancel; thử lý do hủy không hợp lệ và sai role. | Actor và lý do được ghi nhận; tăng/giảm BookedCount đúng; Member không tự nhận thao tác quầy; Manager/Receptionist đúng quyền. |
| S2-08 | Coach schedule và roster | Coach gọi `/api/ClassSession/me`, xem `/api/ClassSession/{id}/registrations` hoặc route roster; Coach khác và Member thử truy cập ID của lớp không phụ trách. | Coach được giao xem đúng session/roster; Coach khác/Member bị 403/404 theo contract; không lộ thông tin riêng tư. |
| S2-09 | Hủy session/lớp và kiểm tra tác động | Manager đọc cancel-impact; hủy một session; hủy toàn bộ session tương lai của class; thử class có booking; gọi lại lệnh đã hủy. | Preview khớp số session/booking; booking và BookedCount được xử lý nhất quán; audit/notification tạo đúng; không xóa lịch sử; retry trả conflict phù hợp. |
| S2-10 | Notifications và AuditLog | Sau đổi Coach, hủy lịch, booking quầy và đổi trạng thái, đọc `/api/Notification/me`, mark read; Manager lọc `/api/AuditLog` theo actor/action/entity/date. | Notification đúng chủ thể/sự kiện, mark-read ownership đúng; audit ghi đúng actor/action/entity và không có API sửa/xóa log. |

## 6. Sprint 3 — vận hành, báo cáo và xuất dữ liệu

| ID | Luồng/kịch bản | Các bước chính | Kết quả mong đợi |
|---|---|---|---|
| S3-01 | UC33 đăng ký Member tại quầy atomic | Tạo Member qua endpoint một lần; kiểm response, invoice history, membership status; giả lập lỗi email và lỗi DB trong test cô lập. | Response không có credential; account/member/subscription/invoice cùng commit hoặc cùng rollback; email lỗi không yêu cầu tạo lại đơn; trạng thái ban đầu pending. |
| S3-02 | UC35 tìm lịch sử hóa đơn/biên nhận | Dùng `/api/MembershipInvoice/search` lọc member/status/method/search và page; Member thử memberId người khác; Coach truy cập; mở receipt pending/paid/canceled. | Pagination/count tôn trọng filter và ownership; Coach bị chặn; Member không dò invoice người khác; receipt chỉ trả cho invoice PAID và chứa snapshot/collector chính xác. |
| S3-03 | UC36 doanh thu | Manager gọi `/api/reports/revenue` theo DAY/MONTH, CASH, khoảng ngày qua tháng/năm; đối chiếu invoice fixture PAID, pending, canceled, không `paidAt`, phương thức không đối soát. | Chỉ invoice thực thu hợp lệ được cộng; theo `paidAt` và timezone trung tâm; tổng bằng tổng bucket; ngày thiếu có bucket 0; invalid date/group/method trả 400. |
| S3-04 | UC37 báo cáo Member | Manager gọi `/api/reports/members?from=&to=` với fixture active/expired/suspended/upcoming/pending/no subscription/inactive; kiểm ngày biên và range đảo. | Counts distinct Member; from/to inclusive theo ngày trung tâm; metric có thể overlap đúng định nghĩa; response chỉ aggregate, không có PII. Các role khác bị 403. |
| S3-05 | UC38 báo cáo Class/registration | Manager gọi `/api/reports/classes` lọc ngày, class, sport, BOOKED/CANCELED/ALL, pagination; fixture một Member đặt nhiều session và session canceled/archive. | Class/session/registration/distinct counts theo định nghĩa; session canceled không cộng BOOKED hợp lệ; total là toàn bộ filter, không chỉ trang; query sai trả lỗi validation. |
| S3-06 | UC39 CSV export an toàn | Xuất revenue/member/class; kiểm status/content-type/filename/BOM/CRLF/encoding; dùng text fixture có tiếng Việt, dấu phẩy, quote, newline và chuỗi bắt đầu `=`, `+`, `-`, `@`; class vượt ngưỡng export. | CSV dùng cùng filter với JSON, không có ID/PII ngoài aggregate; escape đúng và chống formula injection; total row không tính trùng; vượt ngưỡng trả 413 JSON error chứ không trả CSV giả. |
| S3-07 | Snapshot quầy và Check-in/out | Receptionist đọc `/api/TrainingAndOperations/reception/snapshot`; check-in Member active có package CONFIRMED còn hạn; checkout visit; thử check-in trùng, Member inactive, không có gói, gói hết hạn/đang suspend. | Lượt vào/ra có thời gian/actor chuẩn; không tạo nhiều visit mở; chỉ member đủ điều kiện check-in; checkout chỉ tác động visit hợp lệ. |
| S3-08 | Suspend/Resume subscription | Receptionist suspend gói CONFIRMED đang hiệu lực; Member có booking tương lai; đọc booking/count; resume; thử suspend pending/expired và resume khi không suspended. | Suspend hủy booking tương lai và giảm count đúng; resume cộng bù đúng số ngày bảo lưu/end date; thao tác không hợp lệ trả conflict và không sửa trạng thái. |
| S3-09 | Phiếu hỗ trợ | Receptionist/Manager tạo support request; lọc snapshot/list; cập nhật OPEN → IN_PROGRESS → RESOLVED kèm note; Member/Coach thử đọc/cập nhật; gửi note/category sai. | Lịch sử giữ từng trạng thái/actor/time/note; validation và quyền đúng; không cho role ngoài scope xem dữ liệu quầy. |
| S3-10 | Điểm danh | Coach phụ trách đọc attendance; ghi PRESENT/LATE/EXCUSED/ABSENT và mark completed; thử trước giờ, session canceled, registration không BOOKED, Coach không phụ trách, đánh dấu tất cả. | Chỉ được điểm danh session hợp lệ sau giờ bắt đầu; thống kê cập nhật đúng; không điểm danh/hoàn thành session hủy; quyền ownership được cưỡng chế ở server. |
| S3-11 | Coach workspace, mục tiêu và giáo án | Coach đọc `/api/TrainingAndOperations/coach/workspace`; cập nhật hồ sơ mục tiêu; tạo plan CLASS và INDIVIDUAL; thử student/class ngoài phạm vi, cân nặng ngoài giới hạn, EndDate trước StartDate. | Workspace chỉ gồm học viên/session phụ trách; profile và plan hợp lệ được lưu; dữ liệu vượt giới hạn/ngoài phạm vi bị chặn. |
| S3-12 | Kết quả tập luyện và homework | Coach tạo result cho Member có booking ở session đã bắt đầu và date thuộc plan; thử ngày tương lai, session chưa bắt đầu, không BOOKED, ngoài thời hạn plan; gửi homework notice. | Result hợp lệ lưu đúng %/progress/feedback; các case vi phạm không ghi result/notification; homework notice gửi đúng Member và không sang người khác. |
| S3-13 | Member workspace và quyền riêng tư | Member gọi `/api/TrainingAndOperations/member/workspace`, đọc goal/plans/results/attendance; thử cập nhật profile hợp lệ; Member B thử URL/resource của Member A. | Member thấy dữ liệu của mình và kế hoạch lớp đang theo; không xem chéo; attendance stats khớp; giới hạn profile được enforce. |
| S3-14 | Dynamic permissions | Manager đọc `/api/TrainingAndOperations/permissions`, cập nhật feature/role; thử role không phải Manager và cấu hình rỗng/sai; token các role thử feature trước/sau thay đổi. | Chỉ Manager thay đổi; thay đổi audit được ghi; API kiểm quyền ở server có hiệu lực đúng theo semantics đã cấu hình; không dùng UI-only restriction. |

## 7. Luồng nghiệm thu tổng hợp theo thứ tự nghiệp vụ

Đây là luồng chính để demo/test tích hợp, sử dụng ID động lưu từ response:

1. Manager đăng nhập, tạo discipline, package active, gán package với sport, tạo room, coachA và receptionistA.
2. Public Member đăng ký/xác thực; Manager/Receptionist tạo thêm Member qua UC33 để kiểm quầy.
3. Receptionist kiểm receipt pending, tìm invoice, thu CASH; xác nhận invoice `PAID`, subscription `CONFIRMED`, receipt chỉ đọc được sau thanh toán.
4. Manager tạo class DRAFT, schedule session, gán Coach phù hợp; mở class OPEN.
5. Member đặt chỗ; xác nhận BookedCount, lịch cá nhân, quyền truy cập và notification.
6. Chạy ba ca hủy quanh ranh giới 7 giờ; xác nhận hủy được đúng 7h/7h01, bị chặn ở 6h59; kiểm count và lịch sử.
7. CoachA xem roster/teaching schedule, điểm danh buổi đã bắt đầu, đặt mục tiêu, tạo plan/result và homework.
8. Member đọc workspace cá nhân; CoachB/MemberB thử truy cập chéo và phải bị chặn.
9. Receptionist check-in/out, tạo support ticket, xử lý ticket; suspend/resume subscription của fixture riêng và kiểm booking/endDate.
10. Manager đọc audit, revenue, member, class report và xuất CSV; đối chiếu aggregate với fixtures đã tạo.
11. Thử logout manager và sử dụng lại token; API bảo vệ từ chối token đã thu hồi.

## 8. Điều kiện đạt và bằng chứng cần lưu

- Không có lỗi 5xx ngoài ca cố ý mô phỏng; mã 4xx phải khớp business rule và error code.
- Không có transaction dang dở sau lỗi; mọi count, status, lịch sử, audit và notification được đối chiếu sau thao tác.
- Tất cả ca RBAC/ownership âm phải chặn ở BE, kể cả gọi API trực tiếp không qua FE.
- Lưu log test đã ẩn token/email/phone/credentials; giữ request ID/traceId, status/error code, entity IDs test và kết quả assertion.
- Chỉ ghi `PASS` sau khi chạy trên test DB đã xác nhận; ghi `BLOCKED` nếu thiếu schema/SMTP/sandbox/role/test fixture; ghi lỗi cụ thể thay vì bỏ qua.

### Mẫu ghi kết quả

| Test ID | Run ID | Ngày/Timezone | HTTP + error.code | Entity IDs test | Kết quả | Bằng chứng/Ghi chú |
|---|---|---|---|---|---|---|
| S1-01 |  |  |  |  | NOT RUN |  |

## 9. Test tự động hiện có nên đối chiếu

Trước khi bổ sung test mới, chạy và đối chiếu các suite hiện có theo môi trường test đã cấu hình: auth/account/member, class/room/scheduling/registration, Sprint 3 live/authorization, invoice, report, export và training operations. Các test cần SQL Server thật chỉ được chạy khi `SCMS_SQLSERVER_TEST_CONNECTION` trỏ tới database test đã provision; nếu không có, ghi `SKIPPED/BLOCKED`, không coi là pass. Không chạy fixture live có endpoint/connection mặc định trước khi xác nhận môi trường đích.

## 10. Tài liệu nguồn

- `BE_API_HANDOFF.md`
- `FE_HANDOFF_SPRINT2_UC17_UC32.md`
- `SPRINT2_API_TEST_REPORT_FOR_FE_2026-10-15.md`
- `docs/SPRINT3_PHUC_API_HANDOFF.md`
- `docs/SPRINT3_REVENUE_REPORT_API.md`
- `docs/SPRINT3_CLASS_REPORT_API.md`
- `docs/SPRINT3_REPORT_EXPORT_API.md`
- `Database/tests/flow2/README.md`
