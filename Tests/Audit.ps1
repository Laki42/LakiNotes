param([string]$GameDir = 'F:\1.40.8')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $GameDir 'Libs\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'dist\Plugins\Laki.dll'))
function Walk($types) { foreach($type in $types) { $type; Walk $type.NestedTypes } }
$errors = [Collections.Generic.List[string]]::new()
$calls = [Collections.Generic.HashSet[string]]::new()
foreach($type in (Walk $assembly.MainModule.Types)) {
    foreach($method in $type.Methods) {
        if (!$method.HasBody) { continue }
        foreach($instruction in $method.Body.Instructions) {
            $operand = $instruction.Operand
            if ($operand -is [Mono.Cecil.MethodReference]) {
                $null = $calls.Add($operand.FullName)
                if ($operand.DeclaringType.FullName -match '^(PauseController|PauseMenuManager|AudioTimeSyncController|GameScenesManager)$') {
                    $errors.Add("Unexpected pause/start/lifecycle controller call: $operand")
                }
                if ($operand.DeclaringType.FullName -match '^(System\.Net\.|HarmonyLib\.|ScoreController$|ComboController$|SaberManager$|IMultiplayerSessionManager$|IGameEnergyCounter$|BoxCuttableBySaber$|CuttableBySaber$|UnityEngine\..*Collider$)') {
                    $errors.Add("Unexpected gameplay/network call: $operand")
                }
                if ($operand.DeclaringType.FullName -eq 'NoteData' -and $operand.Name -notmatch '^get_') {
                    $errors.Add("NoteData modification: $operand")
                }
                if ($operand.DeclaringType.FullName -eq 'UnityEngine.Renderer' -and $operand.Name -eq 'get_material') {
                    $errors.Add('Implicit per-renderer material instantiation')
                }
            }
            if ($instruction.OpCode.Name -in @('stfld','stsfld') -and $operand -is [Mono.Cecil.FieldReference]) {
                if ($operand.DeclaringType.Scope -ne $assembly.MainModule -and
                    $operand.DeclaringType.FullName -notmatch '^UnityEngine\.(Vector[234]|Color)$') {
                    $errors.Add("External field write: $operand")
                }
            }
        }
    }
}
$resources = @($assembly.MainModule.Resources.Name)
if ('Laki.manifest.json' -notin $resources -or 'Laki.Settings.bsml' -notin $resources) { $errors.Add('Missing embedded resources') }
foreach($reference in $assembly.MainModule.AssemblyReferences) {
    if ($reference.Name -in @('BeatLeader','ScoreSaber','CustomJSONData','0Harmony','BeatSaberPlus_Multiplayer','ChatPlexSDK_BS','MultiplayerCore')) { $errors.Add('Unexpected hard optional dependency') }
}
if ($errors.Count -gt 0) { throw ($errors -join "`n") }
$lines = @('PASS: compiled assembly static audit',
    'No network, Harmony, score/combo/energy/saber mutation calls found.',
    'No external game field writes or NoteData mutator calls found.',
    'No Renderer.material getter; materials use sharedMaterial.',
    'Both BSIPA/BSML embedded resources are present.',
    'Replay and CustomJSONData integrations have no hard assembly dependency.',
    'This is a static audit, not a Unity/VR runtime test.', '', 'Assembly references:')
$lines += $assembly.MainModule.AssemblyReferences | ForEach-Object { $_.FullName }
$lines | Tee-Object -FilePath (Join-Path $root 'obj\Release\audit.txt')
$assembly.Dispose()
