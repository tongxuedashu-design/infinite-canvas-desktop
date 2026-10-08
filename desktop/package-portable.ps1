param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$desktopDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $desktopDir
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $desktopDir 'portable'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$webDirectory = Join-Path $projectRoot 'web'
$publishedExecutable = Join-Path $desktopDir 'publish\InfiniteCanvasDesktop.exe'
$nodeModules = Join-Path $webDirectory 'node_modules'

if (-not (Test-Path (Join-Path $webDirectory 'package.json'))) {
    throw "Web directory not found: $webDirectory"
}
if (-not (Test-Path $nodeModules)) {
    throw 'web\node_modules not found. Run npm install in the web directory first.'
}

& (Join-Path $desktopDir 'publish.ps1')
if (-not (Test-Path $publishedExecutable)) {
    throw "Published executable was not created: $publishedExecutable"
}

if (Test-Path $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Set-Content -LiteralPath (Join-Path $OutputDirectory '.infinite-canvas-portable') -Value 'portable' -Encoding ascii
$releaseFiles = @(
    'VERSION',
    'CHANGELOG.md',
    'LICENSE',
    'FIRST_RUN.md',
    'ATTRIBUTIONS.md'
)
foreach ($releaseFile in $releaseFiles) {
    $sourcePath = Join-Path $projectRoot $releaseFile
    if (-not (Test-Path $sourcePath)) {
        throw "Required release file not found: $sourcePath"
    }
    Copy-Item -LiteralPath $sourcePath -Destination $OutputDirectory
}

Copy-Item -LiteralPath $publishedExecutable -Destination (Join-Path $OutputDirectory 'InfiniteCanvasDesktop.exe')

$portableWeb = Join-Path $OutputDirectory 'web'
$robocopyArgs = @(
    $webDirectory,
    $portableWeb,
    '/E',
    '/XD', (Join-Path $webDirectory 'node_modules\.cache'),
    '/NFL', '/NDL', '/NJH', '/NJS', '/NC', '/NS'
)
& robocopy @robocopyArgs | Out-Null
if ($LASTEXITCODE -gt 7) {
    throw "Failed to copy the Web directory. robocopy exit code: $LASTEXITCODE"
}

$nodeCommand = Get-Command node.exe -ErrorAction SilentlyContinue
if ($null -eq $nodeCommand) {
    throw 'node.exe was not found. Install Node.js or add node.exe to PATH.'
}
$portableRuntime = Join-Path $OutputDirectory 'runtime'
New-Item -ItemType Directory -Path $portableRuntime | Out-Null
Copy-Item -LiteralPath $nodeCommand.Source -Destination (Join-Path $portableRuntime 'node.exe')

$forbiddenReleaseEntries = @(
    'AGENTS.md',
    'CODEX_BACKEND_HANDOFF.md',
    'TEST_CHECKLIST.md',
    'docs',
    'desktop'
)
foreach ($forbiddenEntry in $forbiddenReleaseEntries) {
    if (Test-Path (Join-Path $OutputDirectory $forbiddenEntry)) {
        throw "Internal development material must not be included in the portable package: $forbiddenEntry"
    }
}

Write-Host "Portable package created: $OutputDirectory"
Write-Host 'Move the complete portable directory, including web, runtime, and InfiniteCanvasDesktop.exe.'
