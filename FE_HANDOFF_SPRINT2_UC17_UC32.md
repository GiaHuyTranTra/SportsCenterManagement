# Tài Liệu Bàn Giao API Cho Frontend — Sprint 2 (UC17 – UC32)

Tài liệu này chuẩn hóa toàn bộ các API endpoint, phân quyền vai trò (RBAC), hợp đồng dữ liệu (Request / Response mẫu), tham số phân trang/lọc, mã lỗi nghiệp vụ thực tế và Checklist nghiệm thu tích hợp Frontend cho các Use Case từ **UC17** đến **UC32**.

---

## 1. Kiến Trúc & Quy Ước Chung Dành Cho Frontend

### 1.1. Cấu Trúc Phản Hồi Lỗi Nghiệp Vụ Chuẩn (`ApiErrorResponse`)
Hệ thống backend sử dụng `GlobalExceptionMiddleware` và trả về cấu trúc lỗi JSON nhất quán (HTTP 400, 401, 403, 404, 409, 500):
```json
{
  "success": false,
  "error": {
    "code": "MEMBER_SCHEDULE_CONFLICT",
    "message": "Một hoặc nhiều học viên đã có lịch học trùng với khung giờ mới.",
    "details": {
      "field": ["Mô tả chi tiết lỗi nếu có"]
    }
  },
  "traceId": "0HN123456789ABC"
}
```
> **Frontend Rule:**
> - Luôn kiểm tra `error.code` để ánh xạ hiển thị thông báo lỗi hoặc dịch đa ngôn ngữ (i18n).
> - Sử dụng `error.message` làm nội dung hiển thị toast/alert mặc định.
> - Nếu `error.details` có dữ liệu (key-value), ánh xạ lỗi trực tiếp vào viền đỏ của các trường input tương ứng trên Form.

### 1.2. Xác Thực & Cơ Chế Quản Lý Phiên (Auth Session)
- **Token phát hành:** Khi đăng nhập thành công (`POST /api/Auth/login`), backend trả về phiên xác thực (`AuthSessionAPIViewModel`):
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "tokenType": "Bearer",
    "expiresAtUtc": "2026-10-15T12:00:00Z",
    "accountId": "a3f7c63e-f129-4a79-8df7-ebbe30241e88",
    "email": "member@sportscenter.local",
    "role": "Member",
    "fullName": "Nguyễn Văn A",
    "createdAt": "2026-10-15T08:00:00Z"
  }
  ```
  *(Lưu ý: Backend hiện tại chỉ cấp `accessToken` JWT và cơ chế blacklist logout bằng `IMemoryCache`; backend **chưa hỗ trợ `refreshToken`**).*
- Mọi request yêu cầu xác thực phải gửi kèm Header:
  ```http
  Authorization: Bearer <accessToken>
  ```
- **Tuyệt đối không truyền `accountId` trên query hoặc body của các endpoint cá nhân dạng `/me`** (`/api/ClassRegistration/me`, `/api/ClassSession/me`, `/api/Notification/me`). Hệ thống backend tự trích xuất ID từ JWT Claims và đảm bảo cách ly dữ liệu.

### 1.3. Phân Trang Chuẩn (`PagedResult<T>`)
- Tham số truy vấn (Query):
  - `page` (int, mặc định `1`, tối thiểu `1`).
  - `pageSize` (int, mặc định `20`, tối đa `100`).
- Cấu trúc Response:
  ```json
  {
    "items": [ ... ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 45,
    "totalPages": 3,
    "hasPrevious": false,
    "hasNext": true
  }
  ```

### 1.4. Quy Tắc Khung Giờ & Quản Lý Dữ Liệu Nghiệp Vụ
- **Mô hình khung giờ học:** Dùng nửa khoảng mở `[StartTime, EndTime)`. Buổi kết thúc lúc `10:00` và buổi kế tiếp bắt đầu lúc `10:00` cùng phòng/cùng HLV là **hợp lệ (liền kề, không xung đột)**.
- **Nguyên tắc dữ liệu (Zero LocalStorage for Business Data):**
  - `localStorage` chỉ được sử dụng để lưu trữ token xác thực (`accessToken`, thời hạn `expiresAtUtc`, thông tin cơ bản của user khi login).
  - **Tuyệt đối không lưu danh sách lớp đã đặt, số chỗ trống, hoặc trạng thái đăng ký vào `localStorage` làm nguồn dữ liệu nghiệp vụ.**
  - Mọi thao tác hiển thị lịch, đặt chỗ, hủy chỗ, kiểm tra số lượng chỗ trống (`remainingCapacity`) phải gọi trực tiếp API và cập nhật State của ứng dụng dựa trên kết quả API trả về.

---

## 2. Chi Tiết API Bàn Giao Theo Use Case (UC17 – UC32)

---

### UC17: Quản Lý Bộ Môn (Sports)
- **Mục tiêu:** Quản lý danh mục các môn thể thao (Gym, Yoga, Pilates, Boxing, Bơi lội,...).
- **Roles:**
  - `CenterManager`: Toàn quyền Create, Update, Activate/Deactivate.
  - `CenterManager, Coach, Member, Receptionist`: Xem danh sách bộ môn.
- **Endpoints:**
  - `GET /api/Sport`: Danh sách bộ môn (Query: `page`, `pageSize`, `search`, `isActive`).
  - `GET /api/Sport/{id}`: Chi tiết bộ môn.
  - `POST /api/Sport`: Tạo bộ môn mới (`CenterManager`).
  - `PUT /api/Sport/{id}`: Cập nhật bộ môn (`CenterManager`).
  - `PATCH /api/Sport/{id}/status`: Bật/Tắt hoạt động (`CenterManager`).
- **Request Mẫu (`POST /api/Sport`):**
  ```json
  {
    "name": "Pilates",
    "description": "Rèn luyện độ dẻo dai và cơ cốt lõi"
  }
  ```
- **Response Mẫu (`201 Created`):**
  ```json
  {
    "id": 1,
    "name": "Pilates",
    "normalizedName": "PILATES",
    "description": "Rèn luyện độ dẻo dai và cơ cốt lõi",
    "isActive": true,
    "createdAt": "2026-10-15T08:00:00Z",
    "updatedAt": null
  }
  ```
- **Mã lỗi thường gặp:**
  - `SPORT_NOT_FOUND` (404): Bộ môn không tồn tại.
  - `SPORT_NAME_DUPLICATE` (409): Tên bộ môn đã tồn tại trong hệ thống.
  - `VALIDATION_ERROR` (400): Tên bộ môn rỗng hoặc vượt quá 80 ký tự.

---

### UC18: Quản Lý Phòng Tập (Rooms)
- **Mục tiêu:** Quản lý phòng tập, sức chứa tối đa và trạng thái sẵn sàng hoạt động.
- **Roles:**
  - `CenterManager`: Toàn quyền Create, Update, Bật/Tắt hoạt động.
  - `CenterManager, Coach, Member, Receptionist`: Xem danh sách phòng.
- **Endpoints:**
  - `GET /api/Room`: Danh sách phòng (Query: `page`, `pageSize`, `search`, `isActive`).
  - `GET /api/Room/{id}`: Chi tiết phòng.
  - `POST /api/Room`: Tạo phòng mới (`CenterManager`).
  - `PUT /api/Room/{id}`: Sửa tên, sức chứa phòng (`CenterManager`).
  - `PATCH /api/Room/{id}/status`: Ngừng hoạt động / Kích hoạt lại (`CenterManager`).
- **Request Mẫu (`PUT /api/Room/{id}`):**
  ```json
  {
    "name": "Studio A1",
    "capacity": 25,
    "location": "Tầng 2, Tòa A",
    "roomType": "STUDIO"
  }
  ```
- **Mã lỗi thường gặp:**
  - `ROOM_CAPACITY_BELOW_SCHEDULE` (409): Không thể giảm sức chứa phòng xuống thấp hơn sức chứa của các buổi học tương lai đã xếp trong phòng này.
  - `ROOM_INACTIVE` (409): Không thể xếp lịch buổi học mới vào phòng đang ngừng hoạt động.
- **Quy tắc hiển thị (UI Rule):**
  - Cho phép Deactivate phòng (`isActive = false`) ngay cả khi phòng đang có lịch học; các buổi học đã lên lịch vẫn được giữ nguyên.
  - Phòng có `isActive == false` hiển thị badge "Ngừng hoạt động" và bị ẩn/disable khỏi danh sách dropdown chọn phòng khi lập lịch buổi mới.

---

### UC19: Quản Lý Lớp Học & Vòng Đời (Classes)
- **Mục tiêu:** Tạo và quản lý thông tin lớp học, sức chứa tổng thể và trạng thái vòng đời.
- **Roles:**
  - `CenterManager`: Tạo lớp, cập nhật thông tin, chuyển đổi trạng thái lớp.
  - `CenterManager, Coach, Member, Receptionist`: Xem danh sách lớp.
- **Bốn trạng thái vòng đời của Lớp (`Status`) & Hành vi thực tế của Backend:**
  1. **`DRAFT` (Nháp):** Lớp đang trong quá trình thiết lập. Chỉ Quản lý và Nhân viên thấy; học viên (`Member`) không nhìn thấy trong danh sách và không thể đặt chỗ.
  2. **`OPEN` (Mở):** Lớp hoạt động công khai, hội viên xem được và đặt chỗ được.
     - *Ràng buộc backend:* Chỉ cho phép chuyển sang `OPEN` khi lớp có ít nhất 1 buổi học tương lai ở trạng thái `SCHEDULED` (nếu không có sẽ trả về lỗi `CLASS_HAS_NO_VALID_SESSION`).
  3. **`CLOSED` (Đóng):** Lớp tạm dừng nhận đăng ký mới.
     - *Hành vi backend:* Lớp bị ẩn hoàn toàn khỏi danh sách của Hội viên (`Member`). Nếu Hội viên gọi `GET /api/Class/{id}` trên lớp `CLOSED`, backend trả về `404 Not Found`. Các buổi học thuộc lớp này không còn xuất hiện trong danh sách khả dụng cho hội viên. Chỉ nhân viên (`CenterManager, Receptionist, Coach`) mới xem được lớp `CLOSED`.
  4. **`ARCHIVED` (Lưu trữ):** Đóng băng vĩnh viễn lớp học.
     - *Hành vi backend:* Bị ẩn hoàn toàn đối với Hội viên (`Member`). Khóa toàn bộ các thao tác chỉnh sửa: Gọi `PUT /api/Class/{id}` trả về `CLASS_ARCHIVED`; gọi `POST /api/ClassSession` thêm buổi học trả về `CLASS_ARCHIVED`; gọi `PUT /api/ClassSession/{id}` dời lịch buổi học trả về `CLASS_ARCHIVED`.
- **Endpoints:**
  - `GET /api/Class`: Danh sách lớp (Query: `page`, `pageSize`, `search`, `sportId`, `status`). *Lưu ý: Role Member luôn tự động chỉ nhận các lớp `OPEN`.*
  - `GET /api/Class/{id}`: Chi tiết lớp (Member chỉ xem được nếu lớp `OPEN`, ngược lại trả về 404).
  - `POST /api/Class`: Tạo lớp mới (`CenterManager`).
  - `PUT /api/Class/{id}`: Sửa lớp (`CenterManager`).
  - `PATCH /api/Class/{id}/status`: Đổi trạng thái (`CenterManager`). Body: `{"status": "OPEN"}`.
- **Mã lỗi thường gặp:**
  - `CLASS_NOT_FOUND` (404): Lớp không tồn tại hoặc hội viên truy cập lớp không phải `OPEN`.
  - `CLASS_NAME_DUPLICATE` (409): Tên lớp đã tồn tại.
  - `CLASS_ARCHIVED` (409): Lớp đã lưu trữ không thể chỉnh sửa hoặc thêm/sửa buổi học.
  - `CLASS_HAS_NO_VALID_SESSION` (409): Không thể mở lớp (`OPEN`) vì chưa có buổi học tương lai nào được lên lịch.
  - `CAPACITY_BELOW_REGISTRATIONS` (409): Sức chứa lớp không thể nhỏ hơn số học viên đã đăng ký hoặc sức chứa của các buổi học đã lập.

---

### UC20: Phân Công Huấn Luyện Viên (Assign Coach)
- **Mục tiêu:** Phân công hoặc thay đổi Huấn luyện viên phụ trách một buổi học.
- **Role:** `CenterManager`.
- **Endpoint:**
  - `PUT /api/ClassSession/{id}/coach`
- **Request Mẫu:**
  ```json
  {
    "coachId": "853eb8ce-6265-46e9-84ac-25ea2fc6c165"
  }
  ```
- **Mã lỗi thường gặp:**
  - `COACH_NOT_FOUND` (404): Không tìm thấy Huấn luyện viên.
  - `COACH_INACTIVE` (409): HLV đã bị khóa hoặc ngừng hoạt động.
  - `COACH_SCHEDULE_CONFLICT` (409): HLV đã có lịch dạy khác trong khung giờ này.
- **Quy tắc UI & Thông báo:**
  - Hệ thống tự động phát thông báo `SESSION_COACH_CHANGED` tới HLV cũ, HLV mới và các học viên đã đặt chỗ.
  - Thao tác có cơ chế chống trùng lặp: Nếu người dùng bấm lưu nhiều lần với cùng một HLV, hệ thống không tạo thông báo rác lặp lại.

---

### UC21: Lập & Dời Lịch Buổi Học (Create & Reschedule Session)
- **Mục tiêu:** Xếp lịch buổi học mới hoặc thay đổi ngày, giờ, phòng tập của buổi học.
- **Role:** `CenterManager`.
- **Endpoints:**
  - `POST /api/ClassSession`: Tạo buổi học mới.
  - `PUT /api/ClassSession/{id}`: Cập nhật / dời lịch buổi học.
- **Request Mẫu (`PUT /api/ClassSession/{id}`):**
  ```json
  {
    "classId": 15,
    "coachId": "853eb8ce-6265-46e9-84ac-25ea2fc6c165",
    "roomId": 22,
    "date": "2026-10-25",
    "startTime": "16:00",
    "endTime": "17:00",
    "capacity": 8,
    "notes": "Phòng Studio thoáng mát"
  }
  ```
- **Mã lỗi thường gặp:**
  - `SESSION_IN_PAST` (400): Không thể lên lịch hoặc dời lịch về thời điểm trong quá khứ.
  - `SESSION_ALREADY_STARTED` (409): Không thể sửa buổi học đã hoặc đang diễn ra.
  - `ROOM_SCHEDULE_CONFLICT` (409): Phòng đã có lớp học trong khung giờ này.
  - `COACH_SCHEDULE_CONFLICT` (409): HLV đã có lịch dạy trong khung giờ này.
  - `MEMBER_SCHEDULE_CONFLICT` (409): Khi dời lịch một buổi học đã có người đăng ký, một hoặc nhiều học viên đã có lịch học ở buổi khác trùng giờ mới.
  - `MEMBERSHIP_NOT_VALID_FOR_SESSION` (409): Khi dời buổi học sang ngày mới vượt ngoài thời hạn gói tập `CONFIRMED` của học viên đã đặt chỗ.
- **Quy tắc hiển thị (UI Rule):**
  - Khi dời lịch thất bại do xung đột (phòng, HLV hoặc học viên), thông báo lỗi hiển thị rõ ràng để Quản lý chọn khung giờ khác. Lịch cũ và danh sách học viên giữ nguyên 100%.

---

### UC22: Tra Cứu Lịch Toàn Trung Tâm (Center Schedule)
- **Mục tiêu:** Màn hình lịch biểu tổng hợp các buổi học theo ngày, tuần, tháng.
- **Roles:** `CenterManager, Coach, Member, Receptionist` (Tất cả tài khoản đã đăng nhập).
- **Endpoint Canonical:**
  - `GET /api/ClassSession`
- **Endpoint Legacy (Hỗ trợ tương thích ngược):**
  - `GET /api/ScheduleQuery/available` *(Ủy quyền về query chính, bắt buộc có Token, không cho phép anonymous)*.
- **Tham số Query:**
  - `from` (string YYYY-MM-DD): Ngày bắt đầu (mặc định: hôm nay).
  - `to` (string YYYY-MM-DD): Ngày kết thúc (mặc định: hôm nay + 7 ngày).
  - `classId` (int?): Lọc theo lớp.
  - `coachId` (string?): Lọc theo HLV.
  - `roomId` (int?): Lọc theo phòng.
  - `sportId` (int?): Lọc theo môn thể thao.
  - `status` (string?): `SCHEDULED`, `COMPLETED`, `CANCELED`.
  - `page` (int), `pageSize` (int).
- **Response Mẫu:**
  ```json
  {
    "items": [
      {
        "id": 26,
        "classId": 15,
        "className": "Yoga Flow Cơ Bản",
        "sportId": 1,
        "sportName": "Yoga",
        "coachId": "853eb8ce-6265-46e9-84ac-25ea2fc6c165",
        "coachName": "Coach One",
        "roomId": 22,
        "roomName": "Studio A",
        "date": "2026-10-25",
        "startTime": "16:00",
        "endTime": "17:00",
        "capacity": 8,
        "bookedCount": 1,
        "remainingCapacity": 7,
        "status": "SCHEDULED"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
  ```

---

### UC23: Chi Tiết Lớp Học & Buổi Học
- **Endpoints:**
  - `GET /api/Class/{id}`: Chi tiết lớp học, mô tả, sức chứa.
  - `GET /api/ClassSession/{id}`: Chi tiết buổi học, HLV, phòng, số lượng đặt chỗ.
- **Quy tắc hiển thị:**
  - `remainingCapacity = capacity - bookedCount`.
  - Khi `remainingCapacity == 0`, nút đặt chỗ chuyển sang trạng thái disabled với nhãn "Hết chỗ".

---

### UC24: Đăng Ký Buổi Học (Member Booking)
- **Mục tiêu:** Hội viên tự đặt chỗ tham gia buổi học.
- **Role:** `Member` (Token của học viên).
- **Endpoint:**
  - `POST /api/ClassRegistration/register`
- **Request Mẫu:**
  ```json
  {
    "sessionId": 26
  }
  ```
- **Response Mẫu (`201 Created`):**
  ```json
  {
    "id": 23,
    "sessionId": 26,
    "memberAccountId": "a3f7c63e-f129-4a79-8df7-ebbe30241e88",
    "status": "BOOKED",
    "registeredAt": "2026-10-15T09:50:00Z",
    "registeredVia": "MEMBER",
    "session": {
      "id": 26,
      "className": "Yoga Flow Cơ Bản",
      "date": "2026-10-25",
      "startTime": "16:00",
      "endTime": "17:00",
      "capacity": 8,
      "bookedCount": 2,
      "remainingCapacity": 6,
      "status": "SCHEDULED"
    }
  }
  ```
- **Mã lỗi thường gặp:**
  - `MEMBERSHIP_NOT_VALID_FOR_SESSION` (409): Hội viên không có gói tập `CONFIRMED` còn hiệu lực vào ngày học.
  - `PACKAGE_NOT_ELIGIBLE` (409): Gói tập của hội viên không bao gồm bộ môn này.
  - `CLASS_FULL` (409): Buổi học đã hết chỗ.
  - `DUPLICATE_REGISTRATION` (409): Hội viên đã đặt chỗ buổi học này rồi.
  - `MEMBER_SCHEDULE_CONFLICT` (409): Bạn đã có lịch học buổi khác trong cùng khung giờ.
- **Quy tắc giao diện (UI Rule):**
  - Disable nút đặt chỗ ngay khi người dùng click và hiển thị spinner để ngăn chặn submit trùng lặp.
  - Cập nhật số lượng chỗ còn lại (`remainingCapacity`) và danh sách lịch học trực tiếp từ kết quả API trả về.

---

### UC25: Hủy Đăng Ký Buổi Học (Member Cancellation)
- **Mục tiêu:** Hội viên tự hủy chỗ đã đặt.
- **Role:** `Member` (Phải là chủ sở hữu lượt đăng ký).
- **Endpoint:**
  - `POST /api/ClassRegistration/{id}/cancel`
- **Request Mẫu:**
  ```json
  {
    "reason": "Bận việc đột xuất"
  }
  ```
- **Response Mẫu (`200 OK`):** Trả về `ClassRegistrationViewModel` với `status: "CANCELED"`.
- **Mã lỗi thường gặp:**
  - `FORBIDDEN` (403): **Hội viên không có quyền hủy đăng ký của người khác**.
  - `REGISTRATION_ALREADY_CANCELED` (409): Buổi học này đã được hủy trước đó.
  - `SESSION_ALREADY_STARTED` (409): Buổi học đã hoặc đang diễn ra, không thể hủy.
- **Quy tắc giao diện:**
  - Chỉ hiển thị nút "Hủy đặt chỗ" khi `status == "BOOKED"` và giờ bắt đầu buổi học ở tương lai.

---

### UC26: Lịch Học Của Tôi (My Class Schedule)
- **Mục tiêu:** Hội viên theo dõi toàn bộ lịch học và lịch sử đặt chỗ của cá nhân.
- **Role:** `Member`.
- **Endpoint:**
  - `GET /api/ClassRegistration/me`
- **Tham số Query:**
  - `includeCanceled` (bool, mặc định `false`).
  - `from` (string YYYY-MM-DD?), `to` (string YYYY-MM-DD?).
  - `page` (int), `pageSize` (int).
- **Quy tắc UI:**
  - Cung cấp tab hoặc toggle "Bao gồm buổi đã hủy" (`includeCanceled=true`) để xem lại lịch sử.

---

### UC27: Lịch Dạy Của Tôi (My Teaching Schedule)
- **Mục tiêu:** Huấn luyện viên xem lịch giảng dạy của cá nhân.
- **Role:** `Coach`.
- **Endpoint:**
  - `GET /api/ClassSession/me`
- **Tham số Query:**
  - `from` (string YYYY-MM-DD, mặc định: hôm nay).
  - `to` (string YYYY-MM-DD, mặc định: hôm nay + 7 ngày). *Khuyến nghị FE luôn truyền rõ khoảng ngày xem theo tuần hoặc tháng*.
  - `status` (string?): `SCHEDULED`, `COMPLETED`, `CANCELED`.
  - `page` (int), `pageSize` (int).
- **Quy tắc bảo mật:**
  - HLV chỉ xem được các buổi dạy do chính mình phụ trách.

---

### UC28: Xem Danh Sách Học Viên Buổi Học (Roster)
- **Mục tiêu:** Xem danh sách học viên đã đặt chỗ của một buổi học cụ thể.
- **Phân quyền thực tế (RBAC):**
  - `CenterManager`: Xem được danh sách học viên của mọi buổi học.
  - `Coach`: **Chỉ được xem danh sách học viên của buổi học mà chính Coach đó phụ trách**.
  - `Receptionist`, `Coach khác`, `Member`: Bị từ chối với mã lỗi **`403 Forbidden`**.
- **Phạm vi nghiệp vụ:**
  - **Chỉ phục vụ xem danh sách (Read-only Roster)**. Sprint 2 **không bao gồm thao tác điểm danh/check-in**.
- **Endpoint:**
  - `GET /api/ClassSession/{id}/registrations`
  - Query: `includeCanceled` (bool), `search` (string: tìm theo tên/mã học viên), `page`, `pageSize`.
- **Response Mẫu (`200 OK`):**
  ```json
  {
    "items": [
      {
        "registrationId": 23,
        "memberAccountId": "a3f7c63e-f129-4a79-8df7-ebbe30241e88",
        "memberName": "Nguyễn Văn A",
        "memberCode": "MB2610153290",
        "status": "BOOKED",
        "registeredAt": "2026-10-15T09:50:00Z",
        "registeredVia": "MEMBER",
        "canceledAt": null
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
  ```
- **Bảo mật quyền riêng tư (Privacy):**
  - Dữ liệu trả về **không chứa số điện thoại cá nhân, email hoặc thông tin thanh toán/gói tập**. Giao diện chỉ hiển thị Tên, Mã hội viên, Ngày đặt và Trạng thái vé đặt chỗ.

---

### UC29: Đăng Ký & Hủy Thay Tại Quầy (Receptionist Operations)
- **Mục tiêu:** Lễ tân hỗ trợ hội viên đặt chỗ hoặc hủy chỗ trực tiếp tại quầy.
- **Role:** `Receptionist` (và `CenterManager` cho danh sách tra cứu).
- **Endpoints:**
  - `GET /api/ClassRegistration`: Tra cứu danh sách đăng ký toàn hệ thống (Query: `memberAccountId`, `includeCanceled`, `page`, `pageSize`).
  - `POST /api/ClassRegistration/counter-register`: Đặt chỗ tại quầy.
    - Body:
      ```json
      {
        "sessionId": 28,
        "memberAccountId": "a3f7c63e-f129-4a79-8df7-ebbe30241e88"
      }
      ```
  - `POST /api/ClassRegistration/{id}/counter-cancel`: Hủy đặt chỗ tại quầy.
    - Body:
      ```json
      {
        "reason": "Học viên gọi điện nhờ hủy hộ vì bận việc gia đình"
      }
      ```
- **Quy tắc nghiệp vụ:**
  - `reason` khi hủy tại quầy là **bắt buộc** và tối thiểu 3 ký tự.
  - Hệ thống tự động ghi nhận: `registeredVia = "RECEPTIONIST"`, `registeredByName = <Tên nhân viên>`, `canceledByName = <Tên nhân viên>`.

---

### UC30: Hủy Buổi Học & Hủy Lớp Tương Lai (Session & Class Cancellation)
- **Mục tiêu:** Quản lý trung tâm hủy 1 buổi học hoặc hủy hàng loạt toàn bộ các buổi học tương lai của lớp (do bảo trì, sửa chữa,...).
- **Role:** `CenterManager`.
- **Endpoints:**
  - `GET /api/ClassSession/{id}/cancel-impact`: Xem mức độ ảnh hưởng của 1 buổi.
  - `POST /api/ClassSession/{id}/cancel`: Hủy 1 buổi học (Body: `{"reason": "HLV bị ốm"}`).
  - `GET /api/Class/{id}/cancel-impact`: Xem mức độ ảnh hưởng của toàn bộ buổi tương lai.
  - `POST /api/Class/{id}/cancel-future-sessions`: Hủy toàn bộ buổi tương lai của lớp.
    - Body:
      ```json
      {
        "reason": "Bảo trì nâng cấp cơ sở vật chất toàn bộ phòng tập"
      }
      ```
- **Response Mẫu Impact (`GET /api/Class/{id}/cancel-impact`):**
  ```json
  {
    "scope": "CLASS_FUTURE_SESSIONS",
    "affectedSessionCount": 2,
    "affectedRegistrationCount": 1,
    "affectedMemberCount": 1
  }
  ```
- **Quy tắc nghiệp vụ & Hiển thị:**
  - Lịch sử đặt chỗ được lưu lại đầy đủ: Buổi học chuyển `CANCELED`, các vé đặt chỗ chuyển `CANCELED` có lưu kèm `cancelReason`.
  - **Không hoàn tiền gói tập (No Package Refund):** Gói tập thời hạn của hội viên giữ nguyên hiệu lực và thời hạn, không bị hủy hoặc trừ tiền.
  - **UI Rule:** Trước khi thực hiện hủy, giao diện **bắt buộc mở Modal xác nhận** lấy số liệu từ API `/cancel-impact` để Quản lý nắm rõ số buổi và số học viên bị ảnh hưởng.

---

### UC31: Trung Tâm Thông Báo (Notifications)
- **Mục tiêu:** Hội viên và Huấn luyện viên nhận thông báo khi có biến động lịch học/lớp học.
- **Roles:** `Member, Coach`.
- **Endpoints:**
  - `GET /api/Notification/me`: Danh sách thông báo (Query: `isRead` (bool?), `page`, `pageSize`).
  - `PATCH /api/Notification/{id}/read`: Đánh dấu đã đọc / chưa đọc (Body: `{"isRead": true}`).
- **Bốn loại sự kiện thông báo thực tế hiện có trong Backend (`type`):**
  1. `CLASS_REGISTERED`: Phát sinh khi Hội viên tự đặt chỗ thành công hoặc Lễ tân đặt chỗ thay tại quầy (gửi cho Member).
  2. `SESSION_RESCHEDULED`: Phát sinh khi Quản lý dời lịch buổi học về ngày/giờ/phòng mới (gửi cho Member đã đặt và Coach phụ trách).
  3. `SESSION_COACH_CHANGED`: Phát sinh khi Quản lý đổi Huấn luyện viên phụ trách buổi học (gửi cho Coach cũ, Coach mới và Member đã đặt).
  4. `SESSION_CANCELED`: Phát sinh khi Quản lý hủy 1 buổi học hoặc hủy hàng loạt buổi tương lai của lớp (gửi cho Member đã đặt và Coach phụ trách).
- **Cách lấy tổng số thông báo chưa đọc (Unread Count):**
  - Backend hiện tại **chưa có endpoint riêng** dạng `/unread-count`.
  - **Giải pháp chính thức cho FE:** Gọi `GET /api/Notification/me?isRead=false&page=1&pageSize=1`, sau đó đọc trường **`totalItems`** trong response để hiển thị số lượng badge đỏ trên Bell Icon.
- **Bảo mật:** Gọi `PATCH /api/Notification/{id}/read` trên thông báo của người khác bị chặn với **`403 Forbidden`**.

---

### UC32: Nhật Ký Hệ Thống & Bộ Lọc Nâng Cao (Audit Log Filters)
- **Mục tiêu:** Quản lý trung tâm tra cứu toàn bộ lịch sử thao tác trên hệ thống.
- **Role:** `CenterManager`.
- **Endpoint:**
  - `GET /api/AuditLog`
- **Bộ lọc Query:**
  - `classId` (int?): Lọc theo lớp.
  - `sessionId` (int?): Lọc theo buổi.
  - `actorId` (string?): Lọc theo ID người thực hiện (hỗ trợ alias `accountId`).
  - `action` (string?): `CLASS_CREATED`, `CLASS_UPDATED`, `CLASS_STATUS_CHANGED`, `CLASS_SESSION_CREATED`, `SESSION_RESCHEDULED`, `SESSION_COACH_CHANGED`, `SESSION_CANCELED`, `CLASS_SESSIONS_CANCELED`, `CLASS_REGISTERED`, `CLASS_REGISTRATION_CANCELED`.
  - `entityType` (string?): `Class`, `ClassSession`, `ClassRegistration`,...
  - `fromDate` (string YYYY-MM-DD), `toDate` (string YYYY-MM-DD): Lọc theo khoảng thời gian. Backend đã tự động mở rộng `toDate` đến `23:59:59.999`.
  - `search` (string?): Tìm kiếm full text trong mô tả.
  - `page`, `pageSize`.

---

## 3. Checklist Nghiệm Thu Tích Hợp Frontend (FE Acceptance Checklist)

Đội ngũ Frontend kiểm tra các luồng theo thứ tự ưu tiên dưới đây:

### Nhóm 1: Quản Lý Lớp & Lịch Biểu (CenterManager)
- [ ] **UC17 Sports:**
  - [ ] Hiển thị danh sách bộ môn, phân trang, tìm kiếm.
  - [ ] Tạo mới bộ môn, cập nhật tên và mô tả.
  - [ ] Bật/tắt trạng thái hoạt động của bộ môn.
- [ ] **UC18 Rooms:**
  - [ ] Hiển thị danh sách phòng, phân trang, lọc theo loại phòng.
  - [ ] Tạo phòng mới, chỉnh sửa sức chứa phòng.
  - [ ] Thử giảm sức chứa phòng xuống dưới sức chứa của buổi học tương lai -> Kiểm tra bắt lỗi `ROOM_CAPACITY_BELOW_SCHEDULE` hiển thị thông báo hợp lý.
  - [ ] Bật/tắt trạng thái phòng -> Xác nhận phòng ngừng hoạt động không thể xếp buổi học mới (`ROOM_INACTIVE`).
- [ ] **UC19 Classes:**
  - [ ] Hiển thị danh sách lớp theo 4 trạng thái: `DRAFT`, `OPEN`, `CLOSED`, `ARCHIVED`.
  - [ ] Tạo lớp mới ở trạng thái `DRAFT`.
  - [ ] Thử chuyển lớp sang `OPEN` khi chưa có buổi tương lai -> Bắt lỗi `CLASS_HAS_NO_VALID_SESSION`.
  - [ ] Thử sửa lớp ở trạng thái `ARCHIVED` -> Bị khóa không cho sửa (`CLASS_ARCHIVED`).
- [ ] **UC21 Create/Reschedule Session:**
  - [ ] Tạo buổi học mới cho lớp, chọn HLV và Phòng.
  - [ ] Dời lịch buổi học (đổi ngày/giờ/phòng).
  - [ ] Thử dời lịch vào giờ HLV hoặc Phòng đã có lịch -> Bắt lỗi `COACH_SCHEDULE_CONFLICT` / `ROOM_SCHEDULE_CONFLICT`.
  - [ ] Thử dời lịch một buổi đã có học viên vào giờ học viên đó bận buổi khác -> Bắt lỗi `MEMBER_SCHEDULE_CONFLICT` và xác nhận giao diện giữ nguyên lịch cũ.
- [ ] **UC20 Assign Coach:**
  - [ ] Đổi HLV cho buổi học qua popup/dropdown.
  - [ ] Kiểm tra phản hồi thông báo và chống spam submit lặp lại.
- [ ] **UC22 Center Schedule:**
  - [ ] Xem lịch theo ngày, tuần với bộ lọc phòng, HLV, bộ môn.
- [ ] **UC23 Class & Session Details:**
  - [ ] Hiển thị chi tiết lớp và buổi học, tính toán `remainingCapacity`.

### Nhóm 2: Hội Viên Đặt Chỗ & Hủy Chỗ (Member)
- [ ] **UC24 Booking:**
  - [ ] Hội viên có gói `CONFIRMED` đặt chỗ thành công -> `remainingCapacity` giảm 1, nút chuyển sang trạng thái "Đã đặt".
  - [ ] Hội viên chưa có gói hoặc gói hết hạn đặt chỗ -> Bắt lỗi `MEMBERSHIP_NOT_VALID_FOR_SESSION`.
  - [ ] Hội viên có gói không hỗ trợ bộ môn đặt chỗ -> Bắt lỗi `PACKAGE_NOT_ELIGIBLE`.
  - [ ] Buổi hết chỗ (`remainingCapacity == 0`) -> Nút chuyển disabled "Hết chỗ" (`CLASS_FULL`).
  - [ ] Chống double click: Nút bị disable ngay sau khi bấm.
- [ ] **UC25 Cancel Booking:**
  - [ ] Hội viên tự hủy chỗ của mình -> Trạng thái đổi sang `CANCELED`, `remainingCapacity` tăng lại 1.
  - [ ] Thử gửi request hủy lượt của hội viên khác -> Bị chặn `403 Forbidden`.
- [ ] **UC26 My Class Schedule:**
  - [ ] Màn hình lịch học cá nhân (`/api/ClassRegistration/me`), lọc xem cả các buổi đã hủy (`includeCanceled=true`).

### Nhóm 3: Nghiệp Vụ Tại Quầy (Receptionist)
- [ ] **UC29 Counter Booking & Cancellation:**
  - [ ] Lễ tân tìm kiếm học viên và chọn buổi để đặt chỗ thay tại quầy.
  - [ ] Hủy đặt chỗ tại quầy với lý do ít hơn 3 ký tự -> Bắt lỗi `VALIDATION_ERROR`.
  - [ ] Hủy đặt chỗ tại quầy với lý do đầy đủ -> Thành công, hiển thị người hủy là Lễ tân.

### Nhóm 4: Lịch Giảng Dạy & Danh Sách Học Viên Đăng Ký (Coach)
- [ ] **UC27 My Teaching Schedule:**
  - [ ] HLV đăng nhập chỉ thấy các buổi dạy của chính mình (`/api/ClassSession/me`).
- [ ] **UC28 Assigned Class Members (Roster):**
  - [ ] HLV bấm xem danh sách học viên của buổi mình dạy -> Hiển thị danh sách họ tên, mã học viên.
  - [ ] Xác nhận giao diện chỉ phục vụ xem danh sách (không có nút điểm danh vì backend chưa hỗ trợ).
  - [ ] Xác nhận không hiển thị số điện thoại, email, thông tin gói tiền của học viên.
  - [ ] Thử truy cập buổi học của HLV khác -> Bị chặn `403 Forbidden`.

### Nhóm 5: Hủy Buổi & Hủy Lớp Tương Lai (CenterManager)
- [ ] **UC30 Session & Bulk Class Cancellation:**
  - [ ] Bấm hủy buổi / hủy toàn bộ buổi tương lai của lớp -> Hiển thị Modal mức độ ảnh hưởng (số buổi, số học viên).
  - [ ] Xác nhận hủy -> Các buổi học và vé đặt chuyển `CANCELED`.
  - [ ] Kiểm tra tài khoản hội viên: Gói tập vẫn giữ nguyên hạn dùng, không bị trừ tiền hay hủy gói.

### Nhóm 6: Trung Tâm Thông Báo (Member & Coach)
- [ ] **UC31 Notifications:**
  - [ ] Bell icon hiển thị số lượng chưa đọc (lấy từ `totalItems` của `GET /api/Notification/me?isRead=false&page=1&pageSize=1`).
  - [ ] Nhận 4 sự kiện thông báo: `CLASS_REGISTERED`, `SESSION_RESCHEDULED`, `SESSION_COACH_CHANGED`, `SESSION_CANCELED`.
  - [ ] Bấm vào thông báo để đánh dấu đã đọc (`PATCH /api/Notification/{id}/read`).

### Nhóm 7: Nhật Ký Hệ Thống (CenterManager)
- [ ] **UC32 Audit Filters:**
  - [ ] Lọc audit log theo lớp học (`classId`), buổi học (`sessionId`).
  - [ ] Lọc audit log theo nhân viên thao tác (`actorId`).
  - [ ] Lọc audit log theo khoảng ngày và kiểm tra hiển thị trọn vẹn cả ngày.

---

## 4. Bảng Tra Cứu Mã Lỗi Nghiệp Vụ Thực Tế

| Mã Lỗi (`code`) | HTTP Status | Ý Nghĩa Nghiệp Vụ | Gợi Ý Xử Lý Giao Diện (FE Action) |
| :--- | :---: | :--- | :--- |
| `UNAUTHORIZED` | 401 | Thiếu hoặc hết hạn JWT Bearer Token | Chuyển hướng về màn hình Đăng nhập |
| `FORBIDDEN` | 403 | Không đúng vai trò hoặc can thiệp dữ liệu người khác | Hiển thị thông báo "Bạn không có quyền thực hiện thao tác này" |
| `VALIDATION_ERROR` | 400 | Dữ liệu nhập không hợp lệ | Đánh dấu đỏ các trường lỗi theo `error.details` |
| `SESSION_IN_PAST` | 400 | Giờ học đã nằm trong quá khứ | Báo lỗi yêu cầu chọn ngày/giờ tương lai |
| `CLASS_NOT_FOUND` | 404 | Lớp học không tồn tại | Toast cảnh báo không tìm thấy bản ghi |
| `ROOM_NOT_FOUND` | 404 | Phòng tập không tồn tại | Toast cảnh báo không tìm thấy bản ghi |
| `COACH_NOT_FOUND` | 404 | Huấn luyện viên không tồn tại | Toast cảnh báo không tìm thấy bản ghi |
| `SESSION_NOT_FOUND` | 404 | Buổi học không tồn tại | Toast cảnh báo không tìm thấy bản ghi |
| `REGISTRATION_NOT_FOUND`| 404 | Lượt đăng ký không tồn tại | Toast cảnh báo không tìm thấy bản ghi |
| `SPORT_NAME_DUPLICATE` | 409 | Tên bộ môn đã tồn tại | Yêu cầu đổi tên bộ môn khác |
| `CLASS_NAME_DUPLICATE` | 409 | Tên lớp đã tồn tại | Yêu cầu đổi tên lớp khác |
| `CLASS_ARCHIVED` | 409 | Lớp đã lưu trữ | Khóa chức năng chỉnh sửa |
| `CLASS_HAS_NO_VALID_SESSION` | 409 | Mở lớp nhưng chưa có buổi tương lai | Yêu cầu tạo lịch buổi học trước khi mở lớp |
| `ROOM_INACTIVE` | 409 | Phòng tập đang ngừng hoạt động | Khóa/Ẩn phòng trong danh sách chọn |
| `COACH_INACTIVE` | 409 | Huấn luyện viên đã bị khóa | Khóa/Ẩn HLV trong danh sách phân công |
| `ROOM_CAPACITY_BELOW_SCHEDULE` | 409 | Sức chứa phòng nhỏ hơn lịch đã xếp | Yêu cầu điều chỉnh sức chứa buổi trước |
| `ROOM_SCHEDULE_CONFLICT` | 409 | Phòng bị trùng giờ | Hiển thị cảnh báo trùng phòng trên lịch |
| `COACH_SCHEDULE_CONFLICT`| 409 | Huấn luyện viên bị trùng giờ dạy | Yêu cầu chọn HLV khác hoặc đổi giờ |
| `MEMBER_SCHEDULE_CONFLICT`| 409 | Học viên bị trùng giờ học khác | Cảnh báo học viên đã có lịch học trùng giờ |
| `MEMBERSHIP_NOT_VALID_FOR_SESSION` | 409 | Gói tập không hợp lệ tại ngày học | Hướng dẫn học viên gia hạn/mua gói tập |
| `PACKAGE_NOT_ELIGIBLE` | 409 | Gói tập không cho phép môn này | Hướng dẫn học viên nâng cấp gói tập |
| `CLASS_FULL` | 409 | Buổi học đã hết chỗ | Chuyển trạng thái nút sang "Hết chỗ" |
| `DUPLICATE_REGISTRATION` | 409 | Đã đăng ký buổi này rồi | Thông báo "Bạn đã đặt chỗ buổi này" |
| `REGISTRATION_ALREADY_CANCELED` | 409 | Đăng ký đã được hủy trước đó | Disable nút hủy |
| `SESSION_ALREADY_STARTED`| 409 | Buổi học đã hoặc đang diễn ra | Disable chức năng hủy/sửa |
| `NO_CANCELABLE_SESSION` | 409 | Lớp không còn buổi tương lai nào để hủy | Disable nút hủy hàng loạt |
