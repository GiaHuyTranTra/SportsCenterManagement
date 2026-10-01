[CmdletBinding()]
param(
    [string]$SqlHost = "localhost",
    [ValidateRange(1, 65535)][int]$SqlPort = 1433,
    [string]$Database = "SportsCenterManagement_SWP_Local",
    [ValidateRange(1, 65535)][int]$ApiPort = 5299,
    [ValidateRange(1, 65535)][int]$FrontendPort = 5174,
    [SecureString]$SaPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Database -notmatch "^[A-Za-z0-9_]+$") { throw "Database name contains unsupported characters." }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw ".NET SDK was not found. Install .NET 10 SDK, then retry." }
if ($null -eq $SaPassword) { $SaPassword = Read-Host "SQL Server sa password" -AsSecureString }

$project = Join-Path (Split-Path -Parent $PSScriptRoot) "SportsCenterManagement/SportsCenterManagement.csproj"
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Backend project was not found." }

$plainPassword = [Net.NetworkCredential]::new("", $SaPassword).Password
$jwtBytes = [byte[]]::new(64)
[Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
$jwtSigningKey = [Convert]::ToBase64String($jwtBytes)

try {
    $env:ConnectionStrings__DefaultConnection = "Server=$SqlHost,$SqlPort;Database=$Database;User Id=sa;Password=$plainPassword;Encrypt=True;TrustServerCertificate=True"
    $env:Jwt__SigningKey = $jwtSigningKey
    $env:Cors__AllowedOrigins__0 = "http://localhost:$FrontendPort"
    $env:Cors__AllowedOrigins__1 = "http://127.0.0.1:$FrontendPort"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:$ApiPort"
    Write-Host "API: http://localhost:$ApiPort"
    Write-Host "Swagger: http://localhost:$ApiPort/swagger"
    Write-Host "Press Ctrl+C to stop."
    & dotnet run --no-launch-profile --project $project
    if ($LASTEXITCODE -ne 0) { throw "Backend exited with code $LASTEXITCODE." }
}
finally {
    Remove-Item Env:ConnectionStrings__DefaultConnection, Env:Jwt__SigningKey, Env:Cors__AllowedOrigins__0, Env:Cors__AllowedOrigins__1, Env:ASPNETCORE_ENVIRONMENT, Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
    [Array]::Clear($jwtBytes, 0, $jwtBytes.Length)
    $jwtSigningKey = $null
    $plainPassword = $null
}
