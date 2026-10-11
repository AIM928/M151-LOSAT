using System;
using System.Collections.Generic;
using GHPC;
using GHPC.Vehicle;
using GHPC.Weapons;
using GHPC.Utility;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private static readonly Dictionary<WeaponSystem, SalvoWeapon> SalvoWeapons = new Dictionary<WeaponSystem, SalvoWeapon>();
        private static readonly Dictionary<AmmoFeed, SalvoWeapon> SalvoFeeds = new Dictionary<AmmoFeed, SalvoWeapon>();
        private static Mod SalvoOwner;
        private bool _salvoHooksReady;

        private sealed class SalvoWeapon
        {
            internal WeaponSystem Weapon;
            internal Vehicle Vehicle;
            internal Transform[] Muzzles;
            internal AmmoType.AmmoClip FullClip;
            internal SalvoSequence Sequence = new SalvoSequence();
            internal GuidanceChannels Guidance=new GuidanceChannels();
            internal AiShotLimiter AiLimiter = new AiShotLimiter();
            internal LiveRound LastRound;
            internal int LastRoundId;
            internal LiveRound AiRound;
            internal int AiRoundId;
            internal TubeLoadState TubeLoad=new TubeLoadState();
            internal float NextHudUpdate;
            internal string HudStatus="";
            internal int HudSeconds;
            internal bool HudWasReloading;
            internal IUnit NativeAiTarget;
            internal readonly ReloadSoundSequence ReloadSounds=new ReloadSoundSequence();
            internal bool ReloadAudioFailed;
        }

        private void InstallSalvoHooks()
        {
            SalvoOwner = this;
            HarmonyInstance.Patch(AccessTools.Method(typeof(WeaponSystem), "Fire"),
                prefix: new HarmonyMethod(typeof(Mod), nameof(SalvoFirePrefix)),
                postfix: new HarmonyMethod(typeof(Mod), nameof(SalvoFirePostfix)));
            HarmonyInstance.Patch(AccessTools.PropertyGetter(typeof(AmmoFeed), "ReserveCount"),
                prefix: new HarmonyMethod(typeof(Mod), nameof(SalvoReservePrefix)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(AmmoFeed), "TryFinalFeedNewClip"),
                prefix: new HarmonyMethod(typeof(Mod), nameof(SalvoClipLoadPrefix)));
            _salvoHooksReady = true;
        }

        private void ConfigureFourTubeSalvo(Vehicle vehicle, WeaponSystem weapon, AmmoType ammo)
        {
            if (!_salvoHooksReady) throw new InvalidOperationException("Four-tube hooks are not ready.");
            if (SalvoWeapons.ContainsKey(weapon)) return;
            HumveeSwap swap = _humveeSwaps.Find(s => s.Vehicle == vehicle && s.LauncherWeapon == weapon);
            if (swap == null || swap.LauncherPivot == null || swap.LauncherAimReference == null)
                throw new InvalidOperationException("Four-tube muzzle rig or independent native aim source is missing.");
            AmmoFeed feed = weapon.Feed;
            // Fixed tubes need command steering, rather than rotating to align
            // before launch. This modifies only this vehicle's private ammo.
            ammo.TurnSpeed=Mathf.Max(ammo.TurnSpeed,FixedLauncherRules.CommandTurnRate);
            AmmoType.AmmoClip sourceClip = feed.LoadedClipType ?? feed.ReadyRack.ClipTypes[0];
            AmmoType.AmmoClip clip = new AmmoType.AmmoClip {
                Name = "MGM-166", Capacity = SalvoSequence.TubeCount,
                MinimalPattern = sourceClip.MinimalPattern
            };
            if (clip.MinimalPattern == null || clip.MinimalPattern.Length == 0)
                throw new InvalidOperationException("LOSAT ammunition pattern is missing.");

            Transform[] muzzles = new Transform[SalvoSequence.TubeCount];
            BarrelInfo[] barrels = new BarrelInfo[SalvoSequence.TubeCount];
            float[] xPositions = { -.64f, -.38f, .38f, .64f };
            for (int i = 0; i < muzzles.Length; i++)
            {
                Transform muzzle = new GameObject("LOSAT_Tube_" + (i + 1) + "_Muzzle").transform;
                muzzle.SetParent(swap.LauncherPivot, false);
                // Exported front lip z=1.075, pivot z=-.15; 20 mm outside the lip.
                muzzle.localPosition = new Vector3(xPositions[i], 0f, 1.245f);
                muzzle.localRotation = Quaternion.identity;
                muzzle.gameObject.layer = vehicle.gameObject.layer;
                muzzles[i] = muzzle;
                barrels[i] = new BarrelInfo {
                    MuzzleIdentity = muzzle, ImpulseLocation = muzzle,
                    MuzzleEffects = BuildNativeTowMuzzleEffects(weapon,muzzle),
                    RoundLoadedObject = null, Destructible = weapon.BarrelHitZone
                };
            }
            weapon.MultiBarrels = barrels;
            weapon.ResetBarrelIndexOnRelease = false;
            RequiredSet(weapon, "_isMultiBarrel", true);
            RequiredSet(weapon, "_multiBarrelIndex", 0);
            weapon.FireWhileGuidingMissile = true;
            weapon.WireGuided = false;
            weapon.TriggerHoldTime = 0f;
            if (weapon.GuidanceUnit != null) weapon.GuidanceUnit.ResetAimOnLaunch = false;

            feed.ReadyRack.UseVisibleRounds = false;
            feed.ReadyRack.ClipCapacity = 3;
            feed.ReadyRack.ClipTypes = new[] { clip };
            List<AmmoType.AmmoClip> reserves = new List<AmmoType.AmmoClip> { CopyClip(clip,4), CopyClip(clip,4), CopyClip(clip,4) };
            RequiredSet(feed.ReadyRack, "<StoredClips>k__BackingField", reserves);
            RequiredSet(feed.ReadyRack, "_initialClipCounts", new[] { 3 });
            feed.AutoFeed = true;
            feed.ReloadDuringMissileTracking = true;
            feed.DualFeed = false;
            RequiredSet(feed, "_auxFeedMode", false);
            RequiredSet(feed, "_feedClipMain", new Queue<AmmoType>(new[] { ammo, ammo, ammo }));
            RequiredSet(feed, "_feedClipAux", new Queue<AmmoType>());
            RequiredSet(feed, "<LoadedClipType>k__BackingField", clip);
            RequiredSet(feed, "<QueuedClipType>k__BackingField", clip);
            RequiredSet(feed, "_queuedClipTypeLockedIn", clip);
            RequiredSet(feed, "<AmmoTypeInBreech>k__BackingField", ammo);
            RequiredSet(feed, "<WaitingOnMissile>k__BackingField", false);
            RequiredSet(feed, "<Reloading>k__BackingField", false);
            RequiredSet(feed, "<Cycling>k__BackingField", false);
            RequiredSet(weapon, "<CurrentAmmoType>k__BackingField", ammo);
            if (feed.RoundCycleStages == null || feed.RoundCycleStages.Length == 0)
                throw new InvalidOperationException("Native tube cycling stages are missing.");
            AmmoFeed.ReloadStage[] stages = new AmmoFeed.ReloadStage[feed.RoundCycleStages.Length];
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i] = (AmmoFeed.ReloadStage)MemberwiseCloneMethod.Invoke(feed.RoundCycleStages[i], null);
                RequiredSet(stages[i], "Duration", .12f / stages.Length);
                stages[i].LiveDuration = .12f / stages.Length;
            }
            feed.RoundCycleStages = stages;
            RequiredSet(feed, "_totalCycleTime", .12f);
            // Shared prefab stages must never be modified for unrelated vehicles.
            if(feed.ClipReloadStages==null || feed.ClipReloadStages.Length==0)
                throw new InvalidOperationException("Native clip reload stages are missing.");
            AmmoFeed.ReloadStage[] reloadStages=new AmmoFeed.ReloadStage[feed.ClipReloadStages.Length];
            for(int i=0;i<reloadStages.Length;i++)
            {
                reloadStages[i]=(AmmoFeed.ReloadStage)MemberwiseCloneMethod.Invoke(feed.ClipReloadStages[i],null);
                RequiredSet(reloadStages[i],"Duration",TubeLoadState.ReloadSeconds/reloadStages.Length);
                reloadStages[i].LiveDuration=TubeLoadState.ReloadSeconds/reloadStages.Length;
            }
            feed.ClipReloadStages=reloadStages;
            RequiredSet(feed,"_totalReloadTime",0f); // Native recalculation retains crew damage multipliers.
            weapon.SetCycleTime(SalvoSequence.Interval);
            RequiredSet(weapon, "_cycleTimeRemaining", 0f);

            SalvoWeapon state = new SalvoWeapon { Weapon = weapon, Vehicle = vehicle, Muzzles = muzzles, FullClip = clip };
            SalvoWeapons.Add(weapon, state);
            SalvoFeeds.Add(feed, state);
            feed.LoadedNewClip += loaded => {
                state.TubeLoad.Reloaded(loaded==null ? 0 : loaded.Capacity);
                state.NextHudUpdate=0f;
            };
            weapon.Fired += (firedAmmo, round) => {
                TrackMissile(round, firedAmmo ?? ammo);
                RegisterGuidanceRound(state,round);
                BindDispatchedMissile(round,weapon);
                BindNativeAiMissile(round,state);
                if(round!=null) PrepareShallowArc(round,weapon.GuidanceUnit);
                SalvoShotFired(state, round);
            };
        }

        private static AmmoType.AmmoClip CopyClip(AmmoType.AmmoClip clip, int capacity)
        {
            return new AmmoType.AmmoClip { Name = clip.Name, Capacity = capacity, MinimalPattern = clip.MinimalPattern };
        }

        private static void RequiredSet(object owner, string fieldName, object value)
        {
            var field = owner.GetType().GetField(fieldName, AnyInstance);
            if (field == null) throw new MissingFieldException(owner.GetType().FullName, fieldName);
            field.SetValue(owner, value);
        }

        private static bool SalvoFirePrefix(WeaponSystem __instance, IUnit __0, ref bool __result)
        {
            SalvoWeapon state;
            if (!SalvoWeapons.TryGetValue(__instance, out state)) return true;
            state.NativeAiTarget=null;
            bool player = IsPlayerWeapon(state);
            bool dispatched=player && SalvoOwner._dispatchWeapon==__instance;
            if(player && !dispatched && SalvoOwner._targetSalvo.Pending)
            {__result=false;return false;}
            if (player && !dispatched && (InputUtil.MainPlayer == null ||
                !state.Sequence.AllowPress(InputUtil.MainPlayer.GetButtonDown("Fire"))))
            {
                __result = false;
                return false;
            }
            if(state.Guidance.Available==0) {__result=false;return false;}
            if(player && !dispatched && SalvoOwner._targetLocks.Count>0)
            {
                SalvoOwner.BeginTargetSalvo(state);
                __result=false;return false; // Never send a locked shot toward the reticle.
            }
            // Cheap per-vehicle checks precede the global count, so AI retries
            // during the cooldown do not repeatedly scan the entire platoon.
            if (!player && (Time.time < state.AiLimiter.NextAllowedTime ||
                MissileStillActive(state.LastRound, state.LastRoundId) ||
                MissileStillActive(state.AiRound, state.AiRoundId) ||
                !state.AiLimiter.AllowAttempt(Time.time, false, CountActiveAiMissiles())))
            {
                __result = false;
                return false;
            }
            if(!SalvoOwner.ShotInLaunchSector(state,player ? null : __0))
            {__result=false;return false;} // Before native ammo, FX or cooldown consumption.
            SalvoOwner.UpdateHumveeVisuals(__instance);
            if(!player) state.NativeAiTarget=__0;
            return true; // Native damage, readiness, movement and FCS checks remain authoritative.
        }

        private static void SalvoFirePostfix(WeaponSystem __instance)
        {
            SalvoWeapon state;
            if(SalvoWeapons.TryGetValue(__instance,out state)) state.NativeAiTarget=null;
        }

        private void BindNativeAiMissile(LiveRound round,SalvoWeapon state)
        {
            // Native AI chooses and authorizes the target; we never acquire enemies
            // for it, force a Fire call or bypass readiness/obstruction checks.
            Vehicle target;TrackedMissile tracked;
            if(round==null || state.NativeAiTarget==null || IsPlayerWeapon(state) ||
                !_lockVehicleByUnit.TryGetValue(state.NativeAiTarget,out target) || !LockCandidateAlive(target) ||
                !_missileLookup.TryGetValue(round,out tracked) || tracked.RoundId!=round.ID || tracked.TargetAssignment!=null) return;
            tracked.TargetAssignment=new MissileTargetAssignment(round.ID,target.GetInstanceID());
            tracked.TargetVehicle=target;tracked.TargetLocalPoint=CenterBodyBounds(target).center;
            tracked.LastTargetPosition=target.transform.TransformPoint(tracked.TargetLocalPoint);
            tracked.TargetGuidance=state.Weapon.GuidanceUnit;
            tracked.UseShallowArc=false;
        }

        private static bool SalvoReservePrefix(AmmoFeed __instance, ref int __result)
        {
            SalvoWeapon state;
            if (!SalvoFeeds.TryGetValue(__instance, out state)) return true;
            __result = 0;
            foreach (AmmoType.AmmoClip clip in __instance.ReadyRack.StoredClips)
                if (clip != null && clip.Equals(state.FullClip)) __result += Math.Max(0, clip.Capacity);
            return false;
        }

        private static void SalvoClipLoadPrefix(AmmoFeed __instance)
        {
            SalvoWeapon state;
            if (!SalvoFeeds.TryGetValue(__instance, out state)) return;
            AmmoType.AmmoClip queued = GetField(__instance, "_queuedClipTypeLockedIn") as AmmoType.AmmoClip;
            if (queued == null || !queued.Equals(state.FullClip)) return;
            foreach (AmmoType.AmmoClip stored in __instance.ReadyRack.StoredClips)
            {
                if (stored == null || !stored.Equals(state.FullClip)) continue;
                // Native Equals intentionally ignores Capacity. Match the actual retrieved
                // clip's 4/4/2 capacity, so the final two rounds cannot become four.
                RequiredSet(__instance, "_queuedClipTypeLockedIn", CopyClip(state.FullClip, Math.Min(4, stored.Capacity)));
                RequiredSet(state.Weapon, "_multiBarrelIndex", 0);
                break;
            }
        }

        private void SalvoShotFired(SalvoWeapon state, LiveRound round)
        {
            state.Sequence.SuccessfulShot();
            // Include player shots in the per-vehicle cooldown when handing control to AI.
            // None of these limits are applied to the player's own Fire calls.
            state.AiLimiter.SuccessfulShot(Time.time);
            state.LastRound = round;
            state.LastRoundId = round == null ? -1 : round.ID;
            if (!IsPlayerWeapon(state))
            {
                state.AiRound = round;
                state.AiRoundId = state.LastRoundId;
            }
            int index = (int)GetField(state.Weapon, "_multiBarrelIndex");
            state.TubeLoad.FiredTube(index);
            state.NextHudUpdate=0f;
            Transform muzzle = state.Muzzles[index];
            if(round!=null) LaunchPresentation(state,round,muzzle.position);
            if(LaunchDiagnostics) {
            float positionError = round == null ? float.PositiveInfinity : Vector3.Distance(round.transform.position, muzzle.position);
            float speed=round==null ? float.NaN : round.CurrentSpeed,max=round==null ? float.NaN : round.MaxSpeed;
            float ke=round==null || round.Info==null ? float.NaN : round.Info.RhaPenetration;
            int check=LaunchDataRules.Check(speed,max,ke,positionError,MaximumSpeed,MaximumPenetration);
            string data="MGM-166 tube="+(index+1)+" speed="+speed.ToString("F0")+" max="+max.ToString("F0")+
                " KE="+ke.ToString("F0")+" check="+(check==0 ? "OK" : "FAIL:"+check);
            // Normal shots use one short record. Detailed identifiers/position
            // are only useful when the retained data checks actually fail.
            if(check!=0) data+=" round="+(round==null ? -1 : round.ID)+" spawnError="+positionError.ToString("F4")+"m";
            if(check==0) MelonLogger.Msg(data);else MelonLogger.Warning(data);
            }
        }

        private static bool MissileStillActive(LiveRound round, int launchId)
        {
            // LiveRound objects are pooled; ID prevents a reused object from blocking
            // this weapon or counting another vehicle's new missile indefinitely.
            return round != null && round.ID == launchId && !round.Pooled &&
                !round.IsDestroyed && round.gameObject.activeInHierarchy;
        }

        private static int CountActiveAiMissiles()
        {
            int count = 0;
            foreach (SalvoWeapon state in SalvoWeapons.Values)
                if (MissileStillActive(state.AiRound, state.AiRoundId)) count++;
            return count;
        }

        private static bool IsPlayerWeapon(SalvoWeapon state)
        {
            return GHPC.Player.PlayerInput.Instance != null &&
                GHPC.Player.PlayerInput.Instance.CurrentPlayerUnit == state.Vehicle.BaseUnit;
        }

        private void UpdateSalvos()
        {
            if (Time.deltaTime <= 0f) return;
            foreach (SalvoWeapon state in SalvoWeapons.Values)
            {
                if (state.Weapon == null || state.Vehicle == null) continue;
                LimitRestockedReserve(state);
                RefreshTubeHud(state);
                UpdateReloadAudio(state);
                if (InputUtil.MainPlayer != null)
                    state.Sequence.ObserveButton(InputUtil.MainPlayer.GetButton("Fire"));
            }
        }

        private static void LimitRestockedReserve(SalvoWeapon state)
        {
            List<AmmoType.AmmoClip> clips = state.Weapon.Feed.ReadyRack.StoredClips;
            int available = SalvoSequence.ReserveCapacity;
            for (int i = 0; i < clips.Count; i++)
            {
                AmmoType.AmmoClip clip = clips[i];
                if (clip == null || !clip.Equals(state.FullClip)) continue;
                int keep = Math.Min(available, Math.Max(0, clip.Capacity));
                if (keep == 0) { clips.RemoveAt(i--); continue; }
                if (keep != clip.Capacity) clips[i] = CopyClip(clip, keep);
                available -= keep;
            }
        }
    }
}
