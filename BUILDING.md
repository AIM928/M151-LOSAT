# Building from source (Windows)

## Prerequisites

- Your local installation of GHPC `0.1.0-alpha+20261002` and MelonLoader `0.7.3` with `net35` assemblies.
- PowerShell and a .NET SDK. The tested compiler is SDK `7.0.410` Roslyn; the script discovers it without a fixed installation path. Other SDK/game versions are not guaranteed compatible.
- Windows .NET Framework support for the selected test executables.
- Local model/texture/audio build inputs. These are intentionally not committed; their publication/licensing has not been established.

The scripts do not download dependencies, write into the game installation or install the result automatically.
Game and MelonLoader/Harmony assemblies are referenced from your local installation, not copied into the repository.

## Recover the existing resource inputs locally

Download the **standalone v0.7.58 DLL**, not the old ZIP:
[M151LOSATTest.dll](https://github.com/AIM928/M151-LOSAT/releases/download/v0.7.57/M151LOSATTest.dll).

From the repository root, run:

```powershell
& .\scripts\Extract-Resources.ps1 -ReleaseDll 'D:\Downloads\M151LOSATTest.dll'
```

This validates the exact published SHA256 before reading the managed assembly's embedded resource streams.
It extracts 11 resources into local `assets/`, without starting the game or enumerating/instantiating mod types.
Existing resource files are never overwritten; choose a new `-Destination` if needed.
The resource directory is ignored by Git. Local extraction is **not** an additional redistribution license.
Do not soften the audio a second time: the v0.7.58 launch WAV already contains the audio update.

Required resource inputs:

```text
assets/
  humvee-painted.meshbin
  losat-four-launcher.meshbin
  humvee-details.meshbin
  humvee-cabin.meshbin
  humvee-weathered-woodland.png
  humvee-paint-normal.png
  humvee-panel-occlusion.png
  humvee-paint-finish.png
  LOSAT_Launch_Restored.wav
  Turn_Left_Female.wav
  Turn_Right_Female.wav
```

For other inputs, use legally authorized replacements matching these formats/resource identifiers.
The current mesh loader and texture use can be found in `src/VisualSwap.cs`, `src/BodyDetails.cs` and `src/CabinInterior.cs`.

## Compile

```powershell
& .\build.ps1 -GameRoot 'D:\Games\GHPC' -OutputDir '.\build-custom-01'
```

Optional parameters: `-ResourceRoot` for another resource directory and `-CompilerPath` for a specific `Roslyn\bincore\csc.dll`.
The output is `M151LOSATTest.dll` plus a source-hash build manifest.
An existing output DLL is not overwritten; use a fresh output directory for each candidate.

The 66 C# production files are unchanged from the released source snapshot; only the public build tooling has been made path-configurable.
Build timestamps/compiler metadata can change the DLL hash: a source rebuild is not claimed to be byte-identical to the published binary.
The original production-source hashes and published DLL hash are recorded in `source-baseline.json`.
New C# files must be added to the explicit compiler input list in `src/build.ps1`.

## Selected offline tests

```powershell
& .\scripts\Test-Rules.ps1 -GameRoot 'D:\Games\GHPC' -OutputDir '.\test-output'
```

Four executions use three supplied fixtures: rolling two-channel guidance, inherited display-name lookup, its missing-field fallback, and launch-volume settings.
The fixtures compile the actual relevant production files with small native-API stand-ins. These are not full game integration tests.
Compilation or a test failure returns an error; do not remove assertions to make a game update appear compatible.

Compilation and rule tests do not establish gameplay accuracy, FPS, smoke behavior, hit replay or perceived audio quality.
For installation, close the game, back up the old DLL, copy only the candidate DLL into `Mods`, and test the relevant cases in-game.
Never put test EXEs into `Mods`. Do not publish game assemblies or ignored resource files without checking their rights.
