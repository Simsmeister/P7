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
  VR/Shield/README.md          dated changelog; "Current state" at the top overrides older text
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
| `ShieldExerciseGame.cs` | Session state machine, tracking, scoring, recording, export | `Begin(bool)`, `SkipTutorialPreview`, `ResetSavedSettings`, `PathDefinition`, `EffectiveCalibration`, `PathVisualPoint`, static `ProjectShield`, `MovementScore`, `HeadPositionSeverity`, `DueAtRepEnd`; props `PathClock`, `RepairQuality`, `Score`, `CompletedRepetitions`, `CurrentStep`, `MenuVisible` | All below | active |
| `ShieldExerciseProfile.cs` | ScriptableObject per exercise: timing, tolerances, path shape | `Progress`, `Phase`, static `Path`, `Quality`, `Cycle`; enum `ShieldMovement` | Read by game, coach, stream | active |
| `AdaptiveExerciseCoach.cs` | Single-script neural coach: training, inference, base model loading and separate personal levels | `Configure`, `ProfileFor`, `RangeFor`, `Observe`, `ExportJson`; class `Decision` | Game, player settings | active |
| `ExercisePlayerSettings.cs` | `player-settings.json` model, playlist; `ExerciseParticipant` is the `participant.json` counter | `Load`, `Save`, `Arm`, `Limit`, `Playlist`, `TransformCalibration`, `UsableCalibration`; `ExerciseParticipant.Load`, `Advance`, `Id`, static `FormatId`, `NextNumber`, `ParseId`, `ParseFile` | Game, coach, menu | active |
| `ExercisePathStream.cs` | Draws current and incoming line, plus a runtime `Path Bridge` LineRenderer (half width, half next colour) joining them | `LateUpdate`, static `Approach`; `approachSpeed` | Reads game | active |
| `ConduitRepairGame.cs` | Active theme: power, battery, nodes, white repaired sections, status text, runtime multiplier pips, floor lights, camera background. `RepairRun` holds outcome logic | `Begin`, `Resolve`, `Export`, `ExportNodes`, `NodeRepaired`, `SectionLit`, `PipLit`, `UpdatePips`, `Result`, `Run` | Reads game; uses `ExercisePathStream.Approach` | active |
| `ExerciseGameMenu.cs` | World-space menu, ray and mouse input. Starts locked: SETTINGS needs 5 presses within about 4 s, RECALIBRATE needs the unlock, and START or RECALIBRATE relocks. Stops accidental changes only, not access control; the gesture awaits team and physiotherapist confirmation | `Navigate`, `Change`, `Recenter`, static `Hit`, `UnlockCount`; `widgets` | Game, player settings | active |
| `ShieldCoachDisplay.cs` | CURRENT card with stick figure during the mission | `Draw`, static `Example` | Reads game; `WandSessionReplay.EstimateElbow` | active |
| `WandSessionReplay.cs` | Records and plays replay; also the tutorial mannequin and calibration guide | `BeginRecording`, `Capture`, `Save`, `Open`, `Close`, `OpenExerciseDemo`, `HoldDemoAt`, `UpdateGuidance`, `LoadLatest`, `MirrorPoint` | Game; `WandPathGame.ResizeWand` and `OpenDemo` (72, 86) | active, with legacy wand branches |
| `EmilVRInput.cs` | Rig hand and controller visuals | `mode`, `allowModeSwitch`, `CycleMode`, `SetMode` | Game forces Controllers (`ShieldExerciseGame.cs:111`) | active |
| `ShieldArcadeFeedback.cs` | Reactor Rescue: all game audio (procedural one-shots, streak notes, multiplier chime, looping line tone on a runtime `Line Tone` child), points, combo, burst ring. HUD hidden; points and multiplier are shown through Conduit status text and pips | `Captured`, `Block`, `Reward`, `LineTone`, `StopLineTone`, `Show`, `ResetMission`, `Points`, `Combo`, `BestCombo`, `Multiplier`, `LastLabel`, static `Perfect`, `Label`, `StreakNote`, `LineLevel`, `LineCutoff` | Called by game | half-alive |
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

1. Menu. `Start` calls `EnsurePlayerSettings` and `SetFrame` (112). `ExerciseGameMenu.LateUpdate` hit-tests rays and invokes `Navigate("start")`, which relocks the menu and calls `Begin()`; `"redo"` calls `Begin(true)` only while unlocked. No A, B or keyboard path reaches `Begin(true)`, reset or Settings; the scene has no `EventSystem`, so `LateUpdate` is the only thing that presses buttons. A first-time START still queues calibration through `Begin()`.
2. `Begin(bool)` (193): sets frame from the head, configures the coach, resets counters. For each enabled exercise and arm it either restores a saved calibration into `calibrations` or queues it (202-206). Queue not empty: `PrepareBlock`. Empty: `StartMission`.
3. Calibration. `PrepareBlock` (209) builds a default path and opens the mannequin demo (`State.Demo`). When the demo ends or A skips it, `StartAutomaticPose(false)` sets `CaptureStart`. `UpdateAutomaticPose` (300) needs 1.25 s within 2.5 cm in an allowed pose (`CalibrationPoseAllowed`, 289). Start is captured, then `CaptureEnd()` (315) validates the endpoint with `RangeDirectionAllowed` (280), stores it and saves it unless in dev mode. `Ready` (148) waits for the hand back at start, then moves to the next queued block or `StartMission`.
4. `StartMission` (231): `conduit.Begin(totalRepetitions)`, `PlayerSettings.Playlist(exercises)` (a balanced shuffle, `ExercisePlayerSettings.cs:63`), `LoadChallenge`; a 9 s `Tutorial` state if needed (236, 153).
5. `LoadChallenge` (238): picks `block`, applies `EffectiveCalibration` (252, adaptive range through `ScaledCalibration`), sets `Approaching` with `clock=-exerciseGapSeconds` (2.5 s).
6. Path drawing, every `LateUpdate` while `PathsVisible`. `ExercisePathStream` (17) samples 180 points for the current path (`n=0`, only unconsumed time) and the next (`n=1`, delayed by `Cycle + gap`), then applies `Approach(point, forward, secondsUntilArrival, 0.55)`. Points come from `PathVisualPoint` (266), which uses `ProjectShield` (225): offset `(±.30, -.32, 1.35)` plus hand displacement. Conduit adds 24 nodes and 119 white or amber sections along the same path.
7. Approach: the clock runs only while tracked (154), and active play (`activeSeconds`, `activeSinceBreak`) grows only while tracked in `Approaching` and `Playing`. At 0, `ResetRep`, `State.Playing`, and `OpenRecording` on first entry (155).
8. Per frame in `Playing` (156-181): `Target` gives `p` at `clock`; the ghost moves to `MapToFront(p)`. Error `pe=|hand-p|`. Then `repQuality`, coverage min/max (163-164), head severity (166-168), start, hold and return flags (170-172), endpoint check with tone, ring and haptic (173), 60 Hz `Record` (175). The clock pauses when untracked (176).
9. Rep complete at `clock>=Profile.Cycle` (177): `CompleteRep` (328) computes `complete` and score, writes a summary row, then calls `arcade.Reward`, `conduit.Resolve` (passing whether the cap is due), `adaptiveCoach.Observe`.
10. Adaptive update: `Observe` changes the state for that exercise and arm, saves `arm.speed` and `arm.range`, and applies to the next attempt of that pair through `ProfileFor` and `RangeFor`. In-progress attempts never change.
11. Conduit power: `RepairRun.Resolve` (`ConduitRepairGame.cs:12`) updates power and battery, then sets defeat, victory or `session_limit` in that order. Floor lights track power (`ConduitRepairGame.cs:49`).
12. Next or finish (178-180): when `conduit.Run.Finished`, `Finish(Run.GoalReached)`, which is true for victory and `session_limit`. Otherwise `playlistIndex++`, and the playlist extends by a new batch when near its end. `DueAtRepEnd` then picks `State.Break` or `LoadChallenge`.
    Break: `clock` counts down from `breakSeconds` whether or not the hand is tracked, then `LoadChallenge` runs the normal gap; A skips through `SkipTutorialPreview`. `PathsVisible` is false, so paths, bridge, nodes, sections and the Conduit status panel hide. `IsMission` is false, so the coach card hides, and the ghost and hand marker hide too. The instruction `board` stays on and `DrawText` shows the wording and countdown. Line tone, samples, summaries and `Resolve` all depend on `Playing`, so FLOW and ENERGY hold still. X still stops the session.
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
| Outcome | Defeat at battery 0, checked first; victory at power full; `session_limit` when the cap is due on that rep | `ConduitRepairGame.cs:18` |
| Node repaired (export, charge, cue) | `q>=.65` |
| Line glow colour | `Glow(q)`: lerp amber*.35 to white over `InverseLerp(.4,.9,q)`; sections stay lit until the rep ends, nodes fade over 1.3 s; also tints the hand trail | `ConduitRepairGame.cs:40, 66, 71` |
| Perfect | `complete && q>=.9 && coverage>=.9` (`ShieldArcadeFeedback.Perfect`); label PRÆCIS if perfect, GOD if complete | `ShieldArcadeFeedback.cs` |
| Points | `+100q`; cell charge if `complete`; if perfect, `Combo++` and `+100q*min(Combo-1,4)`; else `Combo=0`. `BestCombo` = max `Combo`; `Multiplier` = `clamp(Combo,1,5)` | `ShieldArcadeFeedback.cs:23-24` |

Adaptive coach was replaced on 2026-11-08. `AdaptiveExerciseCoach.cs` contains a 2–12–2 tanh network, explicit editor training command and separate player persistence. It loads `Assets/Resources/AdaptiveCoach/base-model.json`; labelled synthetic examples live in `Assets/VR/Training/coach-training.json`. See `ADAPTIVE_COACH.md` for file formats and retraining.

`Observe` predicts range-fraction and relative-speed changes from score/100 and coverage after each valid repetition. Small predictions (<.015 range / <.0225 speed) are ignored. Tracking reliability, dev-mode exclusion, head-displacement no-increase guard and range/speed bounds are outside the network. Personal levels and measurement averages live alongside the settings file in `*-adaptive.json`; speed/range are mirrored to settings for existing UI compatibility. Existing progress migrates if no personal file exists. Reset clears active personal history; recalibration resets that arm. The base weights never change during play.

Bounds: range floor `max(.4, minimumTravel/travel)`, ceiling 1; duration scale ceiling 2 (`slowestDurationScale`), floor `1/maxSpeed`, with `maxSpeed = 1 + fasterPercent/100`, default 1.4. Network outputs are bounded to ±.1 range change and ±.15 relative speed change per attempt.

Session length arithmetic for the P4 estimate. A base move takes 8.5 s plus a 2.5 s gap, so 11 s. Target 120 with perfect play is 120 moves, about 22 minutes. At q=.6 the battery never drains and power gain per move is .467/120, so about 257 moves or 47 minutes. Adaptive slowing can stretch this further. This has not been measured. The cap now stops it at 15 minutes of active play, plus 2 breaks of 45 s, so about 17 minutes without tracking pauses. At 11 s per move that is about 82 moves, so at a target above about 80 the cap comes before victory.

## Tunables

| Value | Location | Default |
|---|---|---|
| Total reps target, exercise toggles, speed cap, saved speed/range, dev speed/range, calibrations, tutorial flag | `player-settings.json` in `persistentDataPath` (editor: `/tmp/emil-editor-player-settings.json`), `ShieldExerciseGame.cs:28`; model and clamps at `ExercisePlayerSettings.cs:8-41` | target 18 (1-120), cap 40% |
| Participant counter (default awaiting team confirmation) | `participant.json` in `persistentDataPath` (editor: `/tmp/emil-editor-participant.json`), `ShieldExerciseGame.Participant`; kept apart from `player-settings.json` so a reset cannot lose it | first value `P001`, +1 per confirmed reset |
| Timing, tolerances, minimum travel, label | `VR/Shield/*.asset` | 3 / 1.5 / 3 / 1 s, .04 / .14 m, travel .02 (serratus .015) |
| `exerciseGapSeconds` | Serialized on game prefab (`ShieldExerciseGame.cs:41`) | 2.5 |
| `breakAfterSeconds`, `breakSeconds`, `sessionCapSeconds` | Serialized on `ShieldExerciseGame`, class defaults apply while the prefab has no saved value. Active-play seconds except the break length; 0 switches a limit off. Prototype values, not clinical guidance; a physiotherapist must confirm them. No menu slider yet, since menu widgets come from `ExerciseMenuSetup` | 300, 45, 900 |
| `approachSpeed` | Serialized on stream prefab (`ExercisePathStream.cs:11`) | .55 |
| `adaptationEnabled`, `minimumRangeScale`, `slowestDurationScale` | Serialized on game prefab (`AdaptiveExerciseCoach.cs:10-16`). `fastestDurationScale` only applies when settings are null (52) | on, .4, 2 |
| Front offset | Constant, `ShieldExerciseGame.cs:228` | `(±.30, -.32, 1.35)` |
| Path samples, line colour multipliers | Constants, `ExercisePathStream.cs:12, 33` | 180; .55 current, .30 incoming |
| Line width | Generator `ExercisePathSetup.cs:15`, repaired `ConduitRepairGame.cs:71` | .025, .018 |
| Camera background | `ConduitRepairGame.cs:45` | `(.018,.03,.055)` |
| Tones | `ShieldArcadeFeedback.Awake` | 660 capture, 330 endpoint, 880 reward; volume .18 |
| Streak notes, multiplier chime, line tone | Constants at the top of `ShieldArcadeFeedback`: `StreakHz`, `LineHz`, `LineLevelOff/On`, `LineCutoffOff/On`, `LineFadeSeconds`, `ChimeDelay` | 880-1760 Hz, A major pentatonic; 220 Hz; level .012-.035; cutoff 300-1600 Hz; fade .2 s; chime after .2 s. None tuned by ear |
| Haptic | `ShieldExerciseGame.cs:173` | amplitude .3, .08 s |

## Data export

Quest path: `/sdcard/Android/data/<bundle ID>/files/Sessions/<yyyyMMddTHHmmssfffZ>/` (`ShieldExerciseGame.cs:378`). Pull it with Export Quest Recordings into gitignored `Recordings/`. Simulation writes only in `TestMode`, to `/tmp/emil-shield-tests`. Files are written in `Finish` (399-403), except samples, which stream during play. Nothing is recorded in a break, so `timestamp_s` in samples and replay jumps across it and replay playback holds the last frame.

| File | Content |
|---|---|
| `samples.csv`, `.tsv` | 40 columns, one row per hand per sample up to 60 Hz. `timestamp_s, exercise_time_s, exercise_id, repetition, phase, active_side, side, valid, scorable`, hand, target and head poses, `hand_error_m, shield_normal_error_deg, head_displacement_m, head_rotation_deg`, `visual_shield_*`, `visual_target_*` (96) |
| `summary.csv`, `.tsv` | 18 columns per completed rep. `exercise_id, side, repetition, score, complete_movement, hold_seconds, start_reached, return_reached, endpoint_reached, valid_seconds, mean_hand_error_m, mean_shield_normal_error_deg, path_accuracy_percent, travel_coverage, phase_completion, unpenalized_score, head_position_severity, head_penalty_points` (403) |
| `session.json` | `schemaVersion=shield-6` (was `shield-5`; `session_limit` and breaks are new meaning, no column changed; nothing in the code compares these strings), `participantId` (additive, same unreleased `shield-6`), `scoreVersion=position-only-hand-5`, `calibrationMode=personal-range-stable-pose-2`, `controlMapping=front-translation-2`, `presentation=finite-3d-paths-mirror-coach-1`, `developerMode`, `breakAfterSeconds`, `breakSeconds`, `sessionCapSeconds`, `challengeOrder`, calibrations, exercises (94) |
| `outcome.json` | `participant_id`, `complete` (true for victory and `session_limit`), `outcome` (`victory`, `defeat`, `session_limit`, `aborted`), `tracking_pause_seconds`, `active_seconds`, `breaks` |
| `adaptive-coach.json` | One `Decision` per rep, `policy=neural-range-tempo-v1`, model ID and predicted range/speed changes |
| `conduit.json`, `conduit.csv`, `.tsv` | `version=conduit-2` (was `conduit-1`; adds outcome `session_limit`, on the last row of the CSV); `movement, accuracy, ship_power, battery, outcome` |
| `conduit-path.csv`, `.tsv` | `movement, exercise_id, side, exercise_time_s, path_quality, node_repaired` |
| `replay.json` | Replay schema "1", capped at 18,000 frames (`WandSessionReplay.cs:48`) |

Participant ID (default awaiting team confirmation): an anonymous per-device counter with no name or other personal data. The tester reads `Participant P003` on the home menu (`homeStatus`) and writes it on the paper or external VRNQ questionnaire. To link data, match that ID to `participantId` in `session.json` or `participant_id` in `outcome.json`; a session's CSV rows join through their session folder, and the CSVs have no ID column. Confirming RESET SAVED SETTINGS moves to the next ID, and the confirmation page footer names it; starting a session does not, so repeat sessions share an ID. Numbers burned by a reset without a session leave harmless gaps. A session that was killed before `Finish` has no `session.json`, so it has no ID.

Missing or damaged `participant.json`: the game continues after the highest ID found in `Sessions/*/session.json`, saves that, and warns once in the footer; with no file and no exports it is a first run and gives `P001`. A failed write keeps the session going and warns in the footer, but the ID can repeat after a restart, so the tester should note it. `TestMode` runs export `TEST` and never advance the counter. IDs are unique per device only, and uninstalling wipes the counter along with `Sessions`.

`repetition` counts per exercise and arm; `movement` counts across the session. `ShieldExerciseVerification.cs:45` asserts the summary column count, so update it if columns change.

## Where to change X

| Backlog item | File and method |
|---|---|
| Line continuity, gaps | `ExercisePathStream.LateUpdate` (offset at 25, `n` loop), `ShieldExerciseGame.LoadChallenge:243` and `exerciseGapSeconds` |
| Line at the hand versus in front (decided: stays in front) | `ShieldExerciseGame.ProjectShield:225`, used by `MapToFront:230` and `PathVisualPoint:266`; `Verify` in `ShieldExerciseSetup:44-48` asserts displacement |
| Line brightness and glow | `ExercisePathStream.cs:32-33`; `ConduitRepairGame.NodeRepaired:40` and `LateUpdate:66, 71` |
| Progressive fill | `ConduitRepairGame.LateUpdate` section loop and `SectionLit` (sections held at `PathVisualPoint` with no approach offset; outward fill .018 m wide, return a .01 m core lifted .01 m toward the player; passed nodes still drift and fade through `Point`), `first` in `ExercisePathStream.LateUpdate` |
| Hand trail | `ConduitRepairGame.UpdateTrails` adds a `TrailRenderer` to `game.shields` at runtime (prefabs untouched); time .25 s, width .03 m. Previously: new component on the player marker. Marker pose is set at `ShieldExerciseGame.cs:129`. Prefab comes from `ConduitSetup.Marker:16`, so add it there. `ControllerTrackingTrails` records unmapped hand positions and is trigger-driven |
| Line look, material | `ExercisePathSetup.cs:11-15`, `VR/Shield/Energy Pulse.mat`; theme in `ConduitSetup.Build:29-37` |
| Show score | `ShieldExerciseGame.DrawText:360`; `ConduitRepairGame.cs:75` status text |
| Multiplier logic | `ShieldArcadeFeedback.Reward`; `complete` and `Perfect(...)` are passed at `ShieldExerciseGame.cs:338` |
| Multiplier display | `ConduitRepairGame` status text and `UpdatePips`: five clones of `nodes[0]` parented to `hud` at runtime, in a row .165 m above its centre, between the status panel and the instruction board and clear of the line and coach card; lit .03 m and bright, unlit .018 m and dim. Not yet seen on a headset. `ShieldArcadeFeedback.Show` is the old HUD, hidden by `!conduit` at `ShieldExerciseGame.cs:139` |
| God/Præcis labels | `ShieldArcadeFeedback.Label`, `LastLabel`; drawn in the `clock<0` cue in `ConduitRepairGame` status text |
| Round summary, best streak | `ExerciseGameMenu.Refresh:69`; streak tracked next to `Combo`; `DrawText:354` does not display with the menu wired |
| Audio | `ShieldArcadeFeedback`: `Awake` and `Tone` build the clips, `Reward` picks what plays at rep end (perfect: streak note in place of the 880 Hz tone, plus the chime when `Multiplier` rises; complete: 880 Hz; otherwise nothing). The line tone is driven each frame from `ShieldExerciseGame.Update` (next to `arcade.Show`) through `LineTone`, using `RepairQuality`; it has its own child GameObject because `AudioLowPassFilter` filters every source on its GameObject. Compiles and passes `ConduitSetup.Verify`; not heard on a headset. A mute would go at those constants |
| Haptics | `ShieldExerciseGame.cs:173`; off switch field in `ExercisePlayerSettings`, toggle via `ExerciseMenuSetup.Build` and `ExerciseGameMenu.Change` |
| Exercise labels | `ExerciseMenuSetup.cs:41` (menu); `ShieldExerciseSetup.cs:27` and `VR/Shield/*.asset` `displayName` (gameplay); cues in `DrawText:360` |
| Game name | `ExerciseMenuSetup.cs:36-39`, `ShieldExerciseGame.cs:352`, `ConduitRepairGame.cs:27, 74-75`, `ProjectSettings/ProjectSettings.asset:16` |
| Player-facing text | `ShieldExerciseGame.DrawText`, `ExerciseGameMenu.Refresh`, `ExerciseMenuSetup.cs:37-39`; menu text needs `ExerciseMenuSetup` and a rebuild. Old `WAND PATH LAB` default on the board in `Shield Exercise Game.prefab` is replaced by `DrawText` at runtime. Wand wording in `WandPathGame` and the `WandSessionReplay` demo label is reached only through the dead `OpenDemo` |
| Session limits, breaks | Values in `breakAfterSeconds`, `breakSeconds`, `sessionCapSeconds`; decision in `ShieldExerciseGame.DueAtRepEnd`, asserted in `ConduitSetup.Verify`; rest in `State.Break` (`Update`, `DrawText`, `SkipTutorialPreview`); ending in `RepairRun.Resolve` and `Result`. Target clamp `ExercisePlayerSettings.cs:38, 69`; slider `ExerciseMenuSetup.cs:40`; sliders for the limits would go there and need a generator run |
| Failure ending | `RepairRun.Resolve` (`ConduitRepairGame.cs:17-18`), `Result:27` |
| Therapist limits | Lock and press count in `ExerciseGameMenu.Navigate` and `UnlockCount` (`UnlockPresses`, `UnlockWindow`), notes in the `footer` line of `Refresh`; asserted in `ConduitSetup.Verify`. Unlock gesture is a default awaiting team and physiotherapist confirmation. Saved limits still apply while locked. Widgets in `ExerciseMenuSetup.cs:37-47`; `ShieldExerciseGame.ResetSavedSettings:34` |
| Participant ID | Counter and recovery in `ExerciseParticipant` (`ExercisePlayerSettings.cs`), helpers asserted in `ConduitSetup.Verify`; advance in `ShieldExerciseGame.ResetSavedSettings`; export in `Settings` and `Finish`; display in `ExerciseGameMenu.Refresh` (`homeStatus`, `footer`). The prefixed line has room at font 23 by Arial width estimate; a fourth line would be cut off by the 100-unit `homeStatus` label. Entry UI does not exist; `ExerciseGameMenu` has no text input. Default awaiting team confirmation |
| Bundle ID | `ProjectSettings/ProjectSettings.asset:173`, `WandDataExport.cs:22` |

## Unverified

- Actual behaviour in Unity or on a Quest. Execution order between `ShieldArcadeFeedback.Update` and `ShieldExerciseGame.Update` decides whether the burst ring shows; no script has a custom execution order.
- Whether `Shader.Find` in `ShieldCoachDisplay.Awake` survives player stripping. README reports earlier device launches, not this commit.
- How `/tmp` paths behave in the Windows editor. Nothing here was run.
- How any of the audio sounds. Clips and levels were chosen on paper; nobody has listened to them.
- `VR/Prototype/TEST_REPORT.md` was not read; it belongs to the legacy prototype.

## 2026-11-05: sustained perfect tracking bonus
`PerfectTrackingBonus` (in `ShieldArcadeFeedback.cs`) is a separate timing model: quality >= .9 for 2 seconds, then 10 × multiplier points every .5 seconds; multiplier rises every four ticks, capped at x5. The first tick is at 2 seconds. `ShieldExerciseGame` drives it with current-frame positional quality only during tracked outward/hold/return phases. Off-target, tracking loss, rest, approach, replay and session reset clear the streak; awarded points remain. Frame time is capped at .1 seconds, matching the exercise clock. It does not change accuracy, clinical summaries or adaptive difficulty.
`ShieldArcadeFeedback` adds bonus points to existing `Points`, creates a world-space total below the status board (2.6 m forward), and a pulsing gold bonus caption above the target ring. Five cached procedural bell clips provide rising dings. Existing repetition combos, line audio and pips remain. Runtime text children inherit the existing prefab font/material, so prefab regeneration is unnecessary. `game-points.json` adds versioned total/bonus points and timing parameters alongside existing exports. `ConduitSetup.Verify` covers timing/reset/cap behavior; preview capture now waits until 3.2 seconds to show the bonus.

### Gameplay HUD polish (2026-11-05)
Game points now use 1,000 × quality per repetition (with the existing perfect-rep multiplier), and 100 × multiplier per tracking tick. A normal successful run is comfortably above 10,000; very short or aborted runs can be lower. Export version `perfect-flow-2` records both base point values. Accuracy/adaptation remain on their existing scale.
During Playing/Approaching the large instruction board and Conduit status panel are hidden. Calibration, tutorial and break instructions remain. `ShieldArcadeFeedback.CreateHud` creates a box-free floating HUD: animated score, compact streak, two thin energy/flow rails, brief completion accent, and a tracking-loss warning only when needed. Each tracking tick pulses a gold ring around the target. The bottom current-movement guide keeps its figure and title but removes the opaque backing. Runtime visuals use existing prefab materials/fonts; generators need no run. Integration now requires a completed simulated run to exceed 10,000 points.

### Dedicated results page (2026-11-05)
`ExerciseGameMenu.EnsureResults` creates a reusable results page, also called by `ExerciseMenuSetup.Build` so it is stored in the menu prefab. Existing prefabs receive the same page at runtime if absent. On a newly finished run, the menu opens results (page 4), with an animated final game score, accuracy, best perfect-rep streak, movement count and perfect-flow bonus contribution. Outcome distinguishes victory, cap, defeat and aborted sessions. Play Again calls the normal saved-settings start; Main Menu switches to home without restarting; Watch Replay returns to the same results page afterward. Results can be re-centered with the existing headset recenter event. Therapist lock and data export stay unchanged. Integration checks results visibility, exact final score, Main Menu and Play Again, and captures `/tmp/emil-results.png`.
