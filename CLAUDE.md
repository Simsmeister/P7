# P7: Quest 3 shoulder rehabilitation game (AAU Medialogy, Group 704)

## 1. What it is
A Just Dance-like VR game for Quest 3: the player follows a line with their hand through three shoulder exercises (Scaption, Serratus Reach, External Rotation), and an adaptive coach adjusts speed and range per exercise and arm. There is no throwing, no parrying and no opponent in the build, and none is planned. The hand is the Quest controller grip, since `ShieldExerciseGame.cs:120` forces controller mode. Code names are historical: "Emil" is the scene and tools, "shield" is the marker the player moves.

Research question: "How can an adaptive VR game be designed to support physiotherapists in providing an appropriate level of challenge while promoting engagement in shoulder rehabilitation exercises for adults aged 18-35 with higher functional demands"

Target group: Danish adults 18-35 with higher functional demands (returning to sport or demanding physical activity). The paper lives in a separate Overleaf project. If it and the code disagree about the mechanic, trust the code and flag the difference.

## 2. Run and build
Unity 6000.6.0f1, URP 17.6.0, OpenXR 1.18.0, XR Hands 1.9.0. The build tool builds only `Assets/Scenes/Emil.unity`; `SampleScene` is also listed in Build Settings but is template boilerplate.

Menu `Tools > Emil VR`: Switch to Quest Android, then Build Quest APK (writes `Builds/Emil-Quest3.apk`, development build), and Validate Quest Setup. README and editor paths assume a Mac: the docs say Mac and the code hard-codes `/tmp`. Editor Play Mode without a headset runs a labelled desktop simulation (Space = A, R = B, mouse on the menu).

## 3. Code map
Read PROJECT_MAP.md before exploring the code; open source files only when you need to change them. When structure changes (scripts, prefabs, wiring, schema, formulas), update the map in the same commit.

Start flow. START runs `Begin(false)`. Every enabled exercise and arm without a usable saved calibration gets an `AutomaticEnvelope`: an estimated example range, not a measured one, that starts at 20 % range and 65 % speed (`range=.2`, `speed=.65`) and is flagged `automaticRange`. An automatic range can shrink to a floor of .15; a manually calibrated range keeps the .4 floor. While a line approaches, `TryAutomaticAnchor` moves the estimated start to the hand once, if the hand holds an allowed pose within 2.5 cm for 0.25 s and within 35 cm of the estimate. It never moves a target during scoring. The OPTIONAL CALIBRATION menu button runs `Begin(true)`, the demo and pose-capture flow, which replaces the estimate with a measured range.

Adaptive coach (`AdaptiveExerciseCoach.cs`, details in `ADAPTIVE_COACH.md`). A 3-16-2 tanh network. The inputs are score, coverage and the rep's mean hand error (normalised by `ErrorSeverity`); the outputs are the range and speed change for the next attempt of that exercise and arm, bounded to ±.45 and ±.35. It was trained offline on 2,205 synthetic examples labelled by hand-written rules (1,792 train, 413 held out), with the menu item `Tools/Emil VR/AI/Train Base Neural Model`. The weights live in `Assets/Resources/AdaptiveCoach/base-model.json` and never change during play; each player's levels go to a separate `*-adaptive.json` next to `player-settings.json`. Gates outside the network: a dead zone ignores changes under .02 range and .0225 speed, a head-displacement flag (severity above .2) blocks increases but still allows easing, and a rep with more than 10 % tracking loss is not learned from. Dev mode switches learning off. The labels are invented, so the coach is not clinically validated.

## 4. Traps
- `ShieldExerciseVerification.Run` and `VerifiedBuild` have no menu items (they need `-executeMethod`) and rebuild prefabs first, so they can overwrite manual prefab edits.
- Generated prefabs in general: `Build Conduit Repair` rewrites the Conduit, menu and marker prefabs. `Set Up Shield Exercises` rewrites the Left/Right/Target Shield prefabs back to old discs before its early return. Change the generator, or edit the prefab and stop running that tool.
- Conduit Repair is the active theme and supplies line feedback; extract it rather than delete it.
- Reactor Rescue (`arcade`) HUD is off, but its audio, points and combo logic still run.
- `WandSessionReplay` and `Session Replay.prefab` are live. `WandPathGame` and `WandPathPrototypeSetup.cs` are otherwise dead, but `ShieldExerciseSetup.Build` reads `WandPathGame`, `WandSessionReplay` calls it at compile time (lines 72, 86), and the live `Test Desktop Session` menu item sits in `WandPathPrototypeSetup.cs`.
- The `projectile` field in `ShieldExerciseGame` is forced inactive and is legacy, but the code dereferences it unguarded. `Energy Pulse.mat` is still the line material.
- `WandDataExport.cs` hard-codes the package ID (line 22), so a bundle rename breaks export.
- Editor tools look for `Emil VR Rig`, but the prefab and scene object are now `VR Rig`. `Configure Quest 3` would add a second rig; `Update Controller Hand Models` and `Set Up Tracking Trails` load a path that no longer exists.
- `Shield/README.md` is a dated changelog. Its "Current state, 2026-11-04" section at the top overrides older text below, which still says rotation-based quality, a 20 degree hold check, shield-4 and exact session length. Trust the code.
- No `[Test]` methods, no asmdefs. Checks are static `Verify` methods run via `-executeMethod`. README reports no confirmed headset run for recent versions.
- Replay stops capturing at 18,000 frames, about 5 minutes at 60 Hz (`WandSessionReplay.cs:48`). Raw samples continue.
- There is no therapist lock. It was removed on request on 2026-11-09, so SETTINGS and OPTIONAL CALIBRATION open with one click and a patient can reach them. RESET SAVED SETTINGS still asks for confirmation.
- The saved `devMode` persists between sessions. If it is left on, the patient plays with the dev overrides, no adaptation and nothing saved, so switch it off in Settings before handing over.
- Break and cap numbers (5 min, 45 s, 15 min) are prototype values, not clinical guidance, and a physiotherapist must confirm them. They live as serialized fields on `ShieldExerciseGame`, and active play counts only while the game clock runs, so a session lasts longer on the wall clock than the cap says.
- Participant ID (default awaiting team confirmation). Tester: read `Participant P003` on the home menu and write it on the VRNQ sheet; before the next participant, open Settings and confirm RESET SAVED SETTINGS, which advances the ID and clears calibration and the coach's personal levels. Repeat sessions by one participant need no reset.
- The participant counter is per device, so two headsets both issue `P001`, and uninstalling the app wipes the counter and `Sessions`. Keep a headset label on the paper sheet and pull exports before any uninstall.
- `OnApplicationPause` still ends the session as `aborted`. If the Quest pauses the app when the headset comes off, a patient who takes it off during a break loses the rest of the session. Not tested on a headset.

## 5. Status against the team's feedback spec
| Item | Status | Detail |
|---|---|---|
| Continuous line | partial | Finite ribbons about 1.35 m in front, now joined by a dim runtime bridge from current end to next start (`ExercisePathStream` `bridge`); the 2.5 s gap still exists in time; not yet seen on a headset |
| Theme "Beat Saber Lite" night | partial | Chosen 2026-11-04. Ship and repair text replaced with ENERGY / FLOW wording (runtime and menu prefab); visuals still dark and cyan; no post-processing volume (bloom) yet |
| Score | partial | Points and multiplier shown in the Conduit status line (`ConduitRepairGame` status text); Path match %, last move 0-100 |
| Multiplier on consecutive perfects | built | Counts perfect reps (`ShieldArcadeFeedback.Perfect`), up to x5, shown as text and pips |
| Audio: completion sound | partial | 880 Hz on a complete rep that is not perfect; a perfect rep plays its streak note instead. Compiles and passes `ConduitSetup.Verify`; nobody has listened to it |
| Audio: in-tolerance tone, soft dampening off the line | partial | Looping 220 Hz tone on a runtime child `Line Tone` with its own low-pass, audible only while `Playing` and tracked; volume and cutoff follow `RepairQuality` continuously (`ShieldArcadeFeedback.LineTone`). Compiles and passes `ConduitSetup.Verify`; nobody has listened to it, on a headset or anywhere else |
| Audio: rising notes, multiplier sound | partial | Perfect reps step up an A major pentatonic from 880 to 1760 Hz, capped at the sixth perfect in a row (`StreakNote`); a two-note chime follows .2 s later when `Multiplier` rises. Compiles and passes `ConduitSetup.Verify`; nobody has listened to it, on a headset or anywhere else |
| Visual: line glow | partial | Graded by quality (`ConduitRepairGame.Glow`, amber at 0.4 to white at 0.9), now held for the whole rep through the progressive fill; not yet run in Unity on a headset |
| Visual: progressive fill | partial | The followed part stays lit until the rep ends, tinted per section by `Glow`; tracking gaps stay unlit (`ConduitRepairGame.SectionLit`). Sections hold at their arrival position. The return draws as a thin core in front of the outward fill. Compiles and passes `ConduitSetup.Verify`; not yet seen on a headset |
| Visual: hand light trail | partial | 0.25 s `TrailRenderer` added to the markers at runtime by `ConduitRepairGame.UpdateTrails`, tinted by `Glow`; not yet run on a headset |
| Visual: completion effect | partial | Ring at endpoint, not tied to quality, may be hidden by `Show(false)` |
| Visual: multiplier indicator | partial | Five pips above the status panel, created at runtime by `ConduitRepairGame.UpdatePips`; the lit count equals the multiplier, and lit pips are brighter and larger. The "x3" text stays beside the points. `Multiplier` reads x1 at both 0 and 1 perfect reps, so the second pip lights on the second perfect in a row. Compiles and passes `ConduitSetup.Verify`; not yet seen on a headset |
| Visual: milestone lighting | partial | Floor lights track cumulative power |
| Haptics: completion | partial | Fires on endpoint reached |
| Haptics: multiplier pattern, off switch | missing | |
| Rewards: God/Præcis labels | built | PRÆCIS = perfect, GOD = complete; shown in the Conduit status line during the gap before the next path |
| Rewards: personal progress | partial | Data saved, no in-game view |
| Rewards: cosmetic unlocks | missing | |
| Rewards: round summary | partial | Results page (`ExerciseGameMenu.EnsureResults`, page 4) shows outcome, total score, accuracy, best streak, moves and perfect-flow bonus; no per-exercise breakdown |
| Rewards: positive ending | partial | "FULL ENERGY - GREAT SESSION"; failure ending still exists as "OUT OF FLOW - TRY AGAIN" but did not trigger in the pilot; planned break "BREAK - GREAT WORK" and session cap "SESSION GOAL REACHED" are in, treated as completed goals |

## 6. Backlog, in priority order
Pilot test 1 (2026-11-11) changes the priorities: see section 10 for the iteration 2 plan, which comes after the data processing and Overleaf write-up. The items below are the older backlog.

- P1 Line: headset look. Progressive fill, graded brightness, hand trail and the bridge between paths are in, but none has been seen on a headset. Check the frame rate too, since up to 119 lit sections can stay active at once.
- P2 Rewards: headset look at the multiplier pips, which have not been seen on a headset. Score, multiplier on perfect reps, PRÆCIS/GOD labels and best streak are in.
- P3 Audio: listen on a headset. In-tolerance tone, soft dampening, rising notes on streaks and multiplier chime are in, but nobody has heard them, so levels, the 220 Hz pitch on the Quest speakers and whether a sustained tone gets tiring are all open. No mute toggle yet; the tunables sit together at the top of `ShieldArcadeFeedback`.
- P4 Clinical: a therapist lock on Settings and recalibration, asked for by physiotherapist Expert 2, was built and then removed on 2026-11-09 (see Traps), so therapist-only limits are open again. Session length and breaks are in: a 45 s break after each 5 minutes of active play and a finish at the next rep boundary after 15 minutes, exported as outcome `session_limit`. The numbers are prototype values, not clinical guidance, and a physiotherapist must confirm them, along with whether A may skip a break. Only the timing helper and `RepairRun` are asserted, so the break has not been run. Follow-up: menu sliders for these values, which needs `ExerciseMenuSetup` and a generator run.
- P5 Evaluation: participant ID in exports is in as a default awaiting team confirmation: an anonymous per-device counter (`P001`, `P002`...) in `participant.json`, shown on the home menu, advanced by the RESET SAVED SETTINGS confirmation, written to `session.json` and `outcome.json` (see Traps). It compiles and passes `ConduitSetup.Verify`, which asserts the helpers and file recovery but not the menu or a real export. Still open: VRNQ questionnaire outside the app (no in-app support), headset test.
- P6 Cleanup: one game name (working title SHOULDER FLOW in the menu, ProjectSettings product name unchanged); consistent exercise labels (menu says FORWARD REACH / OUTWARD ROTATION, gameplay says SERRATUS REACH / EXTERNAL ROTATION); real bundle ID (currently template default `com.UnityTechnologies.com.unity.template.urpblank`) with the export tool updated. Runtime stale strings and README sync are done, checked with `ConduitSetup.Verify` only; the old `WAND PATH LAB` default text on the board in `Shield Exercise Game.prefab` remains and needs a prefab change, though `DrawText` replaces it at runtime.
- Later, optional: haptic toggle and pattern, milestone lighting, cosmetic unlocks, in-game progress view.

## 7. Team decisions
No open decisions left from the original list. New design questions still go to a human; do not assume.

Decided 2026-11-04: the failure ending stays, but should be improved (see 7a).

### 7a. Failure ending: how it works now and ideas to discuss
How it works (`RepairRun.Resolve`, `ConduitRepairGame.cs`): FLOW (code: `battery`) starts at 100. Each rep subtracts `max(0, .6-q)*16` and adds 2 when `q>=.85`. At 0 the session ends with "OUT OF FLOW - TRY AGAIN", which is checked before victory. Tracking pauses never drain it. `ConduitSetup.Verify` requires that a stationary player (q=.25) fails even a short session; that is the anti-idle guard, so keep some failure path or replace that check.

Why it needs work: in rehab a hard loss can punish fatigue or pain, which is the moment the player should stop or slow down. It also overlaps with the adaptive coach, which already makes the next rep easier after a poor one, so the player can lose while the game is still easing.

Ideas, not decided or built. Discuss with the team and a physiotherapist:
- Grace period: no drain during the first N reps (for example 6) or until every exercise and arm has been seen once.
- Cap the drain per rep (for example at 6) so one bad rep cannot end the session.
- Turn failure into a break: at 0 FLOW, pause with "TAKE A BREAK" and a 30-60 s rest, then refill to 50 and continue, instead of ending. The planned break from P4 is time-based and separate from this.
- Coach first: only drain when the adaptive coach is already at its floor for that exercise and arm (range at minimum, speed at slowest). Then a loss means "too hard even at the easiest", which is useful data for the therapist.
- Therapist switch: failure on or off per player in `player-settings.json`, set in Settings.
- Kinder ending screen: show what went well (points, best streak, PRÆCIS count, best exercise) next to the failure text, so the session ends on progress.
- Log it: `outcome.json` and `conduit.json` already say why the session ended, and the FLOW curve is in `conduit.csv`. Report in the paper how often failure happened.

Decided 2026-11-04: night "Beat Saber Lite" theme, no ship story. Player-facing text says ENERGY (was ship power) and FLOW (was battery). Code names, export files and CSV columns keep `conduit`, `ship_power`, `battery` so old data stays comparable. Menu title "SHOULDER FLOW" is a working title until the team picks one game name (P6).

Decided 2026-11-04: the line stays about 1.35 m in front of the player (no move to the hand).

Decided 2026-11-04: perfect = `complete`, score >= 90 and coverage >= 90 % (`ShieldArcadeFeedback.Perfect`).

## 8. Limits to state honestly
Compensation is only a head-movement heuristic. Elbow, scapula and torso are not measured, and the adaptive coach learned from synthetic labels with no clinical validation. Sections 1-6 come from static code reading on 2026-11-04, rechecked against the code on 2026-11-12; section 9 adds what was compiled and checked in batch mode. Before the pilot (section 10), nothing had run on a headset.

`origin/Emil` lacks this file; teammates should pull `Share-load`.

## 9. Handoff: work done on 2026-11-04 and where to pick up
Commits on `Share-load` after the original handoff (`f9e970ad`), all pushed:
- Graded line glow (`ConduitRepairGame.Glow`) and a 0.25 s hand trail (`UpdateTrails`), both added at runtime.
- Points, x1-x5 multiplier and best streak shown in the status line and end-of-session menu.
- "Perfect" defined (`ShieldArcadeFeedback.Perfect`); multiplier counts perfect reps; PRÆCIS / GOD label shown in the gap before the next path.
- Dim bridge joining the current path to the next (`ExercisePathStream`, runtime `Path Bridge`).
- Night theme wording: ENERGY / FLOW instead of ship power / battery; menu prefab regenerated (text only).

Also done on 2026-11-04, checked in batch mode only, and not listed as a commit until someone commits it: procedural audio for P3 (line tone with dampening, streak notes, multiplier chime) in `ShieldArcadeFeedback`, driven from `ShieldExerciseGame`. It has not been heard on a headset.

Also uncommitted then: a P4 therapist lock in `ExerciseGameMenu`. It was committed in `74c4033c` and removed again on 2026-11-09.

Also uncommitted and checked the same way: P4 session length and breaks. A new `State.Break` in `ShieldExerciseGame` hides the paths and shows a countdown on the instruction board, and `RepairRun` gains outcome `session_limit`, with `shield-6` and `conduit-2` marking the changed meaning. `ShieldExerciseVerification` needed no edit: its 240 s timeout ends the run before the 300 s of active play that the first break needs. It was not run.

Also uncommitted: the P5 participant ID (`ExerciseParticipant` in `ExercisePlayerSettings.cs`), checked with `ConduitSetup.Verify` only. A missing or damaged `participant.json` continues after the highest ID found in `Sessions/*/session.json` and warns in the footer. `TestMode` never advances the counter and exports `TEST`, so `ShieldExerciseVerification` needed no edit and was not run. Schema stays `shield-6`, since it is unreleased and the field is additive.

How it was checked: Unity 6000.6.0f1 batch mode on Windows compiles with 0 errors, and `ConduitSetup.Verify` and `ExercisePathSetup.Verify` pass. The Play Mode test `ShieldExerciseVerification.Run` was not run (it rebuilds prefabs first). Nothing was looked at in Play Mode or on a headset, so the first job is a headset look at glow, trail, bridge, and whether the longer status line fits, plus a first listen to the audio.

Batch check command (Windows, Unity closed):
`"C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath <repo> -executeMethod ConduitSetup.Verify -logFile <log>`

Suggested next steps, in order: headset look; improve the failure ending (7a); bloom / post-processing volume for the night look; tune the audio by ear (P3); confirm the break and cap values (P4); confirm the participant ID default (P5).

Uncommitted local noise: `UniversalRenderPipelineGlobalSettings.asset`, `EditorBuildSettings.asset`, `ProjectAuditorSettings.asset`, `ShaderGraphSettings.asset` were changed by Unity before this work and are deliberately not committed.

## 10. Pilot test 1 (2026-11-11) and the plan for iteration 2
Order agreed by the team: first process the pilot data and write it into Overleaf (Methods, Findings, Limitations), then build iteration 2. Nothing below is built yet. This is the first time the game ran on a headset with participants, so the "not seen on a headset" notes above are partly outdated; the findings here are what was actually seen.

Sources (Drive folder P7/Test): "Pilottest d. 10-11-2026" (procedure and interview guide), "Observatør Noter", "Pilottest - Lydfiler" (7 recordings), and the SurveyXact survey "P7 - UES-SF" (id 1919004). Participants: 4 Medialogy students (MED7), healthy, 2 with moderate and 2 with no VR experience; participants 3 and 4 spoke English. The headset ran commit `457c05e1`, whose game code matches the current `Share-load`, so the pilot used the automatic start, the neural coach and the one-click menus.

Results, descriptive only (n = 4). UES-SF total mean 3.57 of 5 (FA 3.46, PU 3.50 reversed, AE 3.75, RW 3.58). SUS mean 73.75 (52.5 to 85). Game results exist for participants 1 and 2 only: 88 % and 92 % accuracy, best streak 4 and 6, 22 and 21 movements. Survey rows R1-R4 are assumed to be participants 1-4 by completion time; confirm against the team's notes. The FLOW defeat ("OUT OF FLOW - TRY AGAIN") is implemented but did not trigger in the pilot.

Findings, most severe first, with what iteration 2 should do:
1. Targets out of reach, fall risk. Participant 3 stepped forward and back and reached behind himself; participant 4 stood on one leg and nearly fell on the overhead exercise ("the circle is so far away sometimes"); participant 1 felt he had to walk forward. Fix: calibrate each participant standing still, add a floor marker, and clamp target distance and height to the calibrated reach. Note that the default flow now skips calibration and starts from the estimated `AutomaticEnvelope` (section 3), so a measured reach exists only if the tester presses OPTIONAL CALIBRATION. Safety issue, so it goes first.
2. Depth of the target ring is hard to judge (3 of 4: "the rings are very 2D", "too behind or too close"). Fix: a depth cue, for example the ring changing colour or size with the z-distance to the hand, or a line or shadow from hand to target. Alternative: weight depth less in the position error.
3. Tutorial text disappears too fast (participants 1, 2, 4). The tutorial lasts 9 s (`ShieldExerciseGame.cs:287`, `clock=9`) for five lines of text. Fix: wait for A, or at least about 20 s with fewer lines.
4. Players are unsure how they are doing early on, and the ENERGY bar is unclear (participant 1: "what does the energy bar mean?"). Fix: show points from the first rep and explain ENERGY and FLOW in the tutorial.
5. The exercise card is missed, so players just follow the line and use the arm (participant 2). Fix: move the coach figure closer to the line or into view.
6. Compensation seen: elbow pulled back and trunk movement (participant 4), "more the arm" (participant 1). Ties to 1 and 5, and confirms the limit that only head movement is measured.
7. Feedback works, mainly as information. The sound was the most praised element ("very pleasant, ding ding"). Keep it. Participant 1 missed music ("snorr saber"); consider a beat or music.
8. Engagement varied: participant 1 found it long and boring and would not replay; participant 2 could have played 10 more minutes; 3 and 4 would play once more.

Test procedure fixes before the next test:
- Participant 4 pressed REPLAY on the menu, so the test was restarted; lock replay to dev mode. Still open: REPLAY and LAST REPLAY are not gated in `ExerciseGameMenu.Navigate`.
- Participant 3 ended in the main menu without a score; make sure the result screen stays until the test leader moves on. Still open, with no code change yet.
- Survey: UES items are set up as multiple choice (two respondents ticked two boxes on three items); make them single choice. "Application X" is still in the item text, "appealed to be visual senses" has a typo, two SUS items miss the leading "I", and participant 3 was unsure of the language.
- Think-aloud gave almost nothing while playing; prompt during play or rely on the interview.
- The recording for participant 1 only has the interview.

Paper (Overleaf) work from this pilot: done on 2026-11-12. Pilot results sit in Design as "Iteration 1 and pilot test" (`sec:pilot-results`, UES/SUS table and themes table), not in Findings, which is kept for the main evaluation with physiotherapy students and experts; a second pilot would get its own iteration subsection. Still to write: a Design section "Iteration 2" that ties each change to a finding. Also in: Methods (transcription and the survey error) and Limitations (n = 4, healthy Medialogy students rather than physiotherapy students, the survey errors, missing game data for two participants). Whisper transcripts of the recordings were not corrected word for word; check quotes against the audio before citing them.
