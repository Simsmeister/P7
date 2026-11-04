# P7: Quest 3 shoulder rehabilitation game (AAU Medialogy, Group 704)

## 1. What it is
A Just Dance-like VR game for Quest 3: the player follows a line with their hand through three shoulder exercises (Scaption, Serratus Reach, External Rotation), and an adaptive coach adjusts speed and range per exercise and arm. There is no throwing, no parrying and no opponent in the build, and none is planned. The hand is the Quest controller grip, since `ShieldExerciseGame.cs:111` forces controller mode. Code names are historical: "Emil" is the scene and tools, "shield" is the marker the player moves.

Research question: "How can an adaptive VR game be designed to support physiotherapists in providing an appropriate level of challenge while promoting engagement in shoulder rehabilitation exercises for adults aged 18-35 with higher functional demands"

Target group: Danish adults 18-35 with higher functional demands (returning to sport or demanding physical activity). The paper lives in a separate Overleaf project and still describes throw-and-parry while that is being fixed, so do not use it as the source of truth for the mechanic.

## 2. Run and build
Unity 6000.6.0f1, URP 17.6.0, OpenXR 1.18.0, XR Hands 1.9.0. The build tool builds only `Assets/Scenes/Emil.unity`; `SampleScene` is also listed in Build Settings but is template boilerplate.

Menu `Tools > Emil VR`: Switch to Quest Android, then Build Quest APK (writes `Builds/Emil-Quest3.apk`, development build), and Validate Quest Setup. README and editor paths assume a Mac: the docs say Mac and the code hard-codes `/tmp`. Editor Play Mode without a headset runs a labelled desktop simulation (Space = A, R = B, mouse on the menu).

## 3. Code map
Read PROJECT_MAP.md before exploring the code; open source files only when you need to change them. When structure changes (scripts, prefabs, wiring, schema, formulas), update the map in the same commit.

## 4. Traps
- `ShieldExerciseVerification.Run` and `VerifiedBuild` have no menu items (they need `-executeMethod`) and rebuild prefabs first, so they can overwrite manual prefab edits.
- Generated prefabs in general: `Build Conduit Repair` rewrites the Conduit, menu and marker prefabs. `Set Up Shield Exercises` rewrites the Left/Right/Target Shield prefabs back to old discs before its early return. Change the generator, or edit the prefab and stop running that tool.
- Conduit Repair is the active theme and supplies line feedback; extract it rather than delete it.
- Reactor Rescue (`arcade`) HUD is off, but its audio, points and combo logic still run.
- `WandSessionReplay` and `Session Replay.prefab` are live. `WandPathGame` and `WandPathPrototypeSetup.cs` are otherwise dead, but `ShieldExerciseSetup.Build` reads `WandPathGame`, `WandSessionReplay` calls it at compile time (lines 72, 86), and the live `Test Desktop Session` menu item sits in `WandPathPrototypeSetup.cs`.
- The `projectile` field in `ShieldExerciseGame` is forced inactive and is legacy, but the code dereferences it unguarded. `Energy Pulse.mat` is still the line material.
- `WandDataExport.cs` hard-codes the package ID (line 22), so a bundle rename breaks export.
- Editor tools look for `Emil VR Rig`, but the prefab and scene object are now `VR Rig`. `Configure Quest 3` would add a second rig; `Update Controller Hand Models` and `Set Up Tracking Trails` load a path that no longer exists.
- `Shield/README.md` is partly stale: it says rotation-based quality, a 20 degree hold check, shield-4 and exact session length. Code is position-only (`position-only-hand-5`), schema `shield-5`, and Conduit extends the playlist until win or loss. Trust the code.
- No `[Test]` methods, no asmdefs. Checks are static `Verify` methods run via `-executeMethod`. README reports no confirmed headset run for recent versions.
- Replay stops capturing at 18,000 frames, about 5 minutes at 60 Hz (`WandSessionReplay.cs:48`). Raw samples continue.

## 5. Status against the team's feedback spec
| Item | Status | Detail |
|---|---|---|
| Continuous line | partial | Finite ribbons with gaps, offset about 1.35 m in front of the player (`ExercisePathStream.cs:26-31`, `ShieldExerciseGame.cs:228`) |
| Theme "Beat Saber Lite" night | partial | Dark and cyan but sci-fi repair; no post-processing volume |
| Score | partial | Points and multiplier shown in the Conduit status line (`ConduitRepairGame` status text); Path match %, last move 0-100 |
| Multiplier on consecutive perfects | partial | Counts `complete` reps (not yet "perfect", open decision), up to x5, shown as text |
| Audio: completion sound | built | 880 Hz |
| Audio: in-tolerance tone, rising notes, multiplier sound, soft dampening off the line | missing | |
| Visual: line glow | partial | Graded by quality (`ConduitRepairGame.Glow`, amber at 0.4 to white at 0.9), fades about 1.2 s; not yet run in Unity on a headset |
| Visual: progressive fill | partial | |
| Visual: hand light trail | partial | 0.25 s `TrailRenderer` added to the markers at runtime by `ConduitRepairGame.UpdateTrails`, tinted by `Glow`; not yet run on a headset |
| Visual: completion effect | partial | Ring at endpoint, not tied to quality, may be hidden by `Show(false)` |
| Visual: multiplier indicator | missing | |
| Visual: milestone lighting | partial | Floor lights track cumulative power |
| Haptics: completion | partial | Fires on endpoint reached |
| Haptics: multiplier pattern, off switch | missing | |
| Rewards: God/Præcis labels | missing | |
| Rewards: personal progress | partial | Data saved, no in-game view |
| Rewards: cosmetic unlocks | missing | |
| Rewards: round summary | partial | Menu shows result, accuracy, points and best streak (`ExerciseGameMenu.Refresh`); no per-exercise breakdown |
| Rewards: positive ending | partial | Also a failure ending "REPAIR BATTERY EMPTY"; no planned pause |

## 6. Backlog, in priority order
- P1 Line: read as one continuous line, close gaps, progressive fill. Graded brightness and the hand trail are in, but need a headset look.
- P2 Rewards: base the multiplier on perfect reps (needs the "perfect" decision), God/Præcis labels, a visual multiplier indicator. Score, multiplier text and best streak are in.
- P3 Audio: in-tolerance tone, soft dampening, rising notes on streaks, multiplier sound.
- P4 Clinical: therapist-only limits (none now; the player can change the dev toggle and speed cap, reset and recalibrate). Session length and breaks (none now; roughly 47 minutes possible at target 120, an estimate).
- P5 Evaluation: participant ID in exports, VRNQ questionnaire outside the app (no in-app support), headset test.
- P6 Cleanup: one game name; consistent exercise labels (menu says FORWARD REACH / OUTWARD ROTATION, gameplay says SERRATUS REACH / EXTERNAL ROTATION); real bundle ID (currently template default `com.UnityTechnologies.com.unity.template.urpblank`) with the export tool updated; stale strings ("defend against a mixed sequence", "WATCH THE GHOST SHIELD"); inaccurate "Power target: N perfect repairs" text; README sync.
- Later, optional: haptic toggle and pattern, milestone lighting, cosmetic unlocks, in-game progress view.

## 7. Open team decisions: ask a human, do not assume
- Night "Beat Saber Lite" theme versus the Conduit Repair ship story.
- Line at the hand versus the current path about 1.35 m in front.
- Whether the failure ending stays in a rehab game.
- What "perfect" means as a score threshold.

## 8. Limits to state honestly
Compensation is only a head-movement heuristic. Elbow, scapula and torso are not measured, and the adaptive thresholds have no clinical validation. Everything in this file comes from static code reading on 2026-11-04, not from a Unity or headset run.

`origin/Emil` lacks this file; teammates should pull `Share-load`.
