Param(
  [string]$ProjectRoot = "",
  [string]$BackendHost = "127.0.0.1",
  [int]$BackendPort = 8000,
  [string]$FrontendHost = "127.0.0.1",
  [int]$FrontendPort = 5173,
  [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
  if (Test-Path (Join-Path $PSScriptRoot "backend-dotnet")) {
    $ProjectRoot = $PSScriptRoot
  } else {
    $ProjectRoot = "c:\Users\mikedell\Mozaika"
  }
}

$backendDir = Join-Path $ProjectRoot "backend-dotnet"
$frontendDir = Join-Path $ProjectRoot "frontend"
$dbFile = Join-Path $ProjectRoot "backend\mozaika.local.db"

if (-not (Test-Path $backendDir)) {
  throw "Backend (.NET) directory not found: $backendDir"
}
if (-not (Test-Path $frontendDir)) {
  throw "Frontend directory not found: $frontendDir"
}

function Stop-PortProcess {
  Param([int]$Port)
  $connections = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
  foreach ($connection in $connections) {
    $processId = $connection.OwningProcess
    if ($processId -gt 0) {
      try {
        Stop-Process -Id $processId -Force -ErrorAction Stop
        Write-Host "Stopped process $processId on port $Port"
      } catch {
        Write-Host "Failed to stop process $processId on port $Port"
      }
    }
  }
}

function Wait-Port {
  Param(
    [int]$Port,
    [int]$TimeoutSeconds = 25
  )
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    $listening = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    if ($listening) {
      return $true
    }
    Start-Sleep -Milliseconds 400
  }
  return $false
}

Write-Host "Starting Mozaika locally (.NET backend)..."
Write-Host "Project root: $ProjectRoot"

Stop-PortProcess -Port $BackendPort
Stop-PortProcess -Port $FrontendPort

Write-Host "Restoring backend dependencies..."
Push-Location $backendDir
& dotnet restore | Out-Null
Pop-Location

$env:MOZAIKA__DATABASE__PROVIDER = "sqlite"
$env:MOZAIKA__DATABASE__CONNECTIONSTRING = "Data Source=$dbFile"
$env:MOZAIKA__MAXUPLOADMB = "25"
$env:MOZAIKA__CORSORIGINS__0 = "*"

Write-Host "Starting backend on http://$BackendHost`:$BackendPort ..."
$backendProc = Start-Process `
  -FilePath "dotnet" `
  -ArgumentList "run","--urls","http://$BackendHost`:$BackendPort" `
  -WindowStyle Hidden `
  -WorkingDirectory $backendDir `
  -RedirectStandardOutput (Join-Path $backendDir "backend.out.log") `
  -RedirectStandardError (Join-Path $backendDir "backend.err.log") `
  -PassThru

if (-not (Wait-Port -Port $BackendPort -TimeoutSeconds 35)) {
  throw "Backend did not start on port $BackendPort. Check backend-dotnet/backend.err.log"
}

Write-Host "Installing frontend dependencies..."
Push-Location $frontendDir
npm install | Out-Null
Pop-Location

Write-Host "Starting frontend on http://$FrontendHost`:$FrontendPort ..."
$frontendProc = Start-Process `
  -FilePath "npm.cmd" `
  -ArgumentList "run","dev","--","--host",$FrontendHost,"--port",$FrontendPort `
  -WindowStyle Hidden `
  -WorkingDirectory $frontendDir `
  -RedirectStandardOutput (Join-Path $frontendDir "frontend.out.log") `
  -RedirectStandardError (Join-Path $frontendDir "frontend.err.log") `
  -PassThru

if (-not (Wait-Port -Port $FrontendPort -TimeoutSeconds 40)) {
  throw "Frontend did not start on port $FrontendPort. Check frontend/frontend.err.log"
}

$url = "http://$FrontendHost`:$FrontendPort"
Write-Host "Backend PID: $($backendProc.Id)"
Write-Host "Frontend PID: $($frontendProc.Id)"
Write-Host "Open: $url"

if (-not $NoBrowser) {
  Start-Process $url | Out-Null
}
