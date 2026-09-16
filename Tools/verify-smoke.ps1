# Repeatable play-mode smoke test for every scene.
#
# Opens each scene, enters Play Mode, lets it initialize, then fails if the Unity console
# recorded any errors (missing scripts, duplicate serialized fields, null refs in Awake/Start).
# This is the check that caught the duplicate `config` field bug.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File Tools/verify-smoke.ps1
#   powershell -ExecutionPolicy Bypass -File Tools/verify-smoke.ps1 -SettleSeconds 8
param(
    [string]$Unity = "C:\Users\Kevin\AppData\Local\Unity\bin\unity.exe",
    [int]$SettleSeconds = 5
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Unity)) { throw "Unity CLI not found: $Unity" }

$scenes = @(
    "Assets/Scenes/Overworld.unity",
    "Assets/Scenes/WorldB.unity"
)

$failures = @()

foreach ($scene in $scenes)
{
    Write-Host "=== Smoke test: $scene ==="

    & $Unity command open_scene --path $scene --non-interactive | Out-Null
    Start-Sleep -Seconds 3
    & $Unity command clear_console --non-interactive | Out-Null
    & $Unity command editor_play --non-interactive | Out-Null
    Start-Sleep -Seconds $SettleSeconds

    $console = (& $Unity command console --tail 300 --level error --non-interactive 2>&1 | Out-String)

    & $Unity command editor_stop --non-interactive | Out-Null

    if ($console -match '"counts":\{"error":(\d+)')
    {
        $errorCount = [int]$Matches[1]
        if ($errorCount -gt 0)
        {
            $failures += "$scene -> $errorCount error(s)"
            Write-Host $console
        }
        else
        {
            Write-Host "  OK (0 console errors)"
        }
    }
    else
    {
        $failures += "$scene -> could not read console error count"
    }
}

Write-Host ""
if ($failures.Count -gt 0)
{
    Write-Host "SMOKE TEST FAILED:"
    $failures | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host "SMOKE TEST PASSED (no console errors in any scene)."
exit 0
