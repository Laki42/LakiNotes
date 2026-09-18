# Laki Notes

A little luck for Beat Saber! Occasionally, a note appears with clovers, rainbow stars, or a secret moon design, followed by a short effect when cut correctly.

Local visual cosmetics only. No changes to note timing, movement, cut detection, score, combo, energy, or multiplayer packets.

[日本語 README](README.ja.md)

## Compatibility

Version 0.1.4 targets Beat Saber PC **1.40.8 / 1.40.8_7379**, with BSIPA 4.3.6, SiraUtil 3.2.1 and BSML 1.12.5. Supports Solo, Practice and **Multiplayer+ 6.4.2**. Native Multiplayer / BeatTogether cosmetics remain disabled.

Small Notes and Pro Mode use the existing visual-only scale handling. Other game versions and unknown Multiplayer+ versions are not supported by the current safety checks.

## Installation

Close the game and place the release's `Laki.dll` in your game's `Plugins` folder. Install the dependencies listed above first. Settings and assets are embedded in the DLL. Keep `UserData/Laki.json` when upgrading to preserve settings and Secret Pity progress.

In the tested mod configuration, AutoPauseStealth 0.1.20 prevented Multiplayer+ songs from progressing. Removing AutoPauseStealth restored play, including with Laki Notes installed. Its exact internal interaction has not been established. Laki Notes does not modify or disable other mods.

## Settings

Open **Mod Settings → Laki Notes** to enable the mod, effects, rare variants, and Multiplayer+ support.

**Test Mode** offers Off, Force Laki, Force Super Laki and Force Secret Laki. A force request selects one existing eligible note near the middle of the map, independently of the normal lottery and Secret Pity. It returns to Off after successful target selection, visual preparation and event subscription. Disabled or rejected sessions do not consume the request.

For an immediate visual check in Multiplayer+, enable the mod, effects and multiplayer setting, then select Force Laki before starting a song. Correct cuts trigger the success effect; bad cuts and misses do not.

## Build and tests

Use Windows, Visual Studio 2022 with the C# compiler, and your own matching Beat Saber installation with the dependencies installed. Game and dependency DLLs are not distributed in this repository. The BSML integration test runner additionally requires .NET 8.

```powershell
./Build.ps1 -GameDir 'D:\YourGame\Beat Saber'
./Tests/Run.ps1 -GameDir 'D:\YourGame\Beat Saber'
./Tests/Settings.ps1 -GameDir 'D:\YourGame\Beat Saber'
./Tests/Audit.ps1 -GameDir 'D:\YourGame\Beat Saber'
./Tests/ApiContract.ps1 -GameDir 'D:\YourGame\Beat Saber'
```

Output: `dist/Plugins/Laki.dll`. The compiler path can be overridden with `-Compiler` on the build and managed test scripts.

The optional `-DiagnosticNoGameplay` build disables all gameplay integration. Build that variant before running `Tests/MultiplayerAudit.ps1`, which inspects both normal and diagnostic DLLs against the installed Multiplayer+ and SiraUtil APIs.

0.1.4 passed 12,283 managed assertions plus compiled API/safety audits. These do not replace VR testing. The user subsequently confirmed Multiplayer+ visual operation; each modifier/cut-effect combination still requires its own runtime check.
