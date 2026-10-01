# Sports Center Management — Long Sprint 1 Backend

Nhánh bàn giao: `long/swp-sprint1-integration`.

## Yêu cầu

- Git, Docker Desktop, PowerShell 7 và .NET 10 SDK.
- Node.js 20 trở lên cho frontend.

## Clone backend

~~~powershell
git clone --branch long/swp-sprint1-integration https://github.com/GiaHuyTranTra/SportsCenterManagement.git
cd SportsCenterManagement
~~~

## Dựng database

~~~powershell
$saPassword = Read-Host "SQL Server sa password" -AsSecureString
$demoPassword = Read-Host "Demo account password" -AsSecureString
.\scripts\setup-long-local.ps1 -SaPassword $saPassword -DemoPassword $demoPassword
~~~

Lệnh tạo hoặc dùng lại container `scms-sql`, database `SportsCenterManagement_SWP_Local`, schema, patch Sprint 1 và dữ liệu demo. Script không xóa database/container đã tồn tại.

## Chạy API

~~~powershell
.\scripts\run-long-api.ps1 -SaPassword $saPassword
~~~

- API: `http://localhost:5299`
- Swagger: `http://localhost:5299/swagger`
- Development trả `demoCode` nếu SMTP chưa cấu hình.

## Tài khoản demo

Mật khẩu chung: mật khẩu demo bạn vừa nhập khi dựng database.

| Vai trò | Email |
| --- | --- |
| Center Manager | `manager.test@sportscenter.local` |
| Receptionist | `receptionist.test@sportscenter.local` |
| Member | `member.active@sportscenter.local` |
| Coach | `coach.test@sportscenter.local` |

Database còn có `member.expiring@sportscenter.local`, `member.suspended@sportscenter.local` và `member.inactive@sportscenter.local` để kiểm thử trạng thái.

## Kiểm thử

~~~powershell
dotnet test .\SportsCenterManagement.slnx
dotnet build .\SportsCenterManagement.slnx -c Release
.\scripts\smoke-long-sprint1.ps1 -ApiBaseUrl http://localhost:5299 -StaffEmail receptionist.test@sportscenter.local -StaffPassword $demoPassword
~~~

## Frontend

~~~powershell
git clone --branch long/swp-sprint1-integration https://github.com/NakinoMinh/Sports-Center-Management-System-FE.git
~~~

Làm theo `docs/LONG_SPRINT1_DEMO.md` trong repository FE.
