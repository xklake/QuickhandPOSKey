Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-InstallerCompiler([string]$ExplicitPath) {
    if (!('Quickhand.ReleaseBuild.InnoCompilerLocator' -as [type])) {
        Add-Type -Path (Join-Path $PSScriptRoot '../LicenseKeyGenerator/InnoCompilerLocator.cs')
    }
    $compiler = [Quickhand.ReleaseBuild.InnoCompilerLocator]::Find($ExplicitPath)
    if (!$compiler) { throw 'Install Inno Setup 6 or 7, or supply -ISCCPath with the path to ISCC.exe.' }
    return $compiler
}

function New-InstallerPayload([string]$Root, [string]$BinaryDirectory = '') {
    $main = if ($BinaryDirectory) { $BinaryDirectory } else { Join-Path $Root 'LicenseKeyGenerator/bin/Release' }
    $legacy = if ($BinaryDirectory) { $BinaryDirectory } else { Join-Path $Root 'KeyGenerator/bin/Release' }
    $files = [ordered]@{
        'LicenseKeyGenerator.exe' = Join-Path $main 'LicenseKeyGenerator.exe'
        'LicenseKeyGenerator.exe.config' = Join-Path $main 'LicenseKeyGenerator.exe.config'
        'LicenseKeyGenerator.Core.dll' = Join-Path $main 'LicenseKeyGenerator.Core.dll'
        'Newtonsoft.Json.dll' = Join-Path $main 'Newtonsoft.Json.dll'
        'KeyGenerator.exe' = Join-Path $legacy 'KeyGenerator.exe'
        'KeyGenerator.exe.config' = Join-Path $legacy 'KeyGenerator.exe.config'
        'Usage.html' = Join-Path $Root 'Installer/Usage.html'
        'ThirdPartyNotices.txt' = Join-Path $Root 'Installer/ThirdPartyNotices.txt'
    }
    foreach ($file in $files.Values) { if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing installer input: $file. Build Release first." } }
    foreach ($name in @('LicenseKeyGenerator.Core.dll','Newtonsoft.Json.dll')) {
        if ((Get-FileHash -LiteralPath (Join-Path $main $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $legacy $name)).Hash) {
            throw "The two tools have different $name builds. Rebuild the complete solution."
        }
    }
    $stage = Join-Path $Root ('Output/.staging/'+[Guid]::NewGuid().ToString('N'))
    $null = [IO.Directory]::CreateDirectory($stage)
    foreach ($entry in $files.GetEnumerator()) { Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $stage $entry.Key) }
    return $stage
}

Export-ModuleMember -Function Find-InstallerCompiler, New-InstallerPayload
