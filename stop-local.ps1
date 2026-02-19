Param(
  [int[]]$Ports = @(8000, 5173),
  [string]$BackendDir = "c:\Users\mikedell\Mozaika\backend-dotnet"
)

foreach ($port in $Ports) {
  $connections = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
  foreach ($connection in $connections) {
    $processId = $connection.OwningProcess
    if ($processId -gt 0) {
      try {
        Stop-Process -Id $processId -Force -ErrorAction Stop
        Write-Host "Stopped process $processId on port $port"
      } catch {
        Write-Host "Failed to stop process $processId on port $port"
      }
    }
  }
}

$backendPath = $BackendDir.ToLowerInvariant()
$targetIds = New-Object System.Collections.Generic.HashSet[int]

$appHost = Get-Process -Name "Mozaika.Api" -ErrorAction SilentlyContinue
foreach ($proc in $appHost) {
  [void]$targetIds.Add($proc.Id)
}

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
