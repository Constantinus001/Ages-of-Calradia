param(
    [Parameter(Mandatory = $true)]
    [string]$SourceArchive,

    [Parameter(Mandatory = $true)]
    [string]$DestinationArchive,

    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$sourcePath = (Resolve-Path -LiteralPath $SourceArchive).Path
$destinationPath = [IO.Path]::GetFullPath($DestinationArchive)
$assetSourcesPath = Join-Path $ModuleRoot 'AssetSources'
if ($sourcePath -eq $destinationPath) {
    throw 'SourceArchive and DestinationArchive must be different files.'
}
if (Test-Path -LiteralPath $destinationPath) {
    throw "Destination archive already exists: $destinationPath"
}
if (-not (Test-Path -LiteralPath $assetSourcesPath -PathType Container)) {
    throw "Runtime asset sources are missing: $assetSourcesPath"
}

function Test-IsDevelopmentEntry {
    param([string]$EntryName)

    $isRedundantSpriteSource = $EntryName.StartsWith(
        'AgesOfCalradia/GUI/SpriteParts/',
        [StringComparison]::OrdinalIgnoreCase) -and
        -not $EntryName.Equals(
            'AgesOfCalradia/GUI/SpriteParts/Config.xml',
            [StringComparison]::OrdinalIgnoreCase) -and
        -not $EntryName.StartsWith(
            'AgesOfCalradia/GUI/SpriteParts/ui_world_calendar/',
            [StringComparison]::OrdinalIgnoreCase)

    return $isRedundantSpriteSource -or $EntryName.StartsWith(
        'AgesOfCalradia/GUI/CustomUI/',
        [StringComparison]::OrdinalIgnoreCase) -or
        $EntryName -match '(?i)(\.bak$|\.before-|\.backup-)'
}

$inputArchive = [IO.Compression.ZipFile]::OpenRead($sourcePath)
$outputArchive = [IO.Compression.ZipFile]::Open(
    $destinationPath,
    [IO.Compression.ZipArchiveMode]::Create)
try {
    $writtenEntries = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $inputArchive.Entries) {
        if (Test-IsDevelopmentEntry -EntryName $entry.FullName) {
            continue
        }

        $newEntry = $outputArchive.CreateEntry(
            $entry.FullName,
            [IO.Compression.CompressionLevel]::Optimal)
        [void]$writtenEntries.Add($entry.FullName)
        $newEntry.LastWriteTime = $entry.LastWriteTime
        if ([string]::IsNullOrEmpty($entry.Name)) {
            continue
        }

        $sourceStream = $entry.Open()
        $destinationStream = $newEntry.Open()
        try {
            $sourceStream.CopyTo($destinationStream)
        }
        finally {
            $destinationStream.Dispose()
            $sourceStream.Dispose()
        }
    }

    foreach ($assetSource in Get-ChildItem -LiteralPath $assetSourcesPath -Recurse -File) {
        $relativePath = $assetSource.FullName.Substring($assetSourcesPath.Length).
            TrimStart('\', '/').Replace('\', '/')
        $entryName = "AgesOfCalradia/AssetSources/$relativePath"
        if (-not $writtenEntries.Add($entryName)) {
            continue
        }

        $newEntry = $outputArchive.CreateEntry(
            $entryName,
            [IO.Compression.CompressionLevel]::Optimal)
        $newEntry.LastWriteTime = $assetSource.LastWriteTime
        $sourceStream = $assetSource.OpenRead()
        $destinationStream = $newEntry.Open()
        try {
            $sourceStream.CopyTo($destinationStream)
        }
        finally {
            $destinationStream.Dispose()
            $sourceStream.Dispose()
        }
    }
}
finally {
    $outputArchive.Dispose()
    $inputArchive.Dispose()
}

$archive = [IO.Compression.ZipFile]::OpenRead($destinationPath)
try {
    $entryNames = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        [void]$entryNames.Add($entry.FullName)
        if (Test-IsDevelopmentEntry -EntryName $entry.FullName) {
            throw "Development entry remained in player archive: $($entry.FullName)"
        }
    }

    $spriteDataEntry = $archive.GetEntry(
        'AgesOfCalradia/GUI/Ages Of CalradiaSpriteData.xml')
    if ($null -eq $spriteDataEntry) {
        throw 'Player archive is missing Ages Of CalradiaSpriteData.xml.'
    }

    $reader = [IO.StreamReader]::new($spriteDataEntry.Open())
    try {
        [xml]$spriteData = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $requiredDirectTextures = @(
        'strategic_map_atlas.png',
        'strategic_province_index.png',
        'strategic_city_labels.png',
        'strategic_marker_town.png',
        'strategic_marker_castle.png'
    ) | ForEach-Object {
        "AgesOfCalradia/GUI/SpriteParts/ui_world_calendar/$_"
    }
    $missingDirectTextures = @($requiredDirectTextures | Where-Object {
        -not $entryNames.Contains($_)
    })
    if ($missingDirectTextures.Count -gt 0) {
        throw "Player archive is missing directly loaded textures: $($missingDirectTextures -join ', ')"
    }

    $missingCategoryAssets = @()
    foreach ($category in $spriteData.SpriteData.SpriteCategories.SpriteCategory) {
        for ($sheetId = 1; $sheetId -le [int]$category.SpriteSheetCount; $sheetId++) {
            $atlasEntry = 'AgesOfCalradia/AssetSources/GauntletUI/{0}_{1}.png' -f
                [string]$category.Name,
                $sheetId
            $tpacEntry = 'AgesOfCalradia/Assets/GauntletUI/{0}_{1}_tex.tpac' -f
                [string]$category.Name,
                $sheetId
            if (-not $entryNames.Contains($atlasEntry)) {
                $missingCategoryAssets += $atlasEntry
            }
            if (-not $entryNames.Contains($tpacEntry)) {
                $missingCategoryAssets += $tpacEntry
            }
        }
    }
    if ($missingCategoryAssets.Count -gt 0) {
        throw "Player archive is missing declared category assets: $($missingCategoryAssets -join ', ')"
    }

    [pscustomobject]@{
        Archive = $destinationPath
        Entries = $archive.Entries.Count
        SizeMB = [Math]::Round((Get-Item -LiteralPath $destinationPath).Length / 1MB, 2)
        DeclaredSpriteParts = @($spriteData.SpriteData.SpriteParts.SpritePart).Count
        MissingDirectTextures = $missingDirectTextures.Count
        MissingCategoryAssets = $missingCategoryAssets.Count
        SHA256 = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
    }
}
finally {
    $archive.Dispose()
}
