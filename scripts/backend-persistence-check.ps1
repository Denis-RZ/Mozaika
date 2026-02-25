param(
    [string]$BaseUrl = "http://127.0.0.1:8000",
    [string]$ImagePath = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

if ([string]::IsNullOrWhiteSpace($ImagePath)) {
    $ImagePath = Join-Path (Split-Path -Parent $PSScriptRoot) "tmp_smoke3.png"
}
$ImagePath = [System.IO.Path]::GetFullPath($ImagePath)
if (-not (Test-Path $ImagePath)) {
    throw "Image file not found: $ImagePath"
}

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Url,
        [string]$Token = "",
        [object]$Body = $null
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }

    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 100 -Compress
        return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers -ContentType "application/json" -Body $json
    }

    return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers
}

function Wait-BackendPort {
    param([int]$TimeoutSeconds = 35)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Get-NetTCPConnection -State Listen -LocalPort 8000 -ErrorAction SilentlyContinue) {
            return $true
        }
        Start-Sleep -Milliseconds 400
    }
    return $false
}

Write-Output "Running backend persistence check..."
try {
    $login = Invoke-Json -Method "POST" -Url "$BaseUrl/api/auth/login" -Body @{
        username = "customer"
        password = "customer123"
    }
    $token = $login.token

    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $imgBytes = [System.IO.File]::ReadAllBytes($ImagePath)
    $fileContent = [System.Net.Http.ByteArrayContent]::new($imgBytes)
    $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("image/png")
    $multipart.Add($fileContent, "image", [System.IO.Path]::GetFileName($ImagePath)) | Out-Null
    $multipart.Add([System.Net.Http.StringContent]::new("700"), "field_width_mm") | Out-Null
    $multipart.Add([System.Net.Http.StringContent]::new("500"), "field_height_mm") | Out-Null
    $multipart.Add([System.Net.Http.StringContent]::new("10"), "cell_size_mm") | Out-Null
    $multipart.Add([System.Net.Http.StringContent]::new("2"), "gap_mm") | Out-Null

    $client = [System.Net.Http.HttpClient]::new()
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$BaseUrl/api/mosaic/generate")
    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $token)
    $request.Content = $multipart
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $rawMosaic = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        throw "Generate failed: $($response.StatusCode) $rawMosaic"
    }
    $mosaic = $rawMosaic | ConvertFrom-Json

    $sourceImageBase64 = [Convert]::ToBase64String($imgBytes)
    $suffix = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $create = Invoke-Json -Method "POST" -Url "$BaseUrl/api/projects" -Token $token -Body @{
        name = "Persist check $suffix"
        description = "backend restart persistence"
        source_image_mime_type = "image/png"
        source_image_base64 = $sourceImageBase64
        initial_generation = @{
            name = "persist-v1"
            note = ""
            snapshot = @{
                mosaic = $mosaic
                include_color_ids = @()
                exclude_color_ids = @()
                grout_color_id = 1
                preview_zoom = 1
                preview_pan_x = 0
                preview_pan_y = 0
            }
        }
    }
    $projectId = [int]$create.id
    Write-Output "Created project id: $projectId"

    $backendPid = Get-NetTCPConnection -State Listen -LocalPort 8000 -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty OwningProcess
    if ($backendPid) {
        Stop-Process -Id $backendPid -Force
        Write-Output "Stopped backend PID: $backendPid"
    }

    $env:MOZAIKA__DATABASE__PROVIDER = "sqlite"
    $env:MOZAIKA__DATABASE__CONNECTIONSTRING = "Data Source=C:\Users\mikedell\Mozaika\data\mozaika.local.db"
    $env:MOZAIKA__MAXUPLOADMB = "25"
    $env:MOZAIKA__CORSORIGINS__0 = "*"

    $backendProc = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList "run", "--urls", "http://127.0.0.1:8000" `
        -WorkingDirectory "C:\Users\mikedell\Mozaika\backend-dotnet" `
        -RedirectStandardOutput "C:\Users\mikedell\Mozaika\backend-dotnet\backend.out.log" `
        -RedirectStandardError "C:\Users\mikedell\Mozaika\backend-dotnet\backend.err.log" `
        -PassThru
    Write-Output "Started backend PID: $($backendProc.Id)"

    if (-not (Wait-BackendPort -TimeoutSeconds 35)) {
        throw "Backend did not come up on port 8000 after restart."
    }

    $loginAfter = Invoke-Json -Method "POST" -Url "$BaseUrl/api/auth/login" -Body @{
        username = "customer"
        password = "customer123"
    }
    $tokenAfter = $loginAfter.token

    $projects = Invoke-Json -Method "GET" -Url "$BaseUrl/api/projects?page=1&limit=100" -Token $tokenAfter
    $found = @($projects.items | Where-Object { [int]$_.id -eq $projectId }).Count -gt 0
    Write-Output "Found after backend restart: $found"

    if (-not $found) {
        throw "Project was not found after backend restart."
    }

    $null = Invoke-WebRequest -Method "DELETE" -Uri "$BaseUrl/api/projects/$projectId" -Headers @{ Authorization = "Bearer $tokenAfter" }
    Write-Output "Cleanup delete completed."
    Write-Output "BACKEND_PERSISTENCE_OK=1"
}
catch {
    Write-Output "BACKEND_PERSISTENCE_OK=0"
    Write-Output $_.Exception.Message
    exit 1
}
