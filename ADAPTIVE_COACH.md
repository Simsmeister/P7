# Neural adaptive coach

All adaptive logic lives in `Assets/VR/Runtime/AdaptiveExerciseCoach.cs`. No ML package, server or internet connection is needed. The existing prefab still references this same script.

## What runs after each movement

1. Read that movement's positional score and travel coverage.
2. Run the trained network: **2 inputs → 12 tanh neurons → 2 outputs**.
3. Interpret the outputs as a change in range fraction and a relative change in speed.
4. Apply tracking, head-displacement and configured difficulty limits.
5. Save the player's new level for that exercise and arm. Use it on the next matching movement, never halfway through the current one.

The network does not receive hand rotation or head rotation. Its inputs are score / 100 and coverage, each 0–1 (internally normalized to -1–1). A speed change of +0.05 means 5% faster than the current speed. A range change of +0.05 means five percentage points of calibrated range.

## Three different files

| File | Contains | Changes when |
|---|---|---|
| `Assets/VR/Training/coach-training.json` | Labelled examples: score, coverage, desired range/speed changes, train/validation split | You edit or replace the training examples |
| `Assets/Resources/AdaptiveCoach/base-model.json` | Network weights/biases, schema, model ID, seed, training counts and error metrics | You explicitly run the training command |
| `<app save folder>/player-settings-adaptive.json` | Current player's speed, range, observation count, mean score/coverage and last model ID, independently per exercise/arm | A valid normal-mode movement completes |

The Unity desktop prototype uses `/tmp/emil-editor-player-settings-adaptive.json`. The filename is derived from the settings path, so test sessions and separate player-settings files get separate personal files. On Quest it lives alongside `player-settings.json` in `Application.persistentDataPath`.

The trained model is bundled in the app and loaded through `Resources`. Play never writes its weights. Personal adaptation is **not online neural-weight training**: the fixed network guides changes to each player's separately saved level. Those changes alter the next challenge, whose measured performance feeds the network again. The mean measurements are diagnostics, not extra network inputs.

The app currently has one active participant per device. The existing RESET SAVED SETTINGS flow starts a new participant and clears the active personal adaptive file. This does not create a multi-user account picker. Recalibration resets only the recalibrated exercise/arm. Existing speed/range settings are imported when no personal file exists; settings retain a compatibility mirror for the UI.

## Train or retrain

In Unity choose **Tools → Emil VR → AI → Train Base Neural Model**.

The command reads `coach-training.json`, trains deterministically (seed 42, 1,200 shuffled passes, backpropagation), checks held-out error and writes `base-model.json` only if validation succeeds. Commit the script, dataset, model JSON and their Unity metadata together. Rebuild the APK to put a new base model on Quest. Player progress is not reset by training or model updates.

Each example uses this structure:

```json
{
  "score": 0.4,
  "coverage": 0.6,
  "rangeChange": -0.05,
  "speedChange": -0.10,
  "validation": false
}
```

Changes must fit the hard per-attempt limits: range ±0.10, speed ±0.15. Keep at least five examples with `validation: true`; these are evaluated but never used in gradient updates. For real participant data, split by participant/session rather than neighbouring frames so validation does not leak training observations. Raw exported movement samples are not training labels: someone must specify the desired adjustment.

## Starter model and limits

The supplied 441 examples are **synthetic**, approximating the previous ease/hold/progress behavior; 359 train and 82 validate. They use the lower of score and coverage: easing below .65, holding in the middle, progressing above .85. The runtime uses neural predictions, not these label-generation rules.

Starter normalized-output MSE: training 0.001614, held-out 0.002315. These measure fit to simulated examples, not treatment effectiveness or performance on real people. The model is a working trainable prototype, not a clinically validated policy.

Independent runtime guards remain:

- No learning when disabled, in developer mode, with invalid measurements, or when tracking loss exceeds 10% of the movement.
- No increase during the existing sustained head-displacement warning; easing is still allowed.
- Range never exceeds calibration or falls below the minimum useful movement range.
- Speed stays inside the player's configured cap and the existing slowest limit.
- Tiny predicted changes are ignored to avoid drift in the middle band.
- Missing/invalid model: retain current difficulty and log the problem, without silently training a replacement.
- Unreadable personal file: retain the settings mirror and log the problem.

Every decision in each session's `adaptive-coach.json` includes `policy: neural-range-tempo-v1`, the model ID, raw predictions, actual bounded next level and reason. Thus game points remain separate from the movement measurements driving adaptation.

## Verification performed

Unity compiled the trainer and runtime. `ExercisePathSetup.Verify` passed neural model/schema, prediction bounds, response direction, separate personal persistence/reload, dev-mode no-write, unreliable-data rejection and reset checks, plus the existing cap/dead-zone tests. `ShieldExerciseVerification.Run` passed the complete simulated game with 23 neural decisions: six easing attempts and seventeen increases, followed by replay and a restart using saved progress. No headset installation or clinical validation was performed.
