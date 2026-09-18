param([string]$GameDir = 'F:\1.40.8')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $GameDir 'Libs\Mono.Cecil.dll')
$bsml = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Plugins\BSML.dll'))
$main = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Beat Saber_Data\Managed\Main.dll'))
$core = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Beat Saber_Data\Managed\GameplayCore.dll'))
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'dist\Plugins\Laki.dll'))
function MethodBody($assembly, $type, $method) {
    $t = $assembly.MainModule.Types | Where-Object FullName -eq $type
    return (($t.Methods | Where-Object Name -eq $method).Body.Instructions | Where-Object { $null -ne $_ } | ForEach-Object { $_.ToString() }) -join "`n"
}
function Require($condition, $message) { if (!$condition) { throw $message }; "PASS: $message" }
$lines = @(
    Require ($bsml.Name.Version.ToString() -eq '1.12.5.0') 'BSML 1.12.5.0 actual assembly'
    $setup = MethodBody $bsml 'BeatSaberMarkupLanguage.Settings.SettingsMenu' 'Setup'
    Require ($setup.Contains('Utilities::GetResourceContent') -and $setup.Contains('BSMLParser::Parse') -and $setup.Contains('get_Host()')) 'Settings resource is passed to the real Parse method with the supplied Host'
    Require ($setup.Contains('Error adding settings menu') -and $setup.Contains('::Error') -and $setup.Contains('settings-error.bsml')) 'BSML deferred parser failure logs Error and builds its error page'
    $parse = MethodBody $bsml 'BeatSaberMarkupLanguage.BSMLParser' 'Parse'
    Require ($parse.Contains('post-parse') -and $parse.Contains('UIValue') -and $parse.Contains('BSMLPropertyValue')) 'Real parser harvests property bindings and emits post-parse'
    $scale = MethodBody $core 'GameplayModifiers' 'get_notesUniformScale'
    Require ($scale.Contains('get_smallCubes') -and $scale.Contains('ldc.r4 0.5') -and $scale.Contains('ldc.r4 1')) 'Small Notes scale = 0.5; normal = 1'
    $init = MethodBody $main 'NoteController' 'Init'
    Require ($init.Contains('::_uniformScale') -and $init.Contains('::_noteTransform') -and $init.Contains('Transform::set_localScale')) 'NoteController stores uniformScale and applies it to noteTransform'
    $pro = MethodBody $main 'BeatmapObjectsInstaller' 'InstallBindings'
    Require ($pro.Contains('get_proMode') -and $pro.Contains('::_proModeNotePrefab') -and $pro.Contains('BindMemoryPool<GameNoteController')) 'Pro Mode switches the GameNoteController prefab in the normal-note pool'
    $manager = $main.MainModule.Types | Where-Object FullName -eq 'BasicBeatmapObjectManager'
    $managerIL = ($manager.Methods.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n"
    Require ($managerIL.Contains('GameNoteController::Init') -and $managerIL.Contains('notesUniformScale')) 'Object manager passes modifier scale to GameNoteController.Init'
    $manifest = $plugin.MainModule.Resources | Where-Object Name -eq 'Laki.manifest.json'
    $json = [Text.Encoding]::UTF8.GetString($manifest.GetResourceData()) | ConvertFrom-Json
    Require ($json.id -eq 'Laki' -and $json.name -eq 'Laki Notes' -and $json.version -eq '0.1.4') 'Embedded manifest: stable id Laki, display name Laki Notes, version 0.1.4'
    'Reference DLL SHA256:'
    foreach($file in @('Plugins\BSML.dll','Plugins\SiraUtil.dll','Beat Saber_Data\Managed\IPA.Loader.dll','Beat Saber_Data\Managed\Main.dll','Beat Saber_Data\Managed\GameplayCore.dll')) {
        "$file  $((Get-FileHash -LiteralPath (Join-Path $GameDir $file)).Hash)"
    }
)
$lines | Tee-Object -FilePath (Join-Path $root 'obj\Release\api-contract.txt')
foreach($assembly in @($bsml,$main,$core,$plugin)) { $assembly.Dispose() }

