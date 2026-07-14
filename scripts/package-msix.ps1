[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string] $PackageVersion = '0.1.0.0',

    [ValidateSet('x64', 'x86', 'arm64')]
    [string] $Architecture = 'x64',

    [string] $CertificateThumbprint,

    [switch] $CreateDevelopmentCertificate,

    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$applicationProject = Join-Path $repositoryRoot 'src\StreamNumDeck.App\StreamNumDeck.App.csproj'
$sourceManifestPath = Join-Path $repositoryRoot 'src\StreamNumDeck.App\Package.appxmanifest'
$solution = Join-Path $repositoryRoot 'StreamNumDeck.slnx'
$runtimeIdentifier = "win-$Architecture"

$localDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

[xml] $projectXml = Get-Content -LiteralPath $applicationProject
$targetFramework = [string]($projectXml.Project.PropertyGroup.TargetFramework | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw 'The application target framework could not be read from the project.'
}

[xml] $sourceManifest = Get-Content -LiteralPath $sourceManifestPath
$identity = $sourceManifest.Package.Identity
$packageName = [string] $identity.Name
$publisher = [string] $identity.Publisher
$manifestVersion = [string] $identity.Version
if ($manifestVersion -ne $PackageVersion) {
    throw "Package version $PackageVersion does not match the manifest version $manifestVersion."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $releaseName = 'v' + ($PackageVersion -replace '\.0$', '')
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\release\$releaseName"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

& $dotnet restore $solution
if ($LASTEXITCODE -ne 0) {
    throw 'Package restore failed.'
}

& $dotnet test $solution --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw 'Release tests failed.'
}

& $dotnet build $applicationProject `
    --configuration Release `
    --runtime $runtimeIdentifier `
    --self-contained true `
    --no-restore `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) {
    throw 'Self-contained application build failed.'
}

$layout = Join-Path $repositoryRoot "src\StreamNumDeck.App\bin\Release\$targetFramework\$runtimeIdentifier"
$generatedManifestPath = Join-Path $layout 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $generatedManifestPath)) {
    throw "The MSIX layout was not generated: $generatedManifestPath"
}

[xml] $generatedManifest = Get-Content -LiteralPath $generatedManifestPath
if ([string]$generatedManifest.Package.Identity.Name -ne $packageName -or
    [string]$generatedManifest.Package.Identity.Publisher -ne $publisher -or
    [string]$generatedManifest.Package.Identity.Version -ne $PackageVersion -or
    [string]$generatedManifest.Package.Identity.ProcessorArchitecture -ne $Architecture) {
    throw 'The generated MSIX identity does not match the requested release identity.'
}

$stagingDirectory = [IO.Path]::GetFullPath((Join-Path $OutputDirectory ".staging-$Architecture"))
if (-not $stagingDirectory.StartsWith(
    $OutputDirectory + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The staging directory is outside the release output directory.'
}
if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stagingDirectory | Out-Null

& robocopy $layout $stagingDirectory /E /XD publish /XF *.pdb *.appxrecipe | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "Copying the MSIX layout failed with robocopy exit code $LASTEXITCODE."
}

$buildToolsPackage = Get-ChildItem `
    (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools') `
    -Directory `
    -ErrorAction Stop |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
$makeAppx = Get-ChildItem $buildToolsPackage.FullName -Recurse -Filter makeappx.exe |
    Where-Object { $_.Directory.Name -eq 'x64' } |
    Select-Object -First 1
$signTool = Get-ChildItem $buildToolsPackage.FullName -Recurse -Filter signtool.exe |
    Where-Object { $_.Directory.Name -eq 'x64' } |
    Select-Object -First 1
if ($null -eq $makeAppx -or $null -eq $signTool) {
    throw 'MakeAppx.exe or SignTool.exe was not found in Microsoft.Windows.SDK.BuildTools.'
}

$certificate = $null
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $normalizedThumbprint = $CertificateThumbprint.Replace(' ', '')
    $certificate = Get-Item "Cert:\CurrentUser\My\$normalizedThumbprint" -ErrorAction Stop
} elseif ($CreateDevelopmentCertificate) {
    $friendlyName = 'StreamNumDeck Development Signing'
    $certificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.FriendlyName -eq $friendlyName -and
            $_.Subject -eq $publisher -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(30)
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
    if ($null -eq $certificate) {
        $certificate = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $publisher `
            -FriendlyName $friendlyName `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -KeyAlgorithm RSA `
            -KeyLength 3072 `
            -HashAlgorithm SHA256 `
            -KeyUsage DigitalSignature `
            -KeyExportPolicy NonExportable `
            -NotAfter (Get-Date).AddYears(3) `
            -TextExtension @(
                '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
                '2.5.29.19={critical}{text}ca=false')
    }
} else {
    throw 'Pass -CertificateThumbprint or use -CreateDevelopmentCertificate for a beta package.'
}

if (-not $certificate.HasPrivateKey) {
    throw 'The signing certificate does not have an accessible private key.'
}
if ($certificate.Subject -ne $publisher) {
    throw "The certificate subject '$($certificate.Subject)' does not match '$publisher'."
}

$packageFileName = "StreamNumDeck_${PackageVersion}_${Architecture}.msix"
$packagePath = Join-Path $OutputDirectory $packageFileName
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

$makeAppxOutput = @(& $makeAppx.FullName pack /d $stagingDirectory /p $packagePath /o 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "MSIX package creation failed.`n$($makeAppxOutput -join [Environment]::NewLine)"
}

& $signTool.FullName sign /fd SHA256 /s My /sha1 $certificate.Thumbprint $packagePath
if ($LASTEXITCODE -ne 0) {
    throw 'MSIX package signing failed.'
}

$signature = Get-AuthenticodeSignature -LiteralPath $packagePath
if ($null -eq $signature.SignerCertificate -or
    $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The completed MSIX does not contain the expected signature.'
}

$publicCertificatePath = Join-Path $OutputDirectory 'StreamNumDeck-Development-Signing.cer'
Export-Certificate -Cert $certificate -FilePath $publicCertificatePath -Type CERT -Force | Out-Null

$hashPath = Join-Path $OutputDirectory 'SHA256SUMS.txt'
$releaseFiles = @($packagePath, $publicCertificatePath)
$hashLines = foreach ($releaseFile in $releaseFiles) {
    $hash = Get-FileHash -LiteralPath $releaseFile -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $releaseFile)"
}
[IO.File]::WriteAllText(
    $hashPath,
    ($hashLines -join "`r`n") + "`r`n",
    [Text.UTF8Encoding]::new($false))

Remove-Item -LiteralPath $stagingDirectory -Recurse -Force

[pscustomobject]@{
    Package = $packagePath
    PackageSizeBytes = (Get-Item -LiteralPath $packagePath).Length
    Certificate = $publicCertificatePath
    CertificateThumbprint = $certificate.Thumbprint
    SignatureStatus = $signature.Status
    Checksums = $hashPath
}
