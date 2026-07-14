[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version = '1.0.1',

    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src\StreamNumDeck.Wpf\StreamNumDeck.Wpf.csproj'
$license = Join-Path $repositoryRoot 'LICENSE.md'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$releaseRoot = Join-Path $artifactsRoot 'release'
$stagingRoot = Join-Path $artifactsRoot ".package-wpf-$PID"
$portableRoot = Join-Path $stagingRoot 'StreamNumDeck'
$zipPath = Join-Path $releaseRoot "StreamNumDeck-$Version-portable.zip"
$setupPath = Join-Path $releaseRoot "StreamNumDeck-$Version-Setup.exe"

function Assert-ChildPath([string] $path, [string] $parent) {
    $resolvedPath = [System.IO.Path]::GetFullPath($path)
    $resolvedParent = [System.IO.Path]::GetFullPath($parent).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Packaging path escaped the artifacts directory: $resolvedPath"
    }
}

function Find-DotNet {
    $localDotNet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localDotNet) {
        return $localDotNet
    }

    return (Get-Command dotnet -ErrorAction Stop).Source
}

function Find-InnoCompiler {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

Assert-ChildPath $releaseRoot $artifactsRoot
Assert-ChildPath $stagingRoot $artifactsRoot

$numericVersion = ($Version -split '-', 2)[0]
$fileVersion = "$numericVersion.0"
$dotnet = Find-DotNet

Get-Process StreamNumDeck -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

try {
    & $dotnet publish $project `
        --configuration Release `
        --no-restore `
        --output $portableRoot `
        -p:Version=$Version `
        -p:AssemblyVersion=$fileVersion `
        -p:FileVersion=$fileVersion `
        -p:DebugSymbols=false `
        -p:DebugType=None
    if ($LASTEXITCODE -ne 0) {
        throw 'WPF publish failed.'
    }

    Get-ChildItem -LiteralPath $portableRoot -File |
        Where-Object Extension -In '.pdb', '.xml' |
        Remove-Item -Force
    Copy-Item -LiteralPath $license -Destination (Join-Path $portableRoot 'LICENSE.md')

    $applicationPath = Join-Path $portableRoot 'StreamNumDeck.exe'
    if (-not (Test-Path -LiteralPath $applicationPath)) {
        throw "Published application was not found: $applicationPath"
    }

    $publishedVersion = (Get-Item -LiteralPath $applicationPath).VersionInfo.FileVersion
    if (-not $publishedVersion.StartsWith($numericVersion, [System.StringComparison]::Ordinal)) {
        throw "Published application version '$publishedVersion' does not match '$numericVersion'."
    }

    Compress-Archive `
        -LiteralPath $portableRoot `
        -DestinationPath $zipPath `
        -CompressionLevel Optimal

    if (-not $SkipInstaller) {
        $compiler = Find-InnoCompiler
        if ([string]::IsNullOrWhiteSpace($compiler)) {
            throw 'Inno Setup 6 is required to build Setup.exe.'
        }

        & $compiler `
            "/DMyAppVersion=$Version" `
            "/DMyFileVersion=$fileVersion" `
            "/DMySourceDir=$portableRoot" `
            "/DMyOutputDir=$releaseRoot" `
            (Join-Path $repositoryRoot 'installer\StreamNumDeck.Wpf.iss')
        if ($LASTEXITCODE -ne 0) {
            throw 'Installer build failed.'
        }
    }

    $artifacts = @($zipPath)
    if (-not $SkipInstaller) {
        $artifacts += $setupPath
    }

    foreach ($artifact in $artifacts) {
        if (-not (Test-Path -LiteralPath $artifact)) {
            throw "Expected artifact was not created: $artifact"
        }

        $item = Get-Item -LiteralPath $artifact
        $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
        Write-Host ("{0} ({1:N2} MB)" -f $item.FullName, ($item.Length / 1MB))
        Write-Host "SHA256: $hash"
    }
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
