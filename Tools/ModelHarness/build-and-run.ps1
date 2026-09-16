# Compiles and runs the pure-C# Game.Core model harness with Unity's bundled Roslyn compiler,
# so it needs neither the Unity Editor session nor a .NET SDK.
#
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/ModelHarness/build-and-run.ps1
$ErrorActionPreference = "Stop"

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$unityData  = "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Data"
$dotnet     = Join-Path $unityData "NetCoreRuntime\dotnet.exe"
$csc        = Join-Path $unityData "DotNetSdkRoslyn\csc.dll"
$refApi     = Join-Path $unityData "UnityReferenceAssemblies\unity-4.8-api"
$outDir     = Join-Path $PSScriptRoot "out"
$exe        = Join-Path $outDir "ModelHarness.exe"

if (-not (Test-Path $dotnet)) { throw "Unity bundled dotnet not found: $dotnet" }
if (-not (Test-Path $csc))    { throw "Roslyn csc not found: $csc" }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = @()
$sources += Get-ChildItem (Join-Path $repoRoot "Assets\Scripts\GameData") -Filter *.cs | ForEach-Object { $_.FullName }
$sources += (Join-Path $PSScriptRoot "Program.cs")

$cscArgs = @(
    "exec", $csc, "/nologo", "/target:exe", "/platform:anycpu", "/langversion:9.0",
    "/out:$exe",
    "/r:$refApi\mscorlib.dll",
    "/r:$refApi\System.dll",
    "/r:$refApi\System.Core.dll"
) + $sources

Write-Host "Compiling Game.Core + harness..."
& $dotnet @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($LASTEXITCODE)." }

Write-Host "Running $exe (no Unity, no scene)"
Write-Host "--------------------------------------------------"
& $exe
exit $LASTEXITCODE
