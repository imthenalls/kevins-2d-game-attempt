# Runs the full verification suite in one command:
#   1. Unity compile check
#   2. Unity Edit Mode tests
#   3. .NET (dotnet test) mirror of the same tests
#   4. Play-mode smoke test of every scene (Tools/verify-smoke.ps1)
#
# Exits non-zero if any step fails.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1
param(
    [string]$Unity = "C:\Users\Kevin\AppData\Local\Unity\bin\unity.exe",
    [int]$SettleSeconds = 5,
    [string]$TestsCsproj = "Tools\ModelHarness.Tests\ModelHarness.Tests.csproj"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $Unity)) { throw "Unity CLI not found: $Unity" }

$results = [ordered]@{}

# ── 1. Unity compile ─────────────────────────────────────────────────────────
Write-Host "=== 1/4  Unity compile ==="
& $Unity command recompile --non-interactive | Out-Null
$statusOut = ""
for ($i = 0; $i -lt 40; $i++)
{
    Start-Sleep -Seconds 3
    $statusOut = (& $Unity command recompile_status --non-interactive 2>&1 | Out-String)
    if ($statusOut -match '"status":"(completed|failed)"') { break }
}
$compileOk = $statusOut -match '"compilationFailed":false'
if ($compileOk) { Write-Host "  OK (0 errors)" } else { Write-Host $statusOut }
$results["Unity compile"] = $compileOk

# ── 2. Unity Edit Mode tests ─────────────────────────────────────────────────
Write-Host "=== 2/4  Unity Edit Mode tests ==="
$testOut = (& $Unity command run_tests --mode EditMode --non-interactive 2>&1 | Out-String)
$match = [regex]::Match($testOut, '"Summary":\{"Total":(\d+),"Passed":(\d+),"Failed":(\d+)')
if ($match.Success)
{
    $failed = [int]$match.Groups[3].Value
    Write-Host ("  Total {0}, Passed {1}, Failed {2}" -f $match.Groups[1].Value, $match.Groups[2].Value, $failed)
    $results["Unity tests"] = ($failed -eq 0)
}
else
{
    Write-Host "  could not read test summary"
    $results["Unity tests"] = $false
}

# ── 3. dotnet test ───────────────────────────────────────────────────────────
Write-Host "=== 3/4  dotnet test ==="
& dotnet test $TestsCsproj -c Release --nologo | Select-Object -Last 1 | ForEach-Object { Write-Host "  $_" }
$results["dotnet test"] = ($LASTEXITCODE -eq 0)

# ── 4. Play-mode smoke test ──────────────────────────────────────────────────
Write-Host "=== 4/4  Scene smoke test ==="
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "verify-smoke.ps1") -Unity $Unity -SettleSeconds $SettleSeconds
$results["Scene smoke test"] = ($LASTEXITCODE -eq 0)

# ── Summary ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Summary ==="
$allOk = $true
foreach ($key in $results.Keys)
{
    if ($results[$key]) { Write-Host ("  PASS  " + $key) }
    else { Write-Host ("  FAIL  " + $key); $allOk = $false }
}

Write-Host ""
if ($allOk) { Write-Host "VERIFICATION PASSED"; exit 0 }
Write-Host "VERIFICATION FAILED"; exit 1
