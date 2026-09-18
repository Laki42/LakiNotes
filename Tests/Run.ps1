param([string]$GameDir = 'F:\1.40.8', [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'obj\Tests'
New-Item -ItemType Directory -Force $out | Out-Null
$fixtures = Join-Path $out 'Fixtures'
New-Item -ItemType Directory -Force $fixtures | Out-Null
foreach($name in @('BeatLeader','ScoreSaber')) {
    & $Compiler /nologo /target:library /warnaserror+ ('/out:' + (Join-Path $fixtures "$name.dll")) (Join-Path $PSScriptRoot ($name + 'Fixture.cs'))
    if($LASTEXITCODE -ne 0) { throw 'Replay fixture compilation failed' }
}
$argsList = @('/nologo','/nostdlib+','/target:exe','/langversion:latest','/optimize+','/warnaserror+',('/out:"' + $out + '\Laki.Tests.exe"'))
foreach($f in Get-ChildItem -LiteralPath (Join-Path $GameDir 'Beat Saber_Data\Managed') -Filter *.dll) { $argsList += '/r:"' + $f.FullName + '"' }
foreach($name in @('LakiRules.cs','LakiState.cs','LakiSelector.cs','ReplayGuard.cs','LakiGeometry.cs','VisualScale.cs','GameplayInstallation.cs','MultiplayerPlusGuard.cs')) { $argsList += '"' + (Join-Path $root "Source\$name") + '"' }
$argsList += '"' + (Join-Path $PSScriptRoot 'RulesAndSelectionTests.cs') + '"'
$argsList += '"' + (Join-Path $PSScriptRoot 'GeometryTests.cs') + '"'
$argsList += '"' + (Join-Path $PSScriptRoot 'ModifierTests.cs') + '"'
$argsList += '"' + (Join-Path $PSScriptRoot 'MultiplayerInstallationTests.cs') + '"'
$argsList += '/r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll"'
$rsp = Join-Path $out 'test.rsp'
$argsList | Set-Content -LiteralPath $rsp -Encoding utf8
& $Compiler /noconfig ('@' + $rsp)
if($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& (Join-Path $out 'Laki.Tests.exe') $GameDir | Tee-Object -FilePath (Join-Path $out 'results.txt')
if($LASTEXITCODE -ne 0) { throw 'Tests failed' }
