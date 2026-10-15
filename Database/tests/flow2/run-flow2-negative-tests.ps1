# Negative tests for the Flow 2 upgrade, on disposable LocalDB databases only.
$ErrorActionPreference = 'Stop'
$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE'
$server = '(localdb)\SCMS_Flow2_TestInst'
$fx = $PSScriptRoot
$schema = Join-Path (Split-Path (Split-Path $fx -Parent) -Parent) '2026-10-14-sprint2-flow2-schema.sql'
$common = @('-S', $server, '-b', '-I', '-f', '65001', '-W', '-h', '-1')

function Run([string]$Db, [string]$File, [string]$Query) {
    if ($File) { return (& $sqlcmd @common -d $Db -i $File 2>&1) } else { return (& $sqlcmd @common -d $Db -Q $Query 2>&1) }
}
function Reset-Db([string]$Name) {
    if ($Name -notlike 'SCMS_Flow2_Test_*') { throw 'Refusing non-test database.' }
    Run 'master' $null "IF DB_ID(N'$Name') IS NOT NULL BEGIN ALTER DATABASE [$Name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Name]; END; CREATE DATABASE [$Name];" | Out-Null
}
function Snapshot([string]$Db) {
    $q = "SET NOCOUNT ON; SELECT 'fn=' + CAST(CASE WHEN OBJECT_ID('dbo.NormalizeFlow2Name') IS NULL THEN 0 ELSE 1 END AS varchar) + ' room=' + CAST(CASE WHEN OBJECT_ID('dbo.Room','U') IS NULL THEN 0 ELSE 1 END AS varchar) + ' class=' + CAST(CASE WHEN OBJECT_ID('dbo.Class','U') IS NULL THEN 0 ELSE 1 END AS varchar) + ' event=' + CAST(CASE WHEN OBJECT_ID('dbo.ClassChangeEvent','U') IS NULL THEN 0 ELSE 1 END AS varchar) + ' discNorm=' + CAST(CASE WHEN COL_LENGTH('dbo.Discipline','NormalizedName') IS NULL THEN 0 ELSE 1 END AS varchar) + ' auditClassId=' + CAST(CASE WHEN COL_LENGTH('dbo.AuditLog','ClassId') IS NULL THEN 0 ELSE 1 END AS varchar) + ' disciplines=' + CAST((SELECT COUNT(*) FROM dbo.Discipline) AS varchar) + ' accounts=' + CAST((SELECT COUNT(*) FROM dbo.Account) AS varchar);"
    return ((Run $Db $null $q) -join ' ')
}
function Attempt([string]$Name, [string[]]$Fixtures, [string]$Extra) {
    Write-Output "===== $Name ====="
    Reset-Db $Name
    foreach ($f in $Fixtures) { Run $Name (Join-Path $fx $f) $null | Out-Null }
    if ($Extra) { Run $Name $null $Extra | Out-Null }
    Write-Output ("before: " + (Snapshot $Name))
    $out = (Run $Name $schema $null) -join ' '
    Write-Output ("schema exit=" + $LASTEXITCODE + " msg=" + ($out.Substring(0, [Math]::Min(300, $out.Length))))
    Write-Output ("after : " + (Snapshot $Name))
}
$partial = @('00-baseline-sprint1.sql', '01-partial-room.sql', 'legacy-v1-flow2-schema.sql', '02-partial-seed.sql')
Attempt 'SCMS_Flow2_Test_DirtyName' $partial "INSERT dbo.Discipline(Name) VALUES (N'Spin'),(N'Sp' + NCHAR(236) + N'n');"
Attempt 'SCMS_Flow2_Test_DirtyCount' $partial "ALTER TABLE dbo.ClassSession NOCHECK CONSTRAINT CK_ClassSession_BookedCount; UPDATE dbo.ClassSession SET BookedCount = 11;"
Attempt 'SCMS_Flow2_Test_Rollback' @('00-baseline-sprint1.sql') "CREATE VIEW dbo.ClassChangeEvent AS SELECT 1 AS x;"

