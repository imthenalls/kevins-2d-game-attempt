# Runs the full verification suite in one command:
#   1. Unity compile check
#   2. Unity Edit Mode tests  (Tests.EditMode + Tests.Presentation)
#   3. Unity Play Mode tests  (Tests.PlayMode, async)
#   4. .NET (dotnet test) mirror of the engine-free tests
#   5. Play-mode smoke test of every scene (Tools/verify-smoke.ps1)
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
Write-Host "=== 1/5  Unity compile ==="
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
Write-Host "=== 2/5  Unity Edit Mode tests ==="
$editOut = (& $Unity command run_tests --mode EditMode --non-interactive 2>&1 | Out-String)
$editMatch = [regex]::Match($editOut, '"Summary":\{"Total":(\d+),"Passed":(\d+),"Failed":(\d+)')
if ($editMatch.Success)
{
    $editFailed = [int]$editMatch.Groups[3].Value
    Write-Host ("  Total {0}, Passed {1}, Failed {2}" -f $editMatch.Groups[1].Value, $editMatch.Groups[2].Value, $editFailed)
    $results["Unity EditMode tests"] = ($editFailed -eq 0)
}
else
{
    Write-Host "  could not read test summary"
    $results["Unity EditMode tests"] = $false
}

# ── 3. Unity Play Mode tests (async: play mode drops the synchronous HTTP request) ──
Write-Host "=== 3/5  Unity Play Mode tests ==="
& $Unity command run_tests --mode PlayMode --async_tests --non-interactive | Out-Null
$playOut = ""
for ($i = 0; $i -lt 60; $i++)
{
    Start-Sleep -Seconds 5
    $playOut = (& $Unity command test_status --non-interactive 2>&1 | Out-String)
    if ($playOut -match '"status":\s*"completed"') { break }
}
$playMatch = [regex]::Match($playOut, '"summary":\s*\{\s*"total":\s*(\d+),\s*"passed":\s*(\d+),\s*"failed":\s*(\d+)')
if ($playMatch.Success)
{
    $playFailed = [int]$playMatch.Groups[3].Value
    $playTotal = [int]$playMatch.Groups[1].Value
    Write-Host ("  Total {0}, Passed {1}, Failed {2}" -f $playTotal, $playMatch.Groups[2].Value, $playFailed)
    $results["Unity PlayMode tests"] = ($playFailed -eq 0 -and $playTotal -gt 0)
}
else
{
    Write-Host "  could not read test summary"
    $results["Unity PlayMode tests"] = $false
}

# ── 4. dotnet test ───────────────────────────────────────────────────────────
Write-Host "=== 4/5  dotnet test ==="
& dotnet test $TestsCsproj -c Release --nologo | Select-Object -Last 1 | ForEach-Object { Write-Host "  $_" }
$results["dotnet test"] = ($LASTEXITCODE -eq 0)

# ── 5. Play-mode smoke test ──────────────────────────────────────────────────
Write-Host "=== 5/5  Scene smoke test ==="
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
