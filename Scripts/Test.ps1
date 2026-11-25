param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$CompilerPath = '',
    [string]$BinaryDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$binary = if ($BinaryDirectory) { $BinaryDirectory } else { Join-Path $root "LicenseKeyGenerator/bin/$Configuration" }
if (!$CompilerPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $CompilerPath = & $vswhere -latest -products '*' -find 'MSBuild/**/Bin/Roslyn/csc.exe' | Select-Object -First 1
}
if (!$CompilerPath) { throw 'C# compiler not found. Run Scripts/Build.ps1 -RunTests first.' }
$output = Join-Path $root ('TestResults/'+$Configuration+'/'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($output) | Out-Null
foreach ($name in @('LicenseKeyGenerator.exe','LicenseKeyGenerator.Core.dll','Newtonsoft.Json.dll')) {
    Copy-Item -LiteralPath (Join-Path $binary $name) -Destination $output
}
$exe = Join-Path $output 'StandaloneChecks.exe'
$arguments = @('/nologo','/target:exe',('/out:'+$exe),'/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Xml.dll','/r:System.Security.dll')
foreach ($name in @('LicenseKeyGenerator.exe','LicenseKeyGenerator.Core.dll','Newtonsoft.Json.dll')) { $arguments += '/r:'+(Join-Path $output $name) }
$arguments += Join-Path $root 'Tests/StandaloneChecks.cs'
$arguments += Join-Path $root 'Tests/StorageChecks.cs'
& $CompilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Standalone checks compilation failed.' }
Copy-Item -LiteralPath (Join-Path $binary 'LicenseKeyGenerator.exe.config') -Destination ($exe+'.config')
& $exe $root $output
if ($LASTEXITCODE -ne 0) { throw 'Standalone checks failed.' }
Write-Output "Test artifacts: $output"
