param(
    [string]$Version = "",
    [string]$ApiBaseUrl = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-Date -Format "yyyyMMdd-HHmm"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $repoRoot "release\win-iis-$Version"
$backendOut = Join-Path $releaseRoot "backend"
$frontendOut = Join-Path $releaseRoot "frontend"
$zipPath = Join-Path $repoRoot "release\win-iis-$Version.zip"

Write-Output "Publishing release: $Version"
Write-Output "Repo root: $repoRoot"

if (Test-Path $releaseRoot) {
    Remove-Item -Recurse -Force $releaseRoot
}
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

New-Item -ItemType Directory -Path $backendOut -Force | Out-Null
New-Item -ItemType Directory -Path $frontendOut -Force | Out-Null

Push-Location (Join-Path $repoRoot "frontend")
try {
    if (-not [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        Write-Output "Using VITE_API_BASE_URL=$ApiBaseUrl"
        $env:VITE_API_BASE_URL = $ApiBaseUrl
    }

    Write-Output "Building frontend..."
    npm run build | Out-Host
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        Remove-Item Env:VITE_API_BASE_URL -ErrorAction SilentlyContinue
    }
    Pop-Location
}

$frontendDist = Join-Path $repoRoot "frontend\dist"
if (-not (Test-Path $frontendDist)) {
    throw "Frontend build folder not found: $frontendDist"
}

Write-Output "Copying frontend dist..."
Copy-Item -Path (Join-Path $frontendDist "*") -Destination $frontendOut -Recurse -Force

$frontendWebConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="SPA Fallback" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
    <staticContent>
      <remove fileExtension=".webp" />
      <mimeMap fileExtension=".webp" mimeType="image/webp" />
    </staticContent>
  </system.webServer>
</configuration>
"@
Set-Content -Path (Join-Path $frontendOut "web.config") -Value $frontendWebConfig -Encoding UTF8

Push-Location (Join-Path $repoRoot "backend-dotnet")
try {
    Write-Output "Publishing backend..."
    dotnet publish Mozaika.Api.csproj -c Release -o $backendOut | Out-Host
}
finally {
    Pop-Location
}

$backendDataDir = Join-Path $backendOut "data"
New-Item -ItemType Directory -Path $backendDataDir -Force | Out-Null

$backendConfigPath = Join-Path $backendOut "appsettings.json"
if (Test-Path $backendConfigPath) {
    $backendConfig = Get-Content $backendConfigPath -Raw | ConvertFrom-Json
    if ($null -ne $backendConfig.Mozaika -and $null -ne $backendConfig.Mozaika.Database) {
        $backendConfig.Mozaika.Database.Provider = "json"
        $backendConfig.Mozaika.Database.ConnectionString = "Data Source=./data/mozaika.storage.json"
        $backendConfig.Mozaika.Database.Echo = $false
    }
    $backendConfig | ConvertTo-Json -Depth 100 | Set-Content -Path $backendConfigPath -Encoding UTF8
}

$deployReadme = @"
Mozaika publish for Windows/IIS

Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Version: $Version

Package structure:
- backend/  -> ASP.NET Core API (published)
- frontend/ -> built static SPA (ready for IIS static site)

Recommended deploy variant:
1) Create IIS site/app for backend from backend/ folder.
2) Create IIS site for frontend from frontend/ folder.
3) Point frontend to backend:
   - Option A: build with -ApiBaseUrl (already baked into frontend JS)
   - Option B: keep same domain and reverse proxy /api to backend.
4) Ensure backend app pool identity has write access to backend/data.

Default backend storage in this package:
- Provider: json
- Connection string: Data Source=./data/mozaika.storage.json

If startup fails:
- Install ASP.NET Core Hosting Bundle for matching runtime.
- Check Application Event Log / stdout logs in backend folder.
"@
Set-Content -Path (Join-Path $releaseRoot "README-DEPLOY.txt") -Value $deployReadme -Encoding UTF8

Write-Output "Creating zip archive..."
Compress-Archive -Path (Join-Path $releaseRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output "PUBLISH_DONE=1"
Write-Output "ReleaseFolder=$releaseRoot"
Write-Output "ZipFile=$zipPath"
