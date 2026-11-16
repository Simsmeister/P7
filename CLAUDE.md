# P7: Quest 3 shoulder rehabilitation game (AAU Medialogy, Group 704)

## 1. What it is
A Just Dance-like VR game for Quest 3: the player follows a line with their hand through three shoulder exercises (Scaption, Serratus Reach, External Rotation), and an adaptive coach adjusts speed, range and start point per exercise and arm. There is no throwing, no parrying and no opponent, and none is planned. The hand is the Quest controller: `ExerciseGame` reads the controller pose, and the tracked-hand models in the rig are inactive. All game code is in the `P7` namespace. Some data names are historical and kept so old exports stay comparable: "shield" (the hand marker), "conduit" (ENERGY/FLOW progress), `ship_power` and `battery`.

Research question: "How can an adaptive VR game be designed to support physiotherapists in providing an appropriate level of challenge while promoting engagement in shoulder rehabilitation exercises for adults aged 18-35 with higher functional demands"

Target group: Danish adults 18-35 with higher functional demands (returning to sport or demanding physical activity). The paper lives in a separate Overleaf project. If it and the code disagree about the mechanic, trust the code and flag the difference.

## 2. Run and build
Unity 6000.6.0f1, URP 17.6.0, OpenXR 1.18.0, XR Hands 1.9.0. The only scene is `Assets/Scenes/P7.unity`.

Menu `Tools > P7`: Switch to Quest Android, Build Quest APK and Clean Build Quest APK (write `Builds/P7-Quest3.apk`, development build), Export Quest Recordings, Load Replay File in Play Mode, Train Coach Model, Run Checks, Run Play Mode Test (manual calibration) and Run Play Mode Test (automatic start). Editor Play Mode without a headset runs a labelled desktop simulation (Space = A, R = B, mouse on the menu). In the editor, settings files go to `/tmp/p7-editor-*` and test sessions to `/tmp/p7-tests`.

Batch check (Windows, Unity closed). A pass logs "CHECKS PASSED"; the result also goes to `Temp/p7-checks.txt`, but Unity clears `Temp/` when it quits, so in batch mode read the log:
`"C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath "C:/Users/trist/Desktop/P7" -executeMethod P7Tests.RunChecks -logFile <log>`

The two Play Mode tests run from the menu and write `Temp/p7-playmode-test.txt`.

Do not keep the project in an iCloud-synced folder (such as the Mac Desktop): iCloud creates "name 2" copies inside `Library/`, which breaks the Android build with duplicate-symbol errors. If it happens, delete `Library/Bee` and any "* 2*" files under `Library/`.

## 3. Code map
Read PROJECT_MAP.md before exploring the code. When structure changes (scripts, prefabs, wiring, data formats, formulas), update the map in the same commit.

Start flow. START runs `Begin(false)`. Every enabled exercise and arm without a usable saved calibration gets `AutomaticEstimate`: an estimated full movement scaled by eye height, not a measured range. It starts at 35 % range and 65 % speed (`AutomaticStartRange=.35f`, `AutomaticStartSpeed=.65f` in `ExerciseGame.Calibration.cs`) and is flagged `automaticRange`. An automatic range can shrink to a floor of .15; a manually calibrated range keeps the .4 floor. Saved automatic ranges from an older `estimateVersion` are replaced (current version 3). While a line approaches, `TryAnchorAutomaticStart` moves the estimated start to the hand once, if the hand holds an allowed pose within 2.5 cm for 0.25 s and within 35 cm of the shown start. It never moves a target during scoring. The first START, every START with TUTORIAL ALWAYS on, and every OPTIONAL CALIBRATION run the tutorial (section 5). OPTIONAL CALIBRATION runs `Begin(true)`, the demo and pose-capture flow, which replaces the estimate with a measured range.

Adaptive coach (`AdaptiveCoach.cs`, details in `ADAPTIVE_COACH.md`). A 4-16-3 tanh network. The inputs are score, coverage, the rep's mean hand error (normalised by `ErrorSeverity`) and the start offset (0-20 cm mapped to 0-1). The outputs are the range and speed change for the next attempt of that exercise and arm, bounded to ±.45 and ±.35, and a start follow (0-1): the share of the measured start offset the whole path moves, at most 5 cm per step and 20 cm in total. The model `nn3-28ACEF866E89FF43` was trained offline on 2,205 synthetic examples (1,792 train, 413 held out) with the v4 labels from `Tools/coach_training_labels.py`, using `Tools > P7 > Train Coach Model`. The step that added the start offset (v3) is only described in `ADAPTIVE_COACH.md`. The weights live in `Assets/Resources/AdaptiveCoach/base-model.json` and never change during play; each player's levels and start shift go to a separate `*-adaptive.json` next to `player-settings.json`. Gates outside the network: a dead zone ignores changes under .02 range and .0225 speed, a head-displacement flag (severity above .2) blocks increases but still allows easing, and a rep with more than 10 % tracking loss is not learned from. Dev mode switches learning off. The labels are invented, so the coach is not clinically validated.

## 4. Traps
- The prefabs are the source of truth. The old prefab generators and the old editor menu were removed, so edit prefabs directly.
- Export formats (CSV columns, JSON field names, `schemaVersion` strings) are research data. Do not rename fields in `SessionProgress`, `ReplayData`, `Calibration`, `AdaptiveCoach.Decision` or the `SessionInfo` class without updating the analysis scripts.
- There is no therapist lock. It was removed on request on 2026-11-09, so SETTINGS and OPTIONAL CALIBRATION open with one click and a patient can reach them. RESET SAVED SETTINGS still asks for confirmation.
- The saved `devMode` persists between sessions. If it is left on, the patient plays with the dev overrides, no adaptation and nothing saved, so switch it off in Settings before handing over.
- Break and cap numbers (5 min, 45 s, 15 min) are prototype values, not clinical guidance, and a physiotherapist must confirm them. They are serialized fields on `ExerciseGame`, and active play counts only while the player is tracked and a line is shown, so a session lasts longer on the wall clock than the cap says.
- Participant ID (default awaiting team confirmation): an anonymous per-device counter (`P001`...). Tester: read `Participant P003` on the home menu and write it on the VRNQ sheet; before the next participant, open Settings and confirm RESET SAVED SETTINGS, which advances the ID and clears calibration and the coach's personal levels. Repeat sessions by one participant need no reset.
- The counter is per device, so two headsets both issue `P001`, and uninstalling the app wipes the counter and `Sessions`. Keep a headset label on the paper sheet and pull exports before any uninstall.
- `OnApplicationPause` ends a session as `aborted` once recording has started. If the Quest pauses the app when the headset comes off, a patient who takes it off during a break loses the rest of the session. Not tested on a headset.
- Replay stops capturing at 18,000 frames, about 5 minutes at 60 Hz (`SessionReplay.MaxFrames`). Raw samples continue.
- The bundle ID is still the template default (`com.UnityTechnologies.com.unity.template.urpblank`). Export reads it from Player Settings, so changing it is safe.
- `Shield/README.md` is a dated changelog that still uses the old names and the 20 % start. Trust the code.

Open repo issues (not fixed):
- `Assets/_Recovery/0.unity` is a crash-recovery scene that was committed by accident in `a6ab39a2`.
- About 47 files under `.utmp/` are still tracked although `.gitignore` now lists `/.utmp/`; they need `git rm --cached`.
- `Assets/TempDiagnostics/` (`CompileLog`, `QuestLog`, menu `Tools/Temp`) is temporary debugging code and should go before hand-in.
- `a6ab39a2` deleted `Assets/VR/Shield/README.md.meta` but kept the README, so Unity recreates an untracked meta file.
- `ADAPTIVE_COACH.md` mixes older notes with the current ones (for example start follow "0.5 at 6 cm" and the 120 degree estimate); the model provenance and the code say 0.6 and 140 degrees.

## 5. Status against the team's feedback spec
| Item | Status | Detail |
|---|---|---|
| Continuous line | partial | Finite ribbons about 1.35 m in front, joined by a dim runtime bridge from current end to next start (`PathStream`, `Path Bridge`); the 2.5 s gap still exists in time |
| Theme "Beat Saber Lite" night | partial | Chosen 2026-11-04. Player-facing text says ENERGY / FLOW; visuals still dark and cyan; no post-processing volume (bloom) yet |
| Score | built | Points and streak in the floating score HUD (`ScoreFeedback`), plus thin ENERGY and FLOW rails |
| Multiplier on consecutive perfects | built | Counts perfect reps (`ScoreFeedback.Perfect`), up to x5, shown as "3x / 3 STREAK" under the score |
| Audio: completion sound | partial | 880 Hz on a complete rep that is not perfect; a perfect rep plays its streak note instead. Ran in the pilot, where the sound was the most praised element; levels not tuned |
| Audio: in-tolerance tone, soft dampening off the line | partial | Looping 220 Hz tone on a runtime child `Line Tone` with its own low-pass, audible only while `Playing` and tracked; volume and cutoff follow the follow quality (`ScoreFeedback.LineTone`). Levels not tuned by ear |
| Audio: rising notes, multiplier sound | partial | Perfect reps step up an A major pentatonic from 880 to 1760 Hz, capped at the sixth perfect in a row (`StreakNote`); a two-note chime follows .2 s later when `Multiplier` rises |
| Visual: line glow | partial | Graded by quality (`PathFeedback.Glow`, amber at 0.4 to white at 0.9), held for the whole rep through the progressive fill |
| Visual: progressive fill | partial | The followed part stays lit until the rep ends, tinted per section by `Glow`; tracking gaps stay unlit (`PathFeedback.SectionLit`). The return draws as a thin core in front of the outward fill. Up to 119 sections can be lit at once; frame rate not measured |
| Visual: hand light trail | partial | 0.25 s `TrailRenderer` added to the hand markers at runtime by `PathFeedback.UpdateTrails`, tinted by `Glow` |
| Visual: completion effect | partial | Ring at the endpoint (`ScoreFeedback.EndpointReached`) and a hold ring that grows while the end pose is held; not tied to quality |
| Visual: multiplier indicator | partial | Streak text under the floating score. The old multiplier pips were never visible (their panel was hidden) and were removed |
| Visual: milestone lighting | partial | Floor lights track ENERGY (`PathFeedback`) |
| Haptics: completion | partial | Fires when the endpoint is reached |
| Haptics: multiplier pattern, off switch | missing | |
| Rewards: God/Præcis labels | partial | The floating HUD shows PERFECT / NICE after a complete rep. The PRÆCIS / GOD labels lived on a hidden panel and were removed |
| Rewards: personal progress | partial | Data saved, no in-game view |
| Rewards: cosmetic unlocks | missing | |
| Rewards: round summary | partial | Results page (`GameMenu.RefreshResults`) shows outcome, total score, accuracy, best streak, moves and perfect-flow bonus; no per-exercise breakdown |
| Rewards: positive ending | partial | Results titles: LEVEL COMPLETE (victory), SESSION COMPLETE (cap), RUN ENDED (FLOW at 0), SESSION STOPPED (aborted). The break board says "BREAK - GREAT WORK". The older ending texts on the board are gone. The FLOW defeat did not trigger in the pilot |
| Start point fits the player | partial | 2026-11-10. The coach measures where the hand waits in the last 0.8 s of the approach (within 30 cm of the start), plus the first fifth of the outward phase and the rest phase. The network's start follow decides which share of that offset the whole path moves; hard guards are 5 cm per step, 20 cm total and nothing under 2.5 cm (`AdaptiveCoach.NextStartShift`). Saved per exercise and arm in `*-adaptive.json`. The automatic estimate is also scaled by eye height (`ExerciseGame.BodyScaleFor`). Not yet tried on a headset |
| Larger movements | partial | 2026-11-10. Automatic estimate v3 is the mastered movement: scaption to about 140 degrees, 18 cm serratus reach, 75 degrees rotation; range 1 is the goal and an arm starts at 35 %. Manual calibrations still cap at the captured range. Not yet tried on a headset |
| First-time tutorial | partial | 2026-11-10. Intro board (12 s), then per exercise a full-range demo on the figure and one "your turn" practice rep per enabled arm, with live hints on the lowered board (higher, lower, left, right, forward, closer, "good - hold it here"). The furthest point reached (x0.9, `PracticeRangeShare`) becomes that arm's starting range and the path quality sets its starting speed 0.6-1 (`PracticeSpeed`, `AdaptiveCoach.SetMeasuredLevel`); the start offset feeds the start shift (`AdaptiveCoach.ObserveStart`). Practice reps give no points, recording or FLOW. A skips. Settings has a TUTORIAL ALWAYS switch (`tutorialEveryStart`), a runtime copy of the DEV MODE toggle (`GameMenu.AddTutorialToggle`). Not yet tried on a headset |

## 6. Backlog, in priority order
Pilot test 1 (2026-11-11) changes the priorities: see section 10 for the iteration 2 plan, which comes after the data processing and Overleaf write-up.

- P0 Headset test of the 2026-11-10 changes (model `nn3-28ACEF866E89FF43`; rebuild the APK so it is on the Quest). Emil's own headset try on 2026-11-10 found that "perfect" never got harder, because the old labels only increased at score and coverage >= 90 % by 2-5 %, inside the dead zone. The v4 labels increase range and speed by 3-12 % per attempt at score >= 70 with coverage >= 75 % and low error. Check that it gets harder at a good pace but not too fast, that the start point settles at the right height for short and tall players (`nextStartShift` in `adaptive-coach.json`), that the larger automatic movements are within reach (see pilot finding 1), and that the tutorial is clear and not too long. Open team question: should manual calibrations also be allowed to grow past the captured range?
- P1 Line: frame rate with up to 119 lit sections, and whether the bridge and fill read clearly.
- P2 Rewards: the floating score HUD and streak text are in; check on a headset that they are readable.
- P3 Audio: tune levels by ear, check the 220 Hz pitch on the Quest speakers and whether a sustained tone gets tiring. No mute toggle yet; the tunables sit together at the top of `ScoreFeedback`.
- P4 Clinical: therapist-only limits are open again since the lock was removed (section 4). Session length and breaks are in: a 45 s break after each 5 minutes of active play and a finish at the next rep boundary after 15 minutes, exported as outcome `session_limit`. A physiotherapist must confirm the numbers and whether A may skip a break. Only the timing helper and `SessionProgress` are asserted, so the break has not been run. Follow-up: menu sliders for these values, added to the menu prefab.
- P5 Evaluation: participant ID in exports is in as a default awaiting team confirmation: the counter in `participant.json`, shown on the home menu, advanced by the RESET SAVED SETTINGS confirmation, written to `session.json` and `outcome.json`. `P7Tests` asserts the helpers and file recovery but not the menu or a real export. Still open: VRNQ questionnaire outside the app (no in-app support).
- P6 Cleanup: one game name (working title SHOULDER FLOW in the menu, product name `P7`); consistent exercise labels (menu says FORWARD REACH / OUTWARD ROTATION, gameplay says SERRATUS REACH / EXTERNAL ROTATION); real bundle ID; the open repo issues in section 4.
- Later, optional: haptic toggle and pattern, cosmetic unlocks, in-game progress view.

## 7. Team decisions
No open decisions left from the original list. New design questions still go to a human; do not assume.

Decided 2026-11-04: the failure ending stays, but should be improved (see 7a).

### 7a. Failure ending: how it works now and ideas to discuss
How it works (`SessionProgress.Resolve`): FLOW (code: `battery`) starts at 100. Each rep subtracts `max(0, .6-q)*16` and adds 2 when `q>=.85`. At 0 the session ends as `defeat` and the results page says "RUN ENDED"; defeat is checked before victory. Tracking pauses never drain it. `P7Tests` requires that a stationary player (q=.25) fails even a short session; that is the anti-idle guard, so keep some failure path or replace that check.

Why it needs work: in rehab a hard loss can punish fatigue or pain, which is the moment the player should stop or slow down. It also overlaps with the adaptive coach, which already makes the next rep easier after a poor one, so the player can lose while the game is still easing.

Ideas, not decided or built. Discuss with the team and a physiotherapist:
- Grace period: no drain during the first N reps (for example 6) or until every exercise and arm has been seen once.
- Cap the drain per rep (for example at 6) so one bad rep cannot end the session.
- Turn failure into a break: at 0 FLOW, pause with "TAKE A BREAK" and a 30-60 s rest, then refill to 50 and continue, instead of ending. The planned break from P4 is time-based and separate from this.
- Coach first: only drain when the adaptive coach is already at its floor for that exercise and arm (range at minimum, speed at slowest). Then a loss means "too hard even at the easiest", which is useful data for the therapist.
- Therapist switch: failure on or off per player in `player-settings.json`, set in Settings.
- Kinder ending screen: show what went well (points, best streak, perfect count, best exercise) next to the failure text, so the session ends on progress.
- Log it: `outcome.json` and `conduit.json` already say why the session ended, and the FLOW curve is in `conduit.csv`. Report in the paper how often failure happened.

Decided 2026-11-04: night "Beat Saber Lite" theme, no ship story. Player-facing text says ENERGY (was ship power) and FLOW (was battery). Export files and CSV columns keep `conduit`, `ship_power`, `battery` so old data stays comparable. Menu title "SHOULDER FLOW" is a working title until the team picks one game name (P6).

Decided 2026-11-04: the line stays about 1.35 m in front of the player (no move to the hand).

Decided 2026-11-04: perfect = `complete`, score >= 90 and coverage >= 90 % (`ScoreFeedback.Perfect`).

## 8. Limits to state honestly
Compensation is only a head-movement heuristic. Elbow, scapula and torso are not measured, and the adaptive coach learned from synthetic labels with no clinical validation. The start shift and tutorial measures are untested on players. `P7Tests` and the Play Mode tests cover the rules and a full simulated session; nothing replaces a headset test. This file was rechecked against the code at `a6ab39a2` on 2026-11-16 by static reading plus a batch run of `P7Tests.RunChecks`.

## 9. History
Old names, for reading older commits and notes: the scene was `Emil.unity`, the namespace `EmilVR`, and the scripts were `ShieldExerciseGame` (now `ExerciseGame`), `ConduitRepairGame` (`PathFeedback`), `ShieldArcadeFeedback` (`ScoreFeedback`), `WandSessionReplay` (`SessionReplay`), `AdaptiveExerciseCoach` (`AdaptiveCoach`), `ExerciseGameMenu` (`GameMenu`), `ExercisePathStream` (`PathStream`), `ExercisePlayerSettings` (`SavedSettings`), `ShieldCoachDisplay` (`CoachCard`), `EmilVRInput` (`ControllerVisuals`) and `ShieldExerciseProfile` (`ExerciseProfile`). Checks ran through `ConduitSetup.Verify` and `ShieldExerciseVerification`; both are gone.

- 2026-11-04 (`74c4033c` and follow-ups): line glow, hand trail, path bridge, points and multiplier, procedural audio, ENERGY / FLOW wording, breaks and session cap, participant ID, and a therapist lock.
- 2026-11-05: floating score HUD, perfect-flow bonus (`PerfectFlowBonus`, 100 points per tick) and the results page.
- 2026-11-08 and 11-09: the neural coach replaced the rules, the automatic start replaced mandatory calibration (20 % range then), and the therapist lock was removed.
- 2026-11-15 (`a6ab39a2`): the renames above, the 2026-11-10 work (tutorial, start shift, larger estimate, 35 % start, model nn3), the new `Tools > P7` menu and checks, and removal of the old wand prototype, tracking trails and generators.

## 10. Pilot test 1 (2026-11-11) and the plan for iteration 2
Order agreed by the team: first process the pilot data and write it into Overleaf (Methods, Findings, Limitations), then build iteration 2. Nothing in the plan below is built yet, unless marked. This was the first time the game ran on a headset with participants.

Sources (Drive folder P7/Test): "Pilottest d. 10-11-2026" (procedure and interview guide), "Observatør Noter", "Pilottest - Lydfiler" (7 recordings), and the SurveyXact survey "P7 - UES-SF" (id 1919004). Participants: 4 Medialogy students (MED7), healthy, 2 with moderate and 2 with no VR experience; participants 3 and 4 spoke English. The main test will be with physiotherapy students. The team recorded commit `457c05e1` as the pilot build. That commit has the automatic start at 20 % range, the 9 s tutorial text, model `nn2-914D68A12D0A1CFF` and the one-click menus, but not the 2026-11-10 changes in section 5. Emil's notes mention an APK built on 2026-11-10 at 23:17 with the tutorial, so confirm which build the headset had.

Results, descriptive only (n = 4). UES-SF total mean 3.57 of 5 (FA 3.46, PU 3.50 reversed, AE 3.75, RW 3.58). SUS mean 73.75 (52.5 to 85). Game results exist for participants 1 and 2 only: 88 % and 92 % accuracy, best streak 4 and 6, 22 and 21 movements. Survey rows R1-R4 are assumed to be participants 1-4 by completion time; confirm against the team's notes. The FLOW defeat did not trigger.

Findings, most severe first, with what iteration 2 should do:
1. Targets out of reach, fall risk. Participant 3 stepped forward and back and reached behind himself; participant 4 stood on one leg and nearly fell on the overhead exercise ("the circle is so far away sometimes"); participant 1 felt he had to walk forward. Fix: calibrate each participant standing still, add a floor marker, and clamp target distance and height to the calibrated reach. The default flow skips calibration and starts from the automatic estimate, and the current estimate is larger than in the pilot build (140 degrees scaption instead of shoulder height), so this matters more now. The tutorial practice and start shift fit the line to the player but do not clamp it. Safety issue, so it goes first.
2. Depth of the target ring is hard to judge (3 of 4: "the rings are very 2D", "too behind or too close"). Fix: a depth cue, for example the ring changing colour or size with the z-distance to the hand, or a line or shadow from hand to target. Alternative: weight depth less in the position error.
3. Tutorial text disappears too fast (participants 1, 2, 4). The pilot build showed five lines for 9 s. Partly addressed in `a6ab39a2`: the intro now lasts 12 s (`TutorialIntroSeconds`) and is followed by demos and practice with live hints, but it still runs on a timer. Fix: wait for A, or fewer lines.
4. Players are unsure how they are doing early on, and the ENERGY bar is unclear (participant 1: "what does the energy bar mean?"). Fix: explain ENERGY and FLOW in the tutorial; the intro text does not mention them yet.
5. The exercise card is missed, so players just follow the line and use the arm (participant 2). Fix: move the coach figure (`CoachCard`) closer to the line or into view.
6. Compensation seen: elbow pulled back and trunk movement (participant 4), "more the arm" (participant 1). Ties to 1 and 5, and confirms the limit that only head movement is measured.
7. Feedback works, mainly as information. The sound was the most praised element ("very pleasant, ding ding"). Keep it. Participant 1 missed music ("snorr saber"); consider a beat or music.
8. Engagement varied: participant 1 found it long and boring and would not replay; participant 2 could have played 10 more minutes; 3 and 4 would play once more.

Test procedure fixes before the next test:
- Participant 4 pressed REPLAY on the menu, so the test was restarted; lock replay to dev mode. Still open: the `"replay"` key in `GameMenu.Navigate` (LAST REPLAY, WATCH REPLAY) and the B button are not gated.
- Participant 3 ended in the main menu without a score; make sure the result screen stays until the test leader moves on. Still open, with no code change yet.
- Survey: UES items are set up as multiple choice (two respondents ticked two boxes on three items); make them single choice. "Application X" is still in the item text, "appealed to be visual senses" has a typo, two SUS items miss the leading "I", and participant 3 was unsure of the language.
- Think-aloud gave almost nothing while playing; prompt during play or rely on the interview.
- The recording for participant 1 only has the interview.

Paper (Overleaf) work from this pilot: done on 2026-11-12. Pilot results sit in Design as "Iteration 1 and pilot test" (`sec:pilot-results`, UES/SUS table and themes table), not in Findings, which is kept for the main evaluation with physiotherapy students and experts; a second pilot would get its own iteration subsection. Still to write: a Design section "Iteration 2" that ties each change to a finding. Also in: Methods (transcription and the survey error) and Limitations (n = 4, healthy Medialogy students rather than physiotherapy students, the survey errors, missing game data for two participants). Whisper transcripts of the recordings were not corrected word for word; check quotes against the audio before citing them.
