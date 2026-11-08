## Neural adaptive coach — 2026-11-08
The old rules have been replaced by a small trained neural network. All training, inference and player-persistence logic is in `AdaptiveExerciseCoach.cs`. The read-only base model is `Assets/Resources/AdaptiveCoach/base-model.json`; editable simulated training examples are in `Assets/VR/Training/coach-training.json`. Player levels are saved separately alongside settings in `*-adaptive.json`. This starter learns synthetic examples, not clinical outcomes. See [the training and file guide](../../../ADAPTIVE_COACH.md).

Validation: trained model and bounded predictions passed; separate personal persistence/reload, dev-mode exclusion, bad tracking/invalid input, reset, and the existing range/speed checks passed. A full simulated game session passed with neural decisions, adaptation on subsequent attempts, replay and saved-settings restart. No Quest installation or headset validation in this change. Older adaptive sections below are historical.

## End-of-run results — 2026-11-05
Finished runs now open a dedicated results screen rather than the start menu. It shows the outcome, animated total score, accuracy, best streak, movements and perfect-flow bonus points. PLAY AGAIN uses saved calibration and learned difficulty; MAIN MENU returns home; WATCH REPLAY opens the saved movement replay. The page is generated into Exercise Main Menu.prefab, with a runtime fallback for existing instances.

Validation: full simulated session passed, including automatic results display, exact final score, Main Menu navigation and Play Again retaining saved calibration/difficulty. The rendered results screen was visually inspected. Not installed or tested on Quest yet.

## Minimal gameplay HUD and larger scores — 2026-11-05
Gameplay now has a box-free floating score, short streak label, slim energy/flow bars, and brief completion accents. Large instruction/status boxes are hidden during the moving paths; the current movement figure stays at the bottom without its backing panel. Tutorial/calibration/break instructions and tracking-loss warnings remain available. Score counts up and pulses, and bonus dings send a gold pulse around the target.
Base repetition points are now 1,000 × quality, and tracking ticks start at 100 × multiplier. Standard successful runs exceed 10,000 points; short custom/aborted runs may not. This is cosmetic game scoring and does not change movement scoring or adaptive thresholds. `game-points.json` is now `perfect-flow-2`, with base point values recorded. Earlier validation scores below describe the previous point scale.

Validation: final Unity simulated run passed with 82,142 points (5,000 from tracking bonuses). Board-hidden and score-visible assertions passed, and the final rendered HUD was inspected. Existing adaptive, persistence and replay checks passed. Quest installation/playtest has not been performed.

## Perfect tracking bonus — 2026-11-05
Follow the target at >=90% positional quality for two continuous seconds to start a bonus. Every 0.5 seconds it awards 10 × multiplier points; every four ticks the multiplier increases, capped at x5. A miss, tracking loss, rest or gap resets this streak, but keeps earned points. A pulsing gold caption over the target and a rising bell show each award. A larger world-space total sits in the center at 2.6 m. The existing completed-repetition streak remains separately labelled REP STREAK. Movement accuracy, adaptation, therapist limits and energy/flow rules are unchanged. `game-points.json` exports total and tracking bonus points separately. Runtime UI uses the existing arcade prefab's font/material, so no generator run is needed to use the feature.

Validation: Unity desktop compilation, bonus timing/reset checks, and the full simulated session passed on 2026-11-05. The session exported 8,134 total points, including 420 tracking bonus points; adaptation, replay and saved settings checks passed. Rendered preview inspected. Audio loudness and VR comfort still need a Quest check; no device installation was performed for this change.

# Shield exercise prototype - Emil

## Current state, 2026-11-04

This section overrides anything older below, since the rest of this file is a dated changelog. `PROJECT_MAP.md` has the detail, and the code wins if the two disagree.

The game is line following with a night theme: the player follows a line with the hand through three exercises. Reactor Rescue is not the current game. Its HUD, core and drone stay hidden, so there are no visible reactor segments, and `ShieldArcadeFeedback` only supplies audio, points and combo. `ConduitRepairGame` is the active theme, and the player sees ENERGY (`power` in code, `ship_power` in exports) and FLOW (`battery` in code and exports). Shield, conduit and repair remain as internal names and in export files, so old data stays comparable.

Scoring is position-only (`position-only-hand-5`). Quality falls from full credit at 4 cm to zero at 14 cm. Controller rotation is recorded but does not affect accuracy, endpoint or hold credit, or adaptation. The hold has no 20 degree check, since it only needs the hand within the endpoint tolerance.

`session.json` reports schema `shield-6` and `conduit.json` reports `conduit-2`. Samples keep 40 columns and summaries 18.

Session length is not exact. The total setting (1-120, default 18, labelled "Energy target (strong movements)") only sets how fast ENERGY fills. Conduit extends the playlist with new batches until a win (ENERGY full), a loss (FLOW empty) or the session cap (outcome `session_limit`). The cap is 15 minutes of active play, with a 45 s break after each 5 minutes. These are prototype values that a physiotherapist must confirm.

The multiplier counts perfect repetitions (complete, score at least 90, coverage at least 90 %), up to x5. Nothing in this version has been run on a headset.

The active game in Emil is now Shield Exercise Game. The earlier Wand Path Prototype instance is disabled; its assets and old recordings remain available.

## Exercises

Based on the three supplied exercise illustrations:

1. **Scaption:** lift the arm outward in a plane slightly forward of directly sideways, then lower slowly. The illustration specifies approximately 30–45 degrees forward of the frontal plane and a 90–120-degree lift. This prototype does not enforce that elevation or measure a shoulder angle: the player captures their own comfortable prescribed endpoint. The path uses an estimated shoulder pivot.
2. **Serratus reach:** arm forward at shoulder height with elbow straight; reach a little farther forward through the shoulder blade, hold, then return. The game measures controller reach, not scapular protraction or serratus activity.
3. **External rotation:** elbow bent beside the ribs, rotate the forearm outward, hold, then return. The game guides the hand along an arc using an estimated elbow pivot. Elbow angle, elbow position and actual humeral rotation are not measured.

One arm at a time. Setup teaches and calibrates left then right for each exercise; the mission then mixes the three exercises and alternates arms. Three repetitions per arm, 18 total challenges. Simultaneous-arm harder levels are not implemented yet. Profiles are reusable ScriptableObject assets in this folder; each owns movement type, timings, repetitions and tolerances. Anatomy-dependent correctness requires additional validated tracking or trainer observation.

## Controls and flow (Movement Paths)

- **A:** begin / restart after results. This is the only button needed to start.
- Watch the coach, then copy its stationary starting posture. One large full-body character faces you like a mirror: your left stays on the left of the view. Use the named arm (your left/red or right/green). The drawn body is illustrative.
- A glowing orb shows your hand beside the coach. Hold a broadly suitable posture still for 1.25 seconds to capture it automatically. Movement beyond 2.5 cm or 12 degrees resets the hold timer. Tracking loss resets capture progress.
- Copy the second pose within your comfortable prescribed range and hold still again. No trigger capture is needed. Direction checks reject obviously wrong endpoints; this is not an anatomical form assessment.
- The coach returns to the start. Bring your hand back near its captured starting position and hold still. After all six calibrations, a nine-second unscored controls tutorial lets you try movement in all three axes. During the mission, each challenge approaches automatically with a 2.5-second gap; there is no pose gate or displayed countdown between exercises.
- One animated CURRENT movement card remains visible during the mixed mission. Follow the blue target with your remote shield: 3 seconds outward, 1.5 seconds holding, 3 seconds returning, 1 second rest.
- Each exercise has its own finite 3D line, including its outward curve, hold and return. The following line is visible farther away, separated by a gap; there is no connecting segment between exercises. Reaching the endpoint gives a flash and haptic/audio feedback. Complete the hold and return to earn reactor energy and game points. Consecutive completed repetitions increase the points multiplier, capped at five. Reactor segments represent the six exercise/arm blocks.
- **X:** stop and save available data. **B:** replay from welcome/results. Right trigger pauses replay; right stick scrubs; left stick rotates / changes distance.

## Front-view controls

The visible shields are remote controls, not located on your hands. A fixed offset brings each exercise's starting position into the forward view. From there, every metre of hand displacement produces the same displacement of the shield in the calibrated body frame, on all three axes, without clamping. Overshooting upwards, downwards, sideways or in depth remains visible. This means extreme movement can leave the forward view. The target uses exactly the same transform. A directional centimetre hint identifies the largest position error, including depth. Shield tilt still gives a bounded visual cue for pointing error. The play area stays anchored to calibration rather than following head movement.

Raw hand poses and physical exercise errors still determine scoring. The mapping does not validate an elbow or scapular movement and does not remove compensation from the data. Replay continues to show the recorded physical hand movement with shields at the hands, rather than reproduce the remote arcade display.

Only the active arm and headset must be tracked. Tracking loss pauses the exercise clock and score. It does not break a combo by itself.

## Scoring

Each repetition averages hand-position and shield-normal quality over time, using the lower quality term. Full credit within 4 cm and 12 degrees, falling to zero at 14 cm and 40 degrees. These are prototype game settings, not clinical thresholds.

Movement score is now partial credit: `100 × (0.7 × mean path quality + 0.3 × phase completion) × (0.25 + 0.75 × travel coverage)`. Phase completion averages start reached, endpoint reached, fractional hold, and return reached. Travel coverage is the measured range of progress along the calibrated start–end direction, clamped to 0–1. A stationary hand is capped at 25 even if tolerances overlap. Missing one phase no longer makes all movement accuracy zero. The strict `complete_movement` flag remains separate, also requires at least 65% travel coverage, and controls reactor charge/combos. Partial movement earns base game points; completed repetitions earn combo bonuses.

Start/end tolerance is 30% of calibrated travel, clamped to 2.5–6 cm. Hold checks allow up to 20 degrees with the default profile. An endpoint can be registered throughout the hold interval rather than on a single arrival frame. The full-credit path score remains 4 cm / 12 degrees. These are prototype game settings, not validated clinical thresholds. The score version changed, so do not compare old and new totals as clinical progress. Raw errors remain available for comparable analysis. Circular shield roll is not scored. Endpoint detection uses the prescribed position and orientation.

## Recording and export

Saved under the same headset Sessions folder as before. **Tools > Emil VR > Export Quest Recordings** copies the files over USB.

- `session.json`: shield schema/version, automatic calibration method, three-axis mapping version, input source, calibration frame, all completed endpoint calibrations and exercise settings. Pivot points are estimated, not anatomical measurements.
- `samples.csv` / `samples.tsv`: one row per hand per sample (up to 60 Hz). Includes exercise ID, repetition, phase, active side, validity/scorability, raw controller poses, active-hand target pose, headset pose, hand and shield-normal error, head displacement/rotation, and the projected shield/target positions. The shield-4 schema has 40 columns. Time within the exercise resets each repetition; timestamp_s is wall time from recording start. Blank fields mean unavailable/not applicable, not zero.
- `summary.csv` / `summary.tsv`: one row per completed repetition with score, phase gates, hold duration, valid duration and mean errors. Interrupted in-progress repetitions remain in the raw samples but have no completed-repetition summary.
- `outcome.json`: session completion and tracking-pause duration.
- `replay.json`: recorded headset/controller poses and target poses, exercise/phase/active arm and shield equipment marker. Pauses for demos/calibration have gaps; replay holds the last available pose across gaps instead of inventing intervening motion. Up to 18,000 frames are retained by the existing replay recorder; very long tracking pauses can exhaust this limit. Raw samples continue.

TSV opens/imports using tabs and avoids comma-delimiter regional settings. CSV/TSV numerical fields use decimal points. The shield schema is different from the old wand schema: do not combine score versions or assume the old summary columns. Original recordings are untouched. No cloud upload occurs.

The replay mannequin's shoulders, elbows, torso and legs remain schematic estimates. Shield replays hide the old yellow elbow targets so they cannot be confused with validated exercise-form targets. Older wand recordings still replay as wands.

## Verification

- Path endpoints, phase progress and score bands verified.
- Automated Play Mode: six exercise/arm blocks, demonstrations and two accelerated test repetitions each, 12/12 perfect repetitions, score 100, shield replay saved and reloaded.
- Verified 2,448 sample rows / 1,224 replay frames, six calibrations, all three exercise IDs and both arms. CSV and TSV matched field-for-field.
- Synthetic tests are marked SIMULATED TEST and stored separately in /tmp/emil-shield-tests.
- Physical exercise form, comfortable calibration, visibility at large arm elevations and headset experience require wearer/trainer testing. This is not a clinical assessment or a muscle-activation measurement.

Final checks: corrected demo and gameplay views rendered and inspected; final per-repetition error columns verified with zero-error synthetic input. Quest build succeeded with zero build errors, installation succeeded, cold launch returned OK, and process 24489 remained alive. No new gameplay exception appeared in the inspected startup log; the existing Unity AssetPackManager startup message remains. In-headset exercise execution has not been tested by a wearer. Build log: /tmp/emil-shield-build.log.

Automatic-calibration update verification: the six exercise/arm blocks completed without simulated trigger presses or bypassing the stability test. Twelve accelerated repetitions scored 100, awarded mission points and a 12-repetition combo. Six endpoint calibrations were saved. Verified 2,448 data rows with 40 columns and field-identical CSV/TSV files. Front mapping bounds tested for all exercise types and both arms. Synthetic output remains separate from participant recordings.

Reactor Rescue final checks: automatic calibration and front-view gameplay rendered and visually inspected after HUD adjustments. Final APK built with zero errors, installed successfully, and cold-launched on Quest. Process 32109 remained alive; no new gameplay exceptions appeared in the inspected startup errors (the previously documented AssetPackManager message remains). Physical ease of automatic capture and comfort still require wearer testing. Build log: /tmp/emil-auto-build.log.

## October 22 mixed mission update

Reusable `Movement Coach.prefab` adds four-view calibration and animated NOW/NEXT reference cards. Existing shield, drone, reactor, replay and exercise-profile prefabs remain reusable. `summary.csv` / `.tsv` now include `path_accuracy_percent`, `travel_coverage`, and `phase_completion` alongside the strict completion checks and raw errors. `session.json` identifies score `shield-partial-coverage-3` and mapping `front-translation-2`. Samples retain 40 columns.

Mixed-update verification: full automatic calibration, unscored tutorial, and twelve accelerated mixed challenges passed with score 100 and combo 12. Six exercise/arm pairs and six calibrations were recorded; 2,448 sample rows each had 40 columns, with matching CSV/TSV fields. The 15-column summary and replay round-trip passed. Targeted tests cover three-axis displacement/overshoot, partial credit, stationary-hand score limits, and observed travel outside the calibration range. Four-view calibration and the final separated NOW/NEXT layout were rendered and visually inspected. The NOW illustration follows the live exercise phase.

The session metadata includes score weights, completion coverage, endpoint tolerances and planned challenge order (integer `exercise index * 2 + side`, left=0/right=1). The score is a game measure, not a clinical recovery percentage. Compare raw errors and movement measures across scoring-version changes.

Final mixed-mission APK: targeted checks passed; build succeeded with zero errors (`/tmp/emil-mixed-final-build.log`), installation succeeded, and Quest cold launch returned OK (601 ms). Process 10818 remained running. Wearer comfort and real-player scoring still require a headset trial.

Headset-only marker fix: replaced runtime primitive creation with reusable `Coach Hand Marker.prefab` to avoid stripped collider errors. Final replacement APK built with zero errors (`/tmp/emil-mixed-marker-build.log`), installed, and cold-launched successfully (598 ms, process 11972). The marker errors are absent from the new process log; the pre-existing Unity AssetPackManager startup message remains.

## October 28: finite approaching paths

`Exercise Path Stream.prefab` contains separate current/incoming line renderers and endpoint markers. Future samples are displayed at `mapped exercise position + calibrated forward * (seconds until arrival) * approachSpeed` (default 0.55 m/s). This retains actual exercise depth as well as approach motion. At arrival, the line point exactly coincides with the scored physical target's visual mapping. Consumed points are removed, so paths do not continue behind the player. The gap is 2.5 seconds by default; no countdown or ready-pose gate interrupts the mixed mission. Tracking loss pauses both arrival and scoring. Gaps are unscored and create timestamp gaps between recorded exercises.

The tutorial uses the existing full-body replay mannequin with mirrored position/orientation only in demonstration mode. Normal recorded replay remains unchanged. Only the CURRENT exercise card is displayed. Bullets and drones are hidden. Existing scoring thresholds, free three-axis control, saved raw measurements and replay are retained. Summary schema 4 renames `projectile_blocked` to `endpoint_reached`; the session identifies presentation `finite-3d-paths-mirror-coach-1` and the configured gap.

October 28 verification: 12 accelerated simulated repetitions scored 100, with six calibrations, matching CSV/TSV (2,448 rows, 40 columns), endpoint summary fields, and replay round-trip. Tests assert mirror left/right consistency, arrival alignment, spatial gap, and no mission Ready/countdown state. Rendered the single mirrored character, approaching paths, and current-only card at normal gameplay timing. Removed the old reactor from the line area after visual review. Quest APK build succeeded with zero errors (`/tmp/emil-path-build.log`). No headset was connected, so this version has not been installed or checked on the device.

## October 28: personal ranges and reach side view

The CURRENT card is centred below the play area. Serratus reach uses a side-on full-body tutorial and calibration view, with the active arm nearer the viewer; other exercises retain the front mirror. The live hand marker uses the same view transform. Normal recorded replay is unchanged. The demonstration is an example, not a required endpoint distance.

Every exercise and arm captures its own start position/rotation and comfortable endpoint. Both the scored target and the visible line use those captured poses, so smaller and larger ranges use the same path-generation code. Direction checks now scale with measured displacement instead of requiring fixed 8–10 cm components. Minimum endpoint separations remain 2 cm for scaption/rotation and 1.5 cm for reach, to reject near-identical captures; these are prototype measurement thresholds, not clinical limits. The forward starting height can be lower when that is the chosen exercise setup. A trainer should choose suitable exercise form and range; headset/controllers cannot establish whether elbow or scapular movement is correct.

Session calibration method `personal-range-stable-pose-2` saves `calibratedTravelMetres` per arm/exercise: straight-line controller displacement from start to endpoint, not joint range of motion or total curved path length. Compare this distance and raw errors alongside scores across days; a high score in a small captured range does not mean the same capacity as a high score in a larger range. Existing start/end poses and rotations remain saved.

## Adaptive coach (bounded-tempo-v1)
`AdaptiveExerciseCoach.cs` is a separate local rule-based controller, attached to the game prefab. It observes completed repetitions and keeps independent state for every exercise and arm. It is not an LLM, diagnostic tool, or learned clinical model. State resets each session; cross-day learning is not implemented.

Two consecutive repetitions below 65/100 or 65% travel coverage add 10% of baseline outward/return duration. Two consecutive repetitions at least 90/100 and 90% coverage subtract 5%. Duration is bounded to 85–150% of baseline. Only subsequent repetitions change. Hold/rest durations, calibration, repetitions and scoring thresholds stay unchanged. Runtime copies protect the profile assets. Disable `Adaptation Enabled` before starting a session for fixed timing; disabling mid-session freezes the current pace.

More than 10% tracking loss excludes a repetition from adaptation. Head displacement over 20 cm or rotation over 25 degrees prevents progression and resets evidence streaks; these are configurable-in-code prototype gates, not anatomical compensation detection. The headset cannot verify elbow, torso, or scapular form. A therapist should decide appropriate exercise ranges and pace limits.

Each saved session includes `adaptive-coach.json`, with one decision per completed repetition: arm, exercise, repetition, observed score/coverage, tracking loss, head movement, duration used, next duration scale, and reason. Baseline timing remains in `session.json`. Compare results with the actual tempo used as well as calibrated distance and raw errors; scores at different tempos are not equivalent. No network calls or credentials are required.

Press right-controller **A** (or **Space** in desktop simulation) to skip the animated exercise preview and the free-practice tutorial. Personal start/end calibration remains required. The skip method is public for a future pointer-based UI button.


## Adaptive range update (bounded-range-tempo-v2)
The coach now adjusts each exercise/arm's target excursion as well as tempo. After two difficult repetitions, excursion drops by 10 percentage points, bounded to 70–100% of the original calibrated path. After two strong repetitions, a reduced excursion increases by 5 points; restoring range takes priority over increasing speed. Progression beyond the calibrated maximum requires a new calibration. Extremely small calibrated movements are not shortened below the exercise's minimum tracking threshold.

The baseline calibration is immutable. Effective endpoints follow the original exercise curve, and endpoint orientation scales by the same fraction. Scored targets, incoming curves and replay target poses all use these effective endpoints. Changes occur only between attempts. Decision export now includes `rangeScaleUsed`, `nextRangeScale`, `calibratedTravelMetres` and `targetTravelMetres` (the latter two are start/end chord distances, not joint angles or arc lengths). Compare score together with actual target excursion and tempo. The same disabled/tracking/head-movement gates apply to both adaptations. This supersedes the earlier fixed-target description above.

## Main menu, saved player settings and immediate adaptation (latest)
This section supersedes the session-only and two-attempt policies above.

- Start loads saved head-relative/yaw-relative calibration for enabled exercise/arm pairs and places it relative to the current player location/facing. Missing calibrations are taught first. The tutorial-completed flag skips previews on later starts. Tutorial / Recalibrate explicitly repeats the enabled exercises and replaces their normal calibration and learned difficulty. In dev mode, recalibration is temporary.
- Settings offers independent left/right switches for each exercise, an exact total level length of 1–120 movements, and an Adjust page for each exercise. All-off disables Start; the selected total is distributed cyclically over enabled pairs, so a short level may not include every enabled pair.
- Normal-mode maximum speed increase is per exercise, 0–100% above its original speed (default 40%). Speed means inverse movement duration; 140% is 1.4 times original speed, not a 40% reduction in time. Hold/rest durations do not change.
- Every reliable completed attempt is evaluated. Scores at least 90 with at least 90% travel coverage increase speed by 5% of current speed and range by 2.5 percentage points, subject to caps. Scores below 65 or coverage below 65% slow speed by 10% and reduce range by 5 points. The middle band retains difficulty. Tracking and head-motion gates still hold adaptation. Range remains within 70–100% of calibration (or a higher floor for tiny calibrated movements). The next attempt for that same arm/exercise uses the update; attempts in progress do not change.
- `player-settings.json` in the app's persistent data folder contains normal per-arm presets, relative calibrations, tutorial completion, exercise switches, level length, speed caps, and separate dev overrides. Writes replace the file atomically. Normal learning is saved after each completed reliable repetition, not only at level end. Editor simulation uses `/tmp/emil-editor-player-settings.json`; automated tests use isolated files. This is a single local player profile, not a multi-patient database.
- Dev mode freezes automatic learning and uses per-arm speed (50–200%) and range (30–100% of calibration) overrides, ignoring normal speed caps. It never overwrites normal learned speed/range, calibration, or tutorial-completed state. Dev UI choices can be saved separately within the settings file. Raw dev sessions still export and are marked `developerMode` in session and decision data, so they can be filtered out of progress analysis.
- Menu interaction: point either controller and press trigger; hold trigger and drag sliders. Desktop simulation supports mouse click/drag. A / Space still skips previews. The menu is a reusable `Exercise Main Menu.prefab` built by `ExerciseMenuSetup`.
- Player and target circles now use half their previous alpha (0.325 and 0.125 respectively).


## Responsive adjustment and random order (responsive-range-tempo-v4)
The headset log showed a poor 34.6-point repetition being held unchanged solely because head yaw exceeded 25 degrees. Poor-performance easing now runs before the head-motion gate. Head movement still blocks harder targets, and unreliable tracking/dev mode still block automatic learning.

Each completed reliable attempt below 65 points or 65% coverage reduces speed by 15% and range by 10 percentage points. Below 45 points or 45% coverage, reductions are 20% speed and 15 range points. Strong attempts (90+ points and 90%+ coverage without the head-motion flag) increase speed by 10% and range by 5 points. Normal limits are now 50% of base speed and 40–100% of calibrated path excursion, subject to the exercise's minimum measurable travel and the user's upper speed cap. Changes apply only when a later attempt of that exercise/arm loads. Saved presets support the new lower range floor. The approach screen identifies the previous exercise/arm and whether its next attempt became easier, harder or unchanged; the current percentages refer to the incoming attempt. The movement card now uses calibrated, adaptively shortened target geometry instead of the original demonstration example.

Level order is a shuffled balanced multiset of enabled exercise/arm pairs. Adjacent repeats are allowed; there is no fixed cycle. Short levels randomly select which enabled pairs appear, and longer levels retain balanced counts. Actual order is exported in session.json. Tests now include deliberately poor simulated movements with a 30-degree head-turn flag, later successful attempts, verifying that the next matching attempt uses the saved decision, no changes during movement, and restored settings. Static tests replay the observed headset regression and verify randomized sequences/counts.

## Head-position proxy and reset (latest)
Rotation is now informational telemetry only: looking around never deducts score or blocks difficulty progression. The position proxy requires BOTH downward translation (8 cm deadzone, full severity at 20 cm) and translation opposite the active arm (10 cm deadzone, full severity at 25 cm), measured in the calibrated yaw frame. It must persist for more than 0.5 seconds. Severity is averaged over valid movement time; the score deduction is capped at 10 points out of 100. Pure sideways/downward movement, movement to the active side, small natural shifts, rest phases, and tracking-invalid time do not add a deduction. This heuristic is not measured torso or clinical compensation. Raw head poses/rotation remain available for review.

Mean positional severity above 0.2 prevents automatic increases, but poor attempts can still ease. The decision policy is `position-aware-range-tempo-v5`, score version `shield-position-penalty-4`. summary.csv/tsv now have 18 columns, adding `unpenalized_score`, `head_position_severity`, and `head_penalty_points` so score changes can be distinguished from movement error. Decision JSON also stores positional severity.

Settings > Reset Saved Settings opens a confirmation screen. Confirm resets calibration, learned difficulty, tutorial completion, exercise toggles, speed limits, level length and dev choices to defaults. Cancel changes nothing. Session exports and replays are never removed. No background game has been implemented in this update.

## Conduit repair

The convoy has been removed. The arena and menu style remain in `Conduit Repair.prefab`. The existing personal-range score now restores ship power after each exercise; low-quality repairs drain battery. Sessions continue through enabled random exercise batches until power is full or battery is empty. X is still an emergency stop. Tracking pauses do not drain battery.

Follow the dim coloured conduit with your repair tool. Your solid red/green centre has a white outline; the target is a hollow matching ring. Repaired sections briefly glow white and fade; missed sections dim amber. The endpoint node charges during the hold phase. The tool stays facing forward. Controller rotation is recorded but does not affect accuracy, endpoint/hold credit, rewards or adaptation; hand position, coverage and timing still count. Power and battery sit alongside the guide, and floor circuits illuminate as power is restored. Adaptive speed/range, calibration and movement recording are retained.

`conduit.json` records the outcome; `conduit.csv` and `conduit.tsv` contain per-movement results. `conduit-path.csv` and `.tsv` record time-binned path quality (not every raw tracking sample). Existing samples, summaries and replay remain available. TSV opens in separate columns regardless of comma/decimal locale. Previous convoy recordings are kept.


## Participant ID

Default awaiting team confirmation. The home menu shows an anonymous per-device ID such as `Participant P003`; write it on the VRNQ questionnaire. Confirming Reset Saved Settings moves to the next ID, while starting a session does not, so repeat sessions by one participant share an ID. To link a questionnaire to data, match the ID to `participantId` in `session.json` or `participant_id` in `outcome.json` inside the session folder. Summary and sample CSV files have no ID column and join through that folder.
