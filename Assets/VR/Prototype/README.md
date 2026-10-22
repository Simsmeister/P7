> Emil now uses the shield exercise game. See ../Shield/README.md. This document describes the preserved, disabled wand prototype and its earlier recordings.

# Wand Path prototype — Emil

This is a movement-game prototype, not a validated rehabilitation assessment. Scoring thresholds and the schematic body proportions are prototype values. No muscle activity, shoulder/elbow angle, or torso compensation is measured.

## Play on Quest

1. Hold controllers apart and comfortably in front of you, pointing both wands forward.
2. Hold BOTH index triggers for one second to capture that starting pose. Hands must be in front of the headset; calibration rejects hands down at the sides or wands pointing away.
3. Watch the 18-second demonstration of all three movements, then a three-second countdown. Follow the moving paths. Left = red; right = green.
4. Match the translucent ghost HANDLES and their pointing direction as well as putting each wand tip in the coloured ring. The approaching tube previews the next three seconds.
5. Three 20-second rounds: together, alternating, and smaller controlled movements. Amplitude is 10 cm above/below the calibrated pose, reduced to 65% in the final round.
6. A results screen shows total and challenge scores. Right A recalibrates for a new session. Right B opens replay.

Tracking loss pauses challenge progress and scoring. Head displacement/rotation is displayed as a warning and recorded, not scored as proven torso compensation. This prototype teaches whole-arm movement with a soft elbow bend, not isolated elbow flexion. The demo shows intended hand motion; its inferred elbows are not an anatomical prescription. The exercise remains anchored to the calibration reference; it does not follow head movement. Reset/recalibrate after recentering. The old tracking trails are disabled in this scene.

## Replay

Press right B after a session, or before a new session to load the latest saved replay. B returns to the game. Right index trigger toggles pause/play. Move the right thumbstick left/right to scrub. The LEFT thumbstick rotates the mannequin (left/right) and brings it closer or farther away (up/down). You can also physically move around it within your play area.

The mannequin plays the RECORDED headset and controller poses with the same wands (using the length stored in that session). Translucent spheres show target hand positions, target wand tips and the neutral head position. Yellow spheres show SCHEMATIC elbow guides.

IMPORTANT: Elbows, shoulders, torso and legs are an estimated full-body mannequin derived from sparse headset/controller input and generic limb lengths. They are not recorded anatomy and do not establish whether the exercise was performed with the correct elbow/shoulder/trunk movement. The mannequin is shown at 65% scale in front of the viewer. Gaps in valid tracking hide the affected recorded devices/arm. Body estimates are not used for scoring.

## Saved files

Each headset session gets a timestamped local directory under:

`/sdcard/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/Sessions/`

- `session.json`: calibration frame, starting poses, tolerances, target amplitude, duration, source and scoring/schema versions.
- `samples.csv`: timestamped left/right controller poses, target poses, headset pose, tracking/scorable flags, hand/tip/pointing errors, head displacement and rotation. Distances are metres, time seconds, angles degrees, rotations quaternions. Poses are world coordinates; use the recorded calibration frame to compare sessions.
- `summary.csv`: per-hand, per-challenge score, mean errors, valid duration and session tracking-pause duration. `complete=0` marks interrupted sessions.
- `replay.json`: time-ordered recorded/target poses with validity flags and calibration reference. This supports replay after reopening the app.

Sampling aims for up to 60 Hz with actual timestamps; it does not fabricate missing intermediate samples. Invalid controller/head poses are left blank in CSV. Samples continue while tracking is lost, but the exercise clock and score pause. CSV is flushed approximately once a second. Summary and replay are saved on completion, interruption, or leaving the game normally; abrupt process termination can leave a partial CSV without a finalized replay. All files stay on the headset; there is no cloud upload or participant identity collection.

Version 2 awards full credit within hand error 4 cm, tip error 6.5 cm, and pointing error 12 degrees. Each term then falls linearly to zero at 12 cm, 16 cm, and 35 degrees respectively. The lowest term determines the score; correlated errors are not multiplied. Wand length is now 60 cm. The visible path/ring radius is 6.5 cm, matching the full-credit tip zone. Roll around the wand axis is not scored. Results are prototype game scores, not clinical recovery measurements. Device-reported tracking can include inferred positions outside camera visibility.

## Assets and testing

`Wand Path Prototype.prefab` holds the game, paths, wands, target rings, ghost wands and score panel. `Session Replay.prefab` holds the replay mannequin and target markers. Emil scene instances supply the XR camera and tracking-space references. The trail diagnostic remains available as a separate disabled prefab.

Without XR hardware, editor Play Mode displays an explicitly labelled desktop simulation. SPACE starts it, R opens replay. It does not write participant recordings. Tools > Emil VR > Test Desktop Session runs accelerated simulated rounds and writes clearly labelled synthetic test files only to `/tmp/emil-wand-prototype-test`. Tools > Emil VR > Test Prototype Replay opens that replay. Tools > Emil VR > Verify Wand Scoring checks perfect matching and the position/rotation anti-shortcut gates.

Build with Tools > Emil VR > Build Quest APK, or use Unity Build And Run for the connected headset. A clean-cache rebuild is available under Tools > Emil VR > Clean Build Quest APK. Keep generated Library files out of file-sync conflict resolution: duplicate type database JSON files can corrupt player builds.

To review an exported session on the Mac: enter Play Mode in Emil, then choose Tools > Emil VR > Load Replay File in Play Mode and select its `replay.json`. The viewer reconstructs the recording; this does not require a connected headset. The synthetic recording test and replay disk-load check are separate from physical user testing.

## Export over USB

Connect the Quest by USB with developer mode enabled and accept USB debugging in the headset. In Unity choose **Tools > Emil VR > Export Quest Recordings**, then select a destination folder. It copies the complete Sessions folder and leaves the headset originals intact. Open each session's summary.csv in a spreadsheet for challenge scores, or samples.csv for timestamped movement/error graphs. Keep session.json with the CSVs: it specifies calibration, units, wand length, score version, and tolerance bands. Load replay.json with the existing replay menu in Play Mode.

Your original two sessions have already been copied into the project's Recordings folder. scoring-comparison.json contains a separately labelled retrospective check of the new scoring bands on the original 18 cm wand data. It does not overwrite the original scores or predict performance with the new 60 cm wands. Do not treat game scores across different scoring versions, wand lengths, calibration poses, or exercise settings as directly comparable recovery measurements. Use matching exercise configurations and inspect raw errors as well.
