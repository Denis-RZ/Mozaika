param(
    [string]$BaseUrl = "http://127.0.0.1:8000",
    [string]$ImagePath = "",
    [switch]$SkipRestartCheck
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

$script:Results = [System.Collections.Generic.List[object]]::new()

function Add-Result {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    $script:Results.Add([PSCustomObject]@{
        Name = $Name
        Passed = $Passed
        Detail = $Detail
    }) | Out-Null
}

function Convert-JsonSafe {
    param([string]$Text)
    try {
        return $Text | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token = "",
        [object]$JsonBody = $null,
        [object]$RawContent = $null
    )

    $client = [System.Net.Http.HttpClient]::new()
    try {
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($Method),
            "$BaseUrl$Path"
        )

        if (-not [string]::IsNullOrWhiteSpace($Token)) {
            $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $Token)
        }

        if ($RawContent -is [array]) {
            $RawContent = $RawContent | Where-Object { $_ -is [System.Net.Http.HttpContent] } | Select-Object -First 1
        }

        if ($null -ne $RawContent) {
            $request.Content = $RawContent
        } elseif ($null -ne $JsonBody) {
            $json = $JsonBody | ConvertTo-Json -Depth 100 -Compress
            $request.Content = [System.Net.Http.StringContent]::new(
                $json,
                [System.Text.Encoding]::UTF8,
                "application/json"
            )
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
        $jsonPayload = $null

        if ($text.Trim().StartsWith("{") -or $text.Trim().StartsWith("[")) {
            $jsonPayload = Convert-JsonSafe $text
        }

        $mediaType = ""
        if ($response.Content.Headers.ContentType -and $response.Content.Headers.ContentType.MediaType) {
            $mediaType = $response.Content.Headers.ContentType.MediaType
        }

        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            IsSuccess = $response.IsSuccessStatusCode
            ContentType = $mediaType
            Text = $text
            Json = $jsonPayload
            Bytes = $bytes
        }
    } finally {
        $client.Dispose()
    }
}

function Login {
    param(
        [string]$Username,
        [string]$Password
    )

    $response = Invoke-Api -Method "POST" -Path "/api/auth/login" -JsonBody @{
        username = $Username
        password = $Password
    }

    if ($response.StatusCode -ne 200 -or $null -eq $response.Json -or [string]::IsNullOrWhiteSpace($response.Json.token)) {
        throw "Login failed for '$Username'. HTTP $($response.StatusCode) :: $($response.Text)"
    }

    return $response.Json.token
}

function New-MosaicGenerateContent {
    param(
        [string]$ImagePathLocal,
        [int]$GroutColorId = 1
    )

    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $bytes = [System.IO.File]::ReadAllBytes($ImagePathLocal)
    $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
    $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("image/png")
    $content.Add($fileContent, "image", [System.IO.Path]::GetFileName($ImagePathLocal)) | Out-Null
    $content.Add([System.Net.Http.StringContent]::new("900"), "field_width_mm") | Out-Null
    $content.Add([System.Net.Http.StringContent]::new("600"), "field_height_mm") | Out-Null
    $content.Add([System.Net.Http.StringContent]::new("10"), "cell_size_mm") | Out-Null
    $content.Add([System.Net.Http.StringContent]::new("2"), "gap_mm") | Out-Null
    $content.Add([System.Net.Http.StringContent]::new("6"), "max_colors") | Out-Null
    $content.Add([System.Net.Http.StringContent]::new("0"), "offset_x_mm") | Out-Null
    $content.Add([System.Net.Http.StringContent]::new("0"), "offset_y_mm") | Out-Null
    $content.Add([System.Net.Http.StringContent]::new([string]$GroutColorId), "grout_color_id") | Out-Null
    return ,$content
}

$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

function New-DeterministicHex {
    param(
        [long]$Seed,
        [int]$Salt
    )

    $value = [int](($Seed + $Salt) % 16777215)
    if ($value -lt 1048576) {
        $value += 1048576
    }

    return "#{0:X6}" -f $value
}

$colorHexCreate = New-DeterministicHex -Seed $suffix -Salt 101
$colorHexPatch = New-DeterministicHex -Seed $suffix -Salt 202
$colorHexDuplicate = New-DeterministicHex -Seed $suffix -Salt 303
$groutHexCreate = New-DeterministicHex -Seed $suffix -Salt 404
$groutHexPatch = New-DeterministicHex -Seed $suffix -Salt 505

$adminToken = ""
$customerToken = ""
$viewerToken = ""
$createdColorId = $null
$createdGroutId = $null
$mainProjectId = $null
$mainProjectGen1Id = $null
$mainProjectGen2Id = $null
$mainProjectOrderId = $null
$mainProjectShareId = $null
$mainProjectShareToken = ""
$adminProjectId = $null
$adminProjectOrderId = $null
$persistentProjectId = $null
$mosaic = $null
$mosaicForProject = $null
$sourceImageBase64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($ImagePath))

Write-Host "Running client-server smoke tests against $BaseUrl"
Write-Host "Image: $ImagePath"

try {
    $health = Invoke-Api -Method "GET" -Path "/health"
    Add-Result "Health endpoint" ($health.StatusCode -eq 200 -and $health.Json.status -eq "ok") "HTTP $($health.StatusCode)"

    $anonMe = Invoke-Api -Method "GET" -Path "/api/auth/me"
    $anonMeOk = $anonMe.StatusCode -eq 200 -and $anonMe.Json.is_authenticated -eq $false
    Add-Result "Anonymous session state" $anonMeOk "HTTP $($anonMe.StatusCode)"

    $anonProjects = Invoke-Api -Method "GET" -Path "/api/projects"
    Add-Result "Anonymous projects access blocked" ($anonProjects.StatusCode -eq 401) "HTTP $($anonProjects.StatusCode)"

    $adminToken = Login -Username "admin" -Password "admin123"
    $customerToken = Login -Username "customer" -Password "customer123"
    $viewerToken = Login -Username "viewer" -Password "viewer123"
    Add-Result "Login admin/customer/viewer" $true "All tokens received"

    $viewerDeniedColors = Invoke-Api -Method "POST" -Path "/api/colors" -Token $viewerToken -JsonBody @{
        name = "ViewerBlocked_$suffix"
        ral_code = "RAL V$suffix"
        rgb_hex = "#123456"
        is_active = $true
    }
    Add-Result "Viewer cannot create colors" ($viewerDeniedColors.StatusCode -eq 403) "HTTP $($viewerDeniedColors.StatusCode)"

    $customerDeniedAdmin = Invoke-Api -Method "PUT" -Path "/api/admin/settings" -Token $customerToken -JsonBody @{
        default_gap_mm = 3
    }
    Add-Result "Customer cannot update admin settings" ($customerDeniedAdmin.StatusCode -eq 403) "HTTP $($customerDeniedAdmin.StatusCode)"

    $settingsRead = Invoke-Api -Method "GET" -Path "/api/admin/settings" -Token $adminToken
    $settingsOk = $settingsRead.StatusCode -eq 200 -and $null -ne $settingsRead.Json.default_gap_mm
    Add-Result "Admin can read settings" $settingsOk "HTTP $($settingsRead.StatusCode)"

    if ($settingsOk) {
        $oldGap = [int]$settingsRead.Json.default_gap_mm
        $updateSettings = Invoke-Api -Method "PUT" -Path "/api/admin/settings" -Token $adminToken -JsonBody @{
            default_gap_mm = ($oldGap + 1)
        }
        Add-Result "Admin can update settings" ($updateSettings.StatusCode -eq 200 -and [int]$updateSettings.Json.default_gap_mm -eq ($oldGap + 1)) "HTTP $($updateSettings.StatusCode)"

        $restoreSettings = Invoke-Api -Method "PUT" -Path "/api/admin/settings" -Token $adminToken -JsonBody @{
            default_gap_mm = $oldGap
        }
        Add-Result "Settings restored" ($restoreSettings.StatusCode -eq 200 -and [int]$restoreSettings.Json.default_gap_mm -eq $oldGap) "HTTP $($restoreSettings.StatusCode)"
    }

    $createColor = Invoke-Api -Method "POST" -Path "/api/colors" -Token $adminToken -JsonBody @{
        name = "E2E Color $suffix"
        ral_code = "RAL E2E $suffix"
        rgb_hex = $colorHexCreate
        is_active = $true
    }
    $colorCreateOk = $createColor.StatusCode -eq 201 -and $null -ne $createColor.Json.id
    Add-Result "Color create" $colorCreateOk "HTTP $($createColor.StatusCode)"
    if ($colorCreateOk) {
        $createdColorId = [int]$createColor.Json.id
    }

    if ($null -ne $createdColorId) {
        $patchColor = Invoke-Api -Method "PATCH" -Path "/api/colors/$createdColorId" -Token $adminToken -JsonBody @{
            name = "E2E Color Updated $suffix"
            rgb_hex = $colorHexPatch
        }
        Add-Result "Color update" ($patchColor.StatusCode -eq 200 -and $patchColor.Json.name -like "E2E Color Updated*") "HTTP $($patchColor.StatusCode)"

        $dupColor = Invoke-Api -Method "POST" -Path "/api/colors" -Token $adminToken -JsonBody @{
            name = "E2E Color Updated $suffix"
            ral_code = "RAL DUP $suffix"
            rgb_hex = $colorHexDuplicate
            is_active = $true
        }
        Add-Result "Color duplicate blocked" ($dupColor.StatusCode -eq 409) "HTTP $($dupColor.StatusCode)"

        $deleteColor = Invoke-Api -Method "DELETE" -Path "/api/colors/$createdColorId" -Token $adminToken
        Add-Result "Color deactivate" ($deleteColor.StatusCode -eq 200 -and $deleteColor.Json.is_active -eq $false) "HTTP $($deleteColor.StatusCode)"
    }

    $createGrout = Invoke-Api -Method "POST" -Path "/api/admin/grout-colors" -Token $adminToken -JsonBody @{
        name = "E2E Grout $suffix"
        rgb_hex = $groutHexCreate
        is_active = $true
    }
    $groutCreateOk = $createGrout.StatusCode -eq 201 -and $null -ne $createGrout.Json.id
    Add-Result "Grout create" $groutCreateOk "HTTP $($createGrout.StatusCode)"
    if ($groutCreateOk) {
        $createdGroutId = [int]$createGrout.Json.id
    }

    if ($null -ne $createdGroutId) {
        $patchGrout = Invoke-Api -Method "PATCH" -Path "/api/admin/grout-colors/$createdGroutId" -Token $adminToken -JsonBody @{
            name = "E2E Grout Updated $suffix"
            rgb_hex = $groutHexPatch
        }
        Add-Result "Grout update" ($patchGrout.StatusCode -eq 200 -and $patchGrout.Json.name -like "E2E Grout Updated*") "HTTP $($patchGrout.StatusCode)"

        $deleteGrout = Invoke-Api -Method "DELETE" -Path "/api/admin/grout-colors/$createdGroutId" -Token $adminToken
        Add-Result "Grout deactivate" ($deleteGrout.StatusCode -eq 200 -and $deleteGrout.Json.is_active -eq $false) "HTTP $($deleteGrout.StatusCode)"
    }

    $bootstrap = Invoke-Api -Method "GET" -Path "/api/bootstrap"
    $groutIdForGenerate = 1
    if ($bootstrap.StatusCode -eq 200 -and $bootstrap.Json.grout_colors.Count -gt 0) {
        $activeGrout = $bootstrap.Json.grout_colors | Where-Object { $_.is_active -eq $true } | Select-Object -First 1
        if ($null -ne $activeGrout) {
            $groutIdForGenerate = [int]$activeGrout.id
        }
    }

    $viewerGenerateDenied = Invoke-Api -Method "POST" -Path "/api/mosaic/generate" -Token $viewerToken -RawContent ([System.Net.Http.StringContent]::new(""))
    Add-Result "Viewer cannot generate mosaic" ($viewerGenerateDenied.StatusCode -eq 403) "HTTP $($viewerGenerateDenied.StatusCode)"

    $generateContent = New-MosaicGenerateContent -ImagePathLocal $ImagePath -GroutColorId $groutIdForGenerate
    $generate = Invoke-Api -Method "POST" -Path "/api/mosaic/generate" -Token $customerToken -RawContent $generateContent
    $generateOk = $generate.StatusCode -eq 200 -and $null -ne $generate.Json.rows -and [int]$generate.Json.rows -gt 0
    Add-Result "Mosaic generate" $generateOk "HTTP $($generate.StatusCode)"
    if ($generateOk) {
        $mosaic = $generate.Json
    }

    if ($null -ne $mosaic) {
        $usedColorIds = @($mosaic.used_colors | ForEach-Object { [int]$_.id })
        $fromColor = $usedColorIds[0]
        $toColor = $null
        if ($usedColorIds.Count -gt 1) {
            $toColor = $usedColorIds[1]
        } else {
            $candidate = $bootstrap.Json.colors | Where-Object { $_.is_active -eq $true -and [int]$_.id -ne $fromColor } | Select-Object -First 1
            if ($null -ne $candidate) {
                $toColor = [int]$candidate.id
            }
        }

        $replaceSucceeded = $false
        if ($null -ne $toColor) {
            $replace = Invoke-Api -Method "POST" -Path "/api/mosaic/replace-color" -Token $customerToken -JsonBody @{
                grid_color_ids = $mosaic.grid_color_ids
                from_color_id = $fromColor
                to_color_id = $toColor
                field_width_mm = $mosaic.field_width_mm
                field_height_mm = $mosaic.field_height_mm
                cell_size_mm = $mosaic.cell_size_mm
                gap_mm = $mosaic.gap_mm
                offset_x_mm = $mosaic.offset_x_mm
                offset_y_mm = $mosaic.offset_y_mm
                grout_color_hex = $mosaic.grout_color_hex
                requested_max_colors = $mosaic.requested_max_colors
            }
            $replaceSucceeded = $replace.StatusCode -eq 200 -and $null -ne $replace.Json.total_chips
            Add-Result "Mosaic replace-color" $replaceSucceeded "HTTP $($replace.StatusCode)"
            if ($replaceSucceeded) {
                $mosaicForProject = $replace.Json
            }
        } else {
            Add-Result "Mosaic replace-color" $false "No target color available for replacement"
        }

        if ($null -eq $mosaicForProject) {
            $mosaicForProject = $mosaic
        }

        $exportPayload = @{
            grid_color_ids = $mosaicForProject.grid_color_ids
            field_width_mm = $mosaicForProject.field_width_mm
            field_height_mm = $mosaicForProject.field_height_mm
            cell_size_mm = $mosaicForProject.cell_size_mm
            gap_mm = $mosaicForProject.gap_mm
            offset_x_mm = $mosaicForProject.offset_x_mm
            offset_y_mm = $mosaicForProject.offset_y_mm
            grout_color_hex = $mosaicForProject.grout_color_hex
            dpi = 180
            mirror_horizontal = $false
            include_legend = $true
            module_chip_columns = 16
            module_chip_rows = 16
            module_start_number = 1
            include_color_numbers = $true
            file_name = "e2e-$suffix"
        }

        $formats = @("png", "jpeg", "svg", "pdf", "materials-csv", "grid-csv", "modules-csv", "assembly-kit-pdf")
        foreach ($fmt in $formats) {
            $exportResp = Invoke-Api -Method "POST" -Path "/api/mosaic/export/$fmt" -Token $customerToken -JsonBody $exportPayload
            $ok = $exportResp.StatusCode -eq 200 -and $exportResp.Bytes.Length -gt 0
            Add-Result "Export $fmt" $ok "HTTP $($exportResp.StatusCode), bytes=$($exportResp.Bytes.Length)"
        }

        $generationPayload = @{
            name = "E2E Gen 1"
            note = "Generated by smoke test"
            snapshot = @{
                mosaic = $mosaicForProject
                include_color_ids = @()
                exclude_color_ids = @()
                grout_color_id = $groutIdForGenerate
                preview_zoom = 1
                preview_pan_x = 0
                preview_pan_y = 0
            }
        }

        $createMainProject = Invoke-Api -Method "POST" -Path "/api/projects" -Token $customerToken -JsonBody @{
            name = "E2E Main Project $suffix"
            description = "Customer project for CRUD smoke test"
            source_image_mime_type = "image/png"
            source_image_base64 = $sourceImageBase64
            initial_generation = $generationPayload
        }
        $mainProjectOk = $createMainProject.StatusCode -eq 201 -and $null -ne $createMainProject.Json.id
        Add-Result "Project create (customer)" $mainProjectOk "HTTP $($createMainProject.StatusCode)"
        if ($mainProjectOk) {
            $mainProjectId = [int]$createMainProject.Json.id
            if ($createMainProject.Json.active_generation -and $createMainProject.Json.active_generation.id) {
                $mainProjectGen1Id = [int]$createMainProject.Json.active_generation.id
            }
        }

        if ($null -ne $mainProjectId) {
            $listCustomerProjects = Invoke-Api -Method "GET" -Path "/api/projects?page=1&limit=50" -Token $customerToken
            $hasMainProject = $false
            if ($listCustomerProjects.StatusCode -eq 200) {
                $hasMainProject = @($listCustomerProjects.Json.items | Where-Object { [int]$_.id -eq $mainProjectId }).Count -gt 0
            }
            Add-Result "Project list includes created project" ($listCustomerProjects.StatusCode -eq 200 -and $hasMainProject) "HTTP $($listCustomerProjects.StatusCode)"

            $getMainProject = Invoke-Api -Method "GET" -Path "/api/projects/$mainProjectId" -Token $customerToken
            Add-Result "Project read by owner" ($getMainProject.StatusCode -eq 200) "HTTP $($getMainProject.StatusCode)"

            $patchMainProject = Invoke-Api -Method "PATCH" -Path "/api/projects/$mainProjectId" -Token $customerToken -JsonBody @{
                name = "E2E Main Project Updated $suffix"
                description = "Updated description"
            }
            Add-Result "Project update by owner" ($patchMainProject.StatusCode -eq 200 -and $patchMainProject.Json.name -like "E2E Main Project Updated*") "HTTP $($patchMainProject.StatusCode)"

            $gen2Payload = @{
                name = "E2E Gen 2"
                note = "Second generation"
                snapshot = @{
                    mosaic = $mosaicForProject
                    include_color_ids = @()
                    exclude_color_ids = @()
                    grout_color_id = $groutIdForGenerate
                    preview_zoom = 1.15
                    preview_pan_x = 12
                    preview_pan_y = -4
                }
            }
            $saveGen2 = Invoke-Api -Method "POST" -Path "/api/projects/$mainProjectId/generations" -Token $customerToken -JsonBody $gen2Payload
            $saveGen2Ok = $saveGen2.StatusCode -eq 201 -and $null -ne $saveGen2.Json.id
            Add-Result "Project save generation v2" $saveGen2Ok "HTTP $($saveGen2.StatusCode)"
            if ($saveGen2Ok) {
                $mainProjectGen2Id = [int]$saveGen2.Json.id
            }

            if ($null -ne $mainProjectGen2Id) {
                $getGen2 = Invoke-Api -Method "GET" -Path "/api/projects/$mainProjectId/generations/$mainProjectGen2Id" -Token $customerToken
                Add-Result "Project read generation v2" ($getGen2.StatusCode -eq 200 -and [int]$getGen2.Json.version -ge 2) "HTTP $($getGen2.StatusCode)"
            }

            if ($null -ne $mainProjectGen1Id) {
                $activateGen1 = Invoke-Api -Method "POST" -Path "/api/projects/$mainProjectId/generations/$mainProjectGen1Id/activate" -Token $customerToken
                Add-Result "Project activate generation v1" ($activateGen1.StatusCode -eq 200 -and [int]$activateGen1.Json.active_generation_id -eq $mainProjectGen1Id) "HTTP $($activateGen1.StatusCode)"
            }

            $sharePayload = @{
                generation_id = $mainProjectGen2Id
                expires_in_days = 7
            }
            $createShare = Invoke-Api -Method "POST" -Path "/api/projects/$mainProjectId/shares" -Token $customerToken -JsonBody $sharePayload
            $createShareOk = $createShare.StatusCode -eq 201 -and -not [string]::IsNullOrWhiteSpace($createShare.Json.token)
            Add-Result "Project create share" $createShareOk "HTTP $($createShare.StatusCode)"
            if ($createShareOk) {
                $mainProjectShareId = [int]$createShare.Json.id
                $mainProjectShareToken = [string]$createShare.Json.token
            }

            $listShares = Invoke-Api -Method "GET" -Path "/api/projects/$mainProjectId/shares" -Token $customerToken
            $shareListed = $false
            if ($listShares.StatusCode -eq 200 -and $null -ne $mainProjectShareId) {
                $shareListed = @($listShares.Json | Where-Object { [int]$_.id -eq $mainProjectShareId }).Count -gt 0
            }
            Add-Result "Project list shares" ($listShares.StatusCode -eq 200 -and $shareListed) "HTTP $($listShares.StatusCode)"

            if (-not [string]::IsNullOrWhiteSpace($mainProjectShareToken)) {
                $resolveShare = Invoke-Api -Method "GET" -Path "/api/project-shares/$mainProjectShareToken"
                $resolveOk = $resolveShare.StatusCode -eq 200 -and [int]$resolveShare.Json.project_id -eq $mainProjectId
                Add-Result "Resolve share token" $resolveOk "HTTP $($resolveShare.StatusCode)"
            }

            if ($null -ne $mainProjectShareId) {
                $revokeShare = Invoke-Api -Method "POST" -Path "/api/projects/$mainProjectId/shares/$mainProjectShareId/revoke" -Token $customerToken
                Add-Result "Revoke share" ($revokeShare.StatusCode -eq 200 -and $revokeShare.Json.is_revoked -eq $true) "HTTP $($revokeShare.StatusCode)"
            }

            $orderPayload = @{
                generation_id = $mainProjectGen2Id
                customer_name = "E2E Customer"
                customer_email = "e2e@example.com"
                customer_phone = "+1000000000"
                comment = "Smoke order"
            }
            $createOrder = Invoke-Api -Method "POST" -Path "/api/projects/$mainProjectId/orders" -Token $customerToken -JsonBody $orderPayload
            $createOrderOk = $createOrder.StatusCode -eq 201 -and $null -ne $createOrder.Json.id
            Add-Result "Project create order" $createOrderOk "HTTP $($createOrder.StatusCode)"
            if ($createOrderOk) {
                $mainProjectOrderId = [int]$createOrder.Json.id
            }

            $listOrders = Invoke-Api -Method "GET" -Path "/api/projects/$mainProjectId/orders" -Token $customerToken
            $orderListed = $false
            if ($listOrders.StatusCode -eq 200 -and $null -ne $mainProjectOrderId) {
                $orderListed = @($listOrders.Json | Where-Object { [int]$_.id -eq $mainProjectOrderId }).Count -gt 0
            }
            Add-Result "Project list orders" ($listOrders.StatusCode -eq 200 -and $orderListed) "HTTP $($listOrders.StatusCode)"

            if ($null -ne $mainProjectOrderId) {
                $updateOrderStatus = Invoke-Api -Method "PATCH" -Path "/api/projects/$mainProjectId/orders/$mainProjectOrderId/status" -Token $adminToken -JsonBody @{
                    status = "in_review"
                    status_comment = "Checked by admin"
                }
                Add-Result "Admin updates order status" ($updateOrderStatus.StatusCode -eq 200 -and $updateOrderStatus.Json.status -eq "in_review") "HTTP $($updateOrderStatus.StatusCode)"
            }
        }

        $createAdminProject = Invoke-Api -Method "POST" -Path "/api/projects" -Token $adminToken -JsonBody @{
            name = "E2E Admin Project $suffix"
            description = "Admin ownership visibility test"
            source_image_mime_type = "image/png"
            source_image_base64 = $sourceImageBase64
            initial_generation = $generationPayload
        }
        $adminProjectOk = $createAdminProject.StatusCode -eq 201 -and $null -ne $createAdminProject.Json.id
        Add-Result "Project create (admin)" $adminProjectOk "HTTP $($createAdminProject.StatusCode)"
        if ($adminProjectOk) {
            $adminProjectId = [int]$createAdminProject.Json.id
        }

        if ($null -ne $adminProjectId) {
            $customerList = Invoke-Api -Method "GET" -Path "/api/projects?page=1&limit=100" -Token $customerToken
            $visibleInList = $false
            if ($customerList.StatusCode -eq 200) {
                $visibleInList = @($customerList.Json.items | Where-Object { [int]$_.id -eq $adminProjectId }).Count -gt 0
            }
            Add-Result "Customer list does not show admin project" ($customerList.StatusCode -eq 200 -and -not $visibleInList) "HTTP $($customerList.StatusCode)"

            $customerGetAdmin = Invoke-Api -Method "GET" -Path "/api/projects/$adminProjectId" -Token $customerToken
            Add-Result "Customer cannot read admin project by id" ($customerGetAdmin.StatusCode -in @(403,404)) "HTTP $($customerGetAdmin.StatusCode)"

            $customerShareAdmin = Invoke-Api -Method "POST" -Path "/api/projects/$adminProjectId/shares" -Token $customerToken -JsonBody @{
                expires_in_days = 3
            }
            Add-Result "Customer cannot create share for admin project" ($customerShareAdmin.StatusCode -in @(403,404)) "HTTP $($customerShareAdmin.StatusCode)"

            $customerOrderAdmin = Invoke-Api -Method "POST" -Path "/api/projects/$adminProjectId/orders" -Token $customerToken -JsonBody @{
                customer_name = "Leak Test"
                customer_email = "leak@test.local"
                customer_phone = "+19999999999"
                comment = "Should be blocked"
            }
            Add-Result "Customer cannot create order for admin project" ($customerOrderAdmin.StatusCode -in @(403,404)) "HTTP $($customerOrderAdmin.StatusCode)"

            if ($customerOrderAdmin.StatusCode -eq 201 -and $customerOrderAdmin.Json.id) {
                $adminProjectOrderId = [int]$customerOrderAdmin.Json.id
            }
        }
    }

    if ($null -ne $mainProjectId) {
        $deleteMainProject = Invoke-Api -Method "DELETE" -Path "/api/projects/$mainProjectId" -Token $customerToken
        Add-Result "Project delete by owner" ($deleteMainProject.StatusCode -eq 204) "HTTP $($deleteMainProject.StatusCode)"
    }

    if ($null -ne $adminProjectId) {
        $deleteAdminProject = Invoke-Api -Method "DELETE" -Path "/api/projects/$adminProjectId" -Token $adminToken
        Add-Result "Admin project cleanup delete" ($deleteAdminProject.StatusCode -eq 204) "HTTP $($deleteAdminProject.StatusCode)"
    }

    if (-not $SkipRestartCheck.IsPresent -and $null -ne $mosaicForProject) {
        $genPersistPayload = @{
            name = "E2E Persist Gen"
            note = "Restart persistence check"
            snapshot = @{
                mosaic = $mosaicForProject
                include_color_ids = @()
                exclude_color_ids = @()
                grout_color_id = 1
                preview_zoom = 1
                preview_pan_x = 0
                preview_pan_y = 0
            }
        }

        $createPersistProject = Invoke-Api -Method "POST" -Path "/api/projects" -Token $customerToken -JsonBody @{
            name = "E2E Persist Project $suffix"
            description = "Should survive restart"
            source_image_mime_type = "image/png"
            source_image_base64 = $sourceImageBase64
            initial_generation = $genPersistPayload
        }
        $persistCreated = $createPersistProject.StatusCode -eq 201 -and $null -ne $createPersistProject.Json.id
        Add-Result "Persistence project create" $persistCreated "HTTP $($createPersistProject.StatusCode)"
        if ($persistCreated) {
            $persistentProjectId = [int]$createPersistProject.Json.id
        }

        if ($null -ne $persistentProjectId) {
            Write-Host "Restarting local services for persistence check..."
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path (Split-Path -Parent $PSScriptRoot) "stop-local.ps1") | Out-Null
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path (Split-Path -Parent $PSScriptRoot) "start-local.ps1") -NoBrowser | Out-Null

            $customerToken = Login -Username "customer" -Password "customer123"
            $postRestartList = Invoke-Api -Method "GET" -Path "/api/projects?page=1&limit=100" -Token $customerToken
            $foundAfterRestart = $false
            if ($postRestartList.StatusCode -eq 200) {
                $foundAfterRestart = @($postRestartList.Json.items | Where-Object { [int]$_.id -eq $persistentProjectId }).Count -gt 0
            }
            Add-Result "Persistence after restart" ($postRestartList.StatusCode -eq 200 -and $foundAfterRestart) "HTTP $($postRestartList.StatusCode)"

            $deletePersistProject = Invoke-Api -Method "DELETE" -Path "/api/projects/$persistentProjectId" -Token $customerToken
            Add-Result "Persistence project cleanup delete" ($deletePersistProject.StatusCode -eq 204) "HTTP $($deletePersistProject.StatusCode)"
        }
    }
}
catch {
    Add-Result "Unhandled script failure" $false $_.Exception.Message
}

$passedCount = @($script:Results | Where-Object { $_.Passed }).Count
$failedCount = @($script:Results | Where-Object { -not $_.Passed }).Count

Write-Host ""
Write-Host "==== Smoke Test Results ===="
$script:Results | Format-Table -AutoSize
Write-Host ""
Write-Host "Passed: $passedCount"
Write-Host "Failed: $failedCount"

if ($failedCount -gt 0) {
    exit 1
}

exit 0
