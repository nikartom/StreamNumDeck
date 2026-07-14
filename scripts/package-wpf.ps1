param(
    [string]$Configuration = "Release",
    [string]$Version = "0.2.0-alpha.1",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$project = Join-Path $repositoryRoot "src\StreamNumDeck.Wpf\StreamNumDeck.Wpf.csproj"
$buildOutput = Join-Path $repositoryRoot "src\StreamNumDeck.Wpf\bin\$Configuration\net48"
$artifacts = Join-Path $repositoryRoot "artifacts\wpf"
$portableRoot = Join-Path $artifacts "StreamNumDeck-$Version-portable"
$artifactsFullPath = [System.IO.Path]::GetFullPath($artifacts).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$portableFullPath = [System.IO.Path]::GetFullPath($portableRoot)
if (-not $portableFullPath.StartsWith($artifactsFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Portable staging path escaped the repository artifacts directory."
}

& $dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "WPF build failed."
}

if (Test-Path $portableRoot) {
    Remove-Item -LiteralPath $portableRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null

Get-ChildItem -LiteralPath $buildOutput -File |
    Where-Object Extension -NotIn ".pdb", ".xml" |
    Copy-Item -Destination $portableRoot

$zipPath = Join-Path $artifacts "StreamNumDeck-$Version-portable.zip"
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $portableRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

if (-not $SkipInstaller) {
    $compiler = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $compiler)) {
        throw "Inno Setup 6 is required to build Setup.exe."
    }

    & $compiler "/DMyAppVersion=$Version" "/DMySourceDir=$portableRoot" (Join-Path $repositoryRoot "installer\StreamNumDeck.Wpf.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "Installer build failed."
    }
}

$portableSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "Portable: $zipPath ($portableSize MB)"
if (-not $SkipInstaller) {
    $setupPath = Join-Path $artifacts "StreamNumDeck-$Version-Setup.exe"
    $setupSize = [math]::Round((Get-Item $setupPath).Length / 1MB, 2)
    Write-Host "Installer: $setupPath ($setupSize MB)"
}
