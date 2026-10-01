[CmdletBinding()]
param(
    [string]$ContainerName = "scms-sql",
    [ValidateRange(1, 65535)][int]$SqlPort = 1433,
    [string]$Database = "SportsCenterManagement_SWP_Local",
    [SecureString]$SaPassword,
    [SecureString]$DemoPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Database -notmatch "^[A-Za-z0-9_]+$") { throw "Database name contains unsupported characters." }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker was not found. Install and start Docker Desktop, then retry." }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw ".NET 10 SDK was not found. Install it, then retry." }
& docker info *> $null
if ($LASTEXITCODE -ne 0) { throw "Docker is not running. Start Docker Desktop, then retry." }
if ($null -eq $SaPassword) { $SaPassword = Read-Host "SQL Server sa password" -AsSecureString }
if ($null -eq $DemoPassword) { $DemoPassword = Read-Host "Demo account password" -AsSecureString }

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$databaseDirectory = Join-Path $repositoryRoot "Database"
$scripts = @("00-sports-center-bootstrap.sql", "2026-10-01-long-sprint1-patch.sql", "2026-10-01-long-demo-membership-packages.sql", "2026-10-01-long-demo-accounts.sql")
$plainPassword = [Net.NetworkCredential]::new("", $SaPassword).Password
$plainDemoPassword = [Net.NetworkCredential]::new("", $DemoPassword).Password
$demoPasswordHash = $null
$containerFiles = [Collections.Generic.List[string]]::new()

try {
    if ($plainDemoPassword.Length -lt 8) { throw "Demo account password must contain at least 8 characters." }
    $servicesProject = Join-Path $repositoryRoot "Services\Services.csproj"
    & dotnet restore $servicesProject --nologo *> $null
    if ($LASTEXITCODE -ne 0) { throw "Could not restore the existing BCrypt dependency." }
    [xml]$servicesProjectXml = Get-Content -Raw -LiteralPath $servicesProject
    $bcryptVersion = $servicesProjectXml.SelectSingleNode("//PackageReference[@Include='BCrypt.Net-Next']").Version
    $nugetPackages = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) { Join-Path ([Environment]::GetFolderPath("UserProfile")) ".nuget\packages" } else { $env:NUGET_PACKAGES }
    $bcryptAssembly = Join-Path $nugetPackages "bcrypt.net-next\$bcryptVersion\lib\netstandard2.0\BCrypt-Net-Next.dll"
    if (-not (Test-Path -LiteralPath $bcryptAssembly -PathType Leaf)) { throw "BCrypt assembly was not found after restore." }
    [Reflection.Assembly]::LoadFrom($bcryptAssembly) > $null
    $demoPasswordHash = [BCrypt.Net.BCrypt]::HashPassword($plainDemoPassword)

    $containerExists = @(& docker ps -a --format "{{.Names}}") -contains $ContainerName
    if (-not $containerExists) {
        $portMapping = $SqlPort.ToString() + ":1433"
        & docker run --name $ContainerName -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=$plainPassword" -e "MSSQL_PID=Developer" -p $portMapping -d "mcr.microsoft.com/mssql/server:2022-latest" *> $null
        if ($LASTEXITCODE -ne 0) { throw "Could not create SQL Server container. Check whether port $SqlPort is available." }
    }
    else {
        $running = & docker inspect --format "{{.State.Running}}" $ContainerName
        if ($LASTEXITCODE -ne 0) { throw "Could not inspect SQL Server container." }
        if ($running.Trim() -ne "true") {
            & docker start $ContainerName *> $null
            if ($LASTEXITCODE -ne 0) { throw "Could not start SQL Server container." }
        }
        $portMappings = @(& docker port $ContainerName "1433/tcp")
        if ($LASTEXITCODE -ne 0 -or -not ($portMappings -match ":$SqlPort$")) { throw "Container $ContainerName is not mapped to local port $SqlPort. Use its mapped port or another container name." }
    }

    $ready = $false
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        & docker exec -e "SQLCMDPASSWORD=$plainPassword" $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "SELECT 1" *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw "SQL Server did not become ready. If the container already existed, verify the sa password." }

    foreach ($script in $scripts) {
        $source = Join-Path $databaseDirectory $script
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Database script was not found: $source" }
        $target = "/tmp/long-local-{0}-{1}" -f [Guid]::NewGuid().ToString("N"), $script
        $copyTarget = $ContainerName + ":" + $target
        & docker cp $source $copyTarget *> $null
        if ($LASTEXITCODE -ne 0) { throw "Could not copy database script: $script" }
        $containerFiles.Add($target)
    }

    $schemaExists = & docker exec -e "SQLCMDPASSWORD=$plainPassword" $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$Database') IS NOT NULL AND OBJECT_ID(N'[$Database].dbo.Account', N'U') IS NOT NULL THEN 1 ELSE 0 END;"
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect the target database." }
    if (($schemaExists | Select-Object -Last 1).Trim() -ne "1") {
        & docker exec -e "SQLCMDPASSWORD=$plainPassword" $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -f 65001 -i $containerFiles[0] -v "DatabaseName=$Database"
        if ($LASTEXITCODE -ne 0) { throw "Database bootstrap failed." }
    }

    for ($index = 1; $index -lt $containerFiles.Count; $index++) {
        & docker exec -e "SQLCMDPASSWORD=$plainPassword" $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -f 65001 -d $Database -i $containerFiles[$index] -v "DemoPasswordHash=$demoPasswordHash"
        if ($LASTEXITCODE -ne 0) { throw "Database patch or seed failed: $($scripts[$index])" }
    }

    & docker exec -e "SQLCMDPASSWORD=$plainPassword" $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d $Database -Q "SET NOCOUNT ON; IF (SELECT COUNT(*) FROM dbo.Account WHERE Email LIKE '%@sportscenter.local') < 7 THROW 51010, 'Demo accounts are missing.', 1; IF (SELECT COUNT(*) FROM dbo.MembershipPackage) < 8 THROW 51011, 'Demo packages are missing.', 1; SELECT DB_NAME() AS [Database], (SELECT COUNT(*) FROM dbo.Account) AS Accounts, (SELECT COUNT(*) FROM dbo.MembershipPackage) AS Packages;"
    if ($LASTEXITCODE -ne 0) { throw "Database verification failed." }

    Write-Host "Local database setup: PASS"
    Write-Host "Container: $ContainerName"
    Write-Host "Database: $Database"
    Write-Host "SQL port: $SqlPort"
}
finally {
    foreach ($containerFile in $containerFiles) { & docker exec -u 0 $ContainerName rm -f $containerFile *> $null }
    $demoPasswordHash = $null
    $plainDemoPassword = $null
    $plainPassword = $null
}
