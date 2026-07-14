[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$localDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    & $dotnet restore StreamNumDeck.slnx
    if ($LASTEXITCODE -ne 0) {
        throw 'Package restore failed.'
    }

    & $dotnet build StreamNumDeck.slnx --no-restore --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed.'
    }

    & $dotnet test StreamNumDeck.slnx `
        --no-build `
        --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw 'Tests failed.'
    }
}
finally {
    Pop-Location
}
