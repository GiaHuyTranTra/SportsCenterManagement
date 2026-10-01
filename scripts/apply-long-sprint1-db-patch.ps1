[CmdletBinding()]
param(
    [string]$ContainerName = "scms-sql",

    [string]$Database = "SportsCenterManagement_SWP_E2E",

    [Parameter(Mandatory)]
    [SecureString]$SaPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Database -notmatch "^[A-Za-z0-9_]+$") {
    throw "Database name contains unsupported characters."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$patchPath = Join-Path $repositoryRoot "Database\2026-10-01-long-sprint1-patch.sql"
$containerPatchPath = "/tmp/long-sprint1-{0}.sql" -f [Guid]::NewGuid().ToString("N")
$plainPassword = [Net.NetworkCredential]::new("", $SaPassword).Password

try {
    if (-not (Test-Path -LiteralPath $patchPath -PathType Leaf)) {
        throw "Database patch file was not found."
    }

    $containerRunning = & docker inspect --format "{{.State.Running}}" $ContainerName 2>$null
    if ($LASTEXITCODE -ne 0 -or $containerRunning.Trim() -ne "true") {
        throw "SQL Server container is not running."
    }

    & docker cp $patchPath "${ContainerName}:$containerPatchPath"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not copy the database patch into the container."
    }

    & docker exec -e "SQLCMDPASSWORD=$plainPassword" $ContainerName `
        /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -C -b -d $Database -i $containerPatchPath
    if ($LASTEXITCODE -ne 0) {
        throw "Database patch failed."
    }

    Write-Host "Long Sprint 1 database patch: PASS ($Database)"
}
finally {
    $plainPassword = $null
    & docker exec -u 0 $ContainerName rm -f $containerPatchPath *> $null
}
