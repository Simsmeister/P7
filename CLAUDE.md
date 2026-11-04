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
| Continuous line | partial | Finite ribbons about 1.35 m in front, now joined by a dim runtime bridge from current end to next start (`ExercisePathStream` `bridge`); the 2.5 s gap still exists in time; not yet seen on a headset |
| Theme "Beat Saber Lite" night | partial | Chosen 2026-11-04. Ship and repair text replaced with ENERGY / FLOW wording (runtime and menu prefab); visuals still dark and cyan; no post-processing volume (bloom) yet |
| Score | partial | Points and multiplier shown in the Conduit status line (`ConduitRepairGame` status text); Path match %, last move 0-100 |
| Multiplier on consecutive perfects | built | Counts perfect reps (`ShieldArcadeFeedback.Perfect`), up to x5, shown as text; no visual indicator yet |
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
| Rewards: God/Præcis labels | built | PRÆCIS = perfect, GOD = complete; shown in the Conduit status line during the gap before the next path |
| Rewards: personal progress | partial | Data saved, no in-game view |
| Rewards: cosmetic unlocks | missing | |
| Rewards: round summary | partial | Menu shows result, accuracy, points and best streak (`ExerciseGameMenu.Refresh`); no per-exercise breakdown |
| Rewards: positive ending | partial | "FULL ENERGY - GREAT SESSION"; failure ending still exists as "OUT OF FLOW - TRY AGAIN"; no planned pause |

## 6. Backlog, in priority order
- P1 Line: progressive fill. Graded brightness, hand trail and the bridge between paths are in, but need a headset look.
- P2 Rewards: a visual multiplier indicator. Score, multiplier on perfect reps, PRÆCIS/GOD labels and best streak are in.
- P3 Audio: in-tolerance tone, soft dampening, rising notes on streaks, multiplier sound.
- P4 Clinical: therapist-only limits (none now; the player can change the dev toggle and speed cap, reset and recalibrate). Session length and breaks (none now; roughly 47 minutes possible at target 120, an estimate).
- P5 Evaluation: participant ID in exports, VRNQ questionnaire outside the app (no in-app support), headset test.
- P6 Cleanup: one game name (working title SHOULDER FLOW in the menu, ProjectSettings product name unchanged); consistent exercise labels (menu says FORWARD REACH / OUTWARD ROTATION, gameplay says SERRATUS REACH / EXTERNAL ROTATION); real bundle ID (currently template default `com.UnityTechnologies.com.unity.template.urpblank`) with the export tool updated; stale strings ("defend against a mixed sequence", "WATCH THE GHOST SHIELD"); README sync. ("Power target" text is replaced.)
- Later, optional: haptic toggle and pattern, milestone lighting, cosmetic unlocks, in-game progress view.

## 7. Open team decisions: ask a human, do not assume
- Whether the failure ending stays in a rehab game.

Decided 2026-11-04: night "Beat Saber Lite" theme, no ship story. Player-facing text says ENERGY (was ship power) and FLOW (was battery). Code names, export files and CSV columns keep `conduit`, `ship_power`, `battery` so old data stays comparable. Menu title "SHOULDER FLOW" is a working title until the team picks one game name (P6).

Decided 2026-11-04: the line stays about 1.35 m in front of the player (no move to the hand).

Decided 2026-11-04: perfect = `complete`, score >= 90 and coverage >= 90 % (`ShieldArcadeFeedback.Perfect`), the same band the adaptive coach treats as strong.

## 8. Limits to state honestly
Compensation is only a head-movement heuristic. Elbow, scapula and torso are not measured, and the adaptive thresholds have no clinical validation. Sections 1-6 come from static code reading on 2026-11-04; section 9 adds what was compiled and checked in batch mode. Nothing was run on a headset.

`origin/Emil` lacks this file; teammates should pull `Share-load`.

## 9. Handoff: work done on 2026-11-04 and where to pick up
Commits on `Share-load` after the original handoff (`f9e970ad`), all pushed:
- Graded line glow (`ConduitRepairGame.Glow`) and a 0.25 s hand trail (`UpdateTrails`), both added at runtime.
- Points, x1-x5 multiplier and best streak shown in the status line and end-of-session menu.
- "Perfect" defined (`ShieldArcadeFeedback.Perfect`); multiplier counts perfect reps; PRÆCIS / GOD label shown in the gap before the next path.
- Dim bridge joining the current path to the next (`ExercisePathStream`, runtime `Path Bridge`).
- Night theme wording: ENERGY / FLOW instead of ship power / battery; menu prefab regenerated (text only).

How it was checked: Unity 6000.6.0f1 batch mode on Windows compiles with 0 errors, and `ConduitSetup.Verify` and `ExercisePathSetup.Verify` pass. The Play Mode test `ShieldExerciseVerification.Run` was not run (it rebuilds prefabs first). Nothing was looked at in Play Mode or on a headset, so the first job is a headset look at glow, trail, bridge, and whether the longer status line fits.

Batch check command (Windows, Unity closed):
`"C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath <repo> -executeMethod ConduitSetup.Verify -logFile <log>`

Suggested next steps, in order: headset look; improve the failure ending (7a); bloom / post-processing volume for the night look; visual multiplier indicator; audio (P3); therapist limits and breaks (P4); participant ID (P5).

Uncommitted local noise: `UniversalRenderPipelineGlobalSettings.asset`, `EditorBuildSettings.asset`, `ProjectAuditorSettings.asset`, `ShaderGraphSettings.asset` were changed by Unity before this work and are deliberately not committed.
