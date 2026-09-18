param([string]$GameDir = 'F:\1.40.8', [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'obj\Tests'
New-Item -ItemType Directory -Force $out | Out-Null
$argsList = @('/nologo','/nostdlib+','/target:exe','/langversion:latest','/optimize+','/warn:4','/warnaserror+',('/out:"' + $out + '\Settings.Tests.exe"'))
foreach($f in Get-ChildItem -LiteralPath (Join-Path $GameDir 'Beat Saber_Data\Managed') -Filter *.dll) { $argsList += '/r:"' + $f.FullName + '"' }
$argsList += '/r:"' + (Join-Path $GameDir 'Plugins\BSML.dll') + '"'
$argsList += '"' + (Join-Path $PSScriptRoot 'SettingsIntegrationTests.cs') + '"'
$rsp = Join-Path $out 'settings-test.rsp'
$argsList | Set-Content -LiteralPath $rsp -Encoding utf8
& $Compiler /noconfig ('@' + $rsp)
if($LASTEXITCODE -ne 0) { throw 'Settings test compilation failed' }
# BSML uses String.Split(char, StringSplitOptions), available in Unity's runtime
# and .NET 8, but not the desktop .NET Framework test host.
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content (Join-Path $out 'Settings.Tests.runtimeconfig.json')
& dotnet (Join-Path $out 'Settings.Tests.exe') $GameDir (Join-Path $root 'dist\Plugins\Laki.dll') | Tee-Object -FilePath (Join-Path $out 'settings-results.txt')
if($LASTEXITCODE -ne 0) { throw 'Settings integration tests failed' }
