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
$dbFile = Join-Path $ProjectRoot "data\mozaika.local.db"
$dbDir = Split-Path -Parent $dbFile

if (-not (Test-Path $backendDir)) {
  throw "Backend (.NET) directory not found: $backendDir"
}
if (-not (Test-Path $frontendDir)) {
  throw "Frontend directory not found: $frontendDir"
}
if (-not (Test-Path $dbDir)) {
  New-Item -ItemType Directory -Path $dbDir -Force | Out-Null
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

function Stop-DotnetBackendProcess {
  Param([string]$BackendDir)

  $backendPath = $BackendDir.ToLowerInvariant()
  $targetIds = New-Object System.Collections.Generic.HashSet[int]

  # AppHost process name when running built executable.
  $appHost = Get-Process -Name "Mozaika.Api" -ErrorAction SilentlyContinue
  foreach ($proc in $appHost) {
    [void]$targetIds.Add($proc.Id)
  }

  # dotnet-hosted process (dotnet run / dotnet <dll>) with command line pointing to backend-dotnet.
  $dotnetProcs = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue
  foreach ($proc in $dotnetProcs) {
    $cmd = ($proc.CommandLine | Out-String).ToLowerInvariant()
    if ($cmd -and $cmd.Contains($backendPath)) {
      [void]$targetIds.Add([int]$proc.ProcessId)
    }
  }

  foreach ($procId in $targetIds) {
    try {
      Stop-Process -Id $procId -Force -ErrorAction Stop
      Write-Host "Stopped backend process $procId (.NET)"
    } catch {
      Write-Host "Failed to stop backend process $procId (.NET)"
    }
  }
}

function Get-LogTail {
  Param(
    [string]$Path,
    [int]$TailLines = 80
  )

  if (-not (Test-Path $Path)) {
    return "(log file not found: $Path)"
  }

  try {
    return ((Get-Content -Path $Path -Tail $TailLines) -join [Environment]::NewLine)
  } catch {
    return "(failed to read log '$Path': $($_.Exception.Message))"
  }
}

function Wait-Port {
  Param(
    [int]$Port,
    [int]$TimeoutSeconds = 25,
    [System.Diagnostics.Process]$Process = $null
  )
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    if ($Process -and $Process.HasExited) {
      return $false
    }

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
Stop-DotnetBackendProcess -BackendDir $backendDir

Write-Host "Restoring backend dependencies..."
Push-Location $backendDir
& dotnet restore | Out-Null
Pop-Location

$env:MOZAIKA__DATABASE__PROVIDER = "sqlite"
$env:MOZAIKA__DATABASE__CONNECTIONSTRING = "Data Source=$dbFile"
$env:MOZAIKA__MAXUPLOADMB = "25"
$env:MOZAIKA__CORSORIGINS__0 = "*"

Write-Host "Starting backend on http://$BackendHost`:$BackendPort ..."
$backendOutLog = Join-Path $backendDir "backend.out.log"
$backendErrLog = Join-Path $backendDir "backend.err.log"
Remove-Item $backendOutLog -ErrorAction SilentlyContinue
Remove-Item $backendErrLog -ErrorAction SilentlyContinue

$backendProc = Start-Process `
  -FilePath "dotnet" `
  -ArgumentList "run","--urls","http://$BackendHost`:$BackendPort" `
  -WindowStyle Hidden `
  -WorkingDirectory $backendDir `
  -RedirectStandardOutput $backendOutLog `
  -RedirectStandardError $backendErrLog `
  -PassThru

if (-not (Wait-Port -Port $BackendPort -TimeoutSeconds 35 -Process $backendProc)) {
  $backendReason = if ($backendProc.HasExited) {
    "Backend process exited with code $($backendProc.ExitCode)."
  } else {
    "Timed out waiting for port $BackendPort."
  }

  $backendErrTail = Get-LogTail -Path $backendErrLog -TailLines 120
  $backendOutTail = Get-LogTail -Path $backendOutLog -TailLines 80
  throw "Backend did not start on port $BackendPort. $backendReason`n--- backend.err.log ---`n$backendErrTail`n--- backend.out.log ---`n$backendOutTail"
}

Write-Host "Installing frontend dependencies..."
Push-Location $frontendDir
npm install | Out-Null
Pop-Location

Write-Host "Starting frontend on http://$FrontendHost`:$FrontendPort ..."
$frontendOutLog = Join-Path $frontendDir "frontend.out.log"
$frontendErrLog = Join-Path $frontendDir "frontend.err.log"
Remove-Item $frontendOutLog -ErrorAction SilentlyContinue
Remove-Item $frontendErrLog -ErrorAction SilentlyContinue

$frontendProc = Start-Process `
  -FilePath "npm.cmd" `
  -ArgumentList "run","dev","--","--host",$FrontendHost,"--port",$FrontendPort `
  -WindowStyle Hidden `
  -WorkingDirectory $frontendDir `
  -RedirectStandardOutput $frontendOutLog `
  -RedirectStandardError $frontendErrLog `
  -PassThru

if (-not (Wait-Port -Port $FrontendPort -TimeoutSeconds 40 -Process $frontendProc)) {
  $frontendReason = if ($frontendProc.HasExited) {
    "Frontend process exited with code $($frontendProc.ExitCode)."
  } else {
    "Timed out waiting for port $FrontendPort."
  }

  $frontendErrTail = Get-LogTail -Path $frontendErrLog -TailLines 120
  $frontendOutTail = Get-LogTail -Path $frontendOutLog -TailLines 80
  throw "Frontend did not start on port $FrontendPort. $frontendReason`n--- frontend.err.log ---`n$frontendErrTail`n--- frontend.out.log ---`n$frontendOutTail"
}

$url = "http://$FrontendHost`:$FrontendPort"
Write-Host "Backend PID: $($backendProc.Id)"
Write-Host "Frontend PID: $($frontendProc.Id)"
Write-Host "Open: $url"

if (-not $NoBrowser) {
  Start-Process $url | Out-Null
}
