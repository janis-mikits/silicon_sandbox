param(
    [Parameter(Mandatory = $true)][string]$Editor,
    [Parameter(Mandatory = $true)][ValidateSet('StandaloneWindows64')][string]$Target
)
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'UnityProject'
$versionFile = Join-Path $project 'ProjectSettings/ProjectVersion.txt'
$version = ((Get-Content $versionFile | Select-String '^m_EditorVersion: ').ToString() -split ': ', 2)[1]
if ($version -ne '6000.3.24f1') { throw "Unexpected project Editor version: $version" }
if (-not (Test-Path $Editor -PathType Leaf)) { throw "Editor does not exist: $Editor" }
if ($Editor -notmatch [regex]::Escape($version)) { throw "Editor path does not match $version" }

$logs = Join-Path $project 'Logs/Verification'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$env:UPM_CACHE_ROOT = Join-Path $project '.upm-cache'

function Invoke-Unity([string[]]$UnityArguments) {
    & $Editor -batchmode -projectPath $project -buildTarget $Target @UnityArguments
    if ($LASTEXITCODE -ne 0) { throw "Unity exited with code $LASTEXITCODE" }
}

Invoke-Unity @('-quit', '-executeMethod', 'SiliconSandbox.EditorBuild.SliceZeroBuild.EnsureScene', '-logFile', (Join-Path $logs 'scene.log'))
if (-not (Test-Path (Join-Path $project 'Assets/SiliconSandbox/Scenes/FlatWorld.unity') -PathType Leaf)) { throw 'Smoke scene was not generated.' }

foreach ($mode in @('EditMode', 'PlayMode')) {
    $lower = $mode.ToLowerInvariant()
    $xmlPath = Join-Path $logs "$lower.xml"
    Invoke-Unity @('-runTests', '-testPlatform', $mode, '-testResults', $xmlPath, '-logFile', (Join-Path $logs "$lower.log"))
    if (-not (Test-Path $xmlPath -PathType Leaf)) { throw "Missing results: $xmlPath" }
    [xml]$results = Get-Content $xmlPath -Raw
    $summary = $results.'test-run'
    $total = [int]$summary.total
    $failed = [int]$summary.failed
    $inconclusive = [int]$summary.inconclusive
    $skipped = [int]$summary.skipped
    Write-Output "$mode`: total=$total failed=$failed inconclusive=$inconclusive skipped=$skipped"
    if ($total -lt 1 -or $failed -ne 0 -or $inconclusive -ne 0 -or $skipped -ne 0) { throw "$mode test gate failed" }
}

Invoke-Unity @('-quit', '-executeMethod', 'SiliconSandbox.EditorBuild.SliceZeroBuild.BuildPlayer', '-logFile', (Join-Path $logs 'build.log'))
$player = Join-Path $project 'Builds/Windows/SiliconSandbox.exe'
if (-not (Test-Path $player -PathType Leaf)) { throw 'Windows player build is missing.' }
$env:SILICON_SANDBOX_SMOKE_OUTPUT = Join-Path $logs 'player-smoke.txt'
try {
    & $player -batchmode -nographics -logFile (Join-Path $logs 'player.log')
    if ($LASTEXITCODE -ne 0) { throw "Player exited with code $LASTEXITCODE" }
    if ((Get-Content $env:SILICON_SANDBOX_SMOKE_OUTPUT -Raw).Trim() -ne 'PASS') { throw 'Built-player smoke check failed.' }
} finally {
    Remove-Item Env:SILICON_SANDBOX_SMOKE_OUTPUT -ErrorAction SilentlyContinue
}
Write-Output 'Unity Edit Mode, Play Mode, Windows build and player checks passed.'
