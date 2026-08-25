param(
    [Parameter(Mandatory = $true)]
    [string]$ModuleRoot
)

$ErrorActionPreference = 'Stop'
[xml]$manifest = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'SubModule.xml')
if ($manifest.Module.Id.value -ne 'AgesOfCalradia' -or
    $manifest.Module.Version.value -ne 'v1.5.12') {
    throw 'The v1.5.12 package manifest has the wrong module identity or version.'
}

$bin = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client'
$main = Join-Path $bin 'AgesOfCalradia.dll'
$sidecar = Join-Path $bin 'AgesOfCalradia.Approved560CalendarFixes.dll'
$labels = Join-Path $bin 'AgesOfCalradia.CampaignLabelVisibility.dll'
foreach ($required in @($main, $sidecar, $labels)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing required v1.5.12 assembly: $required"
    }
}

$mainHash = (Get-FileHash -LiteralPath $main -Algorithm SHA256).Hash
if ($mainHash -ne '560F1B5181F8CC2EFE51564D8675FD3089E722606FA55B0B166D36ECD9868D8E') {
    throw "The approved political-renderer DLL changed: $mainHash"
}
$labelHash = (Get-FileHash -LiteralPath $labels -Algorithm SHA256).Hash
if ($labelHash -ne '59F9773D7F0B224FCA0109D0BAC8C9FECCD5ECC3663A0D683D0B6E97D06FDCD5') {
    throw "The approved campaign-label DLL changed: $labelHash"
}

$forbidden = @(Get-ChildItem -LiteralPath $ModuleRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.log') -or
    $_.Name -match 'Diagnostic|RuntimeEconomyDiagnostics'
})
if ($forbidden.Count -ne 0) {
    throw 'The production package contains diagnostics, logs, or symbols.'
}

$sidecarVersion = [Reflection.AssemblyName]::GetAssemblyName($sidecar).Version
if ($sidecarVersion.ToString() -ne '1.5.12.0') {
    throw "Unexpected v1.5.12 sidecar version: $sidecarVersion"
}

Write-Output "PASS: v1.5.12 package is slim, production-only, and preserves the approved visual DLLs. Sidecar SHA-256=$((Get-FileHash -LiteralPath $sidecar -Algorithm SHA256).Hash)"
