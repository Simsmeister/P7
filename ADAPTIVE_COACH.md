## Automatic start (2026-11-09)
START no longer requires manual calibration. New arms/exercises get an estimated example envelope and begin at 20% of that range and 65% of normal speed. The existing trained network then adjusts after completed attempts. Automatic ranges can ease to 15%, but never below the exercise's minimum useful travel; manual calibrated ranges keep the existing 40% floor.

During the incoming path, a suitable stable hand position can automatically anchor the starting pose (250 ms, tracked, posture check, no more than 35 cm from the estimate). This adds no extra screen or mandatory wait. If it cannot find a suitable pose, gameplay continues and can retry on a later approach. Once scoring starts, the path stays fixed. Estimated envelopes are tagged in saved settings/recordings; they must not be presented as measured personal maximum reach.

Existing saved manual calibration and progress are retained. OPTIONAL CALIBRATION remains a one-click manual alternative. The first short gameplay introduction can be skipped. This mechanism finds an in-game challenge level, not a measured anatomical or clinical limit.

# Neural adaptive coach

All adaptive logic lives in `Assets/VR/Runtime/AdaptiveExerciseCoach.cs`. No ML package, server or internet connection is needed. The existing prefab still references this same script.

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

The Unity desktop prototype uses `/tmp/emil-editor-player-settings-adaptive.json`. The filename is derived from the settings path, so test sessions and separate player-settings files get separate personal files. On Quest it lives alongside `player-settings.json` in `Application.persistentDataPath`.

The trained model is bundled in the app and loaded through `Resources`. Play never writes its weights. Personal adaptation is **not online neural-weight training**: the fixed network guides changes to each player's separately saved level. Those changes alter the next challenge, whose measured performance feeds the network again. The stored historical mean measurements are diagnostics. The current attempt’s mean hand error is a network input.

The app currently has one active participant per device. The existing RESET SAVED SETTINGS flow starts a new participant and clears the active personal adaptive file. This does not create a multi-user account picker. Recalibration resets only the recalibrated exercise/arm. Existing speed/range settings are imported when no personal file exists; settings retain a compatibility mirror for the UI.

## Train or retrain

In Unity choose **Tools → Emil VR → AI → Train Base Neural Model**.

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

Unity compiled the trainer and runtime. `ExercisePathSetup.Verify` passed neural model/schema, prediction bounds, response direction, separate personal persistence/reload, dev-mode no-write, unreliable-data rejection and reset checks, plus the existing cap/dead-zone tests. `ShieldExerciseVerification.Run` passed the complete simulated game with neural decisions adapting both down and up, followed by replay and a restart using saved progress. No headset installation or clinical validation was performed.

## 2026-11-09 verification
The three-input model passed direction, bounds, persistence and distance-sensitivity checks. With score .30 and coverage .40, severity 0 predicts about -.100 range / -.139 speed; severity 1 predicts about -.442 range / -.341 speed before limits. One-click Settings and Recalibrate passed in the full simulated session, as did stable in-progress targets, next-attempt adaptation, replay and saved-settings restart. These examples are model tests, not a promise that every starting-position/calibration problem can be solved by shrinking range.
