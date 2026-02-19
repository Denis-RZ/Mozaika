Param(
  [int[]]$Ports = @(8000, 5173)
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

