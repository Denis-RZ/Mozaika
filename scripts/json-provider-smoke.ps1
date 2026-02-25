param(
    [string]$BaseUrl = "http://127.0.0.1:8011",
    [string]$StoragePath = "C:\Users\mikedell\Mozaika\data\mozaika.json-provider.test.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $repoRoot "backend-dotnet"
$stdoutLog = Join-Path $backendDir "json-provider-smoke.out.log"
$stderrLog = Join-Path $backendDir "json-provider-smoke.err.log"

function Wait-ApiReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $null = Invoke-RestMethod `
                -Method Post `
                -Uri "$Url/api/auth/login" `
                -ContentType "application/json" `
                -Body '{"username":"admin","password":"admin123"}'
            return $true
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    return $false
}

function Start-BackendJsonProvider {
    $env:MOZAIKA__DATABASE__PROVIDER = "json"
    $env:MOZAIKA__DATABASE__CONNECTIONSTRING = "Data Source=$StoragePath"
    $env:MOZAIKA__DATABASE__ECHO = "false"
    $env:MOZAIKA__CORSORIGINS__0 = "*"
    $env:MOZAIKA__MAXUPLOADMB = "25"

    return Start-Process `
        -FilePath "dotnet" `
        -ArgumentList "run", "--urls", $BaseUrl `
        -WorkingDirectory $backendDir `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog `
        -PassThru
}

function Stop-Backend {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) {
        return
    }

    if ($Process.HasExited) {
        return
    }

    Stop-Process -Id $Process.Id -Force
    Start-Sleep -Milliseconds 700
}

if (Test-Path $stdoutLog) {
    Remove-Item $stdoutLog -Force
}

if (Test-Path $stderrLog) {
    Remove-Item $stderrLog -Force
}

if (Test-Path $StoragePath) {
    Remove-Item $StoragePath -Force
}

$firstRunProcess = $null
$secondRunProcess = $null

try {
    $firstRunProcess = Start-BackendJsonProvider
    if (-not (Wait-ApiReady -Url $BaseUrl)) {
        throw "Backend was not ready on first run."
    }

    if (-not (Test-Path $StoragePath)) {
        throw "JSON storage file was not created: $StoragePath"
    }

    $login = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/auth/login" `
        -ContentType "application/json" `
        -Body '{"username":"admin","password":"admin123"}'
    $token = $login.token

    $suffix = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $payload = @{
        name      = "Smoke $suffix"
        rgb_hex   = "#123456"
        is_active = $true
    } | ConvertTo-Json -Compress

    $created = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/admin/grout-colors" `
        -Headers @{ Authorization = "Bearer $token" } `
        -ContentType "application/json" `
        -Body $payload

    $createdId = [int]$created.id

    Stop-Backend -Process $firstRunProcess
    $firstRunProcess = $null

    $secondRunProcess = Start-BackendJsonProvider
    if (-not (Wait-ApiReady -Url $BaseUrl)) {
        throw "Backend was not ready on second run."
    }

    $login2 = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/auth/login" `
        -ContentType "application/json" `
        -Body '{"username":"admin","password":"admin123"}'
    $token2 = $login2.token

    $groutColors = Invoke-RestMethod `
        -Method Get `
        -Uri "$BaseUrl/api/admin/grout-colors?include_inactive=true" `
        -Headers @{ Authorization = "Bearer $token2" }

    $found = @($groutColors | Where-Object { [int]$_.id -eq $createdId }).Count -gt 0
    if (-not $found) {
        throw "Imported data not found after restart. Missing grout color id=$createdId"
    }

    Write-Output "JSON_PROVIDER_SMOKE_OK=1"
    Write-Output "StoragePath=$StoragePath"
    Write-Output "CreatedGroutColorId=$createdId"
}
catch {
    Write-Output "JSON_PROVIDER_SMOKE_OK=0"
    Write-Output $_.Exception.Message
    exit 1
}
finally {
    Stop-Backend -Process $firstRunProcess
    Stop-Backend -Process $secondRunProcess
    Remove-Item Env:MOZAIKA__DATABASE__PROVIDER -ErrorAction SilentlyContinue
    Remove-Item Env:MOZAIKA__DATABASE__CONNECTIONSTRING -ErrorAction SilentlyContinue
    Remove-Item Env:MOZAIKA__DATABASE__ECHO -ErrorAction SilentlyContinue
    Remove-Item Env:MOZAIKA__MAXUPLOADMB -ErrorAction SilentlyContinue
    Remove-Item Env:MOZAIKA__CORSORIGINS__0 -ErrorAction SilentlyContinue
}
