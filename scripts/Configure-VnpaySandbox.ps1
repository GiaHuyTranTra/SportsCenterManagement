param([string]$CollectorAccountId = '', [string]$ReturnUrl = '')
$ErrorActionPreference = 'Stop'
$taskProject = Join-Path $PSScriptRoot '../SportsCenterManagement/SportsCenterManagement.csproj'
$taskMerchant = Read-Host 'Sandbox TmnCode'
$taskSecretSecure = Read-Host 'Sandbox HashSecret (hidden)' -AsSecureString
$taskCollector = $CollectorAccountId
if ([string]::IsNullOrWhiteSpace($taskCollector)) { $taskCollector = Read-Host 'SCMS test integration collector account ID' }
$taskReturnUrl = $ReturnUrl
if ([string]::IsNullOrWhiteSpace($taskReturnUrl)) { $taskReturnUrl = Read-Host 'FE result URL (Enter = http://localhost:5173/payments/result)' }
if ([string]::IsNullOrWhiteSpace($taskReturnUrl)) { $taskReturnUrl = 'http://localhost:5173/payments/result' }
$taskPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskSecretSecure)
try {
    $taskSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPointer).Trim()
    if ([string]::IsNullOrWhiteSpace($taskMerchant) -or $taskSecret.Length -lt 32 -or [string]::IsNullOrWhiteSpace($taskCollector)) {
        throw 'Sandbox merchant, secret (at least 32 characters) and collector account are required.'
    }
    $taskSettings = @{
        'VnpaySandbox:Enabled' = 'true'
        'VnpaySandbox:TmnCode' = $taskMerchant.Trim()
        'VnpaySandbox:HashSecret' = $taskSecret
        'VnpaySandbox:CollectorAccountId' = $taskCollector.Trim()
        'VnpaySandbox:ReturnUrl' = $taskReturnUrl.Trim()
    }
    # stdin avoids exposing the secret in command arguments or source files.
    $taskSettings | ConvertTo-Json -Compress | dotnet user-secrets set --project $taskProject
    if ($LASTEXITCODE -ne 0) { throw 'Saving local sandbox settings failed.' }
    Write-Host 'Saved locally outside Git. Restart BE, register the public HTTPS IPN URL with VNPAY, then test from Invoices.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPointer)
    $taskSecret = $null
    $taskSettings = $null
}
