param([switch]$InstallBrowser)
$ErrorActionPreference = 'Stop'
$auditRoot = Split-Path $PSScriptRoot -Parent
$priorBrowser=$env:PLAYWRIGHT_BROWSERS_PATH
$priorAuditRoot=$env:SAIGONCHE_AUDIT_ROOT
$priorTemp=$env:TEMP
Push-Location $auditRoot
try {
    $env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $auditRoot 'artifacts\playwright'
    $env:SAIGONCHE_AUDIT_ROOT = $auditRoot
    $env:TEMP = Join-Path $auditRoot 'artifacts\browser-temp'
    New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
    & dotnet restore .\SaigonChe.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build .\SaigonChe.slnx --no-restore -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Stop the running project in Visual Studio if its files are locked.' }
    if ($InstallBrowser) {
        & pwsh -NoProfile -File .\SaigonChe.Web.Tests\bin\Debug\net10.0\playwright.ps1 install chromium --only-shell
        if ($LASTEXITCODE -ne 0) { throw 'Browser installation failed.' }
    }
    $auditResultName = 'audit-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6) + '.trx'
    & dotnet test .\SaigonChe.slnx --no-build --no-restore --logger ("trx;LogFileName=" + $auditResultName) --results-directory .\artifacts\audit-results
    if ($LASTEXITCODE -ne 0) { throw 'Audit tests failed. Inspect artifacts\audit-results and artifacts\audit.' }
} finally { $env:PLAYWRIGHT_BROWSERS_PATH=$priorBrowser; $env:SAIGONCHE_AUDIT_ROOT=$priorAuditRoot; $env:TEMP=$priorTemp; Pop-Location }
