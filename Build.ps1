param(
    [string]$GameDir = 'F:\1.40.8',
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe',
    [switch]$DiagnosticNoGameplay
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$versionFile = Join-Path $GameDir 'BeatSaberVersion.txt'
if (!(Test-Path -LiteralPath $versionFile) -or (Get-Content -LiteralPath $versionFile -Raw).Trim() -notin @('1.40.8','1.40.8_7379')) {
    throw 'Supply a verified Beat Saber 1.40.8 / 1.40.8_7379 directory. Game files are read only.'
}
if (!(Test-Path -LiteralPath $Compiler)) { throw 'Roslyn csc.exe was not found. Specify -Compiler.' }
$buildDir = Join-Path $projectRoot 'obj\Release'
$distDir = Join-Path $projectRoot 'dist\Plugins'
if ($DiagnosticNoGameplay) {
    $buildDir = Join-Path $projectRoot 'obj\DiagnosticNoGameplay'
    $distDir = Join-Path $projectRoot 'diagnostic-no-gameplay\Plugins'
}
New-Item -ItemType Directory -Force $buildDir, $distDir | Out-Null
$refs = @{}
Get-ChildItem -LiteralPath (Join-Path $GameDir 'Beat Saber_Data\Managed') -Filter *.dll | ForEach-Object { $refs[$_.Name] = $_.FullName }
foreach ($relative in @('Plugins\SiraUtil.dll','Plugins\BSML.dll','Libs\Hive.Versioning.dll')) {
    $file = Get-Item -LiteralPath (Join-Path $GameDir $relative)
    $refs[$file.Name] = $file.FullName
}
$arguments = @('/nologo','/nostdlib+','/target:library','/langversion:latest','/optimize+','/deterministic+','/warn:4','/warnaserror+',
    ('/out:"' + (Join-Path $distDir 'Laki.dll') + '"'),
    ('/resource:"' + (Join-Path $projectRoot 'manifest.json') + '",Laki.manifest.json'),
    ('/resource:"' + (Join-Path $projectRoot 'Resources\Settings.bsml') + '",Laki.Settings.bsml'))
foreach ($reference in ($refs.Values | Sort-Object)) { $arguments += '/reference:"' + $reference + '"' }
if ($DiagnosticNoGameplay) { $arguments += '/define:LAKI_NO_GAMEPLAY' }
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Source') -Filter *.cs | Sort-Object Name | ForEach-Object { $arguments += '"' + $_.FullName + '"' }
$response = Join-Path $buildDir 'compile.rsp'
$arguments | Set-Content -LiteralPath $response -Encoding utf8
& $Compiler /noconfig ('@' + $response) 2>&1 | Tee-Object -FilePath (Join-Path $buildDir 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Release build failed ($LASTEXITCODE)" }
Get-FileHash -LiteralPath (Join-Path $distDir 'Laki.dll') -Algorithm SHA256
# No post-build deployment. Never copy to GameDir, Plugins or UserData outside this project.
