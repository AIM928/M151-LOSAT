# Maintenance notes

Source snapshot: v0.7.58, published 2026-10-12. Tested game API baseline: GHPC `0.1.0-alpha+20261002`, MelonLoader `0.7.3`.

## Code map

- Entry/ammunition/names: `Mod.cs`, `VehicleDisplayName.cs`.
- Incremental discovery/ERA/performance: `IncrementalDiscovery.cs`, `DiscoveryBudget.cs`, `AntiEra.cs`, `PerformanceCompatibility.cs`.
- Target tracking/occlusion/reacquisition: `TargetLock.cs`, `LockTracking.cs`, `LockOcclusion.cs`, `SmokeProvenance.cs` and related rule files.
- Launch/channels/guidance/arcs/replay: `FourTubeSalvo.cs`, `GuidanceChannels.cs`, `GuidanceControl.cs`, `ManualSightGuidance.cs`, `ShallowArc.cs`, `MissileReplay.cs`.
- Visuals/crew/cabin: `VisualSwap.cs`, `BodyDetails.cs`, `CabinInterior.cs`, `CabinViews.cs`, `CrewSeats.cs`.
- Audio/effects: `LaunchAudio.cs`, `LaunchAudioSettings.cs`, `ReloadAudio.cs`, `LaunchEffects.cs`, `TankImpactSmoke.cs`.

## Preserve these distinctions

- Four tracked targets and two guided missiles are separate limits. Both manual and locked shots share the same two channels. Each confirmed completion frees its own channel; a transient target loss must not free an in-flight missile's channel.
- Scope toggling is not leaving the owning crew/view context. Backspace cancels pending locks/reacquisition, not an already-fired missile's flight.
- The launcher is body-fixed at about 3 degrees with a forward +/-15 degree sector; the sight rotates independently. Hull-turn cues must describe the physical vehicle turn direction.
- Four ready rounds, twelve reserve rounds, 30-second reload, four reload-audio events. Audio repetition must not repeat ammo/animation actions.
- The approximately 278 m/s initial speed accelerates to 1500 m/s over roughly 1000 m. The 1600 parameter is KE/RHA penetration, not speed. Impact visuals must not add HE damage.
- Constrained shallow arcs must work for manual and locked shots separately, without turn-back behavior or breaking the native collision/replay path.
- Terrain and deployed IR smoke can obscure targets. Ordinary explosion dust must not cause instant loss. Baseline grace: 1.25 s; reacquisition: 3 s; visibility settling: about 0.2 s. Reacquire the same vehicle identity, not a nearby replacement.
- Pooling requires checking the current round ID. Clean scene-specific caches, audio and effect handles on scene changes.
- `VehicleDisplayName.TryApply` resolves the inherited private field from `GHPC.Unit`. Cosmetic failure should warn once, not restart installation.
- Crew rig initialization may be delayed until `AutoCrewHitZoneRig.Start`; wait for binding, preserve helmet clearance and hit-zone alignment.
- Replay uses native `ShotInfo`. A penetration hit, guidance end and final projectile termination are different events. Do not synthesize hits or force detonation to manufacture AAR records.

## Game updates

Record the new game and loader versions first. Compare the affected native types/signatures before reverse-engineering unrelated systems.
Prioritize Unit/Vehicle inheritance, discovery hooks, AmmoFeed reload-audio access, WeaponAudio launch entry points, AAR APIs, PlayerInput and crew-rig binding.
Cosmetic/optional integrations may fall back safely; failed core guidance/collision adaptation must not be reported as successful conversion.

Compile into a fresh output directory and add a focused failing test for the changed boundary.
In-game checks should include manual/locked hits, rolling two-channel launches, new reinforcement vehicles, terrain/IR-smoke/dust behavior, reacquisition cancellation, AAR, reload, crew/glass and audio.
Record what was actually tested; offline checks are not FPS or gameplay evidence.

## Performance and compatibility

Investigate duplicate impact triggers, particle allocation/cloning, full-scene scans and excessive logs before reducing primary explosion effects.
Keep reinforcement discovery, incremental budgets, effect pooling and hit deduplication.
Do not introduce per-frame preference-file reads: launch volume is initialized once and read from the cached entry.
Detailed diagnostic logs remain disabled; keep basic installation/launch checks and real rate-limited warnings.

ImprovedAI integration is optional and skips safely when absent; do not require another mod's DLL to build or run.
The supported public behavior remains **Player-controlled only: AI cannot use this vehicle.**
