param(
    [string]$BaseUrl = "http://127.0.0.1:8000",
    [string]$OutputDir = "",
    [string]$Username = "customer",
    [string]$Password = "customer123"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path (Split-Path -Parent $PSScriptRoot) "docs\customer-presentation\assets"
}

$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$sourceImagePath = Join-Path $OutputDir "source-demo.png"
$mosaicJsonPath = Join-Path $OutputDir "mosaic-generate.json"

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Uri,
        [string]$Token = "",
        [object]$Body = $null
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }

    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 100 -Compress
        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers -ContentType "application/json" -Body $json
    }

    return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers
}

function New-DemoSourceImage {
    param([string]$Path)

    $width = 1280
    $height = 720
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

        $rect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
        $backBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.Color]::FromArgb(255, 30, 46, 92),
            [System.Drawing.Color]::FromArgb(255, 13, 112, 94),
            25
        )
        $graphics.FillRectangle($backBrush, $rect)
        $backBrush.Dispose()

        for ($i = 0; $i -lt 16; $i++) {
            $size = Get-Random -Minimum 70 -Maximum 240
            $x = Get-Random -Minimum (-40) -Maximum ($width - 20)
            $y = Get-Random -Minimum (-40) -Maximum ($height - 20)
            $color = [System.Drawing.Color]::FromArgb(
                (Get-Random -Minimum 70 -Maximum 180),
                (Get-Random -Minimum 30 -Maximum 255),
                (Get-Random -Minimum 30 -Maximum 255),
                (Get-Random -Minimum 30 -Maximum 255)
            )
            $brush = New-Object System.Drawing.SolidBrush($color)
            $graphics.FillEllipse($brush, $x, $y, $size, $size)
            $brush.Dispose()
        }

        $titleFont = New-Object System.Drawing.Font("Segoe UI", 78, [System.Drawing.FontStyle]::Bold)
        $titleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
        $graphics.DrawString("MOZAIKA", $titleFont, $titleBrush, 80, 230)
        $titleFont.Dispose()
        $titleBrush.Dispose()

        $subFont = New-Object System.Drawing.Font("Segoe UI", 34, [System.Drawing.FontStyle]::Regular)
        $subBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(215, 250, 240, 220))
        $graphics.DrawString("design preview", $subFont, $subBrush, 90, 345)
        $subFont.Dispose()
        $subBrush.Dispose()

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Invoke-GenerateMosaic {
    param(
        [string]$Uri,
        [string]$Token,
        [string]$ImagePath
    )

    $client = [System.Net.Http.HttpClient]::new()
    try {
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, $Uri)
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $Token)

        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $bytes = [System.IO.File]::ReadAllBytes($ImagePath)
        $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("image/png")
        $content.Add($fileContent, "image", [System.IO.Path]::GetFileName($ImagePath)) | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("2200"), "field_width_mm") | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("1400"), "field_height_mm") | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("10"), "cell_size_mm") | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("2"), "gap_mm") | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("20"), "max_colors") | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("1"), "grout_color_id") | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("0"), "offset_x_mm") | Out-Null
        $content.Add([System.Net.Http.StringContent]::new("0"), "offset_y_mm") | Out-Null

        $request.Content = $content
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if (-not $response.IsSuccessStatusCode) {
            throw "Mosaic generate failed: HTTP $([int]$response.StatusCode) $text"
        }

        return ($text | ConvertFrom-Json)
    }
    finally {
        $client.Dispose()
    }
}

function Invoke-ExportBinary {
    param(
        [string]$Uri,
        [string]$Token,
        [object]$Payload,
        [string]$OutPath
    )

    $client = [System.Net.Http.HttpClient]::new()
    try {
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, $Uri)
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $Token)
        $json = $Payload | ConvertTo-Json -Depth 100 -Compress
        $request.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, "application/json")

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            $text = [System.Text.Encoding]::UTF8.GetString($bytes)
            throw "Export failed ($Uri): HTTP $([int]$response.StatusCode) $text"
        }

        [System.IO.File]::WriteAllBytes($OutPath, $bytes)
    }
    finally {
        $client.Dispose()
    }
}

Write-Host "Preparing customer presentation assets in: $OutputDir"
New-DemoSourceImage -Path $sourceImagePath

$login = Invoke-JsonApi -Method "POST" -Uri "$BaseUrl/api/auth/login" -Body @{
    username = $Username
    password = $Password
}
$token = $login.token
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Login failed: token is empty."
}

$mosaic = Invoke-GenerateMosaic -Uri "$BaseUrl/api/mosaic/generate" -Token $token -ImagePath $sourceImagePath
$mosaic | ConvertTo-Json -Depth 100 | Set-Content -Path $mosaicJsonPath -Encoding UTF8

$exportPayload = @{
    grid_color_ids = $mosaic.grid_color_ids
    field_width_mm = $mosaic.field_width_mm
    field_height_mm = $mosaic.field_height_mm
    cell_size_mm = $mosaic.cell_size_mm
    gap_mm = $mosaic.gap_mm
    offset_x_mm = $mosaic.offset_x_mm
    offset_y_mm = $mosaic.offset_y_mm
    grout_color_hex = $mosaic.grout_color_hex
    dpi = 220
    mirror_horizontal = $false
    include_legend = $true
    module_chip_columns = 32
    module_chip_rows = 32
    module_start_number = 1
    include_color_numbers = $true
    file_name = "customer-demo"
}

$targets = @(
    @{ Format = "png"; Path = (Join-Path $OutputDir "mosaic-preview.png") },
    @{ Format = "jpeg"; Path = (Join-Path $OutputDir "mosaic-preview.jpg") },
    @{ Format = "svg"; Path = (Join-Path $OutputDir "mosaic-preview.svg") },
    @{ Format = "pdf"; Path = (Join-Path $OutputDir "mosaic-preview.pdf") },
    @{ Format = "materials-csv"; Path = (Join-Path $OutputDir "materials.csv") },
    @{ Format = "grid-csv"; Path = (Join-Path $OutputDir "grid.csv") },
    @{ Format = "modules-csv"; Path = (Join-Path $OutputDir "modules.csv") },
    @{ Format = "assembly-kit-pdf"; Path = (Join-Path $OutputDir "assembly-kit.pdf") }
)

foreach ($target in $targets) {
    Invoke-ExportBinary `
        -Uri "$BaseUrl/api/mosaic/export/$($target.Format)" `
        -Token $token `
        -Payload $exportPayload `
        -OutPath $target.Path
}

$summary = [PSCustomObject]@{
    generated_at_utc = [DateTime]::UtcNow.ToString("o")
    source_image = [System.IO.Path]::GetFileName($sourceImagePath)
    rows = $mosaic.rows
    columns = $mosaic.columns
    total_chips = $mosaic.total_chips
    actual_colors_used = $mosaic.actual_colors_used
    total_price = $mosaic.price.total_price
    currency = $mosaic.price.currency
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $OutputDir "summary.json") -Encoding UTF8

Write-Host "Done. Assets created:"
Get-ChildItem $OutputDir | Select-Object Name, Length | Format-Table -AutoSize
