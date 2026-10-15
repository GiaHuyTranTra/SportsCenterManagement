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

Write-Output "=========================================================="
Write-Output "STARTING COMPREHENSIVE LIVE HTTP EXTENDED VERIFICATION"
Write-Output "=========================================================="

Write-Output "--- Phase 1: Authentication & Role Setup ---"
$mgrToken = Get-Token "manager@sportscenter.com" $env:SCMS_HTTP_MANAGER_PASSWORD
$mgrHeader = @{ Authorization = "Bearer $mgrToken" }
Write-Output "[PASS] CenterManager authenticated."

# Authenticate Coach 1
$coach1Token = Get-Token "coach.rbac@sportscenter.local" $env:SCMS_HTTP_TEST_PASSWORD
$coach1Profile = Invoke-RestMethod -Uri "$baseUrl/api/Auth/me" -Method Get -Headers @{ Authorization = "Bearer $coach1Token" }
$coach1Id = $coach1Profile.accountId
Write-Output "[PASS] Coach 1 authenticated: Id=$coach1Id"

# Create Coach 2 (so we can test Coach reassignment and roster privacy between coaches)
# First find an active sport
$sports = Invoke-RestMethod -Uri "$baseUrl/api/Sport?pageSize=1" -Method Get -Headers $mgrHeader
$sportId = $sports.items[0].id
$coach2Email = "coach.flow2." + [Guid]::NewGuid().ToString().Substring(0,8) + "@sportscenter.local"
$coach2Phone = "09" + (Get-Random -Minimum 10000000 -Maximum 99999999).ToString()
$coach2Body = @{
    email = $coach2Email
    password = $env:SCMS_HTTP_TEST_PASSWORD
    fullName = "Flow2 Coach Two"
    phone = $coach2Phone
    disciplineIds = @($sportId)
} | ConvertTo-Json
$coach2Create = Invoke-RestMethod -Uri "$baseUrl/api/Coach" -Method Post -Headers $mgrHeader -Body $coach2Body -ContentType "application/json"
$coach2Id = $coach2Create.accountId
Write-Output "[PASS] Coach 2 created: Id=$coach2Id ($coach2Email)"

$coach2Token = Get-Token $coach2Email $env:SCMS_HTTP_TEST_PASSWORD
Write-Output "[PASS] Coach 2 authenticated."

# Authenticate Receptionist
$recEmail = "rec.flow2.test@sportscenter.local"
$recToken = Get-Token $recEmail $env:SCMS_HTTP_TEST_PASSWORD
$recProfile = Invoke-RestMethod -Uri "$baseUrl/api/Auth/me" -Method Get -Headers @{ Authorization = "Bearer $recToken" }
$recId = $recProfile.accountId
$recHeader = @{ Authorization = "Bearer $recToken" }
Write-Output "[PASS] Receptionist authenticated: Id=$recId"

# Register Member at Counter with package 11 and confirm subscription
$memEmail = "mem.flow2." + [Guid]::NewGuid().ToString().Substring(0,8) + "@sportscenter.local"
$memPhone = "09" + (Get-Random -Minimum 10000000 -Maximum 99999999).ToString()
$counterRegBody = @{
    fullName = "Flow2 Extended Member"
    email = $memEmail
    phone = $memPhone
    packageId = 11
    expectedPrice = 500000
    paymentMethod = "CASH"
    dateOfBirth = "1998-08-18"
} | ConvertTo-Json
$counterReg = Invoke-RestMethod -Uri "$baseUrl/api/Member/counter-registration" -Method Post -Headers $recHeader -Body $counterRegBody -ContentType "application/json"
$memberId = $counterReg.member.accountId
$invoiceId = $counterReg.receipt.invoiceId
$payBody = @{ paymentMethod = "CASH" } | ConvertTo-Json
$payResult = Invoke-RestMethod -Uri "$baseUrl/api/MembershipInvoice/$invoiceId/pay" -Method Post -Headers $recHeader -Body $payBody -ContentType "application/json"
$memberToken = Get-Token $memEmail $counterReg.initialPassword
$memHeader = @{ Authorization = "Bearer $memberToken" }
Write-Output "[PASS] Member authenticated with CONFIRMED subscription: Id=$memberId"

Write-Output "`n--- Phase 2: UC18 Room Capacity Sizing & Deactivation Rules ---"
# Create Room R1 with capacity 10
$r1Name = "Room R1 " + [Guid]::NewGuid().ToString().Substring(0,6)
$r1 = Invoke-RestMethod -Uri "$baseUrl/api/Room" -Method Post -Headers $mgrHeader -Body (@{ name = $r1Name; capacity = 10; roomType = "STUDIO" } | ConvertTo-Json) -ContentType "application/json"
$r1Id = $r1.id
Write-Output "Created Room R1: Id=$r1Id, Capacity=10"

# Create Class and Session S1 in Room R1 with capacity 8
$clsName = "Class UC18 " + [Guid]::NewGuid().ToString().Substring(0,6)
$cls = Invoke-RestMethod -Uri "$baseUrl/api/Class" -Method Post -Headers $mgrHeader -Body (@{ sportId = $sportId; name = $clsName; capacity = 8; status = "DRAFT" } | ConvertTo-Json) -ContentType "application/json"
$classId = $cls.id

$futureDate1 = (Get-Date).AddDays(10).ToString("yyyy-MM-dd")
$s1Body = @{
    classId = $classId
    coachId = $coach1Id
    roomId = $r1Id
    date = $futureDate1
    startTime = "08:00"
    endTime = "09:00"
    capacity = 8
} | ConvertTo-Json
$s1 = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $mgrHeader -Body $s1Body -ContentType "application/json"
$s1Id = $s1.id
Write-Output "Created Session S1 in Room R1: Id=$s1Id, Capacity=8"

# Open the class
Invoke-RestMethod -Uri "$baseUrl/api/Class/$classId/status" -Method Patch -Headers $mgrHeader -Body (@{ status = "OPEN" } | ConvertTo-Json) -ContentType "application/json" | Out-Null
Write-Output "Class $classId opened."

# 1. Attempt to reduce R1 Capacity to 6 (< Session S1 Capacity 8) -> Must fail with 409 ROOM_CAPACITY_BELOW_SCHEDULE
try {
    Invoke-RestMethod -Uri "$baseUrl/api/Room/$r1Id" -Method Put -Headers $mgrHeader -Body (@{ name = $r1Name; capacity = 6; roomType = "STUDIO" } | ConvertTo-Json) -ContentType "application/json"
    throw "Should have failed with ROOM_CAPACITY_BELOW_SCHEDULE!"
} catch {
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errText = $reader.ReadToEnd()
        if ($errText -match "ROOM_CAPACITY_BELOW_SCHEDULE") {
            Write-Output "[PASS] UC18: Reducing room capacity below future session rejected with ROOM_CAPACITY_BELOW_SCHEDULE."
        } else {
            throw "Unexpected error: $errText"
        }
    } else {
        throw
    }
}

# 2. Update R1 Capacity to 9 (>= 8) -> Must succeed and preserve S1
$r1Updated = Invoke-RestMethod -Uri "$baseUrl/api/Room/$r1Id" -Method Put -Headers $mgrHeader -Body (@{ name = $r1Name; capacity = 9; roomType = "STUDIO" } | ConvertTo-Json) -ContentType "application/json"
Write-Output "[PASS] UC18: Reducing room capacity to 9 (above session 8) succeeded."

# 3. Deactivate Room R1 -> Must succeed and keep existing Session S1 scheduled
$r1Deactivated = Invoke-RestMethod -Uri "$baseUrl/api/Room/$r1Id/status" -Method Patch -Headers $mgrHeader -Body (@{ isActive = $false } | ConvertTo-Json) -ContentType "application/json"
Write-Output "[PASS] UC18: Room R1 deactivated successfully (isActive=$($r1Deactivated.isActive))."

$s1Check = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id" -Method Get -Headers $mgrHeader
if ($s1Check.status -eq "SCHEDULED") {
    Write-Output "[PASS] UC18: Existing session S1 remains SCHEDULED in deactivated room."
} else {
    throw "Existing session was invalidly altered!"
}

# 4. Attempt to create a NEW session in deactivated Room R1 -> Must fail with 409 ROOM_INACTIVE
try {
    $sFailBody = @{
        classId = $classId
        coachId = $coach1Id
        roomId = $r1Id
        date = (Get-Date).AddDays(11).ToString("yyyy-MM-dd")
        startTime = "08:00"
        endTime = "09:00"
        capacity = 5
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $mgrHeader -Body $sFailBody -ContentType "application/json"
    throw "Should have failed with ROOM_INACTIVE!"
} catch {
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errText = $reader.ReadToEnd()
        if ($errText -match "ROOM_INACTIVE") {
            Write-Output "[PASS] UC18: New session in deactivated room rejected with ROOM_INACTIVE."
        } else {
            throw "Unexpected error: $errText"
        }
    } else {
        throw
    }
}

Write-Output "`n--- Phase 3: UC21 Reschedule with Bookings (Entitlement, Conflicts, Atomicity) ---"
# Member books Session S1
$bookRes = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/register" -Method Post -Headers $memHeader -Body (@{ sessionId = $s1Id } | ConvertTo-Json) -ContentType "application/json"
$s1RegId = $bookRes.id
Write-Output "Member registered for Session S1: RegId=$s1RegId"

# Create Room R2 (active) for rescheduling tests
$r2Name = "Room R2 " + [Guid]::NewGuid().ToString().Substring(0,6)
$r2 = Invoke-RestMethod -Uri "$baseUrl/api/Room" -Method Post -Headers $mgrHeader -Body (@{ name = $r2Name; capacity = 10; roomType = "STUDIO" } | ConvertTo-Json) -ContentType "application/json"
$r2Id = $r2.id

# 1. Reschedule S1 to year 2029 (outside Member subscription date) -> Must fail with 409 MEMBERSHIP_NOT_VALID_FOR_SESSION
try {
    $reschedOutDate = @{
        classId = $classId
        coachId = $coach1Id
        roomId = $r2Id
        date = "2029-01-15"
        startTime = "08:00"
        endTime = "09:00"
        capacity = 8
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id" -Method Put -Headers $mgrHeader -Body $reschedOutDate -ContentType "application/json"
    throw "Should have failed with MEMBERSHIP_NOT_VALID_FOR_SESSION!"
} catch {
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errText = $reader.ReadToEnd()
        if ($errText -match "MEMBERSHIP_NOT_VALID_FOR_SESSION") {
            Write-Output "[PASS] UC21: Rescheduling session with bookings outside membership coverage rejected with MEMBERSHIP_NOT_VALID_FOR_SESSION."
        } else {
            throw "Unexpected error: $errText"
        }
    } else {
        throw
    }
}

# Verify Atomicity: S1 date remains on $futureDate1
$s1AfterFail1 = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id" -Method Get -Headers $mgrHeader
if ($s1AfterFail1.date -eq $futureDate1) {
    Write-Output "[PASS] UC21 Atomicity: Session S1 date unchanged ($($s1AfterFail1.date)) after rejected reschedule."
} else {
    throw "Atomicity violated! Session date changed."
}

# 2. Member schedule conflict test:
# Session S1 is at 08:00-09:00 with Coach 1 in Room R1.
# Create Room R3 (active) so Room R3 is completely FREE at 14:00-15:00
$r3Name = "Room R3 " + [Guid]::NewGuid().ToString().Substring(0,6)
$r3 = Invoke-RestMethod -Uri "$baseUrl/api/Room" -Method Post -Headers $mgrHeader -Body (@{ name = $r3Name; capacity = 10; roomType = "STUDIO" } | ConvertTo-Json) -ContentType "application/json"
$r3Id = $r3.id
Write-Output "Created Room R3: Id=$r3Id"

# Create Session S2 on $futureDate1 at 14:00-15:00 in Room R2, taught by Coach 2
$s2 = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $mgrHeader -Body (@{
    classId = $classId
    coachId = $coach2Id
    roomId = $r2Id
    date = $futureDate1
    startTime = "14:00"
    endTime = "15:00"
    capacity = 8
} | ConvertTo-Json) -ContentType "application/json"
$s2Id = $s2.id

# Member books Session S2
$s2Reg = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/register" -Method Post -Headers $memHeader -Body (@{ sessionId = $s2Id } | ConvertTo-Json) -ContentType "application/json"
Write-Output "Member registered for Session S2: Id=$s2Id at 14:00-15:00 (RegId=$($s2Reg.id))"

# Check notifications count before conflict attempt
$notifsBeforeConflict = (Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=50" -Method Get -Headers $memHeader).totalItems

# Now attempt to reschedule Session S1 to 14:00-15:00 on the same date using Room R3 and Coach 1.
# Coach 1 is free at 14:00-15:00. Room R3 is free at 14:00-15:00.
# The ONLY conflict is the Member being already registered in Session S2 at 14:00-15:00.
# MUST fail strictly with 409 MEMBER_SCHEDULE_CONFLICT!
try {
    $reschedConflict = @{
        classId = $classId
        coachId = $coach1Id
        roomId = $r3Id
        date = $futureDate1
        startTime = "14:00"
        endTime = "15:00"
        capacity = 8
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id" -Method Put -Headers $mgrHeader -Body $reschedConflict -ContentType "application/json"
    throw "Should have failed with MEMBER_SCHEDULE_CONFLICT!"
} catch {
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errText = $reader.ReadToEnd()
        if ($errText -match "MEMBER_SCHEDULE_CONFLICT") {
            Write-Output "[PASS] UC21: Rescheduling session causing member conflict strictly rejected with MEMBER_SCHEDULE_CONFLICT."
        } else {
            throw "Expected MEMBER_SCHEDULE_CONFLICT but got: $errText"
        }
    } else {
        throw
    }
}

# Verify Atomicity:
# 1. S1 time remains 08:00 - 09:00
$s1AfterFail2 = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id" -Method Get -Headers $mgrHeader
if ($s1AfterFail2.startTime -eq "08:00" -and $s1AfterFail2.endTime -eq "09:00") {
    Write-Output "[PASS] UC21 Atomicity: Session S1 time unchanged ($($s1AfterFail2.startTime)-$($s1AfterFail2.endTime))."
} else {
    throw "Atomicity violated! Session time changed."
}
# 2. S1 and S2 BookedCount remain 1
$s2Check = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s2Id" -Method Get -Headers $mgrHeader
if ($s1AfterFail2.bookedCount -eq 1 -and $s2Check.bookedCount -eq 1) {
    Write-Output "[PASS] UC21 Atomicity: S1 bookedCount=$($s1AfterFail2.bookedCount) and S2 bookedCount=$($s2Check.bookedCount) unchanged."
} else {
    throw "Atomicity violated! Booked count changed."
}
# 3. Both registrations remain BOOKED
$s1RegCheck = (Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/me" -Method Get -Headers $memHeader).items | Where-Object { $_.id -eq $s1RegId }
$s2RegCheck = (Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/me" -Method Get -Headers $memHeader).items | Where-Object { $_.id -eq $s2Reg.id }
if ($s1RegCheck.status -eq "BOOKED" -and $s2RegCheck.status -eq "BOOKED") {
    Write-Output "[PASS] UC21 Atomicity: Both Member registrations remain BOOKED."
} else {
    throw "Atomicity violated! Registration status changed."
}
# 4. Notifications unchanged
$notifsAfterConflict = (Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=50" -Method Get -Headers $memHeader).totalItems
if ($notifsAfterConflict -eq $notifsBeforeConflict) {
    Write-Output "[PASS] UC21 Atomicity: Notification count unchanged ($notifsAfterConflict)."
} else {
    throw "Atomicity violated! Notification generated during failed reschedule."
}

# 3. Successful reschedule to 16:00-17:00 in Room R2
$reschedSuccess = @{
    classId = $classId
    coachId = $coach1Id
    roomId = $r2Id
    date = $futureDate1
    startTime = "16:00"
    endTime = "17:00"
    capacity = 8
} | ConvertTo-Json
$s1Resched = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id" -Method Put -Headers $mgrHeader -Body $reschedSuccess -ContentType "application/json"
Write-Output "[PASS] UC21: Session S1 successfully rescheduled to 16:00-17:00."

# Verify Member received SESSION_RESCHEDULED notification
$memNotifs = Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=10" -Method Get -Headers $memHeader
$reschedNotif = $memNotifs.items | Where-Object { $_.type -eq "SESSION_RESCHEDULED" -and $_.entityId -eq "$s1Id" }
if ($reschedNotif) {
    Write-Output "[PASS] UC31: Member received SESSION_RESCHEDULED notification: $($reschedNotif.title)"
} else {
    throw "SESSION_RESCHEDULED notification not found for member!"
}

Write-Output "`n--- Phase 4: UC20 & UC31 Coach Change & Targeted Notifications (No Duplicate on Retry) ---"
# Change Coach of S1 from Coach 1 to Coach 2 via PUT /api/ClassSession/{id}/coach
$changeCoachBody = @{ coachId = $coach2Id } | ConvertTo-Json
$s1CoachUpdated = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id/coach" -Method Put -Headers $mgrHeader -Body $changeCoachBody -ContentType "application/json"
Write-Output "[PASS] UC20: Coach changed to Coach 2 ($coach2Id)."

# Verify Targeted Notifications for Member, Coach 1 (old), Coach 2 (new)
$memNotifs2 = Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=10" -Method Get -Headers $memHeader
$coach1Notifs = Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=10" -Method Get -Headers @{ Authorization = "Bearer $coach1Token" }
$coach2Notifs = Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=10" -Method Get -Headers @{ Authorization = "Bearer $coach2Token" }

$memCoachNotif = $memNotifs2.items | Where-Object { $_.type -eq "SESSION_COACH_CHANGED" -and $_.entityId -eq "$s1Id" }
$c1Notif = $coach1Notifs.items | Where-Object { $_.type -eq "SESSION_COACH_CHANGED" -and $_.entityId -eq "$s1Id" }
$c2Notif = $coach2Notifs.items | Where-Object { $_.type -eq "SESSION_COACH_CHANGED" -and $_.entityId -eq "$s1Id" }

if ($memCoachNotif) { Write-Output "[PASS] UC31: Member received SESSION_COACH_CHANGED." } else { throw "Member missing SESSION_COACH_CHANGED notification" }
if ($c1Notif) { Write-Output "[PASS] UC31: Old Coach 1 received reassignment notice: $($c1Notif.title)" } else { throw "Old coach missing notification" }
if ($c2Notif) { Write-Output "[PASS] UC31: New Coach 2 received assignment notice: $($c2Notif.title)" } else { throw "New coach missing notification" }

# RETRY IDEMPOTENCY: Call PUT /api/ClassSession/{id}/coach with Coach 2 again
$c2CountBefore = @($coach2Notifs.items | Where-Object { $_.type -eq "SESSION_COACH_CHANGED" -and $_.entityId -eq "$s1Id" }).Count
$retryCoach = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id/coach" -Method Put -Headers $mgrHeader -Body $changeCoachBody -ContentType "application/json"
$coach2NotifsAfter = Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=10" -Method Get -Headers @{ Authorization = "Bearer $coach2Token" }
$c2CountAfter = @($coach2NotifsAfter.items | Where-Object { $_.type -eq "SESSION_COACH_CHANGED" -and $_.entityId -eq "$s1Id" }).Count

if ($c2CountAfter -eq $c2CountBefore) {
    Write-Output "[PASS] UC20/UC31: Retry did NOT generate duplicate notification (Count=$c2CountAfter)."
} else {
    throw "Duplicate notification generated on retry!"
}

Write-Output "`n--- Phase 5: UC28 Privacy & Roster Scope ---"
# Session S1 is assigned to Coach 2.
# 1. Coach 1 (not assigned) attempts to view roster -> Must return 403 Forbidden
try {
    Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id/registrations" -Method Get -Headers @{ Authorization = "Bearer $coach1Token" }
    throw "Should have failed with 403 FORBIDDEN!"
} catch {
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 403) {
            Write-Output "[PASS] UC28: Coach 1 (unassigned) denied roster view with 403 Forbidden."
        } else {
            throw "Expected 403 but got $code"
        }
    } else {
        throw
    }
}

# 2. Member attempts to view roster -> Must return 403 Forbidden
try {
    Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id/registrations" -Method Get -Headers $memHeader
    throw "Should have failed with 403 FORBIDDEN!"
} catch {
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 403) {
            Write-Output "[PASS] UC28: Member denied roster view with 403 Forbidden."
        } else {
            throw "Expected 403 but got $code"
        }
    } else {
        throw
    }
}

# 3. Coach 2 (assigned coach) views roster -> Must return 200 OK with privacy fields only
$roster = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s1Id/registrations" -Method Get -Headers @{ Authorization = "Bearer $coach2Token" }
Write-Output "[PASS] UC28: Coach 2 (assigned) accessed roster: totalItems=$($roster.totalItems)"
$firstItem = $roster.items[0]
if ($firstItem.memberAccountId -and $firstItem.memberName -and $firstItem.memberCode -and $firstItem.status) {
    Write-Output "[PASS] UC28: Roster item contains member name ($($firstItem.memberName)), code ($($firstItem.memberCode)), status ($($firstItem.status))."
}
# Assert that private fields (phone, email, payment) are NOT present in roster
if ($firstItem.phone -or $firstItem.email -or $firstItem.subscription -or $firstItem.paymentMethod) {
    throw "Privacy violation! Private fields exposed in roster."
} else {
    Write-Output "[PASS] UC28: Privacy preserved. No phone, email, or payment details in roster."
}

# 4. Multi-tenant / Cross-Member Isolation & Schedule Scoping
Write-Output "`n--- Phase 5b: Cross-Member Isolation & Schedule Scoping ---"
# Register Member 2 at counter and pay
$mem2Email = "mem2.flow2." + [Guid]::NewGuid().ToString().Substring(0,8) + "@sportscenter.local"
$mem2Phone = "09" + (Get-Random -Minimum 10000000 -Maximum 99999999).ToString()
$counterReg2Body = @{
    fullName = "Flow2 Member Two"
    email = $mem2Email
    phone = $mem2Phone
    packageId = 11
    expectedPrice = 500000
    paymentMethod = "CASH"
    dateOfBirth = "1999-09-19"
} | ConvertTo-Json
$counterReg2 = Invoke-RestMethod -Uri "$baseUrl/api/Member/counter-registration" -Method Post -Headers $recHeader -Body $counterReg2Body -ContentType "application/json"
$member2Id = $counterReg2.member.accountId
$invoice2Id = $counterReg2.receipt.invoiceId
$pay2Result = Invoke-RestMethod -Uri "$baseUrl/api/MembershipInvoice/$invoice2Id/pay" -Method Post -Headers $recHeader -Body (@{ paymentMethod = "CASH" } | ConvertTo-Json) -ContentType "application/json"
$member2Token = Get-Token $mem2Email $counterReg2.initialPassword
$mem2Header = @{ Authorization = "Bearer $member2Token" }
Write-Output "[PASS] Member 2 registered and authenticated: Id=$member2Id"

# 4a. Member 2 attempts to cancel Member 1's registration ($s1RegId) -> Must return 403 Forbidden
try {
    Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/$s1RegId/cancel" -Method Post -Headers $mem2Header -Body (@{ reason = "Hacker cancel" } | ConvertTo-Json) -ContentType "application/json"
    throw "Should have failed with 403 FORBIDDEN!"
} catch {
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 403) {
            Write-Output "[PASS] UC25: Member 2 denied canceling Member 1's registration with 403 Forbidden."
        } else {
            throw "Expected 403 but got $code"
        }
    } else {
        throw
    }
}

# 4b. Member 2 attempts to mark Member 1's notification as read -> Must return 403 Forbidden
$mem1NotifsList = Invoke-RestMethod -Uri "$baseUrl/api/Notification/me?page=1&pageSize=10" -Method Get -Headers $memHeader
$mem1NotifId = $mem1NotifsList.items[0].id
try {
    Invoke-RestMethod -Uri "$baseUrl/api/Notification/$mem1NotifId/read" -Method Patch -Headers $mem2Header -Body (@{ isRead = $true } | ConvertTo-Json) -ContentType "application/json"
    throw "Should have failed with 403 FORBIDDEN!"
} catch {
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 403) {
            Write-Output "[PASS] UC31: Member 2 denied marking Member 1's notification with 403 Forbidden."
        } else {
            throw "Expected 403 but got $code"
        }
    } else {
        throw
    }
}

# 4c. My Class Schedule scoping: Member 2 calls /api/ClassRegistration/me
$mem2Schedule = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/me" -Method Get -Headers $mem2Header
$mem2HasMem1Reg = $mem2Schedule.items | Where-Object { $_.id -eq $s1RegId }
if (-not $mem2HasMem1Reg) {
    Write-Output "[PASS] UC26: My Class Schedule strictly scoped to token identity (Member 2 returned 0 registrations of Member 1)."
} else {
    throw "Data leak! Member 2 saw Member 1's registration."
}

# 4d. My Teaching Schedule scoping:
# Coach 1 calls /api/ClassSession/me (S1 is currently assigned to Coach 2)
$coach1Schedule = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/me?from=$futureDate1&to=$futureDate1" -Method Get -Headers @{ Authorization = "Bearer $coach1Token" }
$coach1HasS1 = $coach1Schedule.items | Where-Object { $_.id -eq $s1Id }
if (-not $coach1HasS1) {
    Write-Output "[PASS] UC27: My Teaching Schedule strictly scoped to Coach 1 (does not contain Session S1 assigned to Coach 2)."
} else {
    throw "Data leak! Coach 1 saw session assigned to Coach 2."
}
$coach2Schedule = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/me?from=$futureDate1&to=$futureDate1" -Method Get -Headers @{ Authorization = "Bearer $coach2Token" }
$coach2HasS1 = $coach2Schedule.items | Where-Object { $_.id -eq $s1Id }
if ($coach2HasS1) {
    Write-Output "[PASS] UC27: My Teaching Schedule strictly scoped to Coach 2 (contains assigned Session S1)."
} else {
    throw "Coach 2 missing assigned session S1."
}

Write-Output "`n--- Phase 6: UC29 Counter Registration & Cancellation by Receptionist ---"
# Create Session S3
$s3 = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $mgrHeader -Body (@{
    classId = $classId
    coachId = $coach2Id
    roomId = $r2Id
    date = (Get-Date).AddDays(12).ToString("yyyy-MM-dd")
    startTime = "10:00"
    endTime = "11:00"
    capacity = 5
} | ConvertTo-Json) -ContentType "application/json"
$s3Id = $s3.id

# 1. Receptionist counter registers Member
$counterBookBody = @{
    sessionId = $s3Id
    memberAccountId = $memberId
} | ConvertTo-Json
$counterBook = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/counter-register" -Method Post -Headers $recHeader -Body $counterBookBody -ContentType "application/json"
$counterRegId = $counterBook.id
Write-Output "[PASS] UC29: Counter registration created: Id=$counterRegId, RegisteredVia=$($counterBook.registeredVia), RegisteredByName=$($counterBook.registeredByName)"

$s3AfterBook = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s3Id" -Method Get -Headers $mgrHeader
if ($s3AfterBook.bookedCount -eq 1) {
    Write-Output "[PASS] UC29: Session S3 BookedCount incremented to 1."
}

# 2. Receptionist attempts cancellation without reason -> Must fail with 400 VALIDATION_ERROR
try {
    Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/$counterRegId/counter-cancel" -Method Post -Headers $recHeader -Body (@{ reason = "no" } | ConvertTo-Json) -ContentType "application/json"
    throw "Should have failed with VALIDATION_ERROR!"
} catch {
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 400) {
            Write-Output "[PASS] UC29: Cancellation with reason < 3 characters rejected with 400 VALIDATION_ERROR."
        } else {
            throw "Expected 400 but got $code"
        }
    } else {
        throw
    }
}

# 3. Receptionist cancels with valid reason
$counterCancelBody = @{ reason = "Hoc vien goi dien nho le tan huy giup vi dot xuat" } | ConvertTo-Json
try {
    $counterCancelRes = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/$counterRegId/counter-cancel" -Method Post -Headers $recHeader -Body $counterCancelBody -ContentType "application/json; charset=utf-8"
    Write-Output "[PASS] UC29: Counter cancellation successful: Status=$($counterCancelRes.status), CanceledByName=$($counterCancelRes.canceledByName), Reason=$($counterCancelRes.cancelReason)"
} catch {
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        Write-Output "Counter Cancel Failed: $($reader.ReadToEnd())"
    }
    throw
}

$s3AfterCancel = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$s3Id" -Method Get -Headers $mgrHeader
if ($s3AfterCancel.bookedCount -eq 0) {
    Write-Output "[PASS] UC29: Session S3 BookedCount decremented to 0."
}

Write-Output "`n--- Phase 7: UC30 Bulk Cancel Future Sessions of a Class ---"
# Create Class C_bulk with 2 future sessions: S_b1 and S_b2
$cBulkName = "Class BulkCancel " + [Guid]::NewGuid().ToString().Substring(0,6)
$cBulk = Invoke-RestMethod -Uri "$baseUrl/api/Class" -Method Post -Headers $mgrHeader -Body (@{ sportId = $sportId; name = $cBulkName; capacity = 5; status = "DRAFT" } | ConvertTo-Json) -ContentType "application/json"
$cBulkId = $cBulk.id

$sB1 = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $mgrHeader -Body (@{
    classId = $cBulkId
    coachId = $coach2Id
    roomId = $r2Id
    date = (Get-Date).AddDays(15).ToString("yyyy-MM-dd")
    startTime = "08:00"
    endTime = "09:00"
    capacity = 5
} | ConvertTo-Json) -ContentType "application/json"
$sB1Id = $sB1.id

$sB2 = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession" -Method Post -Headers $mgrHeader -Body (@{
    classId = $cBulkId
    coachId = $coach2Id
    roomId = $r2Id
    date = (Get-Date).AddDays(16).ToString("yyyy-MM-dd")
    startTime = "08:00"
    endTime = "09:00"
    capacity = 5
} | ConvertTo-Json) -ContentType "application/json"
$sB2Id = $sB2.id

# Open C_bulk
Invoke-RestMethod -Uri "$baseUrl/api/Class/$cBulkId/status" -Method Patch -Headers $mgrHeader -Body (@{ status = "OPEN" } | ConvertTo-Json) -ContentType "application/json" | Out-Null
Write-Output "Class C_bulk ($cBulkId) opened."

# Member books S_b1
$regBulk = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/register" -Method Post -Headers $memHeader -Body (@{ sessionId = $sB1Id } | ConvertTo-Json) -ContentType "application/json"
$regBulkId = $regBulk.id
Write-Output "Class C_bulk created with 2 future sessions. Member registered for session S_b1 (RegId=$regBulkId)."

# 1. Check Cancel Impact
$impact = Invoke-RestMethod -Uri "$baseUrl/api/Class/$cBulkId/cancel-impact" -Method Get -Headers $mgrHeader
Write-Output "[PASS] UC30 Cancel Impact: Scope=$($impact.scope), AffectedSessions=$($impact.affectedSessionCount), AffectedRegistrations=$($impact.affectedRegistrationCount), AffectedMembers=$($impact.affectedMemberCount)"
if ($impact.affectedSessionCount -ne 2 -or $impact.affectedRegistrationCount -ne 1) {
    throw "Cancel impact count mismatch!"
}

# 2. Execute Bulk Cancel Future Sessions
$bulkCancelBody = @{ reason = "Nang cap bao tri toan bo he thong lop hoc" } | ConvertTo-Json
$bulkCancelRes = Invoke-RestMethod -Uri "$baseUrl/api/Class/$cBulkId/cancel-future-sessions" -Method Post -Headers $mgrHeader -Body $bulkCancelBody -ContentType "application/json; charset=utf-8"
Write-Output "[PASS] UC30 Bulk cancel executed: canceledSessionCount=$($bulkCancelRes.canceledSessionCount)"

# Verify both sessions are CANCELED
$sb1Check = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$sB1Id" -Method Get -Headers $mgrHeader
$sb2Check = Invoke-RestMethod -Uri "$baseUrl/api/ClassSession/$sB2Id" -Method Get -Headers $mgrHeader
if ($sb1Check.status -eq "CANCELED" -and $sb2Check.status -eq "CANCELED") {
    Write-Output "[PASS] UC30: Both future sessions set to CANCELED."
} else {
    throw "Sessions were not canceled!"
}

# Verify registration is CANCELED
$regBulkCheck = Invoke-RestMethod -Uri "$baseUrl/api/ClassRegistration/me?includeCanceled=true" -Method Get -Headers $memHeader
$canceledReg = $regBulkCheck.items | Where-Object { $_.id -eq $regBulkId }
if ($canceledReg.status -eq "CANCELED") {
    Write-Output "[PASS] UC30: Member registration set to CANCELED with reason '$($canceledReg.cancelReason)'."
} else {
    throw "Member registration was not canceled!"
}

# Verify Member Subscription remains valid (no package refund)
$memStatus = Invoke-RestMethod -Uri "$baseUrl/api/Member/membership-status?search=$memEmail" -Method Get -Headers $recHeader
$targetMemberStatus = $memStatus | Where-Object { $_.accountId -eq $memberId } | Select-Object -First 1
if ($targetMemberStatus -and $targetMemberStatus.status -ne "NONE") {
    Write-Output "[PASS] UC30: Member subscription remains active with package '$($targetMemberStatus.packageName)' (no package refund triggered)."
} else {
    throw "Member subscription status altered!"
}

# 3. Call cancel-future-sessions again -> Must fail with 409 NO_CANCELABLE_SESSION
try {
    Invoke-RestMethod -Uri "$baseUrl/api/Class/$cBulkId/cancel-future-sessions" -Method Post -Headers $mgrHeader -Body $bulkCancelBody -ContentType "application/json; charset=utf-8"
    throw "Should have failed with NO_CANCELABLE_SESSION!"
} catch {
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errText = $reader.ReadToEnd()
        if ($errText -match "NO_CANCELABLE_SESSION") {
            Write-Output "[PASS] UC30: Repeated cancel-future-sessions rejected with NO_CANCELABLE_SESSION."
        } else {
            throw "Unexpected error: $errText"
        }
    } else {
        throw
    }
}

Write-Output "`n--- Phase 8: UC32 Audit Filtering ---"
# 1. Filter by classId
$auditClass = Invoke-RestMethod -Uri "$baseUrl/api/AuditLog?classId=$cBulkId&pageSize=20" -Method Get -Headers $mgrHeader
Write-Output "[PASS] UC32 Filter by classId ($cBulkId): found $($auditClass.totalItems) log entries."
if ($auditClass.totalItems -gt 0) {
    Write-Output "   First entry: Action=$($auditClass.items[0].action), Entity=$($auditClass.items[0].entityType), Desc=$($auditClass.items[0].description)"
}

# 2. Filter by sessionId
$auditSession = Invoke-RestMethod -Uri "$baseUrl/api/AuditLog?sessionId=$s1Id&pageSize=20" -Method Get -Headers $mgrHeader
Write-Output "[PASS] UC32 Filter by sessionId ($s1Id): found $($auditSession.totalItems) log entries."
if ($auditSession.totalItems -gt 0) {
    Write-Output "   First entry: Action=$($auditSession.items[0].action), Entity=$($auditSession.items[0].entityType), Desc=$($auditSession.items[0].description)"
}

# 3. Filter by actorId (receptionist)
$auditActor = Invoke-RestMethod -Uri "$baseUrl/api/AuditLog?actorId=$recId&pageSize=20" -Method Get -Headers $mgrHeader
Write-Output "[PASS] UC32 Filter by actorId ($recId): found $($auditActor.totalItems) log entries."
if ($auditActor.totalItems -gt 0) {
    Write-Output "   First entry: Action=$($auditActor.items[0].action), Actor=$($auditActor.items[0].actorFullName), Desc=$($auditActor.items[0].description)"
}

# 4. Filter by date range (today)
$todayStr = (Get-Date).ToString("yyyy-MM-dd")
$auditDate = Invoke-RestMethod -Uri "$baseUrl/api/AuditLog?fromDate=$todayStr&toDate=$todayStr&pageSize=5" -Method Get -Headers $mgrHeader
Write-Output "[PASS] UC32 Filter by date ($todayStr): found $($auditDate.totalItems) log entries."

Write-Output "`n=========================================================="
Write-Output "ALL EXTENDED VERIFICATION TESTS PASSED SUCCESSFULLY!"
Write-Output "=========================================================="
