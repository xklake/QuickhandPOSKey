param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$RunTests,
    [string]$MSBuildPath = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
    }
}
if (!$MSBuildPath -or !(Test-Path -LiteralPath $MSBuildPath)) { throw 'Install Visual Studio with .NET desktop development and the .NET Framework 4.8 targeting pack, or supply -MSBuildPath.' }
& $MSBuildPath (Join-Path $root 'LicenseKeyGenerator.sln') /restore /t:Build "/p:Configuration=$Configuration" '/p:Platform=Any CPU' /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'License tools build failed.' }
if ($RunTests) { & (Join-Path $PSScriptRoot 'Test.ps1') -Configuration $Configuration -CompilerPath (Join-Path (Split-Path -Parent $MSBuildPath) 'Roslyn/csc.exe') }
Write-Output "Ready: $(Join-Path $root "LicenseKeyGenerator/bin/$Configuration/LicenseKeyGenerator.exe")"
