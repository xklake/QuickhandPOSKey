param([Parameter(Mandatory=$true)][string]$BinaryDirectory, [string]$ISCCPath = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'InstallerTools.psm1') -Force
$compiler = Find-InstallerCompiler $ISCCPath
$payload = New-InstallerPayload $root $BinaryDirectory
$run = Join-Path $root ('TestResults/Installer/'+[Guid]::NewGuid().ToString('N'))
$install = Join-Path $run 'Installed tool'
$null = [IO.Directory]::CreateDirectory($run)
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'LicenseKeyGenerator.exe')).FileVersion
& $compiler /Qp /DTestPackage=1 "/DMainBin=$payload" "/DLegacyBin=$payload" "/DDocsDir=$payload" "/DPackageOutput=$run" (Join-Path $root 'Installer/LicenseKeyGenerator.iss')
if ($LASTEXITCODE -ne 0) { throw 'Isolated installer compilation failed.' }
$setup = Join-Path $run "LicenseKeyGenerator-Test-$version.exe"
$script:checks = 0
function Check([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
    Write-Output "PASS: $message"
}
function Run-Setup([string]$label) {
    # This test flavor has a different AppId, no shortcuts and no uninstall registry.
    # Every write is under this run's unique directory, not a working installation.
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/NORESTARTAPPLICATIONS','/NOICONS',('/DIR="'+$install+'"'),('/LOG="'+(Join-Path $run ($label+'.log'))+'"'))
    $process = Start-Process -FilePath $setup -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try {
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw "Test installer timed out: $label" }
        return $process.ExitCode
    } finally { $process.Dispose() }
}
function Verify-Payload {
    foreach ($file in Get-ChildItem -LiteralPath $payload -File) {
        $installed = Join-Path $install $file.Name
        Check ((Test-Path -LiteralPath $installed) -and ((Get-FileHash -LiteralPath $installed).Hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash)) ("Installed content matches: "+$file.Name)
    }
}

Check ((Run-Setup 'initial-install') -eq 0) 'isolated fresh installation succeeded'
Verify-Payload
$expected = @(Get-ChildItem -LiteralPath $payload -File | Select-Object -ExpandProperty Name)
$unexpected = @(Get-ChildItem -LiteralPath $install -File | Where-Object { $_.Name -notin $expected -and $_.Name -notmatch '^unins\d+\.(exe|dat|msg)$' })
Check ($unexpected.Count -eq 0) 'only explicit program files and uninstaller are installed'
Check (!(Test-Path -LiteralPath (Join-Path $install 'license.key')) -and !(Get-ChildItem -LiteralPath $install -Filter '*.pfx')) 'installer contains no customer license or private key'

# Run the real independent sign/verify/UI checks with the installed binaries.
& (Join-Path $PSScriptRoot 'Test.ps1') -Configuration Release -BinaryDirectory $install
Check ($LASTEXITCODE -eq 0) 'installed assemblies pass independent issuance and UI checks'

# Fake user data only. Reinstall and uninstall must leave these bytes unchanged.
$kept = [ordered]@{ 'license.key'='synthetic customer data'; 'operator.pfx'='not a real private key'; 'license-issuance.jsonl'='synthetic history'; 'operator-notes.txt'='operator notes' }
foreach ($entry in $kept.GetEnumerator()) { [IO.File]::WriteAllText((Join-Path $install $entry.Key), $entry.Value) }

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$csc = & $vswhere -latest -products '*' -find 'MSBuild/**/Bin/Roslyn/csc.exe' | Select-Object -First 1
function Set-VersionFixture([string]$fixtureVersion) {
    $source = Join-Path $run 'VersionFixture.cs'
    [IO.File]::WriteAllText($source, 'using System.Reflection; [assembly:AssemblyFileVersion("'+$fixtureVersion+'")] class Program { static void Main() {} }')
    & $csc /nologo /target:winexe ('/out:'+(Join-Path $install 'LicenseKeyGenerator.exe')) $source
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile version fixture.' }
}
Set-VersionFixture '0.9.0.0'
[IO.File]::WriteAllText((Join-Path $install 'Usage.html'), 'old help fixture')
Check ((Run-Setup 'upgrade') -eq 0) 'upgrade from an older version fixture succeeded'
Verify-Payload
foreach ($entry in $kept.GetEnumerator()) { Check ([IO.File]::ReadAllText((Join-Path $install $entry.Key)) -ceq $entry.Value) ('upgrade preserves '+$entry.Key) }

Set-VersionFixture '65535.65535.65535.65535'
$before = (Get-FileHash -LiteralPath (Join-Path $install 'LicenseKeyGenerator.exe')).Hash
Check ((Run-Setup 'reject-downgrade') -ne 0) 'downgrade to an older application version is blocked'
Check ((Get-FileHash -LiteralPath (Join-Path $install 'LicenseKeyGenerator.exe')).Hash -eq $before) 'blocked downgrade leaves the installed executable unchanged'
Copy-Item -LiteralPath (Join-Path $payload 'LicenseKeyGenerator.exe') -Destination (Join-Path $install 'LicenseKeyGenerator.exe') -Force

$running = [Threading.Mutex]::new($false, 'Local\QuickhandPOS-LicenseKeyGenerator')
try { Check ((Run-Setup 'reject-running') -ne 0) 'installer asks for the running tool to be closed instead of replacing it' }
finally { $running.Dispose() }

$uninstall = @(Get-ChildItem -LiteralPath $install -Filter 'unins*.exe' -File)
Check ($uninstall.Count -eq 1) 'upgrade reuses one uninstall log'
$process = Start-Process -FilePath $uninstall[0].FullName -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $run 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru
try {
    if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Test uninstall timed out.' }
    Check ($process.ExitCode -eq 0) 'isolated uninstall succeeded'
} finally { $process.Dispose() }
foreach ($name in $expected) { Check (!(Test-Path -LiteralPath (Join-Path $install $name))) ('uninstall removes '+$name) }
foreach ($entry in $kept.GetEnumerator()) { Check ([IO.File]::ReadAllText((Join-Path $install $entry.Key)) -ceq $entry.Value) ('uninstall preserves '+$entry.Key) }
Write-Output "PASS: $script:checks installer checks. Logs: $run"
