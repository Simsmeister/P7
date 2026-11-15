## Progression fix (2026-11-10, after a headset test)
A tester got "perfect" repetitions without the game getting harder. The labels only taught increases at score >= 0.9 and coverage >= 0.9, by +2-5 %, and +2 % range fell inside the runtime dead zone; score 0.65-0.89 kept the level, and real people following a moving ring mostly land there. New labels (`Tools/coach_training_labels.py`): score >= 0.70, coverage >= 0.75 and error severity <= 0.25 increase range and speed by 3-12 % per attempt (half below 0.9 coverage). Easing is unchanged. Start follow is 0.6 from 6 cm (up to 0.8 with large hand error). Model `nn3-28ACEF866E89FF43`, training MSE 0.00096, held-out 0.00087.

The start offset now also uses where the hand waits in the last 0.8 s before the line arrives (within 30 cm of the start). During the repetition players follow the ring, so the in-repetition offset alone stayed under the dead zone.

The tutorial demo shows the mastered movement (the full automatic estimate, v3: scaption about 140 degrees, 18 cm reach, 75 degrees rotation) at normal speed. The practice line is the same; the reach (x0.9) sets the starting range and the path quality sets the starting speed (0.6-1).

## Start point from the network, larger estimate and tutorial (2026-11-10)
Players differ in height and arm length, so a fixed start point can sit too high or too low. After each scored repetition (and each tutorial practice repetition) the coach averages the hand's offset from the path start during the first 20% of the outward phase and the rest phase. The network (version 3) gets that offset as a fourth input, `startOffset01` (0-1 over 0-20 cm), and has a third output, start follow (0-1): the fraction of the measured offset the whole path (start, end, pivot) moves. The direction is the measured one. Hard guards around it: at most 5 cm per step and 20 cm in total, offsets under 2.5 cm ignored, at least 0.3 s of samples, the same reliability guards as range/speed, no change in developer mode or without a valid model. The shift is saved per exercise and arm (`startShift` in the personal file) and logged per decision (`measuredStartOffset`, `startOffsetSeverity`, `predictedStartFollow`, `startShiftUsed`, `nextStartShift`). Recalibration clears it.

Start-follow labels are synthetic (see the training file's provenance): 0 under 2.5 cm, ramping to 0.5 at 6 cm, up to 0.7 when the hand error is also large. Each of the 2,205 existing examples got a seeded random start offset; range and speed labels do not depend on it. Model `nn3-E0B86DC292408867`: training MSE 0.00121, held-out 0.00125 (three outputs). The sections below describe version 2; apart from the extra input and output, they still apply.

The automatic estimate is now larger (scaption to about 120 degrees, 15 cm reach, 60 degrees rotation), scaled by eye height (0.8-1.2 of a 1.64 m eye height), and starts at 35% range. The network still grows or shrinks the range from there, so it should settle at what each player manages.

## Automatic start (2026-11-09)
START no longer requires manual calibration. New arms/exercises get an estimated example envelope and begin at 20% of that range and 65% of normal speed. The existing trained network then adjusts after completed attempts. Automatic ranges can ease to 15%, but never below the exercise's minimum useful travel; manual calibrated ranges keep the existing 40% floor.

During the incoming path, a suitable stable hand position can automatically anchor the starting pose (250 ms, tracked, posture check, no more than 35 cm from the estimate). This adds no extra screen or mandatory wait. If it cannot find a suitable pose, gameplay continues and can retry on a later approach. Once scoring starts, the path stays fixed. Estimated envelopes are tagged in saved settings/recordings; they must not be presented as measured personal maximum reach.

Existing saved manual calibration and progress are retained. OPTIONAL CALIBRATION remains a one-click manual alternative. The first short gameplay introduction can be skipped. This mechanism finds an in-game challenge level, not a measured anatomical or clinical limit.

# Neural adaptive coach

All adaptive logic lives in `Assets/VR/Runtime/AdaptiveCoach.cs`. No ML package, server or internet connection is needed. 

## What runs after each movement

1. Read that movement's positional score, travel coverage and mean tracked hand-to-target distance.
2. Run the trained network: **3 inputs → 16 tanh neurons → 2 outputs**.
3. Interpret the outputs as a change in range fraction and a relative change in speed.
4. Apply tracking, head-displacement and configured difficulty limits.
5. Save the player's new level for that exercise and arm. Use it on the next matching movement, never halfway through the current one.

The network does not receive hand rotation or head rotation. Its inputs are score / 100, coverage and sustained error severity, each 0–1 (internally normalized to -1–1). Error severity is the smaller of the absolute-error ramp (4–18 cm) and the relative-error ramp (15–60% of target travel, with an 8 cm denominator floor). Small positional noise therefore stays at zero; a brief spike is diluted by the mean across tracked frames. A speed change of +0.05 means 5% faster than the current speed. A range change of +0.05 means five percentage points of calibrated range.

## Three different files

| File | Contains | Changes when |
|---|---|---|
| `Assets/VR/Training/coach-training.json` | Labelled examples: score, coverage, desired range/speed changes, train/validation split | You edit or replace the training examples |
| `Assets/Resources/AdaptiveCoach/base-model.json` | Network weights/biases, schema, model ID, seed, training counts and error metrics | You explicitly run the training command |
| `<app save folder>/player-settings-adaptive.json` | Current player's speed, range, observation count, mean score/coverage and last model ID, independently per exercise/arm | A valid normal-mode movement completes |

The Unity desktop prototype uses `/tmp/p7-editor-player-settings-adaptive.json`. The filename is derived from the settings path, so test sessions and separate player-settings files get separate personal files. On Quest it lives alongside `player-settings.json` in `Application.persistentDataPath`.

The trained model is bundled in the app and loaded through `Resources`. Play never writes its weights. Personal adaptation is **not online neural-weight training**: the fixed network guides changes to each player's separately saved level. Those changes alter the next challenge, whose measured performance feeds the network again. The stored historical mean measurements are diagnostics. The current attempt’s mean hand error is a network input.

The app currently has one active participant per device. The existing RESET SAVED SETTINGS flow starts a new participant and clears the active personal adaptive file. This does not create a multi-user account picker. Recalibration resets only the recalibrated exercise/arm. Existing speed/range settings are imported when no personal file exists; settings retain a compatibility mirror for the UI.

## Train or retrain

In Unity choose **Tools → P7 → Train Coach Model**.

The command reads `coach-training.json`, trains deterministically (seed 42, 1,800 shuffled passes, backpropagation), checks held-out error and writes `base-model.json` only if validation succeeds. Commit the script, dataset, model JSON and their Unity metadata together. Rebuild the APK to put a new base model on Quest. Player progress is not reset by training or model updates.

Each example uses this structure:

```json
{
  "score": 0.4,
  "coverage": 0.6,
  "errorSeverity": 0.5,
  "rangeChange": -0.05,
  "speedChange": -0.10,
  "validation": false
}
```

Changes must fit the hard per-attempt limits: range ±0.45, speed ±0.35. Keep at least five examples with `validation: true`; these are evaluated but never used in gradient updates. For real participant data, split by participant/session rather than neighbouring frames so validation does not leak training observations. Raw exported movement samples are not training labels: someone must specify the desired adjustment.

## Starter model and limits

The supplied 2,205 version-2 examples are **synthetic**: 1,792 train and 413 validate. They cover score, coverage and five positional-error severities. Training labels teach modest easing for small errors and up to 45 percentage points less range / 35% lower speed for large sustained errors during poor attempts. Successful attempts still teach small increases. Positive examples receive 6× gradient weight because strong successes are rare in the full grid. Runtime uses the network predictions, not these label-generation rules.

Starter normalized-output MSE: training 0.001275, held-out 0.001093. These measure fit to simulated examples, not treatment effectiveness or performance on real people. The model is a working trainable prototype, not a clinically validated policy.

Independent runtime guards remain:

- No learning when disabled, in developer mode, with invalid measurements, or when tracking loss exceeds 10% of the movement.
- No increase during the existing sustained head-displacement warning; easing is still allowed.
- Range never exceeds calibration or falls below the minimum useful movement range.
- Speed stays inside the player's configured cap and the existing slowest limit.
- Tiny predicted changes (<.02 range / <.0225 speed) are ignored to avoid drift in the middle band.
- Missing/invalid model: retain current difficulty and log the problem, without silently training a replacement.
- Unreadable personal file: retain the settings mirror and log the problem.

Every decision in each session's `adaptive-coach.json` includes `policy: neural-range-tempo-v2`, the model ID, mean hand error, normalized error severity, raw predictions, actual bounded next level and reason. Thus game points remain separate from the movement measurements driving adaptation.

## Verification performed

Unity compiled the trainer and runtime. `P7Tests.RunChecks` (Tools → P7 → Run Checks) covers neural model/schema, prediction bounds, response direction, separate personal persistence/reload, dev-mode no-write, unreliable-data rejection and reset checks, plus the existing cap/dead-zone tests. `P7PlayModeTest` (Tools → P7 → Run Play Mode Test) plays the complete simulated game with neural decisions adapting both down and up, followed by replay and a restart using saved progress. No headset installation or clinical validation was performed.

## 2026-11-09 verification
The three-input model passed direction, bounds, persistence and distance-sensitivity checks. With score .30 and coverage .40, severity 0 predicts about -.100 range / -.139 speed; severity 1 predicts about -.442 range / -.341 speed before limits. One-click Settings and Recalibrate passed in the full simulated session, as did stable in-progress targets, next-attempt adaptation, replay and saved-settings restart. These examples are model tests, not a promise that every starting-position/calibration problem can be solved by shrinking range.
