# Thiết kế: Điều khoản hội viên, chuyển gói theo kỳ, hồ sơ HLV và lịch sử bài tập về nhà

**Ngày:** 2026-11-12  
**Phạm vi:** SportsCenterManagement (BE) và Sports-Center-Management-FE (FE)  
**Trạng thái:** Chờ chủ sản phẩm phê duyệt

## 1. Mục đích và lý do

Hệ thống cần thể hiện rõ cam kết giữa trung tâm và học viên để tránh tranh luận nghiệp vụ sau khi học viên đã bắt đầu sử dụng dịch vụ. Một gói đã thanh toán phải được giữ nguyên quyền lợi và thời hạn đến ngày kết thúc; học viên không được hủy, nâng hoặc hạ gói giữa kỳ. Học viên vẫn có thể đặt và thanh toán trước cho kỳ tiếp theo, bao gồm gia hạn, nâng gói hoặc hạ gói.

Trước khi tạo bất kỳ đơn gói tập nào, chính tài khoản học viên phải đọc và chấp nhận phiên bản điều khoản hiện hành. Lễ tân không được chấp nhận thay.

Ngoài ra, học viên cần xem hồ sơ HLV ngay trong lịch học, và “kế hoạch luyện tập” phải được tách khỏi “bài tập về nhà”. Bài tập về nhà có lịch sử riêng để học viên xem; không có nộp bài, đánh dấu hoàn thành hoặc chấm bài.

## 2. Mục tiêu

- Lưu được bằng chứng tài khoản học viên đã chấp nhận đúng phiên bản điều khoản, kèm thời điểm.
- Backend bắt buộc kiểm tra chấp thuận điều khoản cho cả tự đăng ký và đăng ký tại quầy.
- Không cho lễ tân hoặc tài khoản khác chấp nhận điều khoản thay học viên.
- Không thay đổi hoặc kết thúc sớm gói đã thanh toán đang có hiệu lực.
- Cho phép đặt trước đúng một gói tương lai: gia hạn, nâng gói hoặc hạ gói.
- Mọi gói đặt trước bắt đầu vào ngày liền sau ngày kết thúc muộn nhất của gói đã xác nhận.
- Nâng/hạ gói đặt trước thu nguyên giá gói mới; không khấu trừ, hoàn tiền hoặc quy đổi số ngày còn lại.
- Gói tương lai đã thanh toán không được hủy.
- Học viên xem được hồ sơ công khai an toàn của HLV trong các màn hình lịch học.
- Tách bài tập về nhà khỏi kế hoạch luyện tập và kết quả buổi tập; cung cấp lịch sử bài tập đã giao.

## 3. Ngoài phạm vi

- Không xây màn hình quản trị để soạn/phát hành điều khoản động.
- Không hỗ trợ hoàn tiền, chuyển nhượng gói hoặc thay đổi quyền lợi giữa kỳ.
- Không cho xếp hàng nhiều hơn một gói tương lai đã thanh toán.
- Không cho học viên nộp bài, đánh dấu hoàn thành hoặc trao đổi trong từng bài tập về nhà.
- Không công khai email, số điện thoại, trạng thái khóa tài khoản hoặc dữ liệu quản trị của HLV.
- Không thiết kế lại toàn bộ lịch học hoặc trang quản lý nhân sự.

## 4. Điều không được xảy ra

- Chỉ thêm checkbox ở FE nhưng API vẫn cho bỏ qua điều khoản.
- Lễ tân tự đánh dấu đồng ý thay học viên.
- Nâng gói làm gói hiện tại bị cắt ngắn hoặc tính khấu trừ theo ngày còn lại.
- Một học viên có nhiều gói tương lai cùng lúc.
- Gói tương lai đã thanh toán bị hủy qua API hủy hóa đơn/đơn.
- API hồ sơ công khai làm lộ thông tin liên hệ hoặc thuộc tính quản trị của HLV.
- Một nội dung bài tập về nhà tiếp tục xuất hiện đồng thời trong kế hoạch, kết quả buổi tập và thông báo.

## 5. Các phương án đã cân nhắc

### Phương án 1 — Cân bằng, có bằng chứng nghiệp vụ (đã chọn)

Điều khoản được version hóa trong code/cấu hình; chấp thuận được lưu ở database. Các quy tắc chuyển gói, quyền truy cập hồ sơ HLV và lịch sử bài tập được backend thực thi. Phương án này đủ dữ liệu để giải trình nghiệp vụ mà không phát sinh một hệ quản trị nội dung điều khoản.

### Phương án 2 — Điều khoản được quản trị động

Thêm màn hình cho quản lý tạo và phát hành phiên bản điều khoản. Linh hoạt hơn nhưng tăng đáng kể phạm vi, phân quyền, trạng thái bản nháp/phát hành và kiểm thử.

### Phương án 3 — Chỉ chặn trên giao diện

Thêm nội dung và checkbox ở FE, tiếp tục dùng thông báo làm lịch sử bài tập. Nhanh nhưng API có thể bị gọi trực tiếp, không chứng minh được ai đã đồng ý và dữ liệu bài tập vẫn nhập nhằng. Không đáp ứng lý do nghiệp vụ nên bị loại.

## 6. Thiết kế nghiệp vụ gói tập

### 6.1 Quy tắc phân loại đơn

Backend là nguồn quyết định duy nhất; client không được gửi hoặc ép `Kind`.

| Trạng thái hiện tại | Gói được chọn | Loại đơn | Ngày bắt đầu |
|---|---|---|---|
| Không có gói xác nhận còn hiệu lực/tương lai | Bất kỳ | `REGISTER` | Hôm nay |
| Có gói hiện tại | Cùng gói | `RENEW` | Ngày sau `EndDate` muộn nhất |
| Có gói hiện tại | Gói giá cao hơn | `UPGRADE` | Ngày sau `EndDate` muộn nhất |
| Có gói hiện tại | Gói giá thấp hơn | `DOWNGRADE` | Ngày sau `EndDate` muộn nhất |
| Gói gần nhất đã hết hạn, không có gói tương lai | Bất kỳ | `REGISTER` hoặc `RENEW` theo lịch sử | Hôm nay |

Nếu hai gói khác nhau nhưng cùng giá, phân loại theo `PackageId`, không dùng giá để coi là cùng gói. Chọn gói khác cùng giá vẫn là một chuyển gói có lịch; tên hiển thị trung tính là “Đổi gói theo lịch”, còn dữ liệu có thể dùng `CHANGE` nếu cần tránh gán sai nâng/hạ.

### 6.2 Giá và thời hạn

- `Amount` luôn bằng giá niêm yết snapshot của gói mới.
- Xóa công thức tín dụng theo số ngày còn lại khỏi FE và BE.
- Không cập nhật `EndDate` của gói hiện tại khi thanh toán gói mới.
- Quyền lợi được snapshot bao gồm tên, giá, thời hạn, mô tả benefit và danh sách bộ môn được phép tham gia. Kiểm tra eligibility của lớp phải đọc snapshot của subscription, không đọc mapping `MembershipPackageSport` đang thay đổi trong catalog.
- Gói mới chỉ thành `CONFIRMED` sau khi thanh toán thành công; nếu ngày bắt đầu ở tương lai thì trạng thái hiển thị là “Sắp có hiệu lực”.
- Chỉ cho tồn tại tối đa một subscription `PENDING_PAYMENT` và tối đa một subscription `CONFIRMED` có `StartDate > today` cho mỗi học viên.

Snapshot bộ môn dùng bảng con theo subscription (ví dụ `MemberSubscriptionDiscipline`) để catalog vẫn có thể thay đổi cho khách hàng mới mà không làm thay đổi quyền lợi của gói đã bán.

Nếu gói gần nhất đã hết hạn và không có gói tương lai, chọn lại cùng `PackageId` được ghi là `RENEW`; chọn `PackageId` khác được ghi là `REGISTER` vì không còn chuyển tiếp giữa hai kỳ đang nối liền.

### 6.3 Hủy đơn và hủy gói

- Subscription `CONFIRMED`, dù đang hoạt động hay đang chờ ngày bắt đầu, không có thao tác hủy.
- Hóa đơn đã `PAID` không được hủy.
- Hóa đơn `PENDING_PAYMENT` chưa làm phát sinh quyền sử dụng và có thể hủy theo luồng hiện có; thao tác này phải ghi audit log. Đây là hủy yêu cầu chưa thanh toán, không phải hủy gói đã đăng ký.

### 6.4 Thanh toán

Luồng xác nhận thanh toán phải hỗ trợ `REGISTER`, `RENEW`, `UPGRADE`, `DOWNGRADE` và loại đổi gói cùng giá nếu được thêm. Guard hiện đang từ chối `UPGRADE`/`DOWNGRADE` phải được thay bằng kiểm tra ngày bắt đầu, giá snapshot và tính duy nhất của gói tương lai.

Thanh toán không được cắt ngắn gói trước đó. Mọi phương thức thanh toán sử dụng cùng một hàm xác nhận để tránh quy tắc khác nhau giữa tiền mặt, chuyển khoản, demo và VNPAY sandbox.

## 7. Điều khoản và bằng chứng chấp thuận

### 7.1 Phiên bản điều khoản

BE công bố một tài liệu hiện hành với tối thiểu:

- `version`: mã bất biến, ví dụ `membership-terms-v1`;
- `title`;
- `sections`: các mục điều khoản đầy đủ;
- `effectiveFrom`.

Nội dung được lưu trong code/cấu hình có kiểm soát phiên bản. Khi nội dung nghiệp vụ thay đổi đáng kể, tạo mã phiên bản mới; không sửa âm thầm nội dung của mã cũ.

Điều khoản phải nói rõ ít nhất:

- gói đã thanh toán không được hủy;
- không nâng/hạ giữa kỳ;
- gói đặt trước chỉ có hiệu lực sau khi gói hiện tại kết thúc;
- giá, thời hạn và quyền lợi của gói;
- quy định tham gia lớp, an toàn tập luyện và tuân thủ hướng dẫn của trung tâm;
- chính sách thanh toán và xử lý yêu cầu chưa thanh toán.

### 7.2 Dữ liệu chấp thuận

Thêm bảng `MembershipTermAcceptance`:

- `Id`;
- `MemberId` (FK đến tài khoản Member);
- `TermsVersion`;
- `AcceptedAtUtc`;
- `AcceptanceChannel` (`MEMBER_WEB`);
- `ContentHash` để chứng minh nội dung của phiên bản đã chấp nhận.

Unique key `(MemberId, TermsVersion)` giúp thao tác chấp nhận lặp lại an toàn. Không nhận `MemberId` từ body khi chấp nhận; backend lấy từ JWT của tài khoản đang đăng nhập và yêu cầu role `Member`.

### 7.3 API và luồng giao diện

- `GET /api/MembershipTerms/current`: trả điều khoản hiện hành và trạng thái chấp nhận của tài khoản Member hiện tại.
- `POST /api/MembershipTerms/current/accept`: tài khoản Member chấp nhận phiên bản hiện hành; body chứa `version` và `contentHash` để chống chấp nhận nhầm nội dung cũ.
- Mọi API tạo đơn gói tự phục vụ và tại quầy kiểm tra acceptance hiện hành của `MemberId` mục tiêu; thiếu acceptance trả `409 TERMS_ACCEPTANCE_REQUIRED`.

FE yêu cầu học viên mở điều khoản, cuộn đến cuối và chọn các xác nhận bắt buộc trước khi bật nút “Đồng ý điều khoản”. Việc cuộn/checkbox là hướng dẫn UX; bằng chứng có giá trị là bản ghi backend.

Đối với đăng ký tại quầy:

1. Lễ tân có thể tạo tài khoản học viên nhưng chưa được tạo đơn gói.
2. Học viên đăng nhập bằng chính tài khoản của họ trên thiết bị của họ hoặc màn hình dành cho học viên.
3. Học viên đọc và chấp nhận điều khoản.
4. Lễ tân tải lại trạng thái và tiếp tục tạo đơn.

Luồng tạo thành viên và tạo đơn hiện đang gộp phải được tách thành hai bước để bảo đảm lễ tân không thể chấp thuận thay.

## 8. Hồ sơ HLV trong lịch học

### 8.1 API công khai có kiểm soát

Thêm DTO/endpoint chỉ dành cho người dùng đã đăng nhập, ví dụ `GET /api/Coach/{accountId}/public-profile`, cho phép Member, Coach, Receptionist và CenterManager đọc.

Chỉ trả:

- `accountId`;
- `fullName`;
- danh sách chuyên môn/bộ môn đang hoạt động;
- lịch làm việc hoặc mô tả lịch dạy hiện có nếu phù hợp;
- trạng thái khả dụng công khai cần thiết.

Không trả email, số điện thoại, `IsLocked`, audit fields hoặc dữ liệu đã xóa.

### 8.2 Giao diện

Tên HLV trong danh sách lớp, lịch tuần và lịch đã đăng ký trở thành nút/link. Khi chọn, mở dialog hồ sơ HLV, có trạng thái tải/lỗi và cache theo `coachId` trong phiên màn hình. Nếu buổi chưa có HLV, không hiển thị link.

## 9. Tách kế hoạch luyện tập và bài tập về nhà

### 9.1 Kế hoạch luyện tập

`TrainingPlan` chỉ còn:

- phạm vi cá nhân/lớp;
- tiêu đề;
- mục tiêu;
- danh sách bài tập trong kế hoạch;
- ngày bắt đầu/kết thúc;
- HLV tạo kế hoạch.

Loại `Homework` khỏi request/response mới và khỏi form/chi tiết kế hoạch. `TrainingResult` giữ kết quả, tỷ lệ hoàn thành, đánh giá và phản hồi HLV nhưng loại `HomeworkAssigned` khỏi request/response mới và giao diện.

### 9.2 Lịch sử bài tập về nhà

Thêm bảng `HomeworkAssignment`:

- `Id`;
- `CoachId`;
- `MemberId`;
- `ClassId` nullable, dùng để ghi nguồn giao theo lớp;
- `SessionId` nullable, dùng khi giao sau một buổi cụ thể;
- `Title`;
- `Instructions`;
- `AssignedAtUtc`;
- `SourceType` (`DIRECT`, `CLASS`, `SESSION`);

Không có trường hoàn thành, bài nộp hoặc điểm.

Khi HLV giao cho một lớp, backend tạo một bản ghi cho từng học viên đang đăng ký hợp lệ tại thời điểm giao. Cách này giữ đúng lịch sử cá nhân kể cả khi danh sách lớp thay đổi sau đó. Cùng giao dịch đó tạo notification cho từng học viên.

“Đang đăng ký hợp lệ” là tập học viên khác nhau có ít nhất một registration `BOOKED` thuộc buổi không bị hủy của lớp và buổi đó diễn ra từ ngày giao trở đi. Registration đã hủy, buổi đã hủy và học viên chỉ còn lịch sử buổi quá khứ không nhận homework mới.

### 9.3 API và giao diện

- HLV dùng endpoint giao bài tập để tạo assignment cho một học viên hoặc một lớp.
- Workspace HLV trả lịch sử các bài HLV đã giao để tránh gửi trùng và hỗ trợ giải trình.
- Workspace học viên trả `homeworkAssignments` của chính tài khoản, mới nhất trước.
- Trang học viên có khu vực “Lịch sử bài tập về nhà” riêng, hiển thị tiêu đề, hướng dẫn, HLV, lớp/buổi liên quan và thời điểm giao.
- Không trộn bài tập về nhà vào danh sách kế hoạch hoặc kết quả buổi tập.

### 9.4 Dữ liệu cũ

Migration tạo bảng mới và backfill các trường cũ khi xác định được người nhận:

- `TrainingResult.HomeworkAssigned`: tạo assignment trực tiếp cho `MemberId` của kết quả.
- `TrainingPlan.Homework` loại cá nhân: tạo assignment cho `MemberId` của kế hoạch.
- `TrainingPlan.Homework` loại lớp: tạo assignment cho các học viên có đăng ký lớp hợp lệ tại thời điểm migration; ghi `SourceType=CLASS`.

Các cột cũ có thể được giữ tạm trong database để migration an toàn nhưng code mới không ghi hoặc hiển thị chúng. Việc xóa vật lý cột là migration sau, ngoài phạm vi thay đổi này.

Không migrate `COACH_HOMEWORK_NOTICE` cũ vì endpoint hiện tại trộn bài tập và thông báo chung, không có dữ liệu cấu trúc để phân biệt an toàn. Notification cũ vẫn được giữ nguyên trong lịch sử thông báo; chỉ các nguồn homework có cấu trúc (`TrainingPlan.Homework` và `TrainingResult.HomeworkAssigned`) được backfill để tránh tạo bài tập giả hoặc trùng.

## 10. Xử lý lỗi và mã nghiệp vụ

Tối thiểu cần các mã ổn định:

- `TERMS_ACCEPTANCE_REQUIRED`;
- `TERMS_VERSION_MISMATCH`;
- `PENDING_ORDER_EXISTS`;
- `UPCOMING_SUBSCRIPTION_EXISTS`;
- `CONFIRMED_SUBSCRIPTION_CANNOT_BE_CANCELED`;
- `COACH_PUBLIC_PROFILE_NOT_FOUND`;
- `HOMEWORK_TARGET_NOT_FOUND`;
- `HOMEWORK_TARGET_HAS_NO_MEMBERS`.

FE hiển thị thông báo tiếng Việt theo mã, không suy luận nghiệp vụ từ chuỗi message.

## 11. Kiểm thử bắt buộc

### Backend

- Không thể tạo đơn khi chưa chấp nhận điều khoản hiện hành.
- Member chỉ chấp nhận cho chính mình; Receptionist/Manager không thể chấp nhận thay.
- Chấp nhận lặp lại cùng phiên bản là idempotent.
- Gói cùng loại, giá cao hơn, giá thấp hơn và khác gói cùng giá đều bắt đầu sau `EndDate` hiện tại.
- Không còn khấu trừ theo ngày; `Amount` bằng giá snapshot mới.
- Thanh toán `UPGRADE`/`DOWNGRADE` xác nhận gói tương lai mà không sửa ngày gói hiện tại.
- Không thể tạo gói tương lai thứ hai hoặc hủy gói tương lai đã thanh toán.
- Hồ sơ HLV công khai không serialize email, điện thoại hoặc `IsLocked`.
- HLV giao bài trực tiếp/lớp tạo đúng assignment và notification.
- Member chỉ đọc được lịch sử homework của chính mình.
- Kế hoạch/kết quả mới không còn nhận hoặc trả homework.

### Frontend

- Nút tạo đơn bị chặn và mở luồng điều khoản khi chưa chấp nhận.
- Không thể bật nút đồng ý trước khi cuộn hết và chọn đủ xác nhận.
- Quote gia hạn/nâng/hạ dùng ngày bắt đầu tương lai và nguyên giá.
- Gói tương lai đã có làm vô hiệu hóa việc đặt thêm.
- Hồ sơ HLV mở được từ cả danh sách lớp và lịch cá nhân, có trạng thái tải/lỗi.
- Lịch sử homework hiển thị riêng; kế hoạch và kết quả không còn trường homework.

### Migration và hồi quy

- Migration chạy được trên database hiện tại và không tạo duplicate khi chạy theo quy trình deploy chuẩn.
- Migration chạy preflight để phát hiện học viên đang có nhiều pending/future subscription. Nếu có dữ liệu vi phạm, migration dừng với báo cáo định danh; không tự chọn bản ghi thắng và không tự xóa dữ liệu tài chính. Sau khi dữ liệu được xử lý có chủ đích, thêm unique filtered index/constraint phù hợp để chặn race condition ở database.
- Dữ liệu kế hoạch/kết quả/notification hiện có không bị mất.
- Các luồng đăng ký mới, gia hạn, thanh toán tiền mặt/chuyển khoản/demo/VNPAY và lịch học hiện tại vẫn qua kiểm thử liên quan.

## 12. Trình tự triển khai đề xuất

1. Migration và entity/configuration cho acceptance và homework.
2. API điều khoản và enforcement ở mọi điểm tạo đơn.
3. Sửa quy tắc quote/thanh toán chuyển gói theo kỳ.
4. API homework riêng và loại homework khỏi plan/result.
5. API hồ sơ HLV công khai.
6. FE điều khoản và luồng tại quầy hai bước.
7. FE quote/chuyển gói theo kỳ.
8. FE hồ sơ HLV trong lịch học.
9. FE lịch sử homework và dọn trường homework cũ.
10. Chạy test, migration smoke test và kiểm thử tích hợp FE–BE.

## 13. Các quyết định đã được chủ sản phẩm chốt

- Chọn phương án cân bằng, có bằng chứng nghiệp vụ.
- Không nâng/hạ gói giữa kỳ.
- Cho đặt trước gia hạn, nâng và hạ gói; tất cả bắt đầu sau khi gói hiện tại kết thúc.
- Gói đã đăng ký/thanh toán không được hủy.
- Chính tài khoản học viên phải chấp nhận điều khoản; lễ tân không chấp nhận thay.
- Homework tách khỏi kế hoạch; học viên chỉ xem lịch sử bài đã giao, không có hoàn thành/nộp bài.

## 14. Các quyết định kỹ thuật do người thiết kế đề xuất

- Điều khoản được version hóa trong code/cấu hình, không có CMS quản trị.
- Lưu `ContentHash` cùng acceptance để chứng minh đúng nội dung.
- Pending chưa thanh toán vẫn có thể hủy; paid/confirmed thì không.
- Gói khác cùng giá được coi là đổi gói, không phải gia hạn.
- Giao homework theo lớp được fan-out thành bản ghi từng học viên để bảo toàn lịch sử.
- Giữ tạm cột homework cũ sau migration nhưng ngừng sử dụng trong code mới.
- Snapshot bộ môn theo subscription để việc sửa catalog không đổi quyền lợi gói đã bán.
- Không chuyển notification cũ thành homework vì không thể phân biệt an toàn thông báo chung và bài được giao.
- Sau khi gói đã hết hạn: cùng `PackageId` là `RENEW`, `PackageId` khác là `REGISTER`.
- Migration báo lỗi khi gặp dữ liệu pending/future trùng thay vì tự động xóa hoặc chọn bản ghi.
