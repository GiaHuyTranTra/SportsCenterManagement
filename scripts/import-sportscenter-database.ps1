[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SourceSql,

    [string]$ContainerName = "scms-sql",

    [Parameter(Mandatory)]
    [string]$SaPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-DockerCommand {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed."
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$patchSql = Join-Path $repositoryRoot "Database\2026-09-30-long-sprint1-patch.sql"
$temporarySql = Join-Path ([IO.Path]::GetTempPath()) ("scms-import-{0}.sql" -f [Guid]::NewGuid().ToString("N"))
$containerSeedSql = "/tmp/scms-import.sql"
$containerPatchSql = "/tmp/scms-patch.sql"

try {
    if (-not (Test-Path -LiteralPath $SourceSql -PathType Leaf)) {
        throw "Source SQL file was not found."
    }
    if (-not (Test-Path -LiteralPath $patchSql -PathType Leaf)) {
        throw "Database patch file was not found."
    }

    $containerRunning = & docker inspect --format "{{.State.Running}}" $ContainerName 2>$null
    if ($LASTEXITCODE -ne 0 -or $containerRunning.Trim() -ne "true") {
        throw "SQL Server container is not running."
    }

    $sourcePath = (Resolve-Path -LiteralPath $SourceSql).Path
    $source = [IO.File]::ReadAllText($sourcePath, [Text.Encoding]::Unicode)
    $createDatabaseIndex = $source.IndexOf(
        "CREATE DATABASE [SportsCenterManagement]",
        [StringComparison]::OrdinalIgnoreCase
    )
    if ($createDatabaseIndex -lt 0) {
        throw "CREATE DATABASE block was not found."
    }

    $databaseTail = $source.Substring($createDatabaseIndex)
    $useMatch = [regex]::Match(
        $databaseTail,
        "(?im)^\s*USE\s+\[SportsCenterManagement\]\s*$"
    )
    if (-not $useMatch.Success) {
        throw "Portable database section was not found."
    }

    $databaseGuard = @"
IF DB_ID(N'SportsCenterManagement') IS NULL
BEGIN
    CREATE DATABASE [SportsCenterManagement];
END;
GO
"@
    $portableSql =
        $databaseGuard + [Environment]::NewLine + $databaseTail.Substring($useMatch.Index)

    [IO.File]::WriteAllText(
        $temporarySql,
        $portableSql,
        [Text.UTF8Encoding]::new($false)
    )

    Invoke-DockerCommand @("cp", $temporarySql, "${ContainerName}:$containerSeedSql")
    Invoke-DockerCommand @("cp", $patchSql, "${ContainerName}:$containerPatchSql")

    if ([string]::IsNullOrWhiteSpace($SaPassword)) {
        throw "SQL Server password is required."
    }

    $sqlcmd = "/opt/mssql-tools18/bin/sqlcmd"
    Invoke-DockerCommand @(
        "exec", "-e", "SQLCMDPASSWORD=$SaPassword", $ContainerName,
        $sqlcmd, "-S", "localhost", "-U", "sa", "-C", "-b",
        "-i", $containerSeedSql
    )
    Invoke-DockerCommand @(
        "exec", "-e", "SQLCMDPASSWORD=$SaPassword", $ContainerName,
        $sqlcmd, "-S", "localhost", "-U", "sa", "-C", "-b",
        "-d", "SportsCenterManagement", "-i", $containerPatchSql
    )

    Write-Host "Database import: PASS"
}
finally {
    $SaPassword = ""
    Remove-Item -LiteralPath $temporarySql -Force -ErrorAction SilentlyContinue
    & docker exec $ContainerName rm -f $containerSeedSql $containerPatchSql *> $null
}
