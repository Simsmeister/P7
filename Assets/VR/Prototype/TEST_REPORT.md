# Prototype verification

- Unity editor compilation passed.
- Scoring tests passed: perfect pose = 100%; wrong hand position or pointing direction = zero even with a perfect tip; increasing error reduces score.
- Accelerated three-challenge simulation completed and displayed final results.
- CSV verification passed: 720 per-hand rows, 32 columns, valid tracking flags, all three challenge IDs, no malformed rows, and zero pose/tip errors for perfect simulated following.
- Six per-hand/challenge summaries all reported 100 and marked the session complete.
- Replay verification passed: 360 frames, monotonic timestamps, matching sample count.
- Saved JSON replay loaded in a fresh editor Play session; the mannequin and target markers were visually inspected.
- Test recordings are synthetic, stored separately under /tmp/emil-wand-prototype-test, and labelled in metadata. They are not participant measurements.

Physical calibration comfort, controller alignment, perceived UI scale, and user execution of the exercise still require a person wearing the headset. The body/elbow/leg reconstruction is schematic and is not an anatomical tracking validation.

## Quest build and unattended startup — 2026-10-22

- Built a fresh copy of the saved Assets/Packages/ProjectSettings in `/tmp/emil-quest-verified-build` using Unity 6000.6.0f1. Final result: `EMIL_VR_BUILD Succeeded errors=0`. Copied the verified APK to `Builds/Emil-Quest3.apk`.
- Installed successfully on the connected Quest 3; cold activity launch returned status OK. Process 5361 remained alive after startup. OpenXR display/input providers initialized. No scene-corruption crash or prototype managed exception was observed during this unattended check.
- Headset was not worn and did not acquire active VR focus during the check; this does not verify in-headset scene interaction, calibration, rendering, or a real participant session.
- Unity emits an existing Google Play AssetPackManager class-not-found startup message; it did not terminate the process during this check.
- Earlier builds were rejected after duplicate type registrations and a corrupted serialized scene. Duplicate `TypeDb-All 2.json` cache files and `RuntimeInitializeOnLoads 2.json` were moved out of the original Library/BuildPlayerData to `/tmp/emil-duplicate-build-cache`. The successful APK was rebuilt independently from source assets, without those caches.
- Build evidence: `/tmp/emil-quest-build.log`; device startup evidence: `/tmp/emil-quest-device-test.log`.

## Version 2 changes — 2026-10-22

- Original recordings exported unchanged to Recordings. Median hand errors were 2.26 and 2.43 cm; median tip errors were 1.49 and 1.42 cm. The old multiplicative score over-penalized small correlated errors. New explicit tolerance bands yielded retrospective sample-mean scores 99.39 and 98.81 on the old short-wand data (not new participant results).
- Scoring boundary tests passed, including full-credit tolerance bands, wrong-hand-position / tip / pointing zero gates, and monotonic error response.
- Isolated Unity Play Mode test passed: 18-second demonstration, countdown, three accelerated rounds, score 100, saved replay reload, recorded 60 cm wand length and matching replay tip geometry. 1,800 CSV rows matched 900 replay frames; six challenge/side summaries were complete with zero errors and score 100.
- Unity's editor SearchDatabase emitted an unrelated indexing ArgumentOutOfRangeException in the batch test; the gameplay test still completed.
- Replay left stick rotates the model and adjusts distance; physical movement remains possible. Actual headset use of these controls and comfort with 60 cm wands require a wearer test.

- Updated Quest APK built successfully with zero build errors, installed with existing app data preserved, and cold-launched successfully. Process 15754 remained alive; startup log contained only the previously noted AssetPackManager message in the inspected Unity/AndroidRuntime error channels. Build log: /tmp/emil-v2-build.log.
