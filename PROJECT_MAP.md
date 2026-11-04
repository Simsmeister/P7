# PROJECT_MAP

Last verified: commit `28684f98`, 2026-11-04. Static code reading only; nothing was run in Unity or on a headset.

Update this file in the same commit when structure changes. Bare file names below live in `Assets/VR/Runtime/` or `Assets/VR/Editor/`. Line numbers are for this commit; many lines are very long, so search by the named method if a number has drifted.

## Directory tree

```
Assets/
  Scenes/Emil.unity            active, the only scene the build tool builds
  Scenes/SampleScene.unity     boilerplate (still listed in EditorBuildSettings, index 1)
  VR/Runtime/                  active game code plus legacy wand scripts (table below)
  VR/Editor/                   menu tools and prefab generators (table below)
  VR/Shield/                   active: game prefabs, 3 exercise profile .asset files, materials
  VR/Prefabs/                  active: VR Rig, hand prefabs. Tracking trail prefabs are debug only
  VR/Prototype/                legacy wand game, except Session Replay.prefab (live) and
                               materials it uses; Recorded Head.mat seeds Coach Hand Marker
  VR/HandVisualizer/           imported XR Hands sample, source of the hand prefabs
  VR/Materials/                active: Floor, hand materials; Tracking Trail is debug only
  VR/SetupComplete.txt         gate file: Validate Quest Setup exits silently without it
  VR/Validation.txt            output of Validate Quest Setup
  VR/XRGeneralSettings.asset   active, the loader config registered in EditorBuildSettings
  VR/README.md                 Danish rig notes, Mac flow, partly stale
  VR/Shield/README.md          dated changelog, partly stale; later sections supersede earlier
  VR/Prototype/README.md       legacy wand game docs
  XR/                          OpenXR loaders and settings, active
  Settings/                    URP assets and volume profiles; SampleSceneProfile is boilerplate
  Prefabs/gymnasium/           imported environment, not instantiated in Emil.unity
  Resources/, Resources 2/     performance test JSON, boilerplate
  Editor/                      empty
  TutorialInfo/, Readme.asset  boilerplate
  InputSystem_Actions.inputactions  boilerplate; VR scripts build their input actions in code
ProjectSettings/, Packages/    Unity config; bundle ID at ProjectSettings/ProjectSettings.asset:173
Recordings/, Builds/           gitignored outputs
```

## Runtime scripts (`namespace EmilVR`)

| Script | Purpose | Main members | Talks to | Status |
|---|---|---|---|---|
| `ShieldExerciseGame.cs` | Session state machine, tracking, scoring, recording, export | `Begin(bool)`, `SkipTutorialPreview`, `ResetSavedSettings`, `PathDefinition`, `EffectiveCalibration`, `PathVisualPoint`, static `ProjectShield`, `MovementScore`, `HeadPositionSeverity`; props `PathClock`, `RepairQuality`, `Score`, `CompletedRepetitions`, `CurrentStep`, `MenuVisible` | All below | active |
| `ShieldExerciseProfile.cs` | ScriptableObject per exercise: timing, tolerances, path shape | `Progress`, `Phase`, static `Path`, `Quality`, `Cycle`; enum `ShieldMovement` | Read by game, coach, stream | active |
| `AdaptiveExerciseCoach.cs` | Rule-based speed and range per exercise and arm | `Configure`, `ProfileFor`, `RangeFor`, `Observe`, `ExportJson`; class `Decision` | Game, player settings | active |
| `ExercisePlayerSettings.cs` | `player-settings.json` model, playlist | `Load`, `Save`, `Arm`, `Limit`, `Playlist`, `TransformCalibration`, `UsableCalibration` | Game, coach, menu | active |
| `ExercisePathStream.cs` | Draws current and incoming line | `LateUpdate`, static `Approach`; `approachSpeed` | Reads game | active |
| `ConduitRepairGame.cs` | Active theme: power, battery, nodes, white repaired sections, status text, floor lights, camera background. `RepairRun` holds outcome logic | `Begin`, `Resolve`, `Export`, `ExportNodes`, `NodeRepaired`, `Result`, `Run` | Reads game; uses `ExercisePathStream.Approach` | active |
| `ExerciseGameMenu.cs` | World-space menu, ray and mouse input | `Navigate`, `Change`, `Recenter`, static `Hit`; `widgets` | Game, player settings | active |
| `ShieldCoachDisplay.cs` | CURRENT card with stick figure during the mission | `Draw`, static `Example` | Reads game; `WandSessionReplay.EstimateElbow` | active |
| `WandSessionReplay.cs` | Records and plays replay; also the tutorial mannequin and calibration guide | `BeginRecording`, `Capture`, `Save`, `Open`, `Close`, `OpenExerciseDemo`, `HoldDemoAt`, `UpdateGuidance`, `LoadLatest`, `MirrorPoint` | Game; `WandPathGame.ResizeWand` and `OpenDemo` (72, 86) | active, with legacy wand branches |
| `EmilVRInput.cs` | Rig hand and controller visuals | `mode`, `allowModeSwitch`, `CycleMode`, `SetMode` | Game forces Controllers (`ShieldExerciseGame.cs:111`) | active |
| `ShieldArcadeFeedback.cs` | Reactor Rescue: tones, points, combo, burst ring. HUD hidden; points and multiplier are shown through Conduit status text | `Captured`, `Block`, `Reward`, `Show`, `ResetMission`, `Points`, `Combo`, `BestCombo`, `Multiplier` | Called by game | half-alive |
| `ControllerTrackingTrails.cs` | Trigger-drawn debug trails | `RecordFrame`, `ClearTrails` | Own input; instance inactive in `VR Rig.prefab` | dead in gameplay |
| `WandPathGame.cs` | Old two-wand game; instance disabled in Emil.unity | `Target`, static `Quality`, `ResizeWand` | Replay, `PathTube` | dead, compile dependency |
| `PathTube.cs` | Old wand tube mesh | `Draw(WandPathGame,...)` | `WandPathGame` | dead |

## Editor scripts

| Script | Purpose | Menu item (`Tools/Emil VR/...`) | Status |
|---|---|---|---|
| `EmilVRBuild.cs` | Build APK (development flag, Android target required) | Switch to Quest Android; Build Quest APK; Clean Build Quest APK; Restart Editor for Build | active |
| `EmilVRValidation.cs` | OpenXR rules, writes `Validation.txt` | Validate Quest Setup | active |
| `WandDataExport.cs` | `adb -d pull` of Sessions, 60 s timeout, bundle ID hard-coded (22) | Export Quest Recordings | active; dialog text describes old files |
| `ConduitSetup.cs` | Generates Conduit Repair, Left/Right/Target Shield markers, arena materials; re-parents into game prefab; calls `ExerciseMenuSetup.Build`. `Verify` checks `RepairRun` | Build Conduit Repair | active generator |
| `ExerciseMenuSetup.cs` | Generates `Exercise Main Menu.prefab`; menu strings live here | none (from `ConduitSetup`, `ExercisePathSetup`) | active generator |
| `ExercisePathSetup.cs` | Generates `Exercise Path Stream.prefab`, adds coach. `Verify` checks path, range, adaptive rules, playlist | none | active generator |
| `ShieldCoachSetup.cs` | Generates marker and Movement Coach once | none (from `ExercisePathSetup`) | active generator |
| `ShieldExerciseSetup.cs` | Original game setup; profile labels (27). Overwrites shield prefabs before the early return at 32. `Verify` checks scoring | Set Up Shield Exercises | half-alive, see warnings |
| `ShieldExerciseVerification.cs` | Play Mode integration test; `Run`, `VerifiedBuild` rebuild prefabs first; asserts 18 summary columns (45) | none, use `-executeMethod` | active check |
| `ShieldPreviewCapture.cs` | Renders preview PNGs to `/tmp`; `Run`, `RunMenu` rebuild prefabs first | none | active check |
| `ShieldArcadeSetup.cs` | Generates Reactor Rescue and Pulse Drone | none, no callers | dead generator |
| `EmilVRSetup.cs` | XR config and rig creation. Looks for `Emil VR Rig` (27), so it would add a second rig today | Configure Quest 3 | half-alive, do not run casually |
| `EmilVRControllerModels.cs` | Controller hand models; `Populate` is used by `EmilVRSetup`. Menu path loads missing `Emil VR Rig.prefab` (26) | Update Controller Hand Models | half-alive, menu stale |
| `EmilVRTrackingTrailsSetup.cs` | Trails prefab setup; uses missing rig path (30) | Set Up Tracking Trails | dead, menu stale |
| `WandPathPrototypeSetup.cs` | Old prototype generator; `TestDesktopSession` starts the shield game first | Test Desktop Session (live); Build Wand Path Prototype; Verify Wand Scoring | dead except the first item |
| `WandReplaySetup.cs` | Old replay generator; `ImportReplay` still works for exports | Load Replay File in Play Mode (live); Set Up Session Replay; Test Prototype Replay | half-alive |
| `WandPrototypeVerification.cs` | Old wand integration test, needs an active `WandPathGame` | none | dead |

## Runtime flow

Playlist entries and `block` encode `exercise index * 2 + side` (left 0, right 1); `Side=>block%2` (`ShieldExerciseGame.cs:90`). Line numbers in this section are in `ShieldExerciseGame.cs` unless named.

1. Menu. `Start` calls `EnsurePlayerSettings` and `SetFrame` (112). `ExerciseGameMenu.LateUpdate` hit-tests rays and invokes `Navigate("start")`, which calls `Begin()`; `"redo"` calls `Begin(true)`.
2. `Begin(bool)` (193): sets frame from the head, configures the coach, resets counters. For each enabled exercise and arm it either restores a saved calibration into `calibrations` or queues it (202-206). Queue not empty: `PrepareBlock`. Empty: `StartMission`.
3. Calibration. `PrepareBlock` (209) builds a default path and opens the mannequin demo (`State.Demo`). When the demo ends or A skips it, `StartAutomaticPose(false)` sets `CaptureStart`. `UpdateAutomaticPose` (300) needs 1.25 s within 2.5 cm in an allowed pose (`CalibrationPoseAllowed`, 289). Start is captured, then `CaptureEnd()` (315) validates the endpoint with `RangeDirectionAllowed` (280), stores it and saves it unless in dev mode. `Ready` (148) waits for the hand back at start, then moves to the next queued block or `StartMission`.
4. `StartMission` (231): `conduit.Begin(totalRepetitions)`, `PlayerSettings.Playlist(exercises)` (a balanced shuffle, `ExercisePlayerSettings.cs:63`), `LoadChallenge`; a 9 s `Tutorial` state if needed (236, 153).
5. `LoadChallenge` (238): picks `block`, applies `EffectiveCalibration` (252, adaptive range through `ScaledCalibration`), sets `Approaching` with `clock=-exerciseGapSeconds` (2.5 s).
6. Path drawing, every `LateUpdate` while `PathsVisible`. `ExercisePathStream` (17) samples 180 points for the current path (`n=0`, only unconsumed time) and the next (`n=1`, delayed by `Cycle + gap`), then applies `Approach(point, forward, secondsUntilArrival, 0.55)`. Points come from `PathVisualPoint` (266), which uses `ProjectShield` (225): offset `(±.30, -.32, 1.35)` plus hand displacement. Conduit adds 24 nodes and 119 white or amber sections along the same path.
7. Approach: the clock runs only while tracked (154). At 0, `ResetRep`, `State.Playing`, and `OpenRecording` on first entry (155).
8. Per frame in `Playing` (156-181): `Target` gives `p` at `clock`; the ghost moves to `MapToFront(p)`. Error `pe=|hand-p|`. Then `repQuality`, coverage min/max (163-164), head severity (166-168), start, hold and return flags (170-172), endpoint check with tone, ring and haptic (173), 60 Hz `Record` (175). The clock pauses when untracked (176).
9. Rep complete at `clock>=Profile.Cycle` (177): `CompleteRep` (328) computes `complete` and score, writes a summary row, then calls `arcade.Reward`, `conduit.Resolve`, `adaptiveCoach.Observe`.
10. Adaptive update: `Observe` changes the state for that exercise and arm, saves `arm.speed` and `arm.range`, and applies to the next attempt of that pair through `ProfileFor` and `RangeFor`. In-progress attempts never change.
11. Conduit power: `RepairRun.Resolve` (`ConduitRepairGame.cs:12`) updates power and battery. Floor lights track power (`ConduitRepairGame.cs:49`).
12. Next or finish (178-180): when `conduit.Run.Finished`, `Finish(outcome=="victory")`. Otherwise `playlistIndex++`, and the playlist extends by a new batch when near its end, so total reps is not fixed.
13. Stop paths: X (134), `OnDisable`, `OnApplicationPause`, each calling `Finish(false)`.
14. `Finish` (393): marks `aborted` if unresolved, closes writers, writes all export files, `State.Finished`. `MenuVisible` is true again and `homeStatus` shows `SessionResult` and accuracy (`ExerciseGameMenu.cs:69`). `board` is hidden whenever the menu shows (140), so the `DrawText` Welcome and Finished branches do not display.

## Scene and prefab wiring

`Assets/Scenes/Emil.unity` roots: Directional Light; Main Camera (inactive); four prefab instances.

- `VR Rig` (`VR/Prefabs/VR Rig.prefab`): XROrigin, `EmilVRInput`. `Camera Offset` holds VR Camera (`TrackedPoseDriver`), Left/Right Controller Hand, Left/Right Tracked Hand. Root child Controller Tracking Trails, inactive. Generated by `EmilVRSetup`, but the name has since changed.
- `Wand Path Prototype` (`VR/Prototype/`): inactive; `WandPathGame`. Scene overrides `head`, `trackingSpace`, `replay`.
- `Session Replay` (`VR/Prototype/Session Replay.prefab`): `WandSessionReplay`; scene override `viewer` = camera transform. Generated by `WandReplaySetup`. `replayShields` added by `ShieldExerciseSetup`.
- `Shield Exercise Game` (`VR/Shield/Shield Exercise Game.prefab`): root has `ShieldExerciseGame` and `AdaptiveExerciseCoach`. Scene overrides `head` (camera), `trackingSpace` (Camera Offset), `replay` (Session Replay); these are null in the prefab.

| `ShieldExerciseGame` field | Target in prefab | Generator |
|---|---|---|
| `shields[0..1]` | Left Shield, Right Shield | `ConduitSetup.Marker` (and overwritten by `ShieldExerciseSetup`) |
| `ghost` | Target Shield (hollow ring) | same |
| `projectile` | Energy Pulse, legacy, forced off | `ShieldExerciseSetup:35` |
| `board`, `display` | Shield Instructions, its Text | copied from prototype board; panel rescaled by `ConduitSetup:47` |
| `exercises` | `scaption`, `serratus-reach`, `external-rotation` assets | `ShieldExerciseSetup:28` |
| `coach` | Movement Coach (`ShieldCoachDisplay`) | `ShieldCoachSetup` |
| `pathStream` | Exercise Path Stream | `ExercisePathSetup` |
| `adaptiveCoach` | Root component | `ExercisePathSetup:22` |
| `menu` | Exercise Main Menu | `ExerciseMenuSetup` |
| `conduit` | Conduit Repair | `ConduitSetup` |
| `arcade` | Reactor Rescue (core, drone, burst, 6 cells, counter) | `ShieldArcadeSetup` |

Nested components point back through a `game` override. Conduit Repair holds `hud`, `status`, `chargeRing`, 24 `nodes`, 119 `repaired`, 12 `shipLights`. The line material is `VR/Shield/Energy Pulse.mat`, so keep it if the Energy Pulse prefab goes.

The game dereferences `projectile` (140, 182, 405), `arcade.core`, `arcade.drone`, and `replay` without full null guards. Removing one means editing those lines first.

Generated prefab warning. These tools overwrite the assets listed, and manual edits to them are lost:
- `Build Conduit Repair` (`ConduitSetup.Build`): Conduit Repair, Left/Right/Target Shield, Exercise Main Menu, plus colours on the Arena and Player materials. It destroys and re-creates the Conduit child.
- `ExercisePathSetup.Build`: Exercise Path Stream, Exercise Main Menu, sets coach range and duration limits.
- `Set Up Shield Exercises`: Left/Right/Target Shield with the old disc look, shield material colours, and profile `displayName`, `instruction`, `movement`, `minimumTravel`. Existing timing fields are left alone.
- `ShieldExerciseVerification.Run`/`VerifiedBuild`, `ShieldPreviewCapture.Run`/`RunMenu`: run the first two above.

## Scoring and adaptive logic

All values are prototype settings. None has clinical validation.

| Quantity | Formula or threshold | Where |
|---|---|---|
| Frame quality | `1 - InverseLerp(.04, .14, pe)` metres; rotation ignored | `ShieldExerciseProfile.cs:31`, asset fields |
| Path quality | Time-weighted mean of frame quality while tracked | `ShieldExerciseGame.cs:169, 331` |
| Endpoint tolerance | `clamp(0.3 * |end-start|, .025, .06)` m | `ShieldExerciseGame.cs:67` |
| `startedPose` | Within tolerance of start while `clock<.8` | `ShieldExerciseGame.cs:170` |
| `heldSeconds` | Hold phase with `pe<=tolerance` | `ShieldExerciseGame.cs:171` |
| `returnedPose` | Within tolerance of start after the return phase | `ShieldExerciseGame.cs:172` |
| `impactHit` (endpoint reached) | Within .14 m of `end` during hold | `ShieldExerciseGame.cs:173` |
| Coverage | `clamp01(maxProgress-minProgress)`, progress along start-end | `ShieldExerciseGame.cs:163-164, 348` |
| Phases | Mean of started, returned, impact, `clamp01(held/(hold*.65))` | `ShieldExerciseGame.cs:333` |
| `complete` | All flags, `held>=hold*.65`, coverage `>=.65` | `ShieldExerciseGame.cs:330, 334` |
| Movement score | `(.7*path + .3*phases) * lerp(.25, 1, coverage)` | `ShieldExerciseGame.cs:349` |
| Head severity | `min(InverseLerp(.10,.25,opposite), InverseLerp(.08,.20,down))`, counted after .5 s sustained, not in rest | `ShieldExerciseGame.cs:37, 166-168` |
| Head penalty | `max(0, raw - .1*severity)` | `ShieldExerciseGame.cs:38` |
| Session score | `100 * scoreSum / reps` | `ShieldExerciseGame.cs:72` |
| Power | `min(1, power + InverseLerp(.25,1,q)/target)` | `ConduitRepairGame.cs:16` |
| Battery | `clamp(battery - max(0,.6-q)*16 + (q>=.85 ? 2 : 0), 0, 100)` | `ConduitRepairGame.cs:17` |
| Outcome | Defeat at battery 0, checked first; victory at power full | `ConduitRepairGame.cs:18` |
| Node repaired (export, charge, cue) | `q>=.65` |
| Line glow colour | `Glow(q)`: lerp amber*.35 to white over `InverseLerp(.4,.9,q)`; sections fade over 1.2 s, nodes over 1.3 s; also tints the hand trail | `ConduitRepairGame.cs:40, 66, 71` |
| Points | `+100q`; if `complete`, `Combo++` and `+100q*min(Combo-1,4)`; else `Combo=0`. `BestCombo` = max `Combo`; `Multiplier` = `clamp(Combo,1,5)` | `ShieldArcadeFeedback.cs:23-24` |

Adaptive coach, `AdaptiveExerciseCoach.Observe` (66). State is per exercise and arm. `scale` multiplies outward and return duration only (`Apply`, 104).

| Condition, in order | Effect | Line |
|---|---|---|
| Adaptation off, dev mode, or tracking lost over 10% of rep | No change, not saved | 76-79 |
| Score `<65` or coverage `<.65` | range -.10, scale /.85 | 80-86 |
| Same, and score `<45` or coverage `<.45` | range -.15, scale /.8 | 82-84 |
| Head severity `>.2` | No increase | 87-88 |
| Score `>=90` and coverage `>=.9` | range +.05, scale /1.1 | 89-94 |
| Otherwise | Maintain | 95 |

Bounds: range floor `max(.4, minimumTravel/travel)` (62), ceiling 1; scale ceiling 2 (`slowestDurationScale`), floor `1/maxSpeed`, with `maxSpeed = 1 + fasterPercent/100` (50), default 1.4.

Session length arithmetic for the P4 estimate. A base move takes 8.5 s plus a 2.5 s gap, so 11 s. Target 120 with perfect play is 120 moves, about 22 minutes. At q=.6 the battery never drains and power gain per move is .467/120, so about 257 moves or 47 minutes. Adaptive slowing can stretch this further. This has not been measured.

## Tunables

| Value | Location | Default |
|---|---|---|
| Total reps target, exercise toggles, speed cap, saved speed/range, dev speed/range, calibrations, tutorial flag | `player-settings.json` in `persistentDataPath` (editor: `/tmp/emil-editor-player-settings.json`), `ShieldExerciseGame.cs:28`; model and clamps at `ExercisePlayerSettings.cs:8-41` | target 18 (1-120), cap 40% |
| Timing, tolerances, minimum travel, label | `VR/Shield/*.asset` | 3 / 1.5 / 3 / 1 s, .04 / .14 m, travel .02 (serratus .015) |
| `exerciseGapSeconds` | Serialized on game prefab (`ShieldExerciseGame.cs:41`) | 2.5 |
| `approachSpeed` | Serialized on stream prefab (`ExercisePathStream.cs:11`) | .55 |
| `adaptationEnabled`, `minimumRangeScale`, `slowestDurationScale` | Serialized on game prefab (`AdaptiveExerciseCoach.cs:10-16`). `fastestDurationScale` only applies when settings are null (52) | on, .4, 2 |
| Front offset | Constant, `ShieldExerciseGame.cs:228` | `(±.30, -.32, 1.35)` |
| Path samples, line colour multipliers | Constants, `ExercisePathStream.cs:12, 33` | 180; .55 current, .30 incoming |
| Line width | Generator `ExercisePathSetup.cs:15`, repaired `ConduitRepairGame.cs:71` | .025, .018 |
| Camera background | `ConduitRepairGame.cs:45` | `(.018,.03,.055)` |
| Tones | `ShieldArcadeFeedback.cs:15` | 660 capture, 330 endpoint, 880 reward; volume .18 |
| Haptic | `ShieldExerciseGame.cs:173` | amplitude .3, .08 s |

## Data export

Quest path: `/sdcard/Android/data/<bundle ID>/files/Sessions/<yyyyMMddTHHmmssfffZ>/` (`ShieldExerciseGame.cs:378`). Pull it with Export Quest Recordings into gitignored `Recordings/`. Simulation writes only in `TestMode`, to `/tmp/emil-shield-tests`. Files are written in `Finish` (399-403), except samples, which stream during play.

| File | Content |
|---|---|
| `samples.csv`, `.tsv` | 40 columns, one row per hand per sample up to 60 Hz. `timestamp_s, exercise_time_s, exercise_id, repetition, phase, active_side, side, valid, scorable`, hand, target and head poses, `hand_error_m, shield_normal_error_deg, head_displacement_m, head_rotation_deg`, `visual_shield_*`, `visual_target_*` (96) |
| `summary.csv`, `.tsv` | 18 columns per completed rep. `exercise_id, side, repetition, score, complete_movement, hold_seconds, start_reached, return_reached, endpoint_reached, valid_seconds, mean_hand_error_m, mean_shield_normal_error_deg, path_accuracy_percent, travel_coverage, phase_completion, unpenalized_score, head_position_severity, head_penalty_points` (403) |
| `session.json` | `schemaVersion=shield-5`, `scoreVersion=position-only-hand-5`, `calibrationMode=personal-range-stable-pose-2`, `controlMapping=front-translation-2`, `presentation=finite-3d-paths-mirror-coach-1`, `developerMode`, `challengeOrder`, calibrations, exercises (94) |
| `outcome.json` | `complete`, `tracking_pause_seconds` |
| `adaptive-coach.json` | One `Decision` per rep, `policy=position-aware-range-tempo-v5` |
| `conduit.json`, `conduit.csv`, `.tsv` | `version=conduit-1`; `movement, accuracy, ship_power, battery, outcome` |
| `conduit-path.csv`, `.tsv` | `movement, exercise_id, side, exercise_time_s, path_quality, node_repaired` |
| `replay.json` | Replay schema "1", capped at 18,000 frames (`WandSessionReplay.cs:48`) |

There is no participant ID anywhere. Sessions are identified only by folder timestamp. `repetition` counts per exercise and arm; `movement` counts across the session. `ShieldExerciseVerification.cs:45` asserts the summary column count, so update it if columns change.

## Where to change X

| Backlog item | File and method |
|---|---|
| Line continuity, gaps | `ExercisePathStream.LateUpdate` (offset at 25, `n` loop), `ShieldExerciseGame.LoadChallenge:243` and `exerciseGapSeconds` |
| Line at the hand versus in front (open decision) | `ShieldExerciseGame.ProjectShield:225`, used by `MapToFront:230` and `PathVisualPoint:266`; `Verify` in `ShieldExerciseSetup:44-48` asserts displacement |
| Line brightness and glow | `ExercisePathStream.cs:32-33`; `ConduitRepairGame.NodeRepaired:40` and `LateUpdate:66, 71` |
| Progressive fill | `ConduitRepairGame.LateUpdate:69-72`, `first` in `ExercisePathStream.cs:26` |
| Hand trail | `ConduitRepairGame.UpdateTrails` adds a `TrailRenderer` to `game.shields` at runtime (prefabs untouched); time .25 s, width .03 m. Previously: new component on the player marker. Marker pose is set at `ShieldExerciseGame.cs:129`. Prefab comes from `ConduitSetup.Marker:16`, so add it there. `ControllerTrackingTrails` records unmapped hand positions and is trigger-driven |
| Line look, material | `ExercisePathSetup.cs:11-15`, `VR/Shield/Energy Pulse.mat`; theme in `ConduitSetup.Build:29-37` |
| Show score | `ShieldExerciseGame.DrawText:360`; `ConduitRepairGame.cs:75` status text |
| Multiplier logic | `ShieldArcadeFeedback.Reward:21`; `success` is passed at `ShieldExerciseGame.cs:338`, where a perfect threshold would go |
| Multiplier display | `ShieldArcadeFeedback.Show:29`; it is hidden by `!conduit` at `ShieldExerciseGame.cs:139`, or read `Combo` into `ConduitRepairGame.cs:75` |
| God/Præcis labels | `ShieldExerciseGame.CompleteRep:335-346`. `message` is not displayed during the mission, so draw the label in `ConduitRepairGame.cs:74-75` or add new text |
| Round summary, best streak | `ExerciseGameMenu.Refresh:69`; streak tracked next to `Combo`; `DrawText:354` does not display with the menu wired |
| Audio | `ShieldArcadeFeedback.Awake:15`, `Tone:16`. In-tolerance signal is `ShieldExerciseGame.RepairQuality` (45) |
| Haptics | `ShieldExerciseGame.cs:173`; off switch field in `ExercisePlayerSettings`, toggle via `ExerciseMenuSetup.Build` and `ExerciseGameMenu.Change` |
| Exercise labels | `ExerciseMenuSetup.cs:41` (menu); `ShieldExerciseSetup.cs:27` and `VR/Shield/*.asset` `displayName` (gameplay); cues in `DrawText:360` |
| Game name | `ExerciseMenuSetup.cs:36-39`, `ShieldExerciseGame.cs:352`, `ConduitRepairGame.cs:27, 74-75`, `ProjectSettings/ProjectSettings.asset:16` |
| Stale strings | `ShieldExerciseGame.cs:353, 359`, `ExerciseGameMenu.cs:69, 83`, `ExerciseMenuSetup.cs:37-39`; menu text needs `ExerciseMenuSetup` and a rebuild |
| Session limits, breaks | `ShieldExerciseGame.cs:178-180` (extension, next move); `LoadChallenge:238`; target clamp `ExercisePlayerSettings.cs:38, 69`; slider `ExerciseMenuSetup.cs:40` |
| Failure ending | `RepairRun.Resolve` (`ConduitRepairGame.cs:17-18`), `Result:27` |
| Therapist limits | `ExerciseGameMenu.Navigate:40`, `Change:52`, `Refresh:76, 82`; widgets in `ExerciseMenuSetup.cs:37-47`; `ShieldExerciseGame.ResetSavedSettings:34` |
| Participant ID | `ShieldExerciseGame.Settings:94`, `OpenRecording:378`, `Finish:400`. Entry UI does not exist; `ExerciseGameMenu` has no text input |
| Bundle ID | `ProjectSettings/ProjectSettings.asset:173`, `WandDataExport.cs:22` |

## Unverified

- Actual behaviour in Unity or on a Quest. Execution order between `ShieldArcadeFeedback.Update` and `ShieldExerciseGame.Update` decides whether the burst ring shows; no script has a custom execution order.
- Whether `Shader.Find` in `ShieldCoachDisplay.Awake` survives player stripping. README reports earlier device launches, not this commit.
- How `/tmp` paths behave in the Windows editor. Nothing here was run.
- `VR/Prototype/TEST_REPORT.md` was not read; it belongs to the legacy prototype.
