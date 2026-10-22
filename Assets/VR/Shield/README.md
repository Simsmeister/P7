# Shield exercise prototype — Emil

The active game in Emil is now Shield Exercise Game. The earlier Wand Path Prototype instance is disabled; its assets and old recordings remain available.

## Exercises

Based on the three supplied exercise illustrations:

1. **Scaption:** lift the arm outward in a plane slightly forward of directly sideways, then lower slowly. The illustration specifies approximately 30–45 degrees forward of the frontal plane and a 90–120-degree lift. This prototype does not enforce that elevation or measure a shoulder angle: the player captures their own comfortable prescribed endpoint. The path uses an estimated shoulder pivot.
2. **Serratus reach:** arm forward at shoulder height with elbow straight; reach a little farther forward through the shoulder blade, hold, then return. The game measures controller reach, not scapular protraction or serratus activity.
3. **External rotation:** elbow bent beside the ribs, rotate the forearm outward, hold, then return. The game guides the hand along an arc using an estimated elbow pivot. Elbow angle, elbow position and actual humeral rotation are not measured.

One arm at a time. Setup teaches and calibrates left then right for each exercise; the mission then mixes the three exercises and alternates arms. Three repetitions per arm, 18 total challenges. Simultaneous-arm harder levels are not implemented yet. Profiles are reusable ScriptableObject assets in this folder; each owns movement type, timings, repetitions and tolerances. Anatomy-dependent correctness requires additional validated tracking or trainer observation.

## Controls and flow (Reactor Rescue)

- **A:** begin / restart after results. This is the only button needed to start.
- Watch the coach, then copy its stationary starting posture. Four simultaneous views show front, back, left side and right side of the same pose. Use the named arm (your left/red or right/green); the white dot shows your measured hand in every view. The drawn body is illustrative.
- A glowing orb shows your hand beside the coach. Hold a broadly suitable posture still for 1.25 seconds to capture it automatically. Movement beyond 2.5 cm or 12 degrees resets the hold timer. Tracking loss resets capture progress.
- Copy the second pose within your comfortable prescribed range and hold still again. No trigger capture is needed. Direction checks reject obviously wrong endpoints; this is not an anatomical form assessment.
- The coach returns to the start. Bring your hand back near its captured starting position for the countdown. After all six calibrations, a nine-second unscored controls tutorial lets you try movement in all three axes. Every subsequent challenge waits for your starting pose for two seconds; it does not recalibrate.
- Animated NOW and NEXT movement cards remain visible during the mixed mission. Follow the blue target with your remote shield: 3 seconds outward, 1.5 seconds holding, 3 seconds returning, 1 second rest.
- Drones send slow energy pulses into the front play area. Blocking gives a flash and haptic/audio feedback. Complete the hold and return to earn reactor energy and game points. Consecutive completed repetitions increase the points multiplier, capped at five. Reactor segments represent the six exercise/arm blocks.
- **X:** stop and save available data. **B:** replay from welcome/results. Right trigger pauses replay; right stick scrubs; left stick rotates / changes distance.

## Front-view controls

The visible shields are remote controls, not located on your hands. A fixed offset brings each exercise's starting position into the forward view. From there, every metre of hand displacement produces the same displacement of the shield in the calibrated body frame, on all three axes, without clamping. Overshooting upwards, downwards, sideways or in depth remains visible. This means extreme movement can leave the forward view. The target uses exactly the same transform. A directional centimetre hint identifies the largest position error, including depth. Shield tilt still gives a bounded visual cue for pointing error. The play area stays anchored to calibration rather than following head movement.

Raw hand poses and physical exercise errors still determine scoring. The mapping does not validate an elbow or scapular movement and does not remove compensation from the data. Replay continues to show the recorded physical hand movement with shields at the hands, rather than reproduce the remote arcade display.

Only the active arm and headset must be tracked. Tracking loss pauses the exercise clock and score. It does not break a combo by itself.

## Scoring

Each repetition averages hand-position and shield-normal quality over time, using the lower quality term. Full credit within 4 cm and 12 degrees, falling to zero at 14 cm and 40 degrees. These are prototype game settings, not clinical thresholds.

Movement score is now partial credit: `100 × (0.7 × mean path quality + 0.3 × phase completion) × (0.25 + 0.75 × travel coverage)`. Phase completion averages start reached, block, fractional hold, and return reached. Travel coverage is the measured range of progress along the calibrated start–end direction, clamped to 0–1. A stationary hand is capped at 25 even if tolerances overlap. Missing one phase no longer makes all movement accuracy zero. The strict `complete_movement` flag remains separate, also requires at least 65% travel coverage, and controls reactor charge/combos. Partial movement earns base game points; completed repetitions earn combo bonuses.

Start/end tolerance is 30% of calibrated travel, clamped to 2.5–6 cm. Hold checks allow up to 20 degrees with the default profile. A block can be registered throughout the hold interval rather than on a single arrival frame. The full-credit path score remains 4 cm / 12 degrees. These are prototype game settings, not validated clinical thresholds. The score version changed, so do not compare old and new totals as clinical progress. Raw errors remain available for comparable analysis. Circular shield roll is not scored. Projectile interception uses the prescribed endpoint and orientation.

## Recording and export

Saved under the same headset Sessions folder as before. **Tools > Emil VR > Export Quest Recordings** copies the files over USB.

- `session.json`: shield schema/version, automatic calibration method, three-axis mapping version, input source, calibration frame, all completed endpoint calibrations and exercise settings. Pivot points are estimated, not anatomical measurements.
- `samples.csv` / `samples.tsv`: one row per hand per sample (up to 60 Hz). Includes exercise ID, repetition, phase, active side, validity/scorability, raw controller poses, active-hand target pose, headset pose, hand and shield-normal error, head displacement/rotation, and the projected shield/target positions. The shield-3 schema has 40 columns. Time within the exercise resets each repetition; timestamp_s is wall time from recording start. Blank fields mean unavailable/not applicable, not zero.
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
