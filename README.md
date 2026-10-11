# HMMWV LOSAT

HMMWV LOSAT / MGM-166 vehicle mod for **Gunner, HEAT, PC!**
Replaces the native **M151M232 / TOW** vehicle.

**Player-controlled only: AI cannot use this vehicle.**

## Download and installation

Download the installation ZIP from [Releases](https://github.com/AIM928/M151-LOSAT/releases/latest).
Close the game, back up any existing `M151LOSATTest.dll`, then copy the included `Mods/M151LOSATTest.dll` into the game's `Mods` folder.
Keep only one copy of this mod. Models, textures and audio are embedded.

Tested game version: **GHPC 0.1.0-alpha+20261002**, with **MelonLoader 0.7.3**.
No other vehicle or AI mods are required.

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

## v0.7.57

In-game vehicle name: **HMMWV LOSAT**. Missile and ammunition name: **MGM-166**.
Logging is limited to basic load/installation messages, one short check per launch and real warnings/errors.
Model, crew, optics, muzzle-position, target-assignment and acceleration-progress debug messages have been removed.
Missile speed, penetration, guidance, models, textures and audio are unchanged from v0.7.55.
Fixed vehicle display-name lookup through the native Unit base class. Cosmetic naming failures no longer restart vehicle installation or repeatedly print discovery errors.

This repository distributes the mod; the release ZIP does not contain game assemblies, test programs or test logs.
