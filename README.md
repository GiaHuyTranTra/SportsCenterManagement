# BÁO CÁO KIỂM THỬ API SPRINT 2 — BÀN GIAO FRONTEND

Ngày tổng hợp: 15/10/2026. Dự án: Sports Center Management System.

## 1. Phạm vi và kết luận

Báo cáo đối chiếu **đủ 37 endpoint** trong §3 của `API_SPECIFICATION_SPRINT2_UNIFIED.md` ngày 13/10/2026: 34 endpoint Sprint 2 và 3 endpoint tái sử dụng Sprint 1, phục vụ UC17–UC32. Đường dẫn thực tế có tiền tố `/api`. `Manager` trong đặc tả tương ứng role **CenterManager**.

Backend đã có các endpoint phục vụ tích hợp FE. Những luồng đặt/hủy chỗ, dời lịch, đổi Coach, hủy các buổi tương lai, quyền riêng tư và thông báo đã có kết quả kiểm thử được báo cáo. **Không kết luận cả 37 endpoint đều đã pass HTTP hoặc Sprint 2 đã hoàn tất FE.** Một số endpoint chưa có lời gọi trực tiếp trong hai script HTTP hiện có; chúng được đánh dấu rõ ở bảng bên dưới.

Nguồn bằng chứng:

- Đặc tả hợp nhất: `API_SPECIFICATION_SPRINT2_UNIFIED.md`, §3–9 và phụ lục A/B.
- Source hiện tại: controller, `APIViewModel/PagedResult.cs`, các bài test và hai script `Database/tests/flow2/test-http-live.ps1`, `test-http-live-extended.ps1`.
- Kết quả chạy do người triển khai Antigravity cung cấp trong báo cáo bàn giao ngày 15/10/2026. Lần tổng hợp tài liệu này **đọc và đối chiếu source, không chạy lại test, không phát sinh dữ liệu DB**. Chưa có bộ log chạy đầy đủ được đính kèm để xác minh độc lập mọi kết quả.

## 2. Môi trường và kết quả chạy đã báo cáo

| Hạng mục | Kết quả đã báo cáo | Giới hạn |
|---|---|---|
| Backend | ASP.NET Core .NET 10, EF Core Database First, SQL Server | FE chưa được kiểm thử trong workspace này |
| DB dev | SQL Server Express, database SportsCenterManagement | Đã nâng cấp Flow 2; có dữ liệu kiểm thử |
| Nâng cấp DB | Backup + RESTORE VERIFYONLY; apply/verify và chạy schema lần hai thành công | Kết quả từ báo cáo triển khai |
| Build solution | 0 warning, 0 error | Không đồng nghĩa mọi hành vi đều không có lỗi |
| Bộ test mặc định | **168 total: 160 passed, 0 failed, 8 skipped** | 8 test SQL cần cấu hình riêng |
| Bộ SQL riêng | **8 passed, 0 failed, 0 skipped** | LocalDB, database test riêng; chưa phải kiểm thử tải nhiều máy |
| HTTP cơ bản và mở rộng | Người triển khai báo chạy thành công | Không bao phủ trực tiếp đủ 37 endpoint/ mọi tổ hợp filter |

Không cộng hai lần số test: 8 test SQL chính là 8 test bị skip ở lần chạy mặc định. SMTP production, FE end-to-end và hiệu năng tải thật chưa nằm trong kết quả này.

## 3. Ma trận toàn bộ 37 API theo thứ tự đặc tả

**Ký hiệu bằng chứng:**

- **H**: có lời gọi endpoint trong script HTTP và người triển khai báo suite pass. Đây là bằng chứng cho kịch bản được mô tả, không khẳng định mọi nhánh của endpoint đều đã test.
- **C**: endpoint có trong controller; chưa thấy lời gọi trực tiếp tương ứng trong hai script HTTP. Không đánh dấu HTTP PASS. Test service của cùng tài nguyên không thay thế kiểm thử contract HTTP.
- **A**: mọi role đăng nhập; quyền dữ liệu bổ sung vẫn áp dụng. **M**: CenterManager; **R**: Receptionist; **TV**: Member; **HLV**: Coach.

HTTP cột thứ tư là **kỳ vọng hợp đồng**, không phải kết quả chạy cho từng dòng. Các endpoint danh sách trả payload phân trang; endpoint chi tiết/thay đổi trả payload trực tiếp theo DTO thực tế.

| # | Method và đường dẫn thực tế | Quyền / UC | HTTP thành công kỳ vọng | Bằng chứng và phạm vi |
|---:|---|---|---|---|
| 1 | GET /api/Sport | A; 17,19,22 | 200 | H: lấy danh mục; chưa chứng minh đủ search/isActive/phân trang |
| 2 | POST /api/Sport | M; 17 | 201 | H: tạo bộ môn cho flow |
| 3 | PUT /api/Sport/{id} | M; 17 | 200 | C: cần bổ sung HTTP sửa tên/mô tả, trùng tên, ID không tồn tại |
| 4 | PATCH /api/Sport/{id}/status | M; 17 | 200 | C: cần HTTP ẩn/hiện và chặn tạo lớp bằng môn inactive |
| 5 | GET /api/Room | A; 18,21,22 | 200 | C: cần HTTP danh sách, search/isActive/phân trang |
| 6 | POST /api/Room | M; 18 | 201 | H: tạo phòng cho flow |
| 7 | PUT /api/Room/{id} | M; 18 | 200 | H: giảm capacity hợp lệ; giảm dưới lịch trả 409 |
| 8 | PATCH /api/Room/{id}/status | M; 18 | 200 | H: inactive giữ lịch cũ; lịch mới bị chặn ROOM_INACTIVE |
| 9 | GET /api/MembershipPackage/{id}/sports | M; 24, bổ sung | 200 | C: cần HTTP cấu hình quyền bộ môn |
| 10 | PUT /api/MembershipPackage/{id}/sports | M; 24, bổ sung | 200 | C: cần HTTP lưu/read-back, sportIds rỗng, ID không hợp lệ |
| 11 | GET /api/Class | A; 19,23 | 200 | C: cần HTTP danh sách và cưỡng chế Member chỉ thấy OPEN |
| 12 | GET /api/Class/{id} | A; 23 | 200 | C: cần HTTP chi tiết OPEN và ẩn DRAFT/CLOSED/ARCHIVED với Member |
| 13 | POST /api/Class | M; 19 | 201 | H: tạo DRAFT; Coach/Receptionist bị 403 |
| 14 | PUT /api/Class/{id} | M; 19 | 200 | C: cần HTTP sửa, capacity, trùng tên và ARCHIVED |
| 15 | PATCH /api/Class/{id}/status | M; 19 | 200 | H: mở OPEN sau khi có session; chưa chứng minh toàn bộ chuyển trạng thái |
| 16 | GET /api/Class/{id}/cancel-impact | M; 30 | 200 | H: đếm 2 buổi tương lai / 1 booking |
| 17 | POST /api/Class/{id}/cancel-future-sessions | M; 30 | 200 | H: bulk cancel; giữ lịch sử/gói; gọi lại 409 NO_CANCELABLE_SESSION |
| 18 | GET /api/ClassSession | A; 18,20,21,22,23,29 | 200 | C: cần HTTP các filter và phạm vi Member; không thay bằng test /me hoặc /{id} |
| 19 | GET /api/ClassSession/{id} | A; 23 | 200 | H: kiểm tra chi tiết, BookedCount trước/sau đặt/hủy/dời lịch |
| 20 | POST /api/ClassSession | M; 21 | 201 | H: tạo buổi; phòng inactive bị 409; Member bị 403 |
| 21 | PUT /api/ClassSession/{id} | M; 21 | 200 | H: ngoài hạn gói, trùng lịch Member bị 409 và giữ trạng thái; dời hợp lệ thành công |
| 22 | PUT /api/ClassSession/{id}/coach | M; 20 | 200 | H: đổi Coach, thông báo đúng bên; gửi lại cùng Coach không thêm thông báo |
| 23 | GET /api/ClassSession/me | HLV; 27 | 200 | H: Coach cũ không thấy buổi đã chuyển; Coach mới thấy đúng buổi |
| 24 | GET /api/ClassSession/{id}/cancel-impact | M; 30 | 200 | C: cần HTTP impact một buổi và đối chiếu lệnh hủy |
| 25 | POST /api/ClassSession/{id}/cancel | M; 30 | 200 | C: có test SQL booking-vs-cancel; chưa có lời gọi trực tiếp trong hai script HTTP |
| 26 | GET /api/ClassSession/{id}/registrations | M / HLV phụ trách; 21,28 | 200 | H: HLV phụ trách xem được; HLV khác/Member 403; kiểm payload riêng tư |
| 27 | GET /api/ClassRegistration/me | TV; 24,25,26 | 200 | H: BOOKED/CANCELED và cô lập Member; cần kiểm thêm toàn bộ filter/phân trang |
| 28 | GET /api/ClassRegistration | R/M; 29 | 200 | C: cần HTTP lọc memberAccountId/includeCanceled/phân trang và quyền quầy |
| 29 | POST /api/ClassRegistration/register | TV; 24 | 201 | H: gói CONFIRMED đặt được; SQL riêng kiểm chỗ cuối/đặt trùng/trùng giờ |
| 30 | POST /api/ClassRegistration/counter-register | R; 29 | 201 | H: đặt hộ, ghi người thao tác, tăng counter |
| 31 | POST /api/ClassRegistration/{id}/cancel | TV sở hữu; 25 | 200 | H: giảm counter; hủy lặp 409; Member khác 403 |
| 32 | POST /api/ClassRegistration/{id}/counter-cancel | R; 29 | 200 | H: lý do ngắn trả 400; hợp lệ ghi người hủy/lý do và giảm counter |
| 33 | GET /api/Notification/me | TV/HLV; 31 | 200 | H: thông báo theo người nhận; kiểm số lượng khi retry; chưa chứng minh đầy đủ isRead/phân trang |
| 34 | PATCH /api/Notification/{id}/read | Người nhận; 31 | 200 | H: mark read; người khác 403 |
| 35 | GET /api/Coach | M; 20,21 | 200 | C: dùng lại Sprint 1; cần HTTP dropdown/search/status/phân trang |
| 36 | GET /api/Member | M/R; 29 | 200 | C: dùng lại Sprint 1; cần HTTP tìm Member tại quầy |
| 37 | GET /api/AuditLog | M; 32 | 200 | H: classId/sessionId/actorId/fromDate/toDate; chưa chứng minh mọi filter/tổ hợp |

Các API ngoài 37 dòng (ví dụ GET Room/{id}, endpoint ScheduleQuery legacy, đăng nhập, thanh toán hóa đơn) không được tính thêm vào số endpoint Sprint 2 của đặc tả. Auth/thanh toán được dùng để chuẩn bị dữ liệu hợp lệ cho test.

## 4. Kết quả nghiệp vụ theo UC17–UC32

| UC | Tiêu chí và kết quả có bằng chứng | FE phụ trách theo bảng phân công |
|---|---|---|
| 17 Sports | Có tạo và danh mục; tên chuẩn hóa có kiểm thử service/normalizer. HTTP sửa và bật/tắt chưa có trong hai script | Long |
| 18 Rooms | HTTP chặn giảm capacity 10 xuống 6 khi session capacity 8; giảm xuống 9 được; inactive giữ lịch cũ, chặn lịch mới | Tâm |
| 19 Classes | HTTP tạo DRAFT, có session rồi OPEN; RBAC chặn Coach/Receptionist. Cần hoàn thiện HTTP sửa/lifecycle/danh sách | Minh |
| 20 Assign Coach | Đổi bằng ID; Member, Coach cũ và mới nhận thông báo; retry cùng Coach không sinh thêm | Tâm |
| 21 Schedule | Dời ngoài hạn gói bị chặn; test cô lập khác phòng/Coach trả đúng MEMBER_SCHEDULE_CONFLICT; ngày/giờ, booking, counter, notification không đổi khi thất bại | Minh |
| 22 Center Schedule | API canonical có filter; test service truy vấn hiện có. Cần thêm HTTP endpoint danh sách và kiểm loading/empty/error trên FE | Long |
| 23 Classes & Sessions | HTTP chi tiết session, counter sau đặt/hủy; cần thêm HTTP chi tiết lớp và scope Member | Long |
| 24 Register | Đặt với subscription CONFIRMED; SQL chứng minh tranh chỗ cuối, chống trùng booking và trùng giờ Member | Minh |
| 25 Cancel | Chủ sở hữu được hủy trước giờ; người khác 403; retry 409 và không hoàn chỗ lần hai | Minh |
| 26 My Schedule | Token quyết định Member; Member khác không nhận booking của người thứ nhất; có lịch sử CANCELED | Long |
| 27 Teaching Schedule | Token quyết định Coach; sau đổi người, lịch của Coach cũ/mới đúng phạm vi | Tâm |
| 28 Roster | Coach phụ trách/M có quyền; Coach khác và Member bị chặn; chỉ thông tin roster cần thiết. Chưa có API điểm danh | Tâm |
| 29 Counter | Cùng luật booking; lưu actor lễ tân; hủy cần lý do 3–500 ký tự. FE thật chưa được rà soát nguồn LocalStorage | Minh |
| 30 Cancel Session/Class | HTTP bulk cancel 2 session/1 booking, không hoàn tiền gói; SQL kiểm đối kháng booking/hủy. HTTP hủy một buổi cần bổ sung | Minh |
| 31 Notifications | Bốn sự kiện, đúng người; mark read và ownership; retry đổi Coach không thêm thông báo | Long |
| 32 Audit | HTTP lọc lớp/buổi/actor/ngày; chỉ đọc; cần thêm test entityId/action/entityType/search và lỗi khoảng ngày | Tâm |

Tên test/nhóm test có thể tra trực tiếp: `ClassServiceTests`, `ClassSessionServiceTests`, `ClassRegistrationServiceTests`, `RoomServiceTests`, `DisciplineServiceTests`, `NotificationServiceTests`, `AuditLogServiceTests`, `ScheduleQueryServiceTests`, `Flow2RegressionTests.cs`, `Flow2HttpTests.cs`, `Flow2SqlServerTests.cs`. Không suy diễn toàn bộ acceptance đã pass chỉ từ tên nhóm test.

### 4.1. Tám ca kiểm thử SQL riêng đã báo cáo PASS

1. `FinalSlot_OnlyOneMemberSucceeds`: một người giữ được chỗ cuối, người còn lại nhận CLASS_FULL.
2. `DuplicateRequest_CreatesOneBookingAndOneNotification`: chỉ một booking và một notification.
3. `SameMember_ConcurrentOverlappingSessions_OnlyOneSucceeds`: cùng Member không giữ hai buổi trùng giờ.
4. `BookingAgainstSessionCancellation_NeverLeavesActiveBooking`: không còn booking hiệu lực trên buổi đã hủy.
5. `ConcurrentScheduleCreation_PreventsCoachAndRoomCollision`: hai lịch cạnh tranh không cùng chiếm Coach/phòng.
6. `ConcurrentCancellation_DecrementsCounterOnce`: hủy đồng thời chỉ giảm counter một lần.
7. `AuditFailure_RollsBackCounterRegistrationAndNotifications`: lỗi audit rollback booking, counter, notification.
8. `RescheduleSession_MemberHasOverlappingSession_ThrowsMemberScheduleConflictAndPreservesState`: dời lịch trùng Member bị chặn dù phòng/Coach không xung đột; giữ dữ liệu.

## 5. Quy ước FE phải dùng khi tích hợp

### 5.1. Auth, response, phân trang

- Gửi `Authorization: Bearer <accessToken>`; không có refreshToken trong contract hiện tại. Không đưa token/OTP vào log hoặc tài liệu nghiệm thu.
- Response thành công trả thẳng payload. Với Axios, `response.data` chính là payload; không mặc định có lớp bọc `data` thứ hai.
- `PagedResult<T>` Flow 2 thực tế có `items`, `page`, `pageSize`, `totalItems`, `totalPages`. **Không có hasNext/hasPrevious trong class này**; FE có thể tính từ page/totalPages. Endpoint Sprint 1 dùng DTO phân trang riêng cần đối chiếu riêng.
- Mặc định page=1, pageSize=20; tối đa 100 theo đặc tả. Danh sách lịch có phân trang: calendar cần lấy đủ các trang của khoảng ngày đang xem.
- Các endpoint `/me` lấy danh tính từ token, không truyền Member/Coach ID để chọn người khác.

Ví dụ payload phân trang Flow 2 (minh họa hình dạng, không phải dữ liệu dev):

```json
{"items":[],"page":1,"pageSize":20,"totalItems":0,"totalPages":0}
```

Lỗi nghiệp vụ qua middleware có dạng sau:

```json
{"success":false,"error":{"code":"MEMBER_SCHEDULE_CONFLICT","message":"Thông báo nghiệp vụ","details":null},"traceId":"<request-trace>"}
```

FE đọc `error.code` để ánh xạ hành vi, `error.message` làm thông báo mặc định. **Không giả định mọi lỗi đều có envelope này:** source hiện tại có nhánh `AuditLogController` trả BadRequest chuỗi khi from > to; Coach/Member có nhánh validation trả chuỗi; lỗi 401/403 của auth có thể không có JSON body. FE cần fallback theo HTTP status và xử lý body rỗng/chuỗi. Đây là điểm chưa đồng nhất cần đưa vào kiểm thử tích hợp.

### 5.2. Query và body chính theo đặc tả

| Tài nguyên | Query/body cần dùng |
|---|---|
| Sport | List: page,pageSize,search,isActive. Create/update: name,description. Status: isActive |
| Room | List: page,pageSize,search,isActive. Create/update: name,location,capacity,roomType. Status: isActive |
| Package sports | PUT: sportIds (mảng int). Mảng rỗng nghĩa allowsAllSports=true, không phải cấm mọi môn |
| Class | List: page,pageSize,search,sportId,status. Create/update: sportId,name,description,capacity,status. Status: status |
| ClassSession | List: from,to,classId,coachId,roomId,sportId,status,page,pageSize. Create/update: classId,coachId,roomId,date,startTime,endTime,capacity,notes |
| Assign Coach | PUT body chỉ coachId; gửi ID tài khoản Coach, không gửi tên |
| Teaching schedule | /me: from,to,status,page,pageSize; không gửi coachId |
| Roster | page,pageSize,search,includeCanceled; mặc định không lấy đăng ký đã hủy |
| Member booking | POST register: sessionId; danh tính Member từ token |
| Counter booking | POST counter-register: sessionId,memberAccountId; actor là lễ tân từ token |
| Cancel registration | Body reason; Member tùy chọn, quầy bắt buộc 3–500 ký tự |
| My registrations | includeCanceled,page,pageSize; implementation hỗ trợ thêm from,to |
| Counter registration list | memberAccountId,includeCanceled,page,pageSize; không có filter sessionId trong controller hiện tại |
| Notification | /me: page,pageSize,isRead. PATCH read: isRead (boolean) |
| Audit | page,pageSize,search,action,entityType,entityId,actorId,fromDate,toDate; implementation thêm classId,sessionId và alias accountId/from/to |

Chi tiết DTO đầy đủ xem `FE_HANDOFF_SPRINT2_UC17_UC32.md` và OpenAPI của backend đang chạy. Bảng trên mô tả contract để tích hợp, không biến các trường chưa chạy HTTP thành bằng chứng PASS.

### 5.3. Lịch, trạng thái, lỗi nghiệp vụ

- Date: YYYY-MM-DD; giờ: HH:mm theo đặc tả. `startsAt`/`endsAt` chứa offset +07:00. Khoảng lịch [start,end): 09:00–10:00 và 10:00–11:00 không trùng.
- Class: DRAFT/OPEN/CLOSED/ARCHIVED. Member chỉ thấy OPEN trên API danh mục/chi tiết lớp. Lịch đăng ký cá nhân cần dùng `/ClassRegistration/me` để giữ lịch sử.
- Session: SCHEDULED/CANCELED/COMPLETED. Không tự thêm background job hoặc chức năng điểm danh. Registration hiện dùng BOOKED/CANCELED.
- Dùng `remainingCapacity` từ server; không coi số chỗ đang hiển thị là bảo đảm đặt thành công. Bắt CLASS_FULL/CONCURRENCY_CONFLICT và tải lại dữ liệu.
- Khi submit: disable nút, hiển thị trạng thái chờ. Sau thất bại dời lịch, khôi phục giao diện theo server. Cache/state client được dùng cho UX, nhưng không làm nguồn quyết định nghiệp vụ; booking không lấy từ LocalStorage.

| Nhóm | Mã lỗi cần xử lý |
|---|---|
| Input và quyền | VALIDATION_ERROR; HTTP 401/403; FORBIDDEN |
| Bộ môn/phòng | SPORT_NAME_DUPLICATE, SPORT_NOT_FOUND, SPORT_INACTIVE, ROOM_NAME_DUPLICATE, ROOM_NOT_FOUND, ROOM_INACTIVE, ROOM_CAPACITY_BELOW_SCHEDULE |
| Lớp | CLASS_NAME_DUPLICATE, CLASS_NOT_FOUND, CLASS_ARCHIVED, CLASS_HAS_NO_VALID_SESSION, CAPACITY_BELOW_REGISTRATIONS |
| Lịch | SESSION_IN_PAST, SESSION_ALREADY_STARTED, COACH_NOT_FOUND, COACH_INACTIVE, COACH_SCHEDULE_CONFLICT, ROOM_SCHEDULE_CONFLICT, MEMBER_SCHEDULE_CONFLICT |
| Booking | ACCOUNT_INACTIVE, MEMBERSHIP_NOT_VALID_FOR_SESSION, PACKAGE_NOT_ELIGIBLE, CLASS_FULL, DUPLICATE_REGISTRATION, REGISTRATION_ALREADY_CANCELED, SESSION_CANCELLED |
| Hủy và cạnh tranh ghi | NO_CANCELABLE_SESSION, CONCURRENCY_CONFLICT |

Đây là nhóm mã theo đặc tả và các ca đã báo cáo, không phải danh sách exhaustive hoặc khẳng định mọi mã đều đã chạy HTTP. FE cần có fallback cho mã mới/không nhận diện.

### 5.4. Notifications và audit

- Bốn loại: CLASS_REGISTERED, SESSION_RESCHEDULED, SESSION_COACH_CHANGED, SESSION_CANCELED. Không hứa có sự kiện riêng khi Member tự hủy booking.
- Đếm chưa đọc: GET `/api/Notification/me?isRead=false&page=1&pageSize=1`, lấy `totalItems`; không đếm riêng items trang hiện tại. Cần xác nhận HTTP filter này trong lượt nghiệm thu bổ sung.
- Roster chỉ đọc, cho CenterManager hoặc Coach phụ trách. Không hiển thị nút điểm danh khi chưa có API.
- Audit chỉ đọc. Dùng classId/sessionId cho filter thực thể lớp/buổi; actorId là người thao tác. fromDate/toDate dạng ngày được implementation mở rộng mốc cuối đến hết ngày. Không truyền các alias với giá trị mâu thuẫn.

## 6. Khác biệt và giới hạn cần nhóm FE biết

1. Dòng “chưa endpoint nào tồn tại” trong đặc tả ngày 13/10 là mốc lịch sử; controller hiện đã có các endpoint. Không dùng dòng đó để kết luận backend chưa triển khai.
2. Tài liệu handoff hiện minh họa hasNext/hasPrevious nhưng PagedResult Flow 2 trong source không có hai trường này; dùng năm trường đã nêu ở §5.1.
3. Các nhánh lỗi chưa đồng nhất envelope như §5.1; cần xử lý fallback và ghi nhận khi nghiệm thu.
4. Backend mở rộng audit classId/sessionId, alias ngày/actor và lịch cá nhân from/to. Đây là bổ sung implementation; không đổi danh tính lấy từ token.
5. Cơ chế dedup notification trong DB dùng EventId/người nhận thay vì thiết kế eventAt trong phụ lục đặc tả. FE không phụ thuộc khóa DB; nghiệm thu theo hành vi gửi đúng người, không lặp.
6. Chưa có bằng chứng FE chạy API thật; chưa kiểm CORS/base URL triển khai, UI theo từng vai trò hoặc hành vi nhiều tab trình duyệt.
7. Không có bản ghi request/response đầy đủ cho tất cả 37 endpoint trong gói bằng chứng hiện tại. Các dòng C cần bổ sung HTTP trước khi tuyên bố 37/37 HTTP PASS.

## 7. Checklist nghiệm thu cùng FE

- [ ] Bổ sung HTTP cho mọi dòng C trong bảng 37 endpoint; lưu method/path/role/status/error.code và assertion payload đã loại bỏ dữ liệu nhạy cảm.
- [ ] Với danh sách: kiểm page/pageSize, tổng số, empty, search, từng filter và phạm vi quyền; không chỉ kiểm HTTP 200.
- [ ] Với thao tác ghi: happy path, input sai, ID không tồn tại, sai role, retry và trạng thái không cho phép.
- [ ] Chạy luồng Manager tạo danh mục → lớp DRAFT → session → OPEN → Member đặt/hủy và tải lại trang.
- [ ] Quầy tìm Member bằng API, đặt/hủy hộ và hiển thị đúng actor/lý do.
- [ ] Coach thấy lịch của mình, roster đúng buổi; thử truy cập chéo bằng ID phải bị chặn.
- [ ] Dời lịch/đổi Coach/hủy buổi hoặc lớp: UI hiển thị tác động, giữ lịch cũ khi lỗi, tải lại notification và counter.
- [ ] Member không thấy lớp chưa OPEN qua danh mục; lịch cá nhân vẫn có lịch sử hủy.
- [ ] Notifications đúng bốn loại, tổng chưa đọc chính xác, ownership khi mark read.
- [ ] Audit lọc đúng lớp/buổi/actor/ngày; thử khoảng ngày sai và response fallback.
- [ ] Không còn mock/LocalStorage làm nguồn booking, counter hoặc lịch cá nhân.

**Trạng thái bàn giao:** sẵn sàng tích hợp FE với các bằng chứng và giới hạn trên. Hoàn tất nghiệm thu Sprint 2 cần FE chạy API thật và hoàn thiện các ca HTTP chưa được chứng minh.
