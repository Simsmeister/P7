# PROJECT_MAP

Last verified: commit `a6ab39a2`, 2026-11-16. Static code reading plus a batch run of `P7Tests.RunChecks` (passed); nothing was run on a headset for this check. Updated 2026-11-17 for the iteration 2 changes (CLAUDE.md section 12): compiled and `RunChecks` passed in the editor, Play Mode tests not run, line numbers below not refreshed. Update this file in the same commit when structure changes.

Bare file names below live in `Assets/VR/Runtime/` or `Assets/VR/Editor/`. Line numbers are for `a6ab39a2`; if one is off, search for the named method.

## Folders

```
Assets/
  Scenes/P7.unity              the only scene, the only one in the build
  VR/Runtime/                  game code (namespace P7), table below
  VR/Editor/                   Tools > P7 menu, checks and the Play Mode test
  VR/Shield/                   game prefabs, the three exercise .asset files, materials;
                               README.md is a dated changelog with old names
  VR/Prefabs/                  VR Rig and hand prefabs
  VR/Prototype/                Session Replay.prefab (the replay/demo mannequin) and its materials
  VR/HandVisualizer/           hand meshes used by the rig's tracked hands (inactive)
  VR/Training/                 coach-training.json, labelled examples for the coach network
  VR/XRGeneralSettings.asset   XR loader settings
  Resources/AdaptiveCoach/     base-model.json, the trained coach network
  XR/, Settings/               OpenXR and URP settings
  Prefabs/gymnasium/           imported gym model, not used in the scene yet
  InputSystem_Actions          project-wide input actions (required by the Input System settings)
  TempDiagnostics/             temporary debugging tools (Tools > Temp); remove before hand-in
  _Recovery/0.unity            crash-recovery scene committed by accident; remove
Tools/coach_training_labels.py  the synthetic label rules for the coach network
ADAPTIVE_COACH.md              coach file formats, retraining steps and guards
Builds/, Recordings/           build output and exported sessions (gitignored)
.utmp/                         Android build cache; gitignored, but about 47 files are still tracked
```

## Runtime scripts

| Script | What it does | Main members |
|---|---|---|
| `ExerciseGame.cs` | The session: reads the controllers, runs the state machine (Welcome, Demo, CaptureStart, CaptureEnd, Ready, Tutorial, Approaching, Playing, Finished, Break), the tutorial, scoring of each repetition, breaks and the end of the session | `Begin(bool)`, `StartMission`, `BeginTutorial`, `AdvanceTutorial`, `LoadChallenge`, `UpdatePlaying`, `MeasureFrame`, `ScoreRepetition`, `DueAtRepEnd`, `Finish`, `EffectiveCalibration`, `ClampToReach`, `ShoulderOffset`, `StepDistance`, `OffSpot`, `DepthDirection`, `PathVisualPoint`, `ProjectToFront`, `ResetSavedSettings`, `SkipTutorialPreview` (A), `SkipTutorial` (B); runtime `Floor Ring` and `Depth Guide` lines |
| `ExerciseGame.Calibration.cs` | Saved calibrations, the automatic estimate and its one-time anchoring, the exercise demo and the manual pose capture | `PrepareCalibration`, `AutomaticEstimate`, `BodyScaleFor`, `TryAnchorAutomaticStart`, `PrepareBlock`, `UpdatePoseCapture`, `TryCaptureEnd`, `UpdateReady`, `RangeDirectionAllowed` |
| `ExerciseGame.Recording.cs` | 60 Hz samples while playing and all files written at the end of a session | `OpenRecording`, `RecordAt60Hz`, `AddSummaryRow`, `WriteSessionFiles`, class `SessionInfo` |
| `ExerciseProfile.cs` | ScriptableObject per exercise: timing, tolerances and the path shape | `Progress`, `Phase`, static `Path`, `Quality`, `Cycle`; enum `ExerciseMovement` |
| `Calibration.cs` | Start, end and pivot of one exercise and arm; converts between world and head-relative storage, and scales the range | `automatic`, `startAnchored`, `estimateVersion`, `ToLocal`, `ToWorld`, `IsUsableFor`, `Scaled` |
| `AdaptiveCoach.cs` | Neural coach (4 inputs, 16 hidden, 3 outputs) that changes range, speed and the start point per exercise and arm; personal levels; training. See ADAPTIVE_COACH.md | `Configure`, `ProfileFor`, `RangeFor`, `StartShiftFor`, `Observe`, `ObserveStart`, `SetMeasuredLevel`, `Recalibrated`, `ExportJson`; static `Predict`, `PredictStartFollow`, `NextStartShift`, `ErrorSeverity`, `Train`, `PersonalPath`, `ResetPersonalData` |
| `SavedSettings.cs` | `player-settings.json`: per-arm settings, speed limits, playlist; also `JsonFile.Write` (atomic write) | `ArmSettings`, `Load`, `Save`, `Arm`, `Limit`, `Playlist` |
| `Participant.cs` | `participant.json`: the anonymous P001, P002... counter, with recovery from `Sessions/*/session.json` | `Load`, `Advance`, `Id`, `NextId`, static `FormatId`, `ParseId`, `ParseFile` |
| `SessionProgress.cs` | ENERGY (`power`) and FLOW (`battery`) and the session outcome; saved as `conduit.json` | `Resolve`, `Finished`, `GoalReached` |
| `PathStream.cs` | Draws the current and next line flying towards the player, plus the runtime `Path Bridge` between them | `LateUpdate`, static `Approach`; `approachSpeed` |
| `PathFeedback.cs` | 24 nodes along the line, up to 119 lit sections behind the hand, the hold ring, hand trail, floor lights and camera background; owns the `SessionProgress` run | `Begin`, `Resolve`, `Run`, `ExportProgress`, `ExportNodes`, static `NodeRepaired`, `SectionLit`, `Glow` |
| `ScoreFeedback.cs` | Points, streak, all sounds and the floating score HUD | `Reward`, `EndpointReached`, `TrackPerfect`, `LineTone`, `StopLineTone`, `ResetMission`, `Points`, `Combo`, `BestCombo`, `Multiplier`, `TrackingBonusPoints`; static `Perfect`, `StreakNote`, `LineLevel`, `LineCutoff` |
| `PerfectFlowBonus.cs` | Bonus points for staying on the line | `Advance`, `Reset`, `Multiplier`; `PointsPerTick=100` |
| `CoachCard.cs` | Stick figure between the two arms' lines that shows the current movement | static `ExampleMovement`, `CardPosition`, `FigureScale` |
| `SessionReplay.cs`, `ReplayData.cs` | Recording and playback on a mannequin; also the exercise demo and calibration guide | `BeginRecording`, `Capture`, `Save`, `LoadRecording`, `Open`, `Close`, `OpenExerciseDemo`, `HoldDemoAt`, `UpdateGuidance`, static `EstimateElbow`, `MirrorPoint`; `MaxFrames=18000` |
| `GameMenu.cs` | World-space menu with controller-ray and mouse hit testing, settings and the results page; adds the TUTORIAL ALWAYS switch at runtime as a copy of the DEV MODE toggle | `Navigate`, `Change`, `Recenter`, `RefreshResults`, `AddTutorialToggle`; static `NeedsConfirm`, `Confirmed` (two presses to leave the results outside dev mode); replay only in dev mode |
| `ControllerVisuals.cs` | Moves the controller models to the tracked controllers and hides them when tracking is lost | `leftController`, `rightController` |

## Editor scripts (Tools > P7)

| Script | Menu items |
|---|---|
| `P7Tools.cs` | Switch to Quest Android, Build Quest APK, Clean Build Quest APK (development build to `Builds/P7-Quest3.apk`), Export Quest Recordings (`adb -d pull` into `Recordings/`, package ID read from Player Settings), Load Replay File in Play Mode, Train Coach Model |
| `P7Tests.cs` | Run Checks (`P7Tests.RunChecks`): rules for paths, calibration, start shift, scoring, progress, participant IDs, the coach network, personal levels and the playlist. Logs "CHECKS PASSED" on success and writes `Temp/p7-checks.txt` (cleared when Unity quits) |
| `P7PlayModeTest.cs` | Run Play Mode Test (manual calibration) and (automatic start): a full simulated session. Result in `Temp/p7-playmode-test.txt` |

`Assets/TempDiagnostics/Editor/` adds `Tools/Temp` items (`QuestLog`: install, launch and save logcat; `CompileLog`). Temporary.

## Scene wiring

`P7.unity`: Directional Light, Main Camera (inactive), and three prefab instances.

- `VR Rig`: XR Origin with `ControllerVisuals`; Camera Offset holds the VR camera and the controller and tracked-hand models.
- `Session Replay`: `SessionReplay`, with `viewer` set to the VR camera in the scene.
- `Shield Exercise Game`: `ExerciseGame` and `AdaptiveCoach`. The scene sets `head`, `trackingSpace` and `replay`. Children: hand markers, target marker, instruction board, Movement Coach (`CoachCard`), Exercise Path Stream (`PathStream`), Exercise Main Menu (`GameMenu`), Conduit Repair (`PathFeedback`) and Reactor Rescue (`ScoreFeedback`). The line material is `VR/Shield/Energy Pulse.mat`.

The scene has no `EventSystem`; `GameMenu.LateUpdate` hit-tests the rays and calls `Navigate`, which is the only way to press a menu button.

## Session flow

Playlist entries are `exerciseIndex * 2 + side` (left 0, right 1); `ActiveSide => block % 2`. Line numbers are in `ExerciseGame.cs` unless named.

1. Menu. START and PLAY AGAIN call `Begin()`; OPTIONAL CALIBRATION (`"redo"`) calls `Begin(true)` (`GameMenu.Navigate`, `GameMenu.cs:123`). Nothing is locked.
2. `Begin(bool)` (431): sets the frame from the head, configures the coach, resets counters. The tutorial runs if this is a recalibration, the tutorial was never finished, or TUTORIAL ALWAYS is on (457). For each enabled exercise and arm, `PrepareCalibration` (`ExerciseGame.Calibration.cs:28`) restores a usable saved calibration, or adds an `AutomaticEstimate` scaled by eye height (`BodyScaleFor`, 0.8-1.2 of a 1.64 m eye height) and seeds the arm at 35 % range and 65 % speed. Saved automatic estimates older than version 3 are replaced. `Begin(true)` queues every arm for manual capture instead.
3. Manual capture (only after OPTIONAL CALIBRATION): `PrepareBlock` shows the demo on the mannequin; A skips it. Then hold the start pose and the end pose still within 2.5 cm for 1.25 s each (`UpdatePoseCapture`), `TryCaptureEnd` checks the direction and size, stores the range, clears `automaticRange` and resets speed and range to 1; `UpdateReady` waits until the hand has held the start again for a few seconds. After the last arm, `StartMission`.
4. `StartMission` (478): `pathFeedback.Begin(totalRepetitions)`, `SavedSettings.Playlist` (a balanced shuffle), then the tutorial or `LoadChallenge`.
5. Tutorial (`BeginTutorial`, 499): four intro pages (`TutorialIntroPages`), each waiting for A (`AdvanceTutorial`, at least `TutorialPageMinSeconds` = 1 s per page); then for each exercise a full-range demo on the figure (skipped after a manual capture) and one practice repetition per enabled arm on the full-range line (`IsPractice`: no points, recording or FLOW; live hints on the lowered board via `PracticeHint`). `EndPractice` (604) passes the start offset to `AdaptiveCoach.ObserveStart` and, if tracking held, sets the arm's level with `AdaptiveCoach.SetMeasuredLevel` (range = `PracticeReach` x .9, speed from `PracticeSpeed`). Wrap-up waits for A. B skips the rest of the tutorial (`SkipTutorial`); A in a demo jumps to its practice rep.
6. `LoadChallenge` (725): picks the next playlist entry, applies `EffectiveCalibration` (1006: range from the coach, plus the coach's start shift, then `ClampToReach` to `ArmReachMetres` x body scale from `ShoulderOffset`) and sets `Approaching` with `clock = -exerciseGapSeconds` (2.5 s).
7. Path drawing, every `LateUpdate` while `PathsVisible`. `PathStream` samples 180 points for the current and next path and moves them in with `Approach`. Points come from `PathVisualPoint` (1036), which uses `ProjectToFront` (1024): offset `(±.30, -.32, 1.35)` plus the hand's displacement from the start. `PathFeedback` adds the nodes and lit sections along the same path.
8. Approach (`UpdateApproaching`, 752): `TryAnchorAutomaticStart` may move an automatic estimate once to a steady start pose (.25 s within 2.5 cm, at most 35 cm from the shown start). The hand's offset from the start in the last 0.8 s, within 30 cm, is averaged for the start shift. The clock runs only while tracked. At 0, `StartRepetition` opens the recording on first entry.
9. Playing (`UpdatePlaying`, 788, and `MeasureFrame`, 821): the target moves along the path; frame quality from the distance; coverage, head severity, start, hold, return and endpoint flags (the endpoint plays a tone, a ring and a haptic pulse); the start offset over the first fifth of the outward phase and the rest; 60 Hz samples. The clock pauses while untracked. Active play (`activeSeconds`, `activeSinceBreak`) grows only while tracked, a line is shown and the rep is not practice.
10. Rep end (`ScoreRepetition`, 890): score and summary row, then `ScoreFeedback.Reward`, `PathFeedback.Resolve` (passing whether the cap is due) and `AdaptiveCoach.Observe` with the mean hand error and the start offset. The coach changes only the next attempt of that exercise and arm.
11. Next (`EndRepetition`, 857): if the run is finished, `Finish(GoalReached)`; otherwise the playlist advances (and extends near its end), and `DueAtRepEnd` (945) picks `Break` or `LoadChallenge`. A break counts down `breakSeconds`; A skips it. Paths, markers, HUD and coach card hide, and the board shows "BREAK - GREAT WORK" with the countdown.
12. Stop: X during a session, `OnDisable`, or `OnApplicationPause` once recording has started, each calling `Finish(false)`.
13. `Finish` (954): marks `aborted` if unresolved, writes the export files, `State.Finished`. The menu opens the results page once for a newly finished run.

## Scoring

All values are prototype settings without clinical validation.

| Quantity | Formula | Where |
|---|---|---|
| Frame quality | `1 - InverseLerp(.04, .14, distance)` metres; rotation is not scored | `ExerciseProfile.Quality` (`ExerciseProfile.cs:70`), asset fields |
| Path quality | Time-weighted mean of frame quality while tracked | `RepetitionResult.PathQuality` |
| Endpoint tolerance | `clamp(.3 * |end-start|, .025, .06)` m | `EndpointTolerance`, `ExerciseGame.cs:148` |
| Start, hold, return | Start within tolerance while `clock<.8`; hold time with the target within tolerance; return within tolerance after the return phase | `MeasureFrame`, `ExerciseGame.cs:836-841` |
| Endpoint reached | Within .14 m of `end` when the hold phase starts | `ExerciseGame.cs:845` |
| Coverage | `clamp01(maxProgress - minProgress)` along start to end | `TravelCoverage`, `ExerciseGame.cs:928` |
| Phases | Mean of start, return, endpoint and `clamp01(held / (hold*.65))` | `ExerciseGame.cs:899` |
| Complete | All flags, `held >= hold*.65`, coverage `>= .65` | `ExerciseGame.cs:901` |
| Movement score | `(.7 * path quality + .3 * phases) * lerp(.25, 1, coverage)` | `MovementScore`, `ExerciseGame.cs:922` |
| Head severity | `min(InverseLerp(.10,.25,towards other side), InverseLerp(.08,.20,down))`, counted after .5 s sustained, not in rest | `HeadPositionSeverity`, `ExerciseGame.cs:936`; `TrackHead` |
| Head penalty | `max(0, score - .1 * severity)` | `HeadAdjustedScore`, `ExerciseGame.cs:942` |
| Session score (accuracy) | `100 * scoreSum / reps` | `Score`, `ExerciseGame.cs:147` |
| ENERGY | `min(1, power + InverseLerp(.25, 1, q) / target)` | `SessionProgress.cs:30` |
| FLOW | `clamp(battery - max(0, .6 - q) * 16 + (q >= .85 ? 2 : 0), 0, 100)` | `SessionProgress.cs:32-34` |
| Outcome | Defeat at FLOW 0, checked first; victory at full ENERGY; `session_limit` when the cap is due on that rep | `SessionProgress.cs:36-41` |
| Node repaired (export, cue) | `q >= .65` | `PathFeedback.NodeRepaired` |
| Line glow colour | `Glow(q)`: amber*.35 to white over `InverseLerp(.4, .9, q)`; also tints sections and the hand trail | `PathFeedback.Glow` |
| Perfect | complete, score >= .9 and coverage >= .9 | `ScoreFeedback.Perfect` (`ScoreFeedback.cs:49`) |
| Points | `+1000q` per rep; if perfect, `Combo++` and `+1000q * min(Combo-1, 4)`, else `Combo = 0`; `Multiplier = clamp(Combo, 1, 5)` | `ScoreFeedback.Reward` (`ScoreFeedback.cs:103`) |
| Perfect-flow bonus | Quality >= .9 for 2 s, then 100 x its own multiplier every .5 s; the multiplier rises every four ticks, capped at x5; any miss, tracking loss or rest restarts the warm-up | `PerfectFlowBonus.cs` |

Adaptive coach (`AdaptiveCoach.cs`, formats and retraining in `ADAPTIVE_COACH.md`). A 4-16-3 tanh network. After each rep `ScoreRepetition` passes score, coverage, mean tracked hand error and the start offset to `Observe` (262). `ErrorSeverity` (432) maps the error to 0..1 as the smaller of `InverseLerp(.04, .18, m)` and `InverseLerp(.15, .6, m / max(.08, targetTravel))`; the start offset maps to 0..1 over 0-20 cm. `Predict` returns a range-fraction change and a relative speed change, scaled to ±.45 (`RangeStep`) and ±.35 (`SpeedStep`). `PredictStartFollow` returns the share of the offset to follow; `NextStartShift` (167) limits that to 5 cm per step and 20 cm in total and ignores offsets under 2.5 cm.

Gates outside the network (`Adjust`, 326, and `IsReliable`, 355): changes under .02 range or .0225 speed are dropped; head severity above .2 blocks increases but still allows easing; a rep with tracking loss above 10 % of its time, or a non-finite input, is not learned from; dev mode and `adaptationEnabled = false` switch learning off; a missing or invalid model keeps the current level.

Training: `Train` runs backpropagation (seed 42, 1,800 epochs, rate .025, 6x weight on examples that ask for an increase) on `Assets/VR/Training/coach-training.json`: 2,205 synthetic examples, 1,792 for training and 413 held out, labelled by `Tools/coach_training_labels.py` (v4 labels). `Tools > P7 > Train Coach Model` writes `base-model.json` (currently `nn3-28ACEF866E89FF43`, held-out MSE .0009) only if the held-out MSE is at most .02. Play never changes the weights. None of this is clinically validated.

Personal data: `PersonalPath` puts `*-adaptive.json` next to the settings file (editor: `/tmp/p7-editor-player-settings-adaptive.json`), one level per exercise and arm with speed, range, start shift, observation count and mean score and coverage. Speed and range are mirrored into `player-settings.json` for the menu. `ResetPersonalData` empties the file on RESET SAVED SETTINGS; `Recalibrated` resets one arm after a manual capture or a new automatic estimate.

Bounds (`LowestRange`, 246): range floor `max(.4, minimumTravel/travel)` for a manual calibration and `max(.15, minimumTravel/travel)` for an automatic one (.3 in dev mode), ceiling 1; speed between `1/slowestDurationScale` (.5) and `1 + fasterPercent/100` (default 1.4).

Session length, arithmetic only, not measured. A move takes 8.5 s at speed 1 plus the 2.5 s gap, so 11 s, and about 14 s at the .65 start speed. At the default target of 18, a strong player (q near 1) wins in about 18 moves, before the first break. At q = .6, FLOW never drains and each move adds .467/target ENERGY, so about 39 moves, which reaches the 5-minute break. The 15-minute cap comes before victory only for targets above roughly 60-80.

## Data export

Quest: `/sdcard/Android/data/<bundle ID>/files/Sessions/<yyyyMMddTHHmmssfffZ>/` (`ExerciseGame.Recording.cs:75`). Pull it with Export Quest Recordings into gitignored `Recordings/`. The editor simulation writes only in `TestMode`, to `/tmp/p7-tests`. Files are written in `WriteSessionFiles` (`ExerciseGame.Recording.cs:218`), except samples, which stream during play. Nothing is recorded in a break or a practice rep, so `timestamp_s` jumps across a break and replay holds the last frame.

| File | Content |
|---|---|
| `samples.csv`, `.tsv` | 40 columns, one row per hand per sample at up to 60 Hz: `timestamp_s, exercise_time_s, exercise_id, repetition, phase, active_side, side, valid, scorable`, hand, target and head poses, `hand_error_m, shield_normal_error_deg, head_displacement_m, head_rotation_deg`, `visual_shield_*`, `visual_target_*` |
| `summary.csv`, `.tsv` | 18 columns per scored rep: `exercise_id, side, repetition, score, complete_movement, hold_seconds, start_reached, return_reached, endpoint_reached, valid_seconds, mean_hand_error_m, mean_shield_normal_error_deg, path_accuracy_percent, travel_coverage, phase_completion, unpenalized_score, head_position_severity, head_penalty_points` |
| `session.json` | `schemaVersion=shield-6`, `participantId`, `scoreVersion=position-only-hand-5`, `calibrationMode=automatic-estimate-or-manual-3`, `controlMapping=front-translation-2`, `presentation=finite-3d-paths-mirror-coach-1`, `developerMode`, gap, break and cap seconds, `challengeOrder`, scoring weights, calibrations (each with `automatic`, `startAnchored` and `estimateVersion`), exercises |
| `outcome.json` | `participant_id` (`TEST` in tests), `complete` (true for victory and `session_limit`), `outcome` (`victory`, `defeat`, `session_limit`, `aborted`), `tracking_pause_seconds`, `active_seconds`, `breaks`, `off_spot_seconds` (added 2026-11-17: time the head was off the floor ring) |
| `adaptive-coach.json` | One `Decision` per rep, `policy=neural-range-tempo-start-v3`: model ID, mean hand error, error severity, raw predicted changes, the bounded next level, `measuredStartOffset`, `startShiftUsed`, `nextStartShift` (head-relative metres), `startOffsetSeverity`, `predictedStartFollow` and the reason |
| `game-points.json` | `version=perfect-flow-2`, total and tracking-bonus points, base 1,000 per rep and 100 per tick |
| `conduit.json`, `conduit.csv`, `.tsv` | `version=conduit-2`; `movement, accuracy, ship_power, battery, outcome` per rep |
| `conduit-path.csv`, `.tsv` | `movement, exercise_id, side, exercise_time_s, path_quality, node_repaired` per time bin |
| `replay.json` | Replay schema "1", capped at 18,000 frames (`SessionReplay.MaxFrames`) |

## Tunables

| Value | Where | Default |
|---|---|---|
| Repetition target, enabled exercises and arms, speed limit, dev overrides, tutorial on every start | Settings menu, saved in `player-settings.json` (`SavedSettings`; editor `/tmp/p7-editor-player-settings.json`) | target 18 (1-120), speed limit +40 % (0-100) |
| Participant counter (default awaiting team confirmation) | `participant.json` (editor `/tmp/p7-editor-participant.json`), kept apart from the settings so a reset cannot lose it | first value `P001`, +1 per confirmed reset |
| Timing and tolerances per exercise | `VR/Shield/*.asset` | 3 / 1.5 / 3 / 1 s, .04 / .14 m, minimum travel .02 (serratus .015) |
| Gap, break and session cap | fields on `ExerciseGame` (`ExerciseGame.cs:32-36`); 0 turns a limit off | 2.5, 300, 45, 900 s |
| Automatic estimate size, start range and speed, reference eye height | constants in `ExerciseGame.Calibration.cs:67-71` | version 3, .35, .65, 1.64 m |
| Tutorial pages, practice range share, approach measurement | constants on `ExerciseGame` | `TutorialIntroPages` (4), `TutorialPageMinSeconds` 1 s, .9, last .8 s within .30 m |
| Reach limit, floor ring, step limit, depth cue | `ArmReachMetres`, `FloorRingRadius`, `DepthCueMetres` (constants) and `stepLimitMetres`, `stepReturnMetres` (fields) on `ExerciseGame` | .66 m x body scale, .3 m, .03 m; .35 / .2 m |
| Coach card place and size | `CoachCard.CardPosition`, `FigureScale` | `(0, -.22, 1.75)`, .26 |
| Results confirmation | `GameMenu.ConfirmMinSeconds`, `ConfirmMaxSeconds` | .4 s, 4 s |
| Approach speed | `PathStream.approachSpeed` | .55 |
| Path samples, line colour | constants in `PathStream` | 180; .55 current, .30 incoming |
| Coach range and speed limits | fields on `AdaptiveCoach` (`minimumRangeScale`, `slowestDurationScale`, `adaptationEnabled`) | .4, 2, on |
| Coach output bounds, start shift step, limit, dead zone, offset scale | constants at the top of `AdaptiveCoach` (`AdaptiveCoach.cs:13-30`); the fraction followed comes from the network | ±.45, ±.35; 5 cm, 20 cm, 2.5 cm, 20 cm |
| Coach training labels | `Tools/coach_training_labels.py` rewrites `coach-training.json`; then Tools > P7 > Train Coach Model | v4 labels |
| Front offset | `ProjectToFront` (`ExerciseGame.cs:1024`) | `(±.30, -.32, 1.35)` |
| Lit sections, hand trail, camera background | `PathFeedback` | sections .018 m (return .01 m); trail .25 s, .03 m; `(.018, .03, .055)` |
| Sounds | constants at the top of `ScoreFeedback` (`ScoreFeedback.cs:9-15`); one-shots in `ScoreFeedback.cs:310-312` | 660 capture, 330 endpoint, 880 complete; streak 880-1760 Hz; line 220 Hz, level .012-.035, cutoff 300-1600 Hz, fade .2 s; chime after .2 s. None tuned by ear |
| Haptic | `ExerciseGame.cs:852` | amplitude .3, .08 s |
| Bundle ID | `ProjectSettings/ProjectSettings.asset:176` | template default |

## Unverified

- Behaviour on a Quest of everything added in `a6ab39a2`: tutorial, start shift, larger estimate, 35 % start, model nn3.
- The Play Mode tests were not run for this check; only `P7Tests.RunChecks` was. They were also not run after the 2026-11-17 changes.
- Everything in CLAUDE.md section 12 on a headset: reach limit, floor ring and step pause, depth guide, coach card placement, tutorial pages.
- Whether `Shader.Find` in `CoachCard` survives player stripping in the current build.
- Which APK the pilot headset ran (see CLAUDE.md section 10).
