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

Start flow. START runs `Begin(false)`. Every enabled exercise and arm without a usable saved calibration gets `AutomaticEstimate`: an estimated full movement scaled by eye height, not a measured range. It starts at 35 % range and 65 % speed (`AutomaticStartRange=.35f`, `AutomaticStartSpeed=.65f` in `ExerciseGame.Calibration.cs`) and is flagged `automaticRange`. An automatic range can shrink to a floor of .15; a manually calibrated range keeps the .4 floor. Saved automatic ranges from an older `estimateVersion` are replaced (current version 3). While a line approaches, `TryAnchorAutomaticStart` moves the estimated start to the hand once, if the hand holds an allowed pose within 2.5 cm for 0.25 s and within 35 cm of the shown start. It never moves a target during scoring. Since 2026-11-17 every path is also kept within arm's reach of the estimated shoulder (`ClampToReach`), and the line pauses while the player is off the floor ring (section 12). The first START, every START with TUTORIAL ALWAYS on, and every OPTIONAL CALIBRATION run the tutorial (section 5). OPTIONAL CALIBRATION runs `Begin(true)`, the demo and pose-capture flow, which replaces the estimate with a measured range.

Provenance (told by the team on 2026-11-17, not recorded in the repo before): asked for exercises he considered suitable, Expert 2 (Nels) sent one illustrated exercise sheet by email (scaption, serratus anterior push, rotator cuff external rotation). It is in the paper as Appendix G (a 669x452 screenshot; replace it with the original attachment for better quality). The sheet says scaption to about 90-120 degrees, 30-45 degrees forward of the frontal plane, hold 1-2 s. The automatic estimate went to about 140 degrees until 2026-11-18 and now goes to about 120 degrees (estimate version 4, section 7). Emil used ChatGPT to turn them into the three exercise movements and into the rules that generate the synthetic training examples (`Tools/coach_training_labels.py`). The network was trained from scratch on those examples; no online dataset or pre-trained model was used, and the network does not see which exercise is played. The ChatGPT conversation and the expert emails are not in the repo; find them and keep them, since the paper (4.2, 4.4, Acknowledgements) now says this. Adaptive coach (`AdaptiveCoach.cs`, details in `ADAPTIVE_COACH.md`). A 4-16-3 tanh network. The inputs are score, coverage, the rep's mean hand error (normalised by `ErrorSeverity`) and the start offset (0-20 cm mapped to 0-1). The outputs are the range and speed change for the next attempt of that exercise and arm, bounded to ±.45 and ±.35, and a start follow (0-1): the share of the measured start offset the whole path moves, at most 5 cm per step and 20 cm in total. The model `nn3-28ACEF866E89FF43` was trained offline on 2,205 synthetic examples (1,792 train, 413 held out) with the v4 labels from `Tools/coach_training_labels.py`, using `Tools > P7 > Train Coach Model`. The step that added the start offset (v3) is only described in `ADAPTIVE_COACH.md`. The weights live in `Assets/Resources/AdaptiveCoach/base-model.json` and never change during play; each player's levels and start shift go to a separate `*-adaptive.json` next to `player-settings.json`. Gates outside the network: a dead zone ignores changes under .02 range and .0225 speed, a head-displacement flag (severity above .2) blocks increases but still allows easing, and a rep with more than 10 % tracking loss is not learned from. Dev mode switches learning off. The labels are invented, so the coach is not clinically validated.

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
- `ADAPTIVE_COACH.md` mixes older notes with the current ones (for example start follow "0.5 at 6 cm"); the model provenance and the code say 0.6.

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
| Larger movements | partial | 2026-11-10. Automatic estimate v4 (2026-11-18) is the mastered movement: scaption to about 120 degrees (was 140 in v3, see section 7), 18 cm serratus reach, 75 degrees rotation; range 1 is the goal and an arm starts at 35 %. Manual calibrations still cap at the captured range. Not yet tried on a headset |
| First-time tutorial | partial | 2026-11-10, intro changed 2026-11-17. Four intro pages that wait for A (B skips the tutorial), then per exercise a full-range demo on the figure and one "your turn" practice rep per enabled arm, with live hints on the lowered board (higher, lower, left, right, forward, closer, "good - hold it here"). The furthest point reached (x0.9, `PracticeRangeShare`) becomes that arm's starting range and the path quality sets its starting speed 0.6-1 (`PracticeSpeed`, `AdaptiveCoach.SetMeasuredLevel`); the start offset feeds the start shift (`AdaptiveCoach.ObserveStart`). Practice reps give no points, recording or FLOW. A goes on, B skips. Settings has a TUTORIAL ALWAYS switch (`tutorialEveryStart`), a runtime copy of the DEV MODE toggle (`GameMenu.AddTutorialToggle`). Not yet tried on a headset |

## 6. Backlog, in priority order
Pilot test 1 (2026-11-11) changes the priorities: see section 10 for the iteration 2 plan. Iteration 2 work started on 2026-11-17; section 12 says what is built and what is left.

- P0 Headset test of the 2026-11-10 changes (model `nn3-28ACEF866E89FF43`; rebuild the APK so it is on the Quest). Emil's own headset try on 2026-11-10 found that "perfect" never got harder, because the old labels only increased at score and coverage >= 90 % by 2-5 %, inside the dead zone. The v4 labels increase range and speed by 3-12 % per attempt at score >= 70 with coverage >= 75 % and low error. Check that it gets harder at a good pace but not too fast, that the start point settles at the right height for short and tall players (`nextStartShift` in `adaptive-coach.json`), that the larger automatic movements are within reach (see pilot finding 1), and that the tutorial is clear and not too long. Open team question: should manual calibrations also be allowed to grow past the captured range?
- P1 Line: frame rate with up to 119 lit sections, and whether the bridge and fill read clearly.
- P2 Rewards: the floating score HUD and streak text are in; check on a headset that they are readable.
- P3 Audio: tune levels by ear, check the 220 Hz pitch on the Quest speakers and whether a sustained tone gets tiring. No mute toggle yet; the tunables sit together at the top of `ScoreFeedback`.
- P4 Clinical: therapist-only limits are open again since the lock was removed (section 4). Session length and breaks are in: a 45 s break after each 5 minutes of active play and a finish at the next rep boundary after 15 minutes, exported as outcome `session_limit`. A physiotherapist must confirm the numbers and whether A may skip a break. Only the timing helper and `SessionProgress` are asserted, so the break has not been run. Follow-up: menu sliders for these values, added to the menu prefab.
- P5 Evaluation: participant ID in exports is in as a default awaiting team confirmation: the counter in `participant.json`, shown on the home menu, advanced by the RESET SAVED SETTINGS confirmation, written to `session.json` and `outcome.json`. `P7Tests` asserts the helpers and file recovery but not the menu or a real export. Still open: VRNQ questionnaire outside the app (no in-app support).
- P6 Cleanup: one game name (working title SHOULDER FLOW in the menu, product name `P7`); consistent exercise labels (menu says FORWARD REACH / OUTWARD ROTATION, gameplay says SERRATUS REACH / EXTERNAL ROTATION); real bundle ID; the open repo issues in section 4.
- P1b Progress view for the physiotherapist (requested by Simon on 2026-11-18): rep counts and a difficulty graph on the results page. Full spec and open questions in section 14.
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

Decided 2026-11-18: the scaption ceiling follows the experts. Expert 2's exercise sheet (Appendix G in the paper) says lift to about 90-120 degrees in the scapular plane, so the automatic estimate must go to about 120 degrees, not 140. Done in code on 2026-11-18: the scaption end in `ExerciseGame.AutomaticEstimate` is now 120 degrees in the same scapular plane, `AutomaticEstimateVersion` is 4 so saved 140-degree ranges are replaced, and `P7Tests` asserts the angle. Not yet run in the Play Mode tests or on a headset. The paper (4.2) already says about 120 degrees, so the code must match it before the final test. Serratus reach (18 cm) and external rotation (75 degrees) were not changed; the sheet gives no number for them.

## 8. Limits to state honestly
Compensation is only a head-movement heuristic. Elbow, scapula and torso are not measured, and the adaptive coach learned from synthetic labels with no clinical validation. The start shift and tutorial measures are untested on players. `P7Tests` and the Play Mode tests cover the rules and a full simulated session; nothing replaces a headset test. This file was rechecked against the code at `a6ab39a2` on 2026-11-16 by static reading plus a batch run of `P7Tests.RunChecks`.

## 9. History
Old names, for reading older commits and notes: the scene was `Emil.unity`, the namespace `EmilVR`, and the scripts were `ShieldExerciseGame` (now `ExerciseGame`), `ConduitRepairGame` (`PathFeedback`), `ShieldArcadeFeedback` (`ScoreFeedback`), `WandSessionReplay` (`SessionReplay`), `AdaptiveExerciseCoach` (`AdaptiveCoach`), `ExerciseGameMenu` (`GameMenu`), `ExercisePathStream` (`PathStream`), `ExercisePlayerSettings` (`SavedSettings`), `ShieldCoachDisplay` (`CoachCard`), `EmilVRInput` (`ControllerVisuals`) and `ShieldExerciseProfile` (`ExerciseProfile`). Checks ran through `ConduitSetup.Verify` and `ShieldExerciseVerification`; both are gone.

- 2026-11-04 (`74c4033c` and follow-ups): line glow, hand trail, path bridge, points and multiplier, procedural audio, ENERGY / FLOW wording, breaks and session cap, participant ID, and a therapist lock.
- 2026-11-05: floating score HUD, perfect-flow bonus (`PerfectFlowBonus`, 100 points per tick) and the results page.
- 2026-11-08 and 11-09: the neural coach replaced the rules, the automatic start replaced mandatory calibration (20 % range then), and the therapist lock was removed.
- 2026-11-15 (`a6ab39a2`): the renames above, the 2026-11-10 work (tutorial, start shift, larger estimate, 35 % start, model nn3), the new `Tools > P7` menu and checks, and removal of the old wand prototype, tracking trails and generators.

## 10. Pilot test 1 (2026-11-11) and the plan for iteration 2
Order agreed by the team: first process the pilot data and write it into Overleaf (Methods, Findings, Limitations), then build iteration 2. Findings 1-5 and the two menu procedure fixes were built on 2026-11-17; section 12 has the details. This was the first time the game ran on a headset with participants.

Sources (Drive folder P7/Test): "Pilottest d. 10-11-2026" (procedure and interview guide), "Observatør Noter", "Pilottest - Lydfiler" (7 recordings), and the SurveyXact survey "P7 - UES-SF" (id 1919004). Participants: 4 Medialogy students (MED7), healthy, 2 with moderate and 2 with no VR experience; participants 3 and 4 spoke English. The main test will be with physiotherapy students. The team recorded commit `457c05e1` as the pilot build. That commit has the automatic start at 20 % range, the 9 s tutorial text, model `nn2-914D68A12D0A1CFF` and the one-click menus, but not the 2026-11-10 changes in section 5. Emil's notes mention an APK built on 2026-11-10 at 23:17 with the tutorial, so confirm which build the headset had.

Results, descriptive only (n = 4). UES-SF total mean 3.57 of 5 (FA 3.46, PU 3.50 reversed, AE 3.75, RW 3.58). SUS mean 73.75 (52.5 to 85). Game results exist for participants 1 and 2 only: 88 % and 92 % accuracy, best streak 4 and 6, 22 and 21 movements. Survey rows R1-R4 are assumed to be participants 1-4 by completion time; confirm against the team's notes. The FLOW defeat did not trigger.

Findings, most severe first, with what iteration 2 should do:
1. Targets out of reach, fall risk. Participant 3 stepped forward and back and reached behind himself; participant 4 stood on one leg and nearly fell on the overhead exercise ("the circle is so far away sometimes"); participant 1 felt he had to walk forward. Fix: calibrate each participant standing still, add a floor marker, and clamp target distance and height to the calibrated reach. The default flow skips calibration and starts from the automatic estimate, and the current estimate is larger than in the pilot build (140 degrees scaption instead of shoulder height), so this matters more now. The tutorial practice and start shift fit the line to the player but do not clamp it. Safety issue, so it goes first.
2. Depth of the target ring is hard to judge (3 of 4: "the rings are very 2D", "too behind or too close"). Fix: a depth cue, for example the ring changing colour or size with the z-distance to the hand, or a line or shadow from hand to target. Alternative: weight depth less in the position error.
3. Tutorial text disappears too fast (participants 1, 2, 4). The pilot build showed five lines for 9 s. Partly addressed in `a6ab39a2`: the intro now lasts 12 s (`TutorialIntroSeconds`) and is followed by demos and practice with live hints, Built on 2026-11-17 (section 12): four short pages that wait for A.
4. Players are unsure how they are doing early on, and the ENERGY bar is unclear (participant 1: "what does the energy bar mean?"). Fix: explain ENERGY and FLOW in the tutorial; the intro text does not mention them yet.
5. The exercise card is missed, so players just follow the line and use the arm (participant 2). Fix: move the coach figure (`CoachCard`) closer to the line or into view.
6. Compensation seen: elbow pulled back and trunk movement (participant 4), "more the arm" (participant 1). Ties to 1 and 5, and confirms the limit that only head movement is measured.
7. Feedback works, mainly as information. The sound was the most praised element ("very pleasant, ding ding"). Keep it. Participant 1 missed music ("snorr saber"); consider a beat or music.
8. Engagement varied: participant 1 found it long and boring and would not replay; participant 2 could have played 10 more minutes; 3 and 4 would play once more.

Test procedure fixes before the next test:
- Participant 4 pressed REPLAY on the menu, so the test was restarted; lock replay to dev mode. Built on 2026-11-17 (section 12).
- Participant 3 ended in the main menu without a score; make sure the result screen stays until the test leader moves on. Built on 2026-11-17 (section 12): two presses to leave, and the last result on the home page.
- Survey: UES items are set up as multiple choice (two respondents ticked two boxes on three items); make them single choice. "Application X" is still in the item text, "appealed to be visual senses" has a typo, two SUS items miss the leading "I", and participant 3 was unsure of the language.
- Think-aloud gave almost nothing while playing; prompt during play or rely on the interview.
- The recording for participant 1 only has the interview.

Paper (Overleaf) work from this pilot: done on 2026-11-12. Pilot results sit in Design as "Iteration 1 and pilot test" (`sec:pilot-results`, UES/SUS table and themes table), not in Findings, which is kept for the main evaluation with physiotherapy students and experts; a second pilot would get its own iteration subsection. Still to write: a Design section "Iteration 2" that ties each change to a finding. Also in: Methods (transcription and the survey error) and Limitations (n = 4, healthy Medialogy students rather than physiotherapy students, the survey errors, missing game data for two participants). The 11 quotes in the themes table were checked with a second Whisper pass on the audio and four were corrected; nobody has listened to them yet.

## 11. Paper review (2026-11-12) and open to-do list
A critical read of the whole Overleaf paper on 2026-11-12, checked against the code and the pilot data. The team answered the first version of this list in the Drive doc "To do 12-11-2026"; questions for the supervisor are in "Møde d. 17-11-2026". Answers are marked below.

Already fixed in Overleaf on 2026-11-12: the research question's closing "?", the circular adherence sentence in 4.3 (now what Jack et al. found), the SUS mean set against the average of about 68 (Lewis 2018), "accuracy" defined as the mean rep score, 3.6 Pilot test in past tense with the questionnaire language (English, SurveyXact), Brooke cited as the 1996 book chapter instead of a ResearchGate PDF, convenience-sample bias in Limitations, narrative citations shown with author names (`\textcite`), O'Brien and Gale as real citations, haptics in 4.3.1, and two more limitations (head-only compensation, unvalidated coach and break values). New sources sit in `Bibliography/engagement.bib`; `ref.bib` is a Zotero import and cannot be edited in Overleaf.

Paper and code: done in Overleaf on 2026-11-17. 4.2 now describes the automatic estimate (35 % range, 65 % speed, eye-height scaling), start anchoring, the tutorial practice rep and optional calibration; 4.4 describes the 4-16-3 network, its inputs and outputs, the synthetic labels written by the group (said plainly) and the gates; 4.5 says the lock was removed; 4.3.1 says PERFECT / NICE and the multiplier as text; Table 1 R5, R6, R9, R10, R13 follow the code; Limitations names the synthetic labels. Still open: the paper describes the current build (`nn3`) and does not yet say that the pilot may have used an older one (`nn2`, 20 % start, 9 s tutorial). Confirm which APK was on the headset, then add one sentence to 4.7. The iteration 2 changes (section 12: reach clamp, floor ring, step-off pause, depth guide, tutorial pages, coach card) are not in the paper yet; they belong in a new "Iteration 2" subsection.

Decided by the team on 2026-11-12: done in Overleaf on 2026-11-17, except the audio.
- 3.4 now says the game rates execution and the experts give feedback on whether the rating is precise enough.
- 4.7 reports that two booked participants did not attend.
- Thematic analysis cites Braun & Clarke (2006) in 3.3 and 3.6, and 3.6 says an AI assistant grouped candidate themes, which were then checked against the transcripts and notes.
- Still to do: cut participant 1's first name from the audio (said at about 01:59-02:00 in "Participant 1.m4a"); the appendix transcript already shows [name].
- Follow-up interviews with the experts: yes. With the physiotherapy students: not decided.

Answered by the supervisor (Casper) on 2026-11-17 (Drive doc "Møde d. 17-11-2026"), still to do:
- AI use: a DRAFT Acknowledgements section is in `main.tex` after the Conclusion (marked with a `% DRAFT` comment). It names Whisper, and Claude for transcript cleaning, theme grouping, literature search, code and writing help. The team must check and approve the wording before hand-in, since it is their own statement.
- Follow-up interview: test with Expert 1 (Trine) too if there is time; it is more data. Nothing was said about follow-ups with the physiotherapy students.
- Shoulder question: add it to the survey (Methods 3.2 already promises to record shoulder conditions). The team wrote "Tror jeg ikke alle var sunde" about the pilot participants, so "healthy" in section 10 and "without a rehabilitation need" in Limitations may be wrong; confirm with the observers.
- Methods: done 2026-11-17, all of section 3 is in the past tense. 3.2 now says participants were "not recruited for shoulder injuries" and that shoulder problems were recorded, which the survey must now actually ask.
- Appendix: keep it at the end of the paper. Built in Overleaf on 2026-11-17, one column, with clickable references (`\hyperref[app:X]{Appendix X}`): A interview Expert 1, B interview Expert 2 (cleaned transcripts from the Google Docs), C pilot procedure and interview guide, D observer notes, E pilot questionnaire responses, F pilot recordings. F uses the first Whisper pass from 2026-11-11 with filler-only lines removed, so its times are approximate; all 11 quotes in the pilot themes table are in F within about 10 s of the times in the table, which were checked against the audio. Table 1 timestamps were checked against Appendix A/B on 2026-11-17 and four that pointed at an interviewer turn were fixed. A precise re-run without voice detection was blocked because Windows Smart App Control blocks newly installed Python DLLs on Frederik's PC.
- Survey language: keep it in English, do not translate the items; the test leader explains words when asked. Done in Methods 3.3 on 2026-11-17.

Before the next test:
- Open, non-leading interview questions ("What did you feel where?" rather than "Did you feel it in the shoulders?").
- Survey: single-choice UES items, remove "Application X", fix the typos, decide the language.
- Lock REPLAY to dev mode and keep the result screen up: built on 2026-11-17 (section 12), not yet tried on a headset.
- Already true: the interviewer did not build the game, and observer notes keep observation and interpretation apart.

Writing left, to do last: abstract, title and Figure 1 (template placeholders); Findings, Discussion beyond Limitations, and Conclusion (empty); appendix A-J names; reference [1] now has its authors (Pilgaard et al. 2025, key `pilgaard_danskernes_2025` in `engagement.bib`, replacing the Zotero entry without author; done 2026-11-18); section 2.2 claims now have sources and 2.1 has a paragraph on dynamic difficulty adjustment (Hunicke 2005, Zahabi and Abdul Razak 2020; done 2026-11-18); the Iteration 2 section once it is built. Moodboard screenshot copyright: the team decided not to check it.

Done in Overleaf on 2026-11-18: think-aloud cited (Ericsson and Simon 1980) in 3.6; the last future-tense sentences in the Introduction and 4.3 moved to past tense; Appendix G now describes the email exchange with Expert 2 (13, 15 and 19 October 2026, then the sheet); Discussion has four empty subsections (engagement and challenge, the adaptive coach, supporting physiotherapists, design implications and future work) with notes as LaTeX comments, to fill in after the final test. Iteration 2 is kept for the final test only, so the paper documents iteration 1 and the pilot in Design; Findings and Discussion are for the final test.

Final test materials (draft, 2026-11-18): the Drive doc "Endelig test – interviewguide, survey og samtykke (udkast)" in P7/Test has a checklist, open interview questions for participants and for the experts, the exact SurveyXact fixes (single choice, "the game" instead of "Application X", typos, background and shoulder questions) and a consent text that covers audio recording. The team must review it, check the consent against AAU's template, and make the changes in SurveyXact. Hunicke (2005) in 2.1 was rechecked against the paper and reworded.

## 12. Iteration 2 build log (started 2026-11-17)
Work on branch `Share-load`, not committed by Claude; the team commits and pushes. Each item says what changed and how far it got, so the next person can pick up here. Nothing below has been tried on a headset. All items compiled in the Unity editor (macOS, 2026-11-17 18:55) and `P7Tests.RunChecks` passed with the new `CheckIteration2`. The two Play Mode tests were updated but NOT run: Unity only advances Play Mode while it is the focused app, and nobody was at the machine. Run both from Tools > P7 before the next build.

Test procedure fixes:
- Replay locked to dev mode. `GameMenu.Navigate("replay")` does nothing outside dev mode and LAST REPLAY / WATCH REPLAY are greyed out; B opens a replay only in dev mode (it can still close one).
- Results screen stays up. Outside dev mode, MAIN MENU, PLAY AGAIN and WATCH REPLAY on the results page need two presses on the same button, 0.4-4 s apart (`GameMenu.NeedsConfirm`, `Confirmed`, `ConfirmMinSeconds`, `ConfirmMaxSeconds`). The first press relabels the button "PRESS AGAIN"; the footer says "Test leader: press a button twice to leave the results." The home page also shows "Last run: outcome, points, accuracy, best streak, moves", so the score survives leaving the page (until the app closes).

Pilot findings:
- 1, reach and fall risk (safety):
  - Reach limit: `ExerciseGame.ClampToReach` keeps every point of the shown and scored path within `ArmReachMetres` (.66 m, times the eye-height scale) of the estimated shoulder (`ShoulderOffset`, the same point the automatic estimate swings around). It first moves the whole path towards the shoulder (the movement keeps its size, only its place changes) and only cuts the end back if that is not enough. It runs last in `EffectiveCalibration`, so it covers the automatic estimate, anchoring, the coach's start shift and manual calibrations. The default automatic estimate is already within reach and is not changed (asserted). The .66 m is an estimate for a 1.75 m adult, not measured; check it on short players.
  - The eye-height scale is now read once per session in `SetFrame` (`bodyScale`), so a target can never move during a rep because the player crouched.
  - Floor ring: a 0.3 m ring on the floor under where the player stood at START (runtime `Floor Ring`, cyan; orange when off the spot). Floor height is `trackingSpace.position.y` (floor tracking).
  - Stepping off: if the head moves more than `stepLimitMetres` (.35 m) horizontally from the START position, the line and clock pause, the board says "STEP BACK ONTO THE FLOOR RING", and the line goes on below `stepReturnMetres` (.2 m). The time counts as lost time for the rep (so a rep with more than 10 % off the spot is not learned from) but not as tracking pause; it is exported as the new field `off_spot_seconds` in `outcome.json` (added, nothing renamed). Team question: are .35 / .2 m right, or does leaning in the serratus reach trigger it?
  - Not done: "calibrate each participant standing still" is a procedure point (use OPTIONAL CALIBRATION per participant); no code change.
- 2, depth: a thin line from the ball to the ring (runtime `Depth Guide`) when the hand is more than 3 cm off in depth (`DepthCueMetres`): cyan = reach further forward, orange = bring it closer. Scoring is unchanged. The alternative (weight depth less in the error) was not done. Added 2026-11-18: the target ring is now a solid torus (runtime child `Target Torus`, 8.5 cm radius, 8 mm tube; the flat 5 mm `LineRenderer` in `Target Shield.prefab` is switched off at runtime), so the ball can pass in front of or behind it. It is white off target and turns green as the hand reaches it (`ExerciseGame.TargetColor`: from follow quality .5, about 9 cm off, to .9, within 5 cm, the same 3D distance that is scored) and grows 12 % when fully on. The ring no longer shows the arm colour; the line and coach card still do. Unlit material, so no shading. Not tried on a headset.
- 3, tutorial text: the 12 s timed intro is now four short pages (`ExerciseGame.TutorialIntroPages`: how to play, stand still on the ring, ENERGY and FLOW, your turn). Each waits for A, which only counts after a page has been up 1 s (`TutorialPageMinSeconds`). The wrap-up also waits for A. B (R in the editor) skips the whole tutorial (`SkipTutorial`); A during a tutorial demo jumps to the practice rep. `TutorialIntroSeconds` and `TutorialOutroSeconds` were removed.
- 4, ENERGY / FLOW: explained on intro page 3 and in the wrap-up.
- 5, coach card: moved from 2.25 m away and 0.8 m below the eyes to the centre between the two arms' lines, 1.75 m away and 0.22 m below the eyes, and slightly larger (`CoachCard.CardPosition`, `FigureScale`). Check on a headset that it does not cover the line in scaption.
- 6, compensation, and 7, music: not started. 8 is not a code item.

Safety, 2026-11-18: scaption estimate lowered from 140 to 120 degrees (estimate version 4; section 7). Saved automatic ranges from v3 are replaced at the next START; manual calibrations are not touched.

Still open from this plan: run both Play Mode tests; headset test of all of the above; the team questions above; failure-ending redesign (section 7a) waits for a team decision.

## 13. Review of the paper by four reviewer roles (2026-11-18)
Four AI reviewers read the whole paper (41 pages incl. appendices) and this file, each in one role: external examiner, supervisor, physiotherapist and HCI methods reviewer. Their views are input for the team, not decisions. Literature they suggested (CORGIS, Smeddinck 2015, Hocine 2015, Lange 2012, Mueller and Isbister 2014, PXI, Braun and Clarke 2019) has NOT been checked; verify each against Crossref before citing.

All four agree on:
1. The final test does not yet measure the whole research question. Engagement is covered (UES-SF), but "appropriate level of challenge" and "support physiotherapists" are not. Add a challenge measure (a "too easy / about right / too hard" item per exercise, or a validated scale such as CORGIS), use the coach logs per participant, exercise and arm (does the level settle, how often it eased or increased), and define in 3.1 beforehand what "appropriate" means. Give the physiotherapists a task in the therapist role (watch a replay, judge progression, set limits), or narrow the research question.
2. The game rates its own execution (3.4), so "88 %/92 % accuracy" only says how well the line was followed. Let the experts rate a sample of reps (live or from replay) with a simple checklist and report agreement with PERFECT/NICE (for example Cohen's kappa).
3. Safety must be fixed in code before the final test, not only in the paper: nothing from iteration 2 has run on a headset, the Play Mode tests have not run, scaption must be lowered to 120 degrees in code (section 7), calibration should be mandatory in the test, a person stands next to the player during the overhead exercise, and there is a stop criterion ("stop at sharp pain").
4. The adaptive coach needs a better defence in 4.4: why a neural network rather than the rules it learned from (the examiner's likely question: "what can the network do that an if-statement cannot?"), report the held-out error (in `ADAPTIVE_COACH.md`), and explain the step limits of ±45 % range and ±35 % speed against R8 "progression must be slow", which Table 1 marks "Met".

Quick text fixes the reviewers found. Done in Overleaf on 2026-11-18 unless marked open:
- 4.3 "calibrated to the player's own range" now says the range is estimated from the player's height and adjusted by the coach.
- 4.7 no longer says "as described above". Still open: confirm which APK the pilot used and write which build each test used.
- Table 1: status "Met" is now "Implemented", and the caption says the status is the group's own assessment, not whether the requirement is met in practice. R4 now says E2 agreed with an interviewer's suggestion (R13 already said "she agreed").
- Table 3: the theme "Feedback supports correct movement" is now "Feedback perceived as useful".
- 4.2 now says the exercises train range of motion and movement control, not strength. The paper only claims compensation detection as a requirement (R7, "Partial", head-movement indicator only).
- The expert's name is Nels. Speech recognition wrote "Niels" in the transcripts; all six places in Appendix A and B and in both Google Docs transcripts were changed to Nels.
- The HCI reviewer would keep Methods in present or future tense until the test has run; the supervisor said past tense, so keep past tense, but it must be true at hand-in.

Supervisor's plan (adjust the dates):
- Week 47: freeze a build tested on a headset (feature freeze after this), lower scaption to 120 degrees, fix SurveyXact and the consent, run a dress rehearsal with 1-2 people outside the group.
- Week 48: final test with 6-10 physiotherapy students (overbook by 30 %; two did not show in the pilot) and follow-ups with both experts. Export data after every participant and never uninstall the app.
- Week 49: analysis (framework matrix, UES/SUS descriptives, coach logs) and the Iteration 2 subsection.
- Week 50: Findings, Discussion, Conclusion. End Findings with a table: the three parts of the research question as rows, data sources as columns.
- Last days: abstract, title, Figure 1, approve Acknowledgements, proofread, keep two days of buffer.

Physiotherapist's extra points: dosing is clinically set in sets, repetitions and symptom response rather than minutes, so show repetitions per exercise; rotation is where trunk compensation is most common and may need a slower pace; a therapist would want to lock limits, switch automatic progression off, see a session report and see the patient move without VR first.

Strong points all four named: honesty about limitations, traceability from expert statement to requirement to implementation (Table 1 with timestamps), and transparent pilot reporting. The examiner rated the written sections 1-4 at about 7, with potential for 10 if the final test measures what the research question asks.

## 14. Feature request: progress view for the physiotherapist (2026-11-18)
Requested by Simon. Not built yet: the next developer picks it up from here and records progress in the build log (section 12).

Why. Both experts asked to see how the player is doing: Expert 1 wants the number and quality of repetitions (Appendix A, 00:12:04 and 00:22:40); Expert 2 wants to see whether the line is held before and after training (Appendix B, 00:25:37). Table 1 R12 is "Partial; no therapist view". The physiotherapist reviewer (section 13) says dosing is set in repetitions per exercise and that a therapist wants a session report, and the reviewers' point 1 says the final test does not yet measure "support physiotherapists". This feature gives the physiotherapists in the final test something concrete to look at and judge.

Scope for version 1 (read-only, on the results page, nothing else changes):
1. Repetition counts per exercise and arm: attempts, complete reps and PERFECT reps. Attempts is what the results page already shows as "moves" (`CompletedRepetitions` counts every finished rep, complete or not). Complete is the `complete` flag in `ExerciseGame` (around line 1080). PERFECT is `ScoreFeedback.Perfect(complete, score, coverage)`: complete, score >= 90 and coverage >= 90 % (section 7, decided 2026-11-04). Label it "PERFECT reps", not "correct reps": the rule only says the line was followed, and it is not clinically validated (section 8, section 13 point 2).
2. Difficulty graph: one small line chart per enabled exercise and arm, x = repetition number, y = the coach's level 0-100 %. Plot range (`AdaptiveCoach.Decision.rangeScaleUsed`) and speed (from `durationScaleUsed`; check how it maps to the speed % shown in Settings, `ArmSettings.speed`, and use the same %). Mark reps where the head flag blocked an increase and reps that were not learned from (tracking loss over 10 %, off the floor ring). Draw it in world space on the results page with a `LineRenderer` or UI lines; no new package.
3. Optional if time allows: mean score of the first three and the last three reps per exercise and arm (Expert 2's "before and after").

Data: everything already exists during the session. Use the coach's in-memory `decisions` list (`AdaptiveCoach.cs`, filled in `Observe`, saved as `adaptive-coach.json`) and the per-rep summary rows (`AddSummaryRow`). No new recording is needed. Tutorial practice reps are not counted (they give no points or recording). In dev mode learning is off, so the graph is flat: show "Dev mode: adaptation off" instead of the graph.

Rules:
- Read-only. Do not change scoring, the coach, the network or the gates.
- Export formats are research data (section 4): add new fields or files only, rename nothing.
- Put the counting and graph data in small pure functions and add a check for them to `P7Tests.RunChecks`.
- Must be readable in the headset: large text, few lines. Check it on a headset.
- Update PROJECT_MAP.md, the "Rewards: personal progress" and "Rewards: round summary" rows in section 5, and the build log in section 12 in the same commit. The paper (Table 1 R12, the Iteration 2 subsection) is updated by the team afterwards.

Timing: the supervisor's plan (section 13) has a feature freeze at the end of week 47, with a headset-tested build. Build only part 1 and 2, keep it small, and test it on a headset before the freeze. If it is not working by then, leave it out of the final test build rather than delay the safety fixes (section 7 scaption, section 13 point 3), which go first.

Open questions for the team (ask, do not assume):
- PERFECT or complete as the headline number?
- Per arm or both arms combined?
- On the results page, or a separate THERAPIST page reached from it?
- Should the final test use it: after each participant, the physiotherapist reads the page and answers whether they could use it? That would give the "support physiotherapists" part of the research question its own data.

Later, not in version 1: progress across sessions (from the saved `Sessions` folder and `*-adaptive.json`, "better than last time", R11), a session report the therapist can export, and a therapist-only lock for the view.
