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
  if (Test-Path (Join-Path $PSScriptRoot "backend")) {
    $ProjectRoot = $PSScriptRoot
  } else {
    $ProjectRoot = "c:\Users\mikedell\Mozaika"
  }
}

$backendDir = Join-Path $ProjectRoot "backend"
$frontendDir = Join-Path $ProjectRoot "frontend"
$venvPython = Join-Path $backendDir ".venv\Scripts\python.exe"

if (-not (Test-Path $backendDir)) {
  throw "Backend directory not found: $backendDir"
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

Write-Host "Starting Mozaika locally..."
Write-Host "Project root: $ProjectRoot"

Stop-PortProcess -Port $BackendPort
Stop-PortProcess -Port $FrontendPort

if (-not (Test-Path $venvPython)) {
  Write-Host "Creating backend virtualenv..."
  python -m venv (Join-Path $backendDir ".venv")
}

Write-Host "Installing backend dependencies..."
& $venvPython -m pip install -e "$backendDir[dev]" | Out-Null

$envFilePath = Join-Path $backendDir ".env"
if (-not (Test-Path $envFilePath)) {
  @"
MOZAIKA_APP_NAME=Mozaika API
MOZAIKA_API_PREFIX=/api
MOZAIKA_DATABASE_URL=sqlite:///./mozaika.local.db
MOZAIKA_DATABASE_ECHO=false
MOZAIKA_MAX_UPLOAD_MB=25
"@ | Set-Content -Path $envFilePath
}

Write-Host "Starting backend on http://$BackendHost`:$BackendPort ..."
$backendProc = Start-Process `
  -FilePath $venvPython `
  -ArgumentList "-m","uvicorn","app.main:app","--host",$BackendHost,"--port",$BackendPort `
  -WindowStyle Hidden `
  -WorkingDirectory $backendDir `
  -RedirectStandardOutput (Join-Path $backendDir "backend.out.log") `
  -RedirectStandardError (Join-Path $backendDir "backend.err.log") `
  -PassThru

if (-not (Wait-Port -Port $BackendPort -TimeoutSeconds 30)) {
  throw "Backend did not start on port $BackendPort. Check backend/backend.err.log"
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
