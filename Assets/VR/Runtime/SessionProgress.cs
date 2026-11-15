using System;
using UnityEngine;

namespace P7
{
    // ENERGY and FLOW for one session. Saved as conduit.json, so the field names stay as they were.
    // In the code "power" is ENERGY and "battery" is FLOW.
    [Serializable]
    public class SessionProgress
    {
        public string version = "conduit-2";
        public string outcome = "active";
        public float battery = 100;
        public float power;
        public int movements;
        public int target = 18;

        public bool Finished => outcome != "active";
        public bool GoalReached => outcome == "victory" || outcome == "session_limit";

        // Called once per repetition with its score (0-1).
        public void Resolve(float accuracy, bool sessionLimitReached = false)
        {
            if (Finished)
                return;

            float q = Mathf.Clamp01(accuracy);
            movements++;

            power = Mathf.Min(1, power + Mathf.InverseLerp(.25f, 1, q) / Mathf.Max(1, target));

            float drain = Mathf.Max(0, .6f - q) * 16;
            float bonus = q >= .85f ? 2 : 0;
            battery = Mathf.Clamp(battery - drain + bonus, 0, 100);

            if (battery <= 0)
                outcome = "defeat";
            else if (power >= .99999f)
                outcome = "victory";
            else if (sessionLimitReached)
                outcome = "session_limit";
        }
    }
}
