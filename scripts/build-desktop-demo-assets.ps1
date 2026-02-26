param(
    [string]$BackendUrl = "http://127.0.0.1:18765",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$desktopRoot = Join-Path $repoRoot "desktop-demo"
$runtimeRoot = Join-Path $desktopRoot "runtime"
$frontendRuntime = Join-Path $runtimeRoot "frontend"
$backendRuntime = Join-Path $runtimeRoot "backend"

Write-Output "Preparing desktop demo assets..."
Write-Output "Repo: $repoRoot"
Write-Output "BackendUrl: $BackendUrl"
Write-Output "RuntimeIdentifier: $RuntimeIdentifier"

New-Item -ItemType Directory -Path $frontendRuntime -Force | Out-Null
New-Item -ItemType Directory -Path $backendRuntime -Force | Out-Null

Get-ChildItem -Path $frontendRuntime -Force | Remove-Item -Recurse -Force
Get-ChildItem -Path $backendRuntime -Force | Remove-Item -Recurse -Force

Push-Location (Join-Path $repoRoot "frontend")
try {
    $env:VITE_API_BASE_URL = $BackendUrl
    Write-Output "Building frontend..."
    npm run build | Out-Host
}
finally {
    Remove-Item Env:VITE_API_BASE_URL -ErrorAction SilentlyContinue
    Pop-Location
}

$frontendDist = Join-Path $repoRoot "frontend\dist"
if (-not (Test-Path $frontendDist)) {
    throw "Frontend dist not found after build: $frontendDist"
}

Copy-Item -Path (Join-Path $frontendDist "*") -Destination $frontendRuntime -Recurse -Force

Push-Location (Join-Path $repoRoot "backend-dotnet")
try {
    Write-Output "Publishing backend (self-contained)..."
    dotnet publish Mozaika.Api.csproj `
        -c Release `
        -r $RuntimeIdentifier `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:EnableCompressionInSingleFile=true `
        -o $backendRuntime | Out-Host
}
finally {
    Pop-Location
}

$backendDataPath = Join-Path $backendRuntime "data"
New-Item -ItemType Directory -Path $backendDataPath -Force | Out-Null

$backendSettingsPath = Join-Path $backendRuntime "appsettings.json"
if (Test-Path $backendSettingsPath) {
    $settings = Get-Content $backendSettingsPath -Raw | ConvertFrom-Json
    if ($null -ne $settings.Mozaika -and $null -ne $settings.Mozaika.Database) {
        $settings.Mozaika.Database.Provider = "json"
        $settings.Mozaika.Database.ConnectionString = "Data Source=./data/mozaika.storage.json"
        $settings.Mozaika.Database.Echo = $false
    }
    if ($null -ne $settings.Mozaika -and $null -ne $settings.Mozaika.CorsOrigins) {
        $settings.Mozaika.CorsOrigins = @("http://127.0.0.1:18766")
    }
    $settings | ConvertTo-Json -Depth 100 | Set-Content -Path $backendSettingsPath -Encoding UTF8
}

Write-Output "DESKTOP_DEMO_ASSETS_OK=1"
Write-Output "FrontendRuntime=$frontendRuntime"
Write-Output "BackendRuntime=$backendRuntime"
