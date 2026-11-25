param([string]$ISCCPath = '', [string]$MSBuildPath = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'InstallerTools.psm1') -Force
$compiler = Find-InstallerCompiler $ISCCPath
# A unique output keeps packaging independent of a running developer executable.
$build = Join-Path $root ('Output/.build/'+[Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -RunTests -MSBuildPath $MSBuildPath -OutputDirectory $build
$stage = New-InstallerPayload $root $build
$output = Join-Path $root 'Output'
$null = [IO.Directory]::CreateDirectory($output)
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'LicenseKeyGenerator.exe')).FileVersion
& $compiler '/Qp' "/DMainBin=$stage" "/DLegacyBin=$stage" "/DDocsDir=$stage" "/DPackageOutput=$output" (Join-Path $root 'Installer/LicenseKeyGenerator.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$exe = Join-Path $output "LicenseKeyGenerator-Setup-$version.exe"
if (!(Test-Path -LiteralPath $exe)) { throw 'Expected installer was not created.' }
$inventory = @(Get-ChildItem -LiteralPath $stage -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ name=$_.Name; bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$report = [ordered]@{ version=$version; builtUtc=[DateTime]::UtcNow.ToString('o'); installer=[IO.Path]::GetFileName($exe); installerSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash; files=$inventory }
[IO.File]::WriteAllText(($exe+'.inventory.json'), ($report | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText(($exe+'.sha256'), $report.installerSha256+'  '+[IO.Path]::GetFileName($exe)+[Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output "Installer ready: $exe"
