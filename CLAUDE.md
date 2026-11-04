# P7 — Movement Paths (VR rehabilitation game)

This is the code repository for a Danish P7 thesis project (Aalborg University Medialogy, Group 704, 7th semester). It is a VR rehabilitation game for Quest 3, built in Unity. An AI opponent throws objects at the player, who parries each one with their arms; the parry motion itself is the rehabilitation exercise rather than something layered on top of the game. Difficulty adapts in a loop: the player's movement is captured, compared against the expected exercise form, tracked over time into an internal player model, and that model sets the next challenge, which the opponent then throws. Each object is thrown from one of three heights relative to the player's body (low, medium, high), and that height selects which arm-parry exercise is performed (scaption, serratus reach, or external rotation). Height is not a difficulty tier; difficulty intensity is controlled separately by the adaptive loop. The target population is Danish adults aged 18 to 35 with higher functional demands, people returning to sport or other demanding physical activity. That range was narrowed from an original 18 to 55 after supervisor feedback that the wider span was too broad for one VR prototype.

In-game the project is currently called Reactor Rescue, and is in the process of being renamed to Movement Paths; see the changelog in Assets/VR/Shield/README.md for where that rename stands.

A separate Overleaf project holds the thesis paper and is not part of this repository. It currently has a complete Introduction, a Design and Implementation section written as plain declarative description with no citations, and a Previous Research section carrying the relevant VR and adaptive-rehabilitation theory (Doumas et al. 2025, Longo et al. 2023, Rizzato et al. 2023). Methods, Findings, Discussion, and Conclusion are still placeholders. Future sessions may be asked to keep code behavior and paper claims consistent with each other, so it is worth knowing this relationship exists even though the paper's content lives elsewhere.

## Repo and branch state

Trunk is `main`, tracking `origin/main`, head commit `dedbcc63` ("yes it does stuff"), working tree clean at last check. `origin/Emil` and `origin/Share-load` both point at the same commit, `ed4b7d22` ("The big weekly update"), one commit ahead of main. They are not divergent feature branches, just two push targets for the same work. That commit only touches content and data inside Assets/VR/Shield/ (prefabs, materials, UI, README expansion), no C# script changes.

## Where things live

The main scene is `Assets/Scenes/Emil.unity`, targeting Quest 3 through OpenXR. `SampleScene.unity` is unused Unity template boilerplate and can be ignored.

`Assets/VR/` holds the actual game:

- `Editor/` has the custom Unity menu tools: EmilVRBuild.cs, EmilVRSetup.cs, ShieldExerciseSetup.cs, ShieldCoachSetup.cs, WandPathPrototypeSetup.cs, and some verification and export tools.
- `Runtime/` has the gameplay scripts: EmilVRInput.cs, ShieldExerciseGame.cs, ShieldExerciseProfile.cs, ShieldArcadeFeedback.cs, ShieldCoachDisplay.cs, AdaptiveExerciseCoach.cs (the adaptive difficulty loop itself), WandPathGame.cs, WandSessionReplay.cs, PathTube.cs, and ControllerTrackingTrails.cs.
- `HandVisualizer/` is an imported Unity XR Hands sample, not project-specific.
- `Shield/` is the active exercise game, the Reactor Rescue / Movement Paths parry mechanic covering scaption, serratus reach, and external rotation, along with its scoring formulas and data export schema (currently `shield-4`). It has its own actively maintained README with a dated changelog. Read that README first for implementation detail; this file points to it rather than repeating it.
- `Prototype/` is an earlier, now-disabled "Wand Path" prototype, kept for reference. It had its own calibration, scoring, and CSV/replay export, and was superseded by Shield. It has its own README explaining why.

Outside Assets/VR/: `Assets/XR/` holds the Loaders and Settings for OpenXR and Quest 3, and `Assets/Prefabs/gymnasium/` holds the environment prefab. The root README.md is minimal ("# P7 / Seventh Semester Project") and should not be relied on for context; this file supersedes it.

For implementation-level detail (exercise definitions, scoring formulas, data export schema, control flow, changelog), read:

- `Assets/VR/Shield/README.md` for the active game.
- `Assets/VR/Prototype/README.md` for the superseded prototype.

## Opening the project

Open the repository root in Unity and load `Assets/Scenes/Emil.unity`. The build target is Quest 3 through OpenXR. No build or run instructions beyond that are confirmed, so don't assume a particular Unity version or a specific build pipeline without checking the Shield README or the Editor tools directly.
