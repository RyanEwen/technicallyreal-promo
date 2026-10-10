# refresh.ps1 - regenerates Assets/PromotedApps from live Microsoft Store listings.
#
# Dev-side tool only: run it occasionally (e.g. when publishing a new app or before a
# release), review the diff, and commit the result. Consuming apps bundle the generated
# files and refresh from the public manifest at runtime.
#
# NOTE: keep this file ASCII-only (see project conventions).

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$outDir = Join-Path $root "Assets\PromotedApps"
$ids = Get-Content (Join-Path $root "ids.json") -Raw | ConvertFrom-Json

New-Item -ItemType Directory -Force $outDir | Out-Null

function Get-BriefDescription([string]$text, [int]$maxLen = 180) {
    if (-not $text) { return "" }
    # First non-empty line, trimmed to a word boundary.
    $line = ($text -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -First 1).Trim()
    if ($line.Length -le $maxLen) { return $line }
    $cut = $line.Substring(0, $maxLen)
    $lastSpace = $cut.LastIndexOf(" ")
    if ($lastSpace -gt 0) { $cut = $cut.Substring(0, $lastSpace) }
    return $cut.TrimEnd(".,;: ") + "..."
}

$apps = @()
foreach ($id in $ids.productIds) {
    Write-Host "Fetching $id ..."
    $url = "https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/${id}?market=US&locale=en-us&deviceFamily=Windows.Desktop"
    $payload = (Invoke-RestMethod $url).Payload

    # Prefer the square logo; fall back to any tile, then the first image.
    $img = $payload.Images | Where-Object { $_.ImageType -eq "logo" } | Select-Object -First 1
    if (-not $img) { $img = $payload.Images | Where-Object { $_.ImageType -eq "tile" } | Sort-Object Width -Descending | Select-Object -First 1 }
    if (-not $img) { $img = $payload.Images | Select-Object -First 1 }
    $imgUrl = $img.Url
    if ($imgUrl -and $imgUrl.StartsWith("//")) { $imgUrl = "https:" + $imgUrl }

    $iconFile = "$id.png"
    Invoke-WebRequest $imgUrl -OutFile (Join-Path $outDir $iconFile) | Out-Null

    $blurb = $null
    if ($ids.blurbOverrides -and $ids.blurbOverrides.PSObject.Properties[$id]) {
        $blurb = $ids.blurbOverrides.$id
    }
    if (-not $blurb) { $blurb = Get-BriefDescription $payload.Description }

    $metadata = $ids.appOverrides.$id
    if (-not $metadata.appId -or -not $metadata.links) {
        throw "Missing appOverrides metadata for $id"
    }

    # Preserve publisher-approved names while Store listing updates catch up.
    $name = $payload.Title
    if ($metadata.name) { $name = $metadata.name }

    $apps += [ordered]@{
        appId             = $metadata.appId
        links             = @($metadata.links)
        destination       = "ms-windows-store://pdp/?ProductId=$id"
        actionLabel       = "View in Store"
        name              = $name
        blurb             = $blurb
        packageFamilyName = @($payload.PackageFamilyNames)[0]
        icon              = $iconFile
    }
    Write-Host "  -> ${name}: $blurb"
}

# Web apps use explicit repository-owned metadata instead of Store listing data.
foreach ($externalApp in @($ids.externalApps)) {
    Write-Host "Fetching $($externalApp.name) icon ..."
    Invoke-WebRequest $externalApp.iconUrl -OutFile (Join-Path $outDir $externalApp.icon) | Out-Null

    $apps += [ordered]@{
        appId             = $externalApp.appId
        links             = @($externalApp.links)
        destination       = $externalApp.destination
        actionLabel       = $externalApp.actionLabel
        name              = $externalApp.name
        blurb             = $externalApp.blurb
        packageFamilyName = $null
        icon              = $externalApp.icon
    }
}

# Publish one canonical order for every consumer, including older Windows apps
# that simply enumerate the manifest. Refuse incomplete or duplicate ordering.
$orderedApps = @()
foreach ($appId in @($ids.appOrder)) {
    $matches = @($apps | Where-Object { $_.appId -eq $appId })
    if ($matches.Count -ne 1 -or $appId -in @($orderedApps | ForEach-Object { $_.appId })) {
        throw "Invalid or duplicate appOrder entry: $appId"
    }
    $orderedApps += $matches[0]
}
if ($orderedApps.Count -ne $apps.Count) {
    throw "appOrder must include every app exactly once"
}

$manifest = [ordered]@{
    generatedUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    publisher    = "Dynamic Solutions Canada"
    apps         = $orderedApps
}
$json = $manifest | ConvertTo-Json -Depth 5
Set-Content -Path (Join-Path $outDir "apps.json") -Value $json -Encoding UTF8
Write-Host "Wrote $(Join-Path $outDir 'apps.json') ($($apps.Count) apps)."
