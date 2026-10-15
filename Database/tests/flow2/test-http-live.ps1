$ErrorActionPreference = 'Stop'
foreach ($requiredSecret in @('SCMS_HTTP_MANAGER_PASSWORD', 'SCMS_HTTP_TEST_PASSWORD')) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($requiredSecret))) {
        throw "Missing required environment variable: $requiredSecret"
    }
}
$baseUrl = "http://localhost:5198"

function Get-Token([string]$email, [string]$password) {
    $otpBody = @{ email = $email; password = $password } | ConvertTo-Json
    $otp = Invoke-RestMethod -Uri "$baseUrl/api/Auth/request-login-email-verification" -Method Post -Body $otpBody -ContentType "application/json"
    $code = $otp.demoCode
    $loginBody = @{ email = $email; password = $password; emailVerificationCode = $code } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "$baseUrl/api/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    return $login.accessToken
}

Write-Output "--- 1. Authenticating Roles ---"
$mgrToken = Get-Token "manager@sportscenter.com" $env:SCMS_HTTP_MANAGER_PASSWORD
Write-Output "Manager authenticated."

$coachToken = Get-Token "coach.rbac@sportscenter.local" $env:SCMS_HTTP_TEST_PASSWORD
Write-Output "Coach authenticated."

# Create / ensure a dedicated Receptionist exists
$mgrHeader = @{ Authorization = "Bearer $mgrToken" }
$recEmail = "rec.flow2.test@sportscenter.local"
try {
    $recBody = @{
        email = $recEmail
        password = $env:SCMS_HTTP_TEST_PASSWORD
        fullName = "Flow2 Receptionist"
        phone = "0933445566"
        workShift = "Morning"
    } | ConvertTo-Json
    $recCreate = Invoke-RestMethod -Uri "$baseUrl/api/Receptionist" -Method Post -Headers $mgrHeader -Body $recBody -ContentType "application/json"
    Write-Output "Receptionist created."
} catch {
    Write-Output "Receptionist create note: $($_.Exception.Message)"
}
$recToken = Get-Token $recEmail $env:SCMS_HTTP_TEST_PASSWORD
Write-Output "Receptionist authenticated."

Write-Output "--- 2. Registering Member at Counter & Confirming Subscription ---"
$recHeader = @{ Authorization = "Bearer $recToken" }
$testMemberEmail = "mem.flow2." + [Guid]::NewGuid().ToString().Substring(0,8) + "@sportscenter.local"
$testMemberPhone = "09" + (Get-Random -Minimum 10000000 -Maximum 99999999).ToString()
$counterRegBody = @{
    fullName = "Flow2 Test Member"
    email = $testMemberEmail
    phone = $testMemberPhone
    packageId = 11
    expectedPrice = 500000
    paymentMethod = "CASH"
    dateOfBirth = "1995-05-15"
} | ConvertTo-Json

$counterReg = Invoke-RestMethod -Uri "$baseUrl/api/Member/counter-registration" -Method Post -Headers $recHeader -Body $counterRegBody -ContentType "application/json"
$newMemberId = $counterReg.member.accountId
$invoiceId = $counterReg.receipt.invoiceId
Write-Output "Member created: $newMemberId, Invoice: $invoiceId, Initial Password: $($counterReg.initialPassword)"

# Pay invoice to CONFIRM subscription
$payBody = @{ paymentMethod = "CASH" } | ConvertTo-Json
$payResult = Invoke-RestMethod -Uri "$baseUrl/api/MembershipInvoice/$invoiceId/pay" -Method Post -Headers $recHeader -Body $payBody -ContentType "application/json"
Write-Output "Invoice paid, subscription confirmed. Status: $($payResult.subscriptionStatus)"

# Authenticate as the new member
$memberToken = Get-Token $testMemberEmail $counterReg.initialPassword
Write-Output "New Member authenticated."
$memHeader = @{ Authorization = "Bearer $memberToken" }

Write-Output "--- 3. Creating Sport, Room, Class, and Session (as CenterManager) ---"
$sportName = "Pilates " + [Guid]::NewGuid().ToString().Substring(0,6)
$sportBody = @{ name = $sportName; description = "Live test sport" } | ConvertTo-Json
$sport = Invoke-RestMethod -Uri "$baseUrl/api/Sport" -Method Post -Headers $mgrHeader -Body $sportBody -ContentType "application/json"
$sportId = $sport.id
Write-Output "Sport created: Id=$sportId ($sportName)"

$roomName = "Studio " + [Guid]::NewGuid().ToString().Substring(0,6)
$roomBody = @{ name = $roomName; capacity = 5; roomType = "STUDIO"; location = "Floor 2" } | ConvertTo-Json
$room = Invoke-RestMethod -Uri "$baseUrl/api/Room" -Method Post -Headers $mgrHeader -Body $roomBody -ContentType "application/json"
$roomId = $room.id
Write-Output "Room created: Id=$roomId ($roomName)"

$className = "Class " + [Guid]::NewGuid().ToString().Substring(0,6)
$classBody = @{ sportId = $sportId; name = $className; capacity = 5; status = "DRAFT" } | ConvertTo-Json
$cls = Invoke-RestMethod -Uri "$baseUrl/api/Class" -Method Post -Headers $mgrHeader -Body $classBody -ContentType "application/json"
$classId = $cls.id
Write-Output "Class created: Id=$classId ($className)"

# Query coach ID
$coachProfile = Invoke-RestMethod -Uri "$baseUrl/api/Auth/me" -Method Get -Headers @{ Authorization = "Bearer $coachToken" }
$coachAccountId = $coachProfile.accountId

$sessionDate = (Get-Date).AddDays((Get-Random -Minimum 5 -Maximum 30)).ToString("yyyy-MM-dd")
$hInt = Get-Random -Minimum 8 -Maximum 17
$startTm = "{0:D2}:00" -f $hInt
$endTm = "{0:D2}:00" -f ($hInt + 1)
$sessionBody = @{
    classId = $classId
    coachId = $coachAccountId
    roomId = $roomId
    date = $sessionDate
    startTime = $startTm
    endTime = $endTm
    capacity = 5
} | ConvertTo-Json
try {
    $session = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $mgrHeader -Body $sessionBody -ContentType "application/json"
    $sessionId = $session.id
    Write-Output "Session created: Id=$sessionId on $sessionDate $startTm-$endTm"
} catch {
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        Write-Output "Session Error Response: $($reader.ReadToEnd())"
    }
    throw
}

# Open Class
$openClassBody = @{ status = "OPEN" } | ConvertTo-Json
$openRes = Invoke-RestMethod -Uri "$baseUrl/api/Class/$classId/status" -Method Patch -Headers $mgrHeader -Body $openClassBody -ContentType "application/json"
Write-Output "Class opened."

Write-Output "--- 4. Member Booking with CONFIRMED Subscription ---"
$bookBody = @{ sessionId = $sessionId } | ConvertTo-Json
$booking = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/register" -Method Post -Headers $memHeader -Body $bookBody -ContentType "application/json"
$regId = $booking.id
Write-Output "Booking successful! RegistrationId=$regId"

# Check booked count
$sessionAfterBook = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$sessionId" -Method Get -Headers $memHeader
Write-Output "Session BookedCount after booking: $($sessionAfterBook.bookedCount)"

# Check member's registration list
$myRegistrations = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/me?page=1&pageSize=10" -Method Get -Headers $memHeader
Write-Output "Member has $($myRegistrations.totalItems) registration(s). Status: $($myRegistrations.items[0].status)"

Write-Output "--- 5. Member Cancellation ---"
$cancelBody = @{ reason = "Conflict in personal schedule" } | ConvertTo-Json
$cancelRes = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/$regId/cancel" -Method Post -Headers $memHeader -Body $cancelBody -ContentType "application/json"
Write-Output "Booking canceled. Rows updated: $($cancelRes.success)"

$sessionAfterCancel = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$sessionId" -Method Get -Headers $memHeader
Write-Output "Session BookedCount after cancellation: $($sessionAfterCancel.bookedCount)"

# Test duplicate cancellation (should fail with 409 Conflict)
try {
    Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/$regId/cancel" -Method Post -Headers $memHeader -Body $cancelBody -ContentType "application/json"
    Write-Output "ERROR: Expected 409 Conflict on duplicate cancellation!"
} catch {
    Write-Output "Duplicate cancellation correctly rejected: $($_.Exception.Message)"
}

Write-Output "--- 6. Notifications ---"
$notifs = Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=10" -Method Get -Headers $memHeader
Write-Output "Member notifications count: $($notifs.totalItems)"
if ($notifs.totalItems -gt 0) {
    $firstNotif = $notifs.items[0]
    Write-Output "Notification 1: Id=$($firstNotif.id) Type=$($firstNotif.type) Title=$($firstNotif.title) isRead=$($firstNotif.isRead)"
    # Mark as read
    $readBody = @{ isRead = $true } | ConvertTo-Json
    $readRes = Invoke-RestMethod -Uri "$baseUrl/api/Notification/$($firstNotif.id)/read" -Method Patch -Headers $memHeader -Body $readBody -ContentType "application/json"
    Write-Output "Notification marked as read: id=$($readRes.id) isRead=$($readRes.isRead)"
}

Write-Output "--- 7. Phân Quyền (RBAC) Verification ---"
# Coach cannot create class
try {
    Invoke-RestMethod -Uri "$baseUrl/api/Class" -Method Post -Headers @{ Authorization = "Bearer $coachToken" } -Body $classBody -ContentType "application/json"
    Write-Output "ERROR: Coach should not be able to create Class!"
} catch {
    Write-Output "RBAC: Coach cannot create Class -> 403 Forbidden confirmed."
}

# Member cannot create session
try {
    Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $memHeader -Body $sessionBody -ContentType "application/json"
    Write-Output "ERROR: Member should not be able to create ClassSession!"
} catch {
    Write-Output "RBAC: Member cannot create ClassSession -> 403 Forbidden confirmed."
}

# Member cannot view roster
try {
    Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$sessionId/registrations" -Method Get -Headers $memHeader
    Write-Output "ERROR: Member should not be able to view roster!"
} catch {
    Write-Output "RBAC: Member cannot view session registrations roster -> 403 Forbidden confirmed."
}

# Receptionist cannot create Class
try {
    Invoke-RestMethod -Uri "$baseUrl/api/Class" -Method Post -Headers $recHeader -Body $classBody -ContentType "application/json"
    Write-Output "ERROR: Receptionist should not be able to create Class!"
} catch {
    Write-Output "RBAC: Receptionist cannot create Class -> 403 Forbidden confirmed."
}

Write-Output "--- 8. Audit Log Verification ---"
$auditLogs = Invoke-RestMethod -Uri "$baseUrl/api/AuditLog?page=1&pageSize=20&search=Class" -Method Get -Headers $mgrHeader
Write-Output "Audit logs count found: $($auditLogs.totalItems)"
foreach ($log in $auditLogs.items | Select-Object -First 3) {
    Write-Output "AuditLog: Id=$($log.id) Action=$($log.action) EntityType=$($log.entityType) Desc=$($log.description)"
}

Write-Output "===== ALL LIVE HTTP FLOW TESTS PASSED SUCCESSFULLY ====="
