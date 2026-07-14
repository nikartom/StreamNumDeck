[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$layout = Join-Path $repositoryRoot "src\StreamNumDeck.App\bin\$Configuration\net10.0-windows10.0.26100.0\win-x64"
$manifest = Join-Path $layout 'AppxManifest.xml'

if (-not (Test-Path -LiteralPath $manifest)) {
    throw "The package layout does not exist. Run scripts\build.ps1 first: $manifest"
}

Get-Process StreamNumDeck -ErrorAction SilentlyContinue | Stop-Process

$sourceManifestPath = Join-Path $repositoryRoot 'src\StreamNumDeck.App\Package.appxmanifest'
[xml] $sourceManifest = Get-Content -LiteralPath $sourceManifestPath
$packageName = [string] $sourceManifest.Package.Identity.Name
$package = Get-AppxPackage -Name $packageName
$resolvedLayout = (Resolve-Path -LiteralPath $layout).Path

if ($null -ne $package) {
    $package | Remove-AppxPackage -PreserveApplicationData
}

Add-AppxPackage -Register $manifest -ForceApplicationShutdown

$registeredPackage = Get-AppxPackage -Name $packageName
if ($null -eq $registeredPackage -or $registeredPackage.InstallLocation -ne $resolvedLayout) {
    throw "The current package layout was not registered correctly."
}

Write-Host "Registered package layout: $resolvedLayout"
