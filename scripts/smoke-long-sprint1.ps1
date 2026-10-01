[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [uri]$ApiBaseUrl,

    [Parameter(Mandatory)]
    [string]$StaffEmail,

    [Parameter(Mandatory)]
    [SecureString]$StaffPassword,

    [string]$VerificationCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$client = [Net.Http.HttpClient]::new()
$client.BaseAddress = [uri]($ApiBaseUrl.AbsoluteUri.TrimEnd("/") + "/")
$plainPassword = [Net.NetworkCredential]::new("", $StaffPassword).Password
$token = $null

function Assert-Smoke {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Path,

        [object]$Body,

        [string]$AccessToken,

        [Parameter(Mandatory)]
        [int[]]$ExpectedStatus
    )

    $request = [Net.Http.HttpRequestMessage]::new(
        [Net.Http.HttpMethod]::new($Method),
        $Path.TrimStart("/"))
    $response = $null
    try {
        if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
            $request.Headers.Authorization =
                [Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $AccessToken)
        }
        if ($null -ne $Body) {
            $json = ConvertTo-Json -InputObject $Body -Depth 8 -Compress
            $request.Content = [Net.Http.StringContent]::new(
                $json,
                [Text.Encoding]::UTF8,
                "application/json")
        }

        $response = $client.Send($request)
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $status = [int]$response.StatusCode
        if ($ExpectedStatus -notcontains $status) {
            throw "HTTP $status for $Method $Path. Body: $content"
        }

        return [pscustomobject]@{
            Status = $status
            Body = if ([string]::IsNullOrWhiteSpace($content)) {
                $null
            }
            else {
                ConvertFrom-Json -InputObject $content
            }
        }
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }
        $request.Dispose()
    }
}

try {
    $otp = Invoke-ApiRequest `
        -Method "POST" `
        -Path "/api/Auth/request-login-email-verification" `
        -Body @{ email = $StaffEmail; password = $plainPassword } `
        -ExpectedStatus @(200)
    $code = if (-not [string]::IsNullOrWhiteSpace($VerificationCode)) {
        $VerificationCode
    }
    else {
        [string]$otp.Body.demoCode
    }
    Assert-Smoke (-not [string]::IsNullOrWhiteSpace($code)) `
        "Verification code is required outside Development."

    $login = Invoke-ApiRequest `
        -Method "POST" `
        -Path "/api/Auth/login" `
        -Body @{
            email = $StaffEmail
            password = $plainPassword
            emailVerificationCode = $code
        } `
        -ExpectedStatus @(200)
    $token = [string]$login.Body.accessToken
    Assert-Smoke (-not [string]::IsNullOrWhiteSpace($token)) `
        "Login did not return an access token."
    Assert-Smoke (@("CenterManager", "Receptionist") -contains [string]$login.Body.role) `
        "Smoke test requires a CenterManager or Receptionist account."

    $packages = Invoke-ApiRequest `
        -Method "GET" `
        -Path "/api/MembershipPackage/active" `
        -ExpectedStatus @(200)
    $activePackage = @($packages.Body)[0]
    Assert-Smoke ($null -ne $activePackage) "No active membership package was found."

    $suffix = "{0:MMddHHmmss}{1:D3}" -f (Get-Date), (Get-Random -Minimum 0 -Maximum 1000)
    $counterEmail = "long.smoke.$suffix@sportscenter.local"
    $counterPhone = "07{0:D8}" -f (Get-Random -Minimum 0 -Maximum 100000000)
    $registered = Invoke-ApiRequest `
        -Method "POST" `
        -Path "/api/Member/counter-registration" `
        -AccessToken $token `
        -Body @{
            fullName = "Long Sprint 1 Smoke"
            email = $counterEmail
            phone = $counterPhone
            dateOfBirth = "2000-01-02"
            packageId = [int]$activePackage.id
            expectedPrice = [decimal]$activePackage.price
            paymentMethod = "CASH"
        } `
        -ExpectedStatus @(201)
    Assert-Smoke ($registered.Body.receipt.invoiceStatus -eq "PENDING_PAYMENT") `
        "Counter registration invoice must be pending payment."
    Assert-Smoke ($registered.Body.receipt.subscriptionStatus -eq "PENDING_PAYMENT") `
        "Counter registration subscription must be pending payment."

    $invoiceId = [int]$registered.Body.receipt.invoiceId
    $memberId = [string]$registered.Body.member.accountId
    $paid = Invoke-ApiRequest `
        -Method "POST" `
        -Path "/api/MembershipInvoice/$invoiceId/pay" `
        -AccessToken $token `
        -Body @{ paymentMethod = "CASH" } `
        -ExpectedStatus @(200)
    Assert-Smoke ($paid.Body.invoiceStatus -eq "PAID") `
        "Payment did not mark the invoice as paid."
    Assert-Smoke ($paid.Body.subscriptionStatus -eq "CONFIRMED") `
        "Payment did not confirm the subscription."

    $renewed = Invoke-ApiRequest `
        -Method "POST" `
        -Path "/api/MemberSubscription/counter-register-or-renew" `
        -AccessToken $token `
        -Body @{
            memberAccountId = $memberId
            packageId = [int]$activePackage.id
            paymentMethod = "CARD"
        } `
        -ExpectedStatus @(201)
    Assert-Smoke ($renewed.Body.invoiceStatus -eq "PENDING_PAYMENT") `
        "Counter renewal invoice must be pending payment."
    Assert-Smoke ($renewed.Body.subscriptionStatus -eq "PENDING_PAYMENT") `
        "Counter renewal subscription must be pending payment."

    $escapedEmail = [uri]::EscapeDataString($counterEmail)
    $statuses = Invoke-ApiRequest `
        -Method "GET" `
        -Path "/api/Member/membership-status?search=$escapedEmail&filter=ALL" `
        -AccessToken $token `
        -ExpectedStatus @(200)
    $rows = @($statuses.Body)
    Assert-Smoke ($rows.Count -eq 1 -and $rows[0].accountId -eq $memberId) `
        "Membership status lookup did not return the created member."

    $null = Invoke-ApiRequest `
        -Method "POST" `
        -Path "/api/Auth/Logout" `
        -AccessToken $token `
        -ExpectedStatus @(200)

    Write-Host "Long Sprint 1 smoke test: PASS"
}
finally {
    $plainPassword = $null
    $token = $null
    $client.Dispose()
}
