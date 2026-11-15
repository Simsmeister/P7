"""Relabels Assets/VR/Training/coach-training.json (version 3) in place.

Run from the project root:  python3 Tools/coach_training_labels.py
Then in Unity: Tools > P7 > Train Coach Model.

The labels are synthetic and not clinically validated. Rules (2026-11-10, "v4 labels"):
- Increase: score >= 0.70, coverage >= 0.75 and error severity <= 0.25 make the next attempt harder.
  t = (score - 0.70) / 0.30; range +0.03..+0.12 and speed +0.03..+0.12 (both above the runtime dead zone).
  Coverage below 0.9 halves the increase. Before this, only score >= 0.9 increased, by +0.02..+0.05,
  so players who felt perfect (score 70-89) never got harder.
- Otherwise the easing labels already in the file are kept (0 or negative).
- Start follow: 0 under 2.5 cm, ramping to 0.6 at 6 cm, up to 0.8 when the hand error is also large.
  The start offset input (startOffset, 0-1 over 0-20 cm) does not change range or speed.
"""
import json
from pathlib import Path

PATH = Path("Assets/VR/Training/coach-training.json")


def ramp(a, b, v):
    return min(1.0, max(0.0, (v - a) / (b - a)))


def main():
    data = json.loads(PATH.read_text())
    assert data["version"] == 3, "expects the version-3 training set"
    for e in data["examples"]:
        s, c, err = e["score"], e["coverage"], e["errorSeverity"]
        if s >= 0.70 and c >= 0.75 and err <= 0.25:
            t = ramp(0.70, 1.0, s)
            step = 0.03 + 0.09 * t
            if c < 0.9:
                step *= 0.5
            e["rangeChange"] = round(step, 4)
            e["speedChange"] = round(step, 4)
        else:
            e["rangeChange"] = min(0.0, e["rangeChange"])
            e["speedChange"] = min(0.0, e["speedChange"])
        metres = e["startOffset"] * 0.20
        e["startFollow"] = round(ramp(0.025, 0.06, metres) * (0.6 + 0.2 * err), 4)
    data["provenance"] = (
        "Synthetic v4 labels (Tools/coach_training_labels.py). Range/speed: score >= 0.70 with coverage >= 0.75 and "
        "low hand error increase both by 3-12% per attempt (half below 0.9 coverage); poor attempts and large errors "
        "ease as before (up to -45% range, -35% speed). Start follow: 0 under 2.5 cm, 0.6 from 6 cm, up to 0.8 with "
        "large hand error; the start offset does not change range or speed. Not clinically validated.")
    PATH.write_text(json.dumps(data, indent=2))
    print("relabelled", len(data["examples"]), "examples")


if __name__ == "__main__":
    main()
