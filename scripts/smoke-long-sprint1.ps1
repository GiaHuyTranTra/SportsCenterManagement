[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [uri]$ApiBaseUrl,

    [Parameter(Mandatory)]
    [string]$ManagerEmail,

    [Parameter(Mandatory)]
    [SecureString]$ManagerPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$client = [Net.Http.HttpClient]::new()
$client.BaseAddress = [uri]($ApiBaseUrl.AbsoluteUri.TrimEnd("/") + "/")
$plainPassword = [Net.NetworkCredential]::new("", $ManagerPassword).Password
$token = $null
$activePackage = $null
$managedMember = $null
$counterMember = $null
$failed = $false
$suffix = "{0:MMddHHmmss}{1:D3}" -f (Get-Date), (Get-Random -Minimum 0 -Maximum 1000)
$phoneSuffix = Get-Random -Minimum 0 -Maximum 100000000
$managedEmail = "long.smoke.managed.$suffix@sportscenter.local"
$counterEmail = "long.smoke.counter.$suffix@sportscenter.local"
$managedPhone = "09{0:D8}" -f $phoneSuffix
$counterPhone = "08{0:D8}" -f $phoneSuffix

function Assert-Smoke {
    param([Parameter(Mandatory)][bool]$Condition)

    if (-not $Condition) {
        throw "Smoke assertion failed."
    }
}

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [object]$Body,
        [string]$AccessToken,
        [Parameter(Mandatory)][int[]]$ExpectedStatus
    )

    $request = [Net.Http.HttpRequestMessage]::new(
        [Net.Http.HttpMethod]::new($Method),
        $Path.TrimStart("/")
    )
    $response = $null
    try {
        if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
            $request.Headers.Authorization =
                [Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $AccessToken)
        }
        if ($null -ne $Body) {
            $json = ConvertTo-Json -InputObject $Body -Depth 10 -Compress
            $request.Content = [Net.Http.StringContent]::new(
                $json,
                [Text.Encoding]::UTF8,
                "application/json"
            )
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $statusCode = [int]$response.StatusCode
        $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ($ExpectedStatus -notcontains $statusCode) {
            throw "Unexpected HTTP status."
        }

        $parsedBody = $null
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            try {
                $parsedBody = ConvertFrom-Json -InputObject $text
            }
            catch {
                $parsedBody = $text
            }
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Body = $parsedBody
        }
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }
        $request.Dispose()
    }
}

function Invoke-SmokeStep {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    Write-Host "$Name`: " -NoNewline
    try {
        & $Action
        Write-Host "PASS"
    }
    catch {
        Write-Host "FAIL"
        throw
    }
}

try {
    Invoke-SmokeStep "Login" {
        $login = Invoke-ApiRequest -Method "POST" -Path "/api/Auth/login" -Body @{
            email = $ManagerEmail.Trim()
            password = $plainPassword
        } -ExpectedStatus @(200)
        Assert-Smoke (-not [string]::IsNullOrWhiteSpace($login.Body.accessToken))
        Assert-Smoke ($login.Body.role -eq "CenterManager")
        $script:token = $login.Body.accessToken
        $script:plainPassword = $null
    }

    Invoke-SmokeStep "Public packages" {
        $packages = Invoke-ApiRequest -Method "GET" -Path "/api/MembershipPackage/active" -ExpectedStatus @(200)
        $packageRows = @($packages.Body)
        Assert-Smoke ($packageRows.Count -gt 0)
        Assert-Smoke ([int]$packageRows[0].id -gt 0)
        Assert-Smoke ([decimal]$packageRows[0].price -gt 0)
        $script:activePackage = $packageRows[0]
    }

    Invoke-SmokeStep "Member list/search/filter" {
        $members = Invoke-ApiRequest -Method "GET" -Path "/api/Member?page=1&pageSize=20" -AccessToken $token -ExpectedStatus @(200)
        $memberRows = @($members.Body.items)
        Assert-Smoke ($memberRows.Count -gt 0)
        $seedMember = $memberRows[0]
        $search = [uri]::EscapeDataString([string]$seedMember.memberCode)
        $status = [uri]::EscapeDataString([string]$seedMember.status)
        $filtered = Invoke-ApiRequest -Method "GET" -Path "/api/Member?page=1&pageSize=20&search=$search&status=$status" -AccessToken $token -ExpectedStatus @(200)
        Assert-Smoke (@($filtered.Body.items).accountId -contains $seedMember.accountId)
    }

    Invoke-SmokeStep "Create managed member" {
        $created = Invoke-ApiRequest -Method "POST" -Path "/api/Member" -AccessToken $token -Body @{
            fullName = "Long Smoke Managed"
            email = $managedEmail
            phone = $managedPhone
            dateOfBirth = "2001-02-03"
            isActive = $true
        } -ExpectedStatus @(201)
        Assert-Smoke (-not [string]::IsNullOrWhiteSpace($created.Body.member.accountId))
        Assert-Smoke (-not [string]::IsNullOrWhiteSpace($created.Body.initialPassword))
        $created.Body.initialPassword = $null
        $script:managedMember = $created.Body.member
    }

    Invoke-SmokeStep "Update/detail managed member" {
        $memberId = [uri]::EscapeDataString([string]$managedMember.accountId)
        $updated = Invoke-ApiRequest -Method "PATCH" -Path "/api/Member/$memberId" -AccessToken $token -Body @{
            fullName = "Long Smoke Managed Updated"
            email = $managedEmail
            phone = $managedPhone
            dateOfBirth = "2001-02-03"
            isActive = $true
        } -ExpectedStatus @(200)
        Assert-Smoke ($updated.Body.fullName -eq "Long Smoke Managed Updated")
        $detail = Invoke-ApiRequest -Method "GET" -Path "/api/Member/$memberId" -AccessToken $token -ExpectedStatus @(200)
        Assert-Smoke ($detail.Body.email -eq $managedEmail)
    }

    Invoke-SmokeStep "Soft delete managed member" {
        $memberId = [uri]::EscapeDataString([string]$managedMember.accountId)
        $null = Invoke-ApiRequest -Method "DELETE" -Path "/api/Member/$memberId" -AccessToken $token -ExpectedStatus @(204)
        $null = Invoke-ApiRequest -Method "GET" -Path "/api/Member/$memberId" -AccessToken $token -ExpectedStatus @(404)
    }

    Invoke-SmokeStep "Counter registration" {
        $registered = Invoke-ApiRequest -Method "POST" -Path "/api/Member/counter-registration" -AccessToken $token -Body @{
            fullName = "Long Smoke Counter"
            email = $counterEmail
            phone = $counterPhone
            dateOfBirth = "2002-03-04"
            packageId = [int]$activePackage.id
            expectedPrice = [decimal]$activePackage.price
            paymentMethod = "CASH"
        } -ExpectedStatus @(201)
        Assert-Smoke (-not [string]::IsNullOrWhiteSpace($registered.Body.member.accountId))
        Assert-Smoke (-not [string]::IsNullOrWhiteSpace($registered.Body.initialPassword))
        Assert-Smoke ($registered.Body.receipt.invoiceStatus -eq "PENDING_PAYMENT")
        Assert-Smoke ([int]$registered.Body.receipt.packageId -eq [int]$activePackage.id)
        $registered.Body.initialPassword = $null
        $script:counterMember = $registered.Body.member
    }

    Invoke-SmokeStep "Membership status" {
        $search = [uri]::EscapeDataString($counterEmail)
        $statuses = Invoke-ApiRequest -Method "GET" -Path "/api/Member/membership-status?search=$search&filter=ALL" -AccessToken $token -ExpectedStatus @(200)
        $statusRows = @($statuses.Body)
        Assert-Smoke ($statusRows.Count -eq 1)
        Assert-Smoke ($statusRows[0].accountId -eq $counterMember.accountId)
        Assert-Smoke ($statusRows[0].status -eq "PENDING_PAYMENT")
    }

    Invoke-SmokeStep "Logout and revocation" {
        $null = Invoke-ApiRequest -Method "POST" -Path "/api/Auth/Logout" -AccessToken $token -ExpectedStatus @(200)
        $null = Invoke-ApiRequest -Method "GET" -Path "/api/Member?page=1&pageSize=20" -AccessToken $token -ExpectedStatus @(401)
    }
}
catch {
    $failed = $true
}
finally {
    $plainPassword = $null
    $token = $null
    $client.Dispose()
}

if ($failed) {
    exit 1
}
