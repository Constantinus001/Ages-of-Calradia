param([string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot))
$itemFile = Join-Path $ModuleRoot 'ModuleData\supply_items.xml'
$moduleFile = Join-Path $ModuleRoot 'SubModule.xml'
[xml]$items = Get-Content -Raw $itemFile
[xml]$module = Get-Content -Raw $moduleFile
$supply = @($items.Items.Item | Where-Object { $_.id -eq 'aoc_logistics_supply' })
if ($supply.Count -ne 1) { throw 'Expected exactly one aoc_logistics_supply item.' }
if ($supply[0].mesh -ne 'crate_a' -or $supply[0].is_merchandise -ne 'true' -or $supply[0].Type -ne 'Goods') {
    throw 'Supply must be a merchantable Goods item using the crate_a mesh.'
}
$dependency = @($module.Module.DependedModules.DependedModule | Where-Object { $_.Id -eq 'AgesOfCalradia' })
if ($dependency.Count -ne 1) { throw 'The logistics module must explicitly load after AgesOfCalradia.' }
if (-not (Test-Path (Join-Path $ModuleRoot 'LogisticsReserveBehavior.cs'))) {
    throw 'The finite reserve behaviour is missing.'
}
$behavior = Get-Content -Raw (Join-Path $ModuleRoot 'LogisticsReserveBehavior.cs')
if ($behavior -notmatch 'DailyTickTownEvent' -or $behavior -notmatch 'aoc_logistics_load_baggage') {
    throw 'Market restocking and the baggage-loading town option are required.'
}
if (-not (Test-Path (Join-Path $ModuleRoot 'BaggageTrainMissionBehavior.cs'))) {
    throw 'The battlefield baggage train behaviour is missing.'
}
$battleBehavior = Get-Content -Raw (Join-Path $ModuleRoot 'BaggageTrainMissionBehavior.cs')
if ($battleBehavior -notmatch 'SupplyRadiusMeters = 6' -or $battleBehavior -notmatch 'BaggageTrainRegistry.Register') {
    throw 'A six-metre baggage supply range is required.'
}
if ($battleBehavior -notmatch 'ComputeSpawnPathDeploymentOffset' -or $battleBehavior -notmatch 'frame.IsValid') {
    throw 'Baggage trains must use a validated vanilla deployment frame.'
}
if ($battleBehavior -notmatch 'RearDeploymentDistanceMeters = 20f' -or $battleBehavior -notmatch 'WagonCount = 12' -or $battleBehavior -notmatch 'GroundSupplyPileCount = 8' -or $battleBehavior -notmatch 'IntactWagonPrefabNames' -or $battleBehavior -notmatch 'bd_hay_cart_b') {
    throw 'Baggage trains must be placed behind deployment with a larger wagon footprint.'
}
if (-not (Test-Path (Join-Path $ModuleRoot 'BaggageResupplyMissionBehavior.cs'))) {
    throw 'The finite ammunition resupply behaviour is missing.'
}
if (-not (Test-Path (Join-Path $ModuleRoot 'BaggageGuardMissionBehavior.cs'))) {
    throw 'The baggage guard behaviour is missing.'
}
$resupplyBehavior = Get-Content -Raw (Join-Path $ModuleRoot 'BaggageResupplyMissionBehavior.cs')
if ($resupplyBehavior -notmatch 'RoundsPerReservePoint = 3' -or $resupplyBehavior -notmatch 'TryConsumeReserve') {
    throw 'Battle resupply must transfer finite reserve into ammunition.'
}
if (-not (Test-Path (Join-Path $ModuleRoot 'LogisticsDiagnostics.cs'))) {
    throw 'The logistics diagnostic logger is missing.'
}
$subModule = Get-Content -Raw (Join-Path $ModuleRoot 'LogisticsSubModule.cs')
if ($subModule -match 'mission != null && mission.IsFieldBattle') {
    throw 'Mission behaviours must be registered before Bannerlord assigns field-battle state.'
}
Write-Host 'Supply item and module dependency contract verified.'
