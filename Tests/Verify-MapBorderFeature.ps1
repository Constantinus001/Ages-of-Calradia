param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$AssemblyPath = ''
)

$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $ModuleRoot 'bin\AgesOfCalradia_Test_Win64_Shipping_Client\AgesOfCalradia.dll'
}

$borderPath = Join-Path $ModuleRoot 'CampaignKingdomBorderBehavior.cs'
$subModulePath = Join-Path $ModuleRoot 'MySubModule.cs'
$projectPath = Join-Path $ModuleRoot 'TwelveMonthCalendar.csproj'
$ledgerPath = Join-Path $ModuleRoot 'CalendarWorldLedgerVM.cs'
$prefabPath = Join-Path $ModuleRoot 'GUI\Prefabs\WorldCalendar\WorldCalendar.xml'

Assert-True (Test-Path -LiteralPath $borderPath) 'Campaign border behavior source is missing.'
Assert-True (Test-Path -LiteralPath $AssemblyPath) "Compiled test assembly is missing: $AssemblyPath"

$borderSource = Get-Content -LiteralPath $borderPath -Raw
$subModuleSource = Get-Content -LiteralPath $subModulePath -Raw
$projectSource = Get-Content -LiteralPath $projectPath -Raw
$ledgerSource = Get-Content -LiteralPath $ledgerPath -Raw
$prefabSource = Get-Content -LiteralPath $prefabPath -Raw

Assert-True ($borderSource -match 'class CampaignKingdomBorderBehavior : CampaignBehaviorBase') 'Campaign border behavior is not a CampaignBehaviorBase.'
Assert-True ($borderSource -match 'OnSettlementOwnerChangedEvent' -and $borderSource -match 'DailyTickEvent') 'Campaign borders are missing ownership or daily refresh hooks.'
Assert-True ($borderSource -match 'ClipCell' -and $borderSource -match 'FindEdgeNeighbor') 'Campaign borders are missing settlement-cell boundary calculation.'
Assert-True ($borderSource -match 'GetHeightAtPoint' -and $borderSource -match 'vertex_color_mat') 'Campaign borders are not projected onto terrain with the native vertex-color material.'
Assert-True ($borderSource -match 'Mesh\.CreateMesh' -and $borderSource -match 'GameEntity\.CreateEmpty') 'Campaign borders are not rendered as runtime map-scene entities.'
Assert-True ($borderSource -notmatch 'ArtemsBetterUIVisuals|ArtemOwnsCampaignBorders') 'Campaign border rendering must remain owned by this module.'
Assert-True ($subModuleSource -match 'AddBehavior\(new CampaignKingdomBorderBehavior\(\)\)') 'Campaign border behavior is not registered in campaign startup.'
Assert-True ($projectSource -match 'Compile Include="CampaignKingdomBorderBehavior\.cs"') 'Campaign border behavior is not part of the main project.'
Assert-True ($ledgerSource -match 'GlowColor' -and $ledgerSource -match 'OnPropertyChangedWithValue\(GlowColor') 'Strategic settlement selection does not expose glow state changes.'
Assert-True ($prefabSource -match 'ImageWidget IsVisible="true"[^>]*Sprite="strategic_province_borders"') 'Strategic province border artwork is not enabled.'
Assert-True (([regex]::Matches($prefabSource, 'IsVisible="@IsSelected"[^>]*Color="@GlowColor"')).Count -eq 4) 'Strategic settlement selection does not render four glow border bars.'

$ildasm = 'C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools\x64\ildasm.exe'
if (Test-Path -LiteralPath $ildasm) {
    $assemblyText = & $ildasm $AssemblyPath /text
    Assert-True (@($assemblyText | Select-String 'CampaignKingdomBorderBehavior').Count -gt 0) 'Compiled test assembly does not contain the campaign border behavior.'
}

Write-Host 'Map border feature verification passed: campaign Voronoi renderer, terrain mesh path, strategic border overlay, and selected-settlement glow are present.'
