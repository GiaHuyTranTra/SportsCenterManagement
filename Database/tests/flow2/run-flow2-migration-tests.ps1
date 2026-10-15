# Runs the Flow 2 upgrade against disposable LocalDB databases only. Never point this at a shared server.
param([string]$Instance = 'SCMS_Flow2_TestInst')
$ErrorActionPreference = 'Stop'
$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE'
$server = "(localdb)\$Instance"
$root = Split-Path $PSScriptRoot -Parent
$db = Split-Path $root -Parent
$schema = Join-Path $db '2026-10-14-sprint2-flow2-schema.sql'
$verify = Join-Path $db '2026-10-14-sprint2-flow2-verify.sql'
$preflight = Join-Path $db '2026-10-14-sprint2-flow2-preflight.sql'

function Invoke-Sql([string]$Database, [string]$File, [string]$Query) {
    $common = @('-S', $server, '-b', '-I', '-f', '65001')
    if ($File) { & $sqlcmd @common -d $Database -i $File } else { & $sqlcmd @common -d $Database -Q $Query }
    if ($LASTEXITCODE -ne 0) { return $false } else { return $true }
}
function New-TestDb([string]$Name) {
    if ($Name -notlike 'SCMS_Flow2_Test_*') { throw 'Refusing non-test database name.' }
    Invoke-Sql 'master' $null "IF DB_ID(N'$Name') IS NOT NULL BEGIN ALTER DATABASE [$Name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Name]; END; CREATE DATABASE [$Name];" | Out-Null
}
$fx = $PSScriptRoot
$cases = @{
    'SCMS_Flow2_Test_New'     = @('00-baseline-sprint1.sql')
    'SCMS_Flow2_Test_Partial' = @('00-baseline-sprint1.sql', '01-partial-room.sql', 'legacy-v1-flow2-schema.sql', '02-partial-seed.sql')
}
foreach ($name in $cases.Keys) {
    Write-Output "===== $name ====="
    New-TestDb $name
    foreach ($f in $cases[$name]) { if (-not (Invoke-Sql $name (Join-Path $fx $f) $null)) { throw "fixture $f failed" } }
    Write-Output '--- preflight'; Invoke-Sql $name $preflight $null
    foreach ($run in 1, 2) {
        Write-Output "--- schema run $run"; $ok = Invoke-Sql $name $schema $null; Write-Output "schema run $run success=$ok"
        Write-Output "--- verify after run $run"; Invoke-Sql $name $verify $null
    }
}

