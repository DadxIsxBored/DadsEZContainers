param([switch]$Package)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$projectPath = Join-Path $root 'DadsEZContainers.csproj'
$packageRoot = Join-Path $root 'package'
$dllPath = Join-Path $root 'bin\Release\netstandard2.1\DadsEZContainers.dll'

dotnet build $projectPath -c Release

if ($Package) {
    $manifestPath = Join-Path $packageRoot 'manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.name -notmatch '^[A-Za-z0-9_]+$') { throw "Invalid package name: $($manifest.name)" }
    if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid semantic version: $($manifest.version_number)" }
    if ([string]::IsNullOrWhiteSpace($manifest.description) -or $manifest.description.Length -gt 250) { throw 'Description must contain 1-250 characters.' }
    if ($null -eq $manifest.dependencies) { throw 'Manifest dependencies field is required.' }

    $sources = @(
        $dllPath,
        $manifestPath,
        (Join-Path $packageRoot 'README.md'),
        (Join-Path $packageRoot 'icon.png'),
        (Join-Path $root 'CHANGELOG.md'),
        (Join-Path $root 'LICENSE')
    )
    foreach ($source in $sources) {
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Required file is missing: $source" }
    }

    Add-Type -AssemblyName System.Drawing
    $iconPath = Join-Path $packageRoot 'icon.png'
    $icon = [System.Drawing.Image]::FromFile($iconPath)
    try {
        if ($icon.Width -ne 256 -or $icon.Height -ne 256) { throw "icon.png must be 256x256; found $($icon.Width)x$($icon.Height)." }
        if ($icon.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid) { throw 'icon.png must be PNG.' }
    }
    finally { $icon.Dispose() }

    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($dllPath).Version
    $assemblySemVer = "$($assemblyVersion.Major).$($assemblyVersion.Minor).$($assemblyVersion.Build)"
    if ($assemblySemVer -ne $manifest.version_number) { throw "Assembly version $assemblySemVer differs from manifest version $($manifest.version_number)." }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $stage = Join-Path $root "dist\DadsEZContainers-$($manifest.version_number)-$stamp"
    $zipPath = "$stage.zip"
    if (Test-Path -LiteralPath $stage) { throw "Stage already exists: $stage" }
    if (Test-Path -LiteralPath $zipPath) { throw "ZIP already exists: $zipPath" }
    New-Item -ItemType Directory -Path $stage | Out-Null
    Copy-Item -LiteralPath $dllPath -Destination $stage
    Copy-Item -LiteralPath $manifestPath -Destination $stage
    Copy-Item -LiteralPath (Join-Path $packageRoot 'README.md') -Destination $stage
    Copy-Item -LiteralPath $iconPath -Destination $stage
    Copy-Item -LiteralPath (Join-Path $root 'CHANGELOG.md') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $stage
    Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $stage -File).FullName -DestinationPath $zipPath -CompressionLevel Optimal

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = @($archive.Entries | ForEach-Object FullName)
        foreach ($required in @('manifest.json', 'README.md', 'icon.png', 'DadsEZContainers.dll')) {
            if ($required -notin $entries) { throw "ZIP is missing root entry: $required" }
        }
        if ($entries | Where-Object { $_ -match '[/\\]' }) { throw 'ZIP contains a nested directory.' }
        foreach ($forbidden in @('assembly_valheim.dll', 'BepInEx.dll', '0Harmony.dll')) {
            if ($forbidden -in $entries) { throw "ZIP contains forbidden assembly: $forbidden" }
        }
        if ($entries | Where-Object { $_ -like 'UnityEngine*.dll' }) { throw 'ZIP contains a UnityEngine assembly.' }
    }
    finally { $archive.Dispose() }

    Write-Host "Thunderstore package: $zipPath"
    Write-Host "SHA256: $((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
}
