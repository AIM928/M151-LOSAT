# HMMWV LOSAT

HMMWV LOSAT / MGM-166 vehicle mod for **Gunner, HEAT, PC!**
Replaces the native **M151M232 / TOW** vehicle.

**Player-controlled only: AI cannot use this vehicle.**

## Download and installation

Download the standalone **M151LOSATTest.dll** from [Releases](https://github.com/AIM928/M151-LOSAT/releases/tag/v0.7.57).
This attachment is the **v0.7.58 audio update**, hosted under the existing v0.7.57 release tag.
The older `M151-LOSAT-0.7.57-release.zip` does **not** contain this audio update.

Close the game, back up any existing `M151LOSATTest.dll`, then put the downloaded DLL into the game's `Mods` folder.
Keep only one copy of this mod. Models, textures and audio are embedded in the DLL; no separate resource folder is needed for installation.

Compatibility baseline: **GHPC 0.1.0-alpha+20261002**, with **MelonLoader 0.7.3**.
No other vehicle or AI mods are required. Other game versions have not been verified.

## Features

- HMMWV body, fixed four-tube launcher and independently rotating sight.
- MGM-166 kinetic missiles with custom launch audio, exhaust and impact smoke.
- Four tracked targets and two shared guidance channels.
- 4 / 8 / 24 / 50x thermal optics.
- Four ready missiles, twelve reserve missiles and a 30-second reload.
- Forward firing sector of 30 degrees, with hull-turn arrows and voice cues.

## Fire-control controls

1. **E / middle mouse button:** lock or unlock the target under the reticle. Track up to 4 targets.
2. **Left mouse button:** attack targets in lock order, up to 2 per click. A maximum of 2 missiles can be guided simultaneously; fire again as soon as a guidance channel becomes available.
3. **Backspace:** clear all pending target locks and reacquisition boxes.
4. **Green light:** steady means ready to fire; blinking means firing is temporarily unavailable. Outside the firing sector, turn the vehicle in the direction indicated by the arrow.

## Launch volume

v0.7.58 lowers the default launch gain and softens the launch recording's high frequencies and abrupt edges.
Run the mod once, close the game, then edit its existing section in `UserData/MelonPreferences.cfg`:

```toml
[HMMWV_LOSAT]
LaunchVolume = 0.65
```

Range: `0` to `1`. `0` mutes the custom launch sound; `0.65` is the default; `1` restores the previous gain while keeping the softened tone.
Restart the game after editing. Preserve other configuration sections.

## Source and development

The repository now contains the **66 C# files corresponding to the v0.7.58 DLL**, a configurable local build script and selected offline regression tests.
See [BUILDING.md](BUILDING.md) for rebuilding, and [MAINTENANCE.md](MAINTENANCE.md) for the fire-control invariants and game-update checklist.

Game assemblies and model/audio resources are not committed to this source repository.
The build requires your own local game/loader installation and resources from the existing verified DLL or appropriately authorized replacements.

This mod is available as a free download. **No source-code license has been selected yet.**
Publishing source does not by itself grant blanket permission to redistribute modified code or third-party assets.
See [RIGHTS.md](RIGHTS.md). Contributions and bug reports are welcome.

## Version and verification notes

- **v0.7.57:** fixed display-name lookup through the native Unit base class. Cosmetic naming failures no longer restart installation and repeatedly print discovery errors.
- **v0.7.58:** quieter/softer configurable launch audio; missile speed, penetration, guidance and models are unchanged from v0.7.57.
- Logging is limited to basic load/installation messages, one short check per launch and real warnings/errors.
- v0.7.57 was reported working in-game by the author. v0.7.58 passed offline checks; renewed in-game listening for that audio update has not been confirmed.
- Publishing source does not create a new release, change existing release tags, or replace installed game files.

Standalone v0.7.58 DLL SHA256:

```text
9537824F443272A9879E2F8A27D0D3C22E8B52E1C749D46CDDCBB12BBC5AB3C7
```
