param([string]$GameDir = 'F:\1.40.8')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $GameDir 'Libs\Mono.Cecil.dll')
function Check($condition, $message) { if(!$condition) { throw $message }; "PASS: $message" }
function Walk($types) { foreach($type in $types) { $type; Walk $type.NestedTypes } }
function Read($path) { [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path) }
$lines = @(
    foreach($variant in @('dist','diagnostic-no-gameplay')) {
        $a = Read (Join-Path $root "$variant\Plugins\Laki.dll")
        $plugin = $a.MainModule.Types | Where-Object FullName -eq 'Laki.Plugin'
        $ctor = $plugin.Methods | Where-Object Name -eq '.ctor'
        $locations = @()
        $constant = -1
        foreach($i in $ctor.Body.Instructions) {
            if($i.OpCode.Name -match '^ldc\.i4\.([0-8])$') { $constant = [int]$Matches[1] }
            elseif($i.OpCode.Name -in @('ldc.i4','ldc.i4.s')) { $constant = [int]$i.Operand }
            if($i.Operand -is [Mono.Cecil.MethodReference] -and $i.Operand.FullName -match 'Zenjector::Install\(SiraUtil.Zenject.Location') { $locations += $constant }
        }
        $expected = if($variant -eq 'dist') { '1,2,4' } else { '1,2' }
        Check (($locations -join ',') -eq $expected) "$variant installer locations: $expected only (App=1, Menu=2, StandardPlayer=4)"
        Check (!($a.MainModule.AssemblyReferences.Name -match 'BeatSaberPlus|MultiplayerCore|ChatPlex')) "$variant has no hard Multiplayer+ dependency"
        if($variant -eq 'diagnostic-no-gameplay') {
            $calls = (Walk $a.MainModule.Types | Where-Object { $_.FullName -like 'Laki.Plugin*' }).Methods.Body.Instructions.Operand
            Check (!(($calls | ForEach-Object { "$_" }) -match 'GameplaySession|GameplayInstallation|MultiplayerPlusGuard')) 'Diagnostic Plugin has no gameplay bind/probe call path'
        }
        $a.Dispose()
    }
    $plus = Read (Join-Path $GameDir 'Plugins\BeatSaberPlus_Multiplayer.dll')
    Check ($plus.Name.Version.ToString() -eq '6.4.2.0') 'Actual Multiplayer+ 6.4.2.0 API verified'
    $network = $plus.MainModule.Types | Where-Object FullName -eq 'BeatSaberPlus_Multiplayer.Network.NetworkManager'
    $getter = $network.Methods | Where-Object Name -eq 'get_RoomData'
    Check (($getter.Body.Instructions.OpCode.Name -join ',') -eq 'ldsfld,ret') 'Actual RoomData getter is a field read, no network or instance-creation call'
    $room = $plus.MainModule.Types | Where-Object FullName -eq 'BeatSaberPlus_Multiplayer.Managers.RoomManager'
    $launch = ($room.Methods | Where-Object Name -eq 'NetworkManager_OnRoomUpdated').Body.Instructions
    Check ([bool]($launch.Operand -match 'Levels::StartBeatmapLevel')) 'Multiplayer+ launches the standard level API from room update'
    $sdk = Read (Join-Path $GameDir 'Plugins\ChatPlexSDK_BS.dll')
    $levels = $sdk.MainModule.Types | Where-Object FullName -eq 'CP_SDK_BS.Game.Levels'
    $start = ($levels.Methods | Where-Object Name -eq 'StartBeatmapLevel').Body.Instructions
    Check ([bool]($start.Operand -match 'StandardLevelScenesTransitionSetupDataSO::Init')) 'SDK launches StandardLevelScenesTransitionSetupDataSO, not native Multiplayer local-player setup'
    $sira = Read (Join-Path $GameDir 'Plugins\SiraUtil.dll')
    $manager = $sira.MainModule.Types | Where-Object FullName -eq 'SiraUtil.Zenject.ZenjectManager'
    $callback = ($manager.Methods | Where-Object Name -eq 'ContextDecorator_ContextInstalling').Body.Instructions
    Check ([bool]($callback.Operand -match 'InstallInstruction::onInstall') -and [bool]($callback.Operand -match 'Action`1<Zenject.DiContainer>::Invoke')) 'SiraUtil invokes stored installer callback with live container at installation time'
    foreach($a in @($plus,$sdk,$sira)) { $a.Dispose() }
    'No Unity/VR runtime or Multiplayer server lifecycle was executed.'
)
$lines | Tee-Object -FilePath (Join-Path $root 'obj\Release\multiplayer-audit.txt')
