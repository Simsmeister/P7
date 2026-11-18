using System.Collections.Generic;
using UnityEngine;

namespace P7
{
    // Finding each player's range: either a saved calibration, an automatic estimate,
    // or a manual capture where the player copies the demo and holds the start and end pose.
    public partial class ExerciseGame
    {
        const float CaptureHoldSeconds = 1.25f;

        readonly List<int> calibrationQueue = new List<int>();
        int calibrationIndex;
        ReplayData demonstration;

        // Manual pose capture: the hand has to stay within 2.5 cm for a while.
        Vector3 stablePosition, positionSum;
        int stableSamples;
        bool holdingStill;
        float holdTime, captureWait;
        string captureMessage = "";

        // Automatic start: the estimate is moved once to where the player actually holds their hand.
        Vector3 anchorCandidate;
        float anchorStableTime;
        bool hasAnchorCandidate;

        void PrepareCalibration(int exerciseIndex, int side, bool recalibrate)
        {
            var profile = exercises[exerciseIndex];
            var arm = Settings.Arm(profile.exerciseId, side);
            if (!arm.enabled)
                return;

            if (recalibrate)
            {
                calibrationQueue.Add(exerciseIndex * 2 + side);
                return;
            }

            // An automatic range from an older estimate version is replaced by the current, larger one.
            bool outdatedEstimate = arm.calibration != null && arm.calibration.automatic
                && arm.calibration.estimateVersion < AutomaticEstimateVersion;
            if (arm.calibration != null && arm.calibration.IsUsableFor(profile) && !outdatedEstimate)
            {
                calibrations.Add(arm.calibration.ToWorld(origin, frame));
                return;
            }

            // No saved range yet: start gently on an estimate scaled to the player's eye height,
            // at AutomaticStartRange of that range and AutomaticStartSpeed. The coach then adjusts both.
            var estimate = AutomaticEstimate(profile, side, origin, frame, bodyScale);
            calibrations.Add(estimate);
            if (!Settings.devMode)
            {
                arm.automaticRange = true;
                arm.speed = AutomaticStartSpeed;
                arm.range = AutomaticStartRange;
                arm.calibration = estimate.ToLocal(origin, frame);
                adaptiveCoach.Recalibrated(profile.exerciseId, side, arm.speed, arm.range);
            }
        }

        // The automatic estimate is the mastered movement: range 1 is the goal, the coach starts lower and works up.
        // Version 4 (2026-11-18): scaption to about 120 degrees, the top of Expert 2's exercise sheet (90-120 degrees,
        // paper Appendix G); 18 cm reach and 75 degrees rotation unchanged. Saved v3 ranges (140 degrees) are replaced.
        // Version 3: 140 degrees, 18 cm, 75 degrees. Version 2: 120 degrees, 15 cm, 60 degrees. Version 1: shoulder height, 10 cm, 40 degrees.
        public const int AutomaticEstimateVersion = 4;
        public const float AutomaticStartRange = .35f;
        public const float AutomaticStartSpeed = .65f;
        // Eye height of a 1.75 m adult; the estimate is drawn for this body.
        public const float ReferenceEyeHeight = 1.64f;

        // Eye height above the floor (the rig uses floor tracking) compared to the reference body, clamped to 0.8-1.2.
        // Readings outside 1.2-2.0 m (sitting, no floor, editor) use 1.
        float BodyScale()
        {
            if (simulated || !trackingSpace)
                return 1;
            return BodyScaleFor(trackingSpace.InverseTransformPoint(head.position).y);
        }

        public static float BodyScaleFor(float eyeHeight)
        {
            if (float.IsNaN(eyeHeight) || eyeHeight < 1.2f || eyeHeight > 2f)
                return 1;
            return Mathf.Clamp(eyeHeight / ReferenceEyeHeight, .8f, 1.2f);
        }

        // A typical range for a body of the given scale. Not a measured personal limit; the coach's range
        // fraction and start shift fit it to the player.
        public static Calibration AutomaticEstimate(ExerciseProfile profile, int side, Vector3 origin, Quaternion frame, float bodyScale = 1)
        {
            float sign = side == 0 ? -1 : 1;
            float s = Mathf.Clamp(bodyScale, .8f, 1.2f);
            Vector3 pivot = new Vector3(sign * .18f, -.25f, 0) * s;
            Vector3 start, end;
            switch (profile.movement)
            {
                case ExerciseMovement.Scaption:
                    // Arm hanging down (0.5 m), lifted in the scapular plane (about 36 degrees forward of the frontal
                    // plane) to 120 degrees, i.e. 30 degrees above horizontal: up .5*sin30, out .5*cos30.
                    start = pivot + Vector3.down * .5f * s;
                    end = pivot + new Vector3(sign * .35f, .25f, .255f) * s;
                    break;
                case ExerciseMovement.SerratusReach:
                    start = pivot + Vector3.forward * .35f * s;
                    end = start + Vector3.forward * .18f * s;
                    break;
                default:
                    pivot += Vector3.down * .25f * s;
                    start = pivot + Vector3.forward * .25f * s;
                    end = pivot + Quaternion.Euler(0, sign * 75, 0) * Vector3.forward * .25f * s;
                    break;
            }

            return new Calibration
            {
                exerciseId = profile.exerciseId,
                side = side,
                automatic = true,
                estimateVersion = AutomaticEstimateVersion,
                start = origin + frame * start,
                end = origin + frame * end,
                pivot = origin + frame * pivot,
                startRotation = frame,
                endRotation = frame,
                calibratedTravelMetres = Vector3.Distance(start, end),
            };
        }

        void ResetAnchor()
        {
            anchorStableTime = 0;
            hasAnchorCandidate = false;
        }

        // While the line approaches, look for a steady, plausible start pose and move the estimate there.
        // This happens at most once per exercise and arm, and never while a repetition is scored.
        void TryAnchorAutomaticStart(bool tracked, float dt)
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            var baseline = FindCalibration(profile.exerciseId, side);
            if (baseline == null || !baseline.automatic || baseline.startAnchored)
                return;

            Vector3 hand = handPosition[side];
            // Compare with the start actually shown (range and the coach's start shift included).
            Vector3 shownStart = EffectiveCalibration(profile, baseline, side).start;
            if (!tracked || !CalibrationPoseAllowed(hand, false) || Vector3.Distance(hand, shownStart) > .35f)
            {
                ResetAnchor();
                return;
            }

            if (!hasAnchorCandidate || Vector3.Distance(anchorCandidate, hand) > .025f)
            {
                anchorCandidate = hand;
                anchorStableTime = 0;
                hasAnchorCandidate = true;
                return;
            }

            anchorStableTime += dt;
            if (anchorStableTime < .25f)
                return;

            Vector3 offset = anchorCandidate - shownStart;
            baseline.start += offset;
            baseline.end += offset;
            baseline.pivot += offset;
            baseline.startAnchored = true;

            var adjusted = EffectiveCalibration(profile, baseline, side);
            start = adjusted.start;
            end = adjusted.end;
            pivot = adjusted.pivot;

            if (!Settings.devMode)
            {
                Settings.Arm(profile.exerciseId, side).calibration = baseline.ToLocal(origin, frame);
                Settings.Save();
            }
        }

        // Shows the exercise on the demo figure before the player captures their own range.
        void PrepareBlock()
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            float sign = side == 0 ? -1 : 1;

            clock = 0;
            holdTime = 0;
            state = State.Demo;
            captureMessage = "";

            pivot = origin + frame * new Vector3(sign * .18f, -.25f, 0);
            startRotation = endRotation = frame;
            switch (profile.movement)
            {
                case ExerciseMovement.Scaption:
                    start = pivot + Vector3.down * .565f;
                    end = pivot + frame * new Vector3(sign * .457f, 0, .332f);
                    break;
                case ExerciseMovement.SerratusReach:
                    start = pivot + frame * Vector3.forward * .565f;
                    end = start + frame * Vector3.forward * .06f;
                    break;
                default:
                    pivot += Vector3.down * .25f;
                    start = pivot + frame * Vector3.forward * .25f;
                    end = pivot + frame * (Quaternion.Euler(0, sign * 40, 0) * Vector3.forward * .25f);
                    endRotation = frame * Quaternion.Euler(0, sign * 40, 0);
                    break;
            }

            demonstration = BuildDemonstration(profile, side);
            bool sideView = profile.movement == ExerciseMovement.SerratusReach;
            string caption = profile.displayName + " - " + SideName(side) + (sideView ? " / SIDE VIEW" : " / MIRROR")
                + "\n" + profile.instruction
                + "\nExample only. Next: capture your own comfortable range."
                + "\nA / Space: skip preview. Calibration follows.";
            replay.OpenExerciseDemo(demonstration, caption, sideView);
        }

        ReplayData BuildDemonstration(ExerciseProfile profile, int side)
        {
            var demo = new ReplayData { origin = origin, rotation = frame, inputSource = "EXERCISE DEMONSTRATION" };
            Vector3 restingHand = origin + frame * new Vector3(side == 0 ? .2f : -.2f, -.7f, 0);
            int frameCount = Mathf.CeilToInt(profile.Cycle * 60);
            for (int i = 0; i <= frameCount; i++)
            {
                float t = Mathf.Min(i / 60f, profile.Cycle);
                Target(t, out var hand, out _);
                Vector3 left = side == 0 ? hand : restingHand;
                Vector3 right = side == 1 ? hand : restingHand;
                bool reach = profile.movement == ExerciseMovement.SerratusReach;
                demo.frames.Add(new ReplayFrame
                {
                    time = t,
                    exerciseId = profile.exerciseId,
                    phase = profile.Phase(t),
                    activeSide = side,
                    illustrativeShoulderOffset = reach ? profile.Progress(t) * .06f : 0,
                    headValid = true, leftValid = true, rightValid = true,
                    head = origin, headRotation = neutralHead,
                    left = left, right = right, targetLeft = left, targetRight = right,
                    leftRotation = frame, rightRotation = frame, targetLeftRotation = frame, targetRightRotation = frame,
                });
            }
            return demo;
        }

        void StartPoseCapture(bool endpoint)
        {
            state = endpoint ? State.CaptureEnd : State.CaptureStart;
            holdTime = 0;
            captureWait = 0;
            stableSamples = 0;
            holdingStill = false;
            captureMessage = "";

            if (!replay.IsActive)
                replay.OpenExerciseDemo(demonstration, "", ActiveProfile.movement == ExerciseMovement.SerratusReach);
            replay.HoldDemoAt(endpoint ? ActiveProfile.outwardSeconds : 0, "");
        }

        void UpdatePoseCapture(bool tracked, float dt)
        {
            int side = ActiveSide;
            Vector3 hand = handPosition[side];
            bool endpoint = state == State.CaptureEnd;
            captureWait += dt;
            bool allowed = tracked && CalibrationPoseAllowed(hand, endpoint) && captureWait > .6f;

            if (!allowed)
            {
                holdingStill = false;
                holdTime = 0;
                stableSamples = 0;
                positionSum = Vector3.zero;
            }
            else
            {
                // Moving more than 2.5 cm restarts the hold.
                if (!holdingStill || Vector3.Distance(stablePosition, hand) > .025f)
                {
                    stablePosition = hand;
                    holdingStill = true;
                    holdTime = 0;
                    positionSum = Vector3.zero;
                    stableSamples = 0;
                }
                holdTime += dt;
                positionSum += hand;
                stableSamples++;
            }

            replay.UpdateGuidance(hand, side, CaptureCaption(tracked, allowed, endpoint));
            if (holdTime < CaptureHoldSeconds)
                return;

            if (!endpoint)
            {
                start = positionSum / stableSamples;
                startRotation = handRotation[side];
                scoreFeedback.Captured();
                StartPoseCapture(true);
            }
            else if (TryCaptureEnd())
            {
                scoreFeedback.Captured();
                replay.HoldDemoAt(0, "");
                state = State.Ready;
                clock = 3;
            }
            else
            {
                holdTime = 0;
                holdingStill = false;
            }
        }

        string CaptureCaption(bool tracked, bool allowed, bool endpoint)
        {
            var movement = ActiveProfile.movement;
            string posture;
            if (endpoint)
                posture = movement == ExerciseMovement.Scaption ? "Lift diagonally outward to a comfortable height."
                    : movement == ExerciseMovement.SerratusReach ? "Reach a little farther forward; keep elbow straight."
                    : "Turn forearm outward; keep elbow beside your ribs.";
            else
                posture = movement == ExerciseMovement.Scaption ? "Let your arm hang comfortably by your side."
                    : movement == ExerciseMovement.SerratusReach ? "Hold your arm forward at your chosen starting height."
                    : "Keep elbow beside your ribs and forearm forward.";

            string status = !tracked ? "Waiting for tracking..."
                : allowed ? "Hold still... " + Mathf.RoundToInt(Mathf.Clamp01(holdTime / CaptureHoldSeconds) * 100) + "%"
                : "Move in the shown direction, then hold still.";

            string reachInfo = "";
            if (endpoint && movement == ExerciseMovement.SerratusReach)
            {
                float forward = (Quaternion.Inverse(frame) * (handPosition[ActiveSide] - start)).z;
                reachInfo = "Reach change: " + (forward * 100).ToString("F1") + " cm forward. ";
            }

            string arm = ActiveSide == 0 ? "LEFT (RED)" : "RIGHT (GREEN)";
            return "SET YOUR RANGE - YOUR " + arm + " ARM\n" + posture + "\n" + status
                + "\nCoach = movement example. Stop at your own comfortable limit.\n" + reachInfo + captureMessage;
        }

        bool TryCaptureEnd()
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            Vector3 candidate = stableSamples > 0 ? positionSum / stableSamples : handPosition[side];

            if (Vector3.Distance(start, candidate) < profile.minimumTravel)
            {
                captureMessage = "Movement too small to distinguish reliably. Ask your trainer to adjust this exercise.";
                return false;
            }
            if (Vector3.Distance(head.position, origin) > .25f)
            {
                captureMessage = "Return to your starting position, then hold still.";
                return false;
            }
            if (!RangeDirectionAllowed(profile.movement, Quaternion.Inverse(frame) * (candidate - start), side, profile.minimumTravel))
            {
                captureMessage = "Use your own range in the shown direction, then hold still.";
                return false;
            }

            end = candidate;
            endRotation = handRotation[side];
            if (profile.movement == ExerciseMovement.ExternalRotation)
                pivot = start - frame * Vector3.forward * .25f;

            var calibration = new Calibration
            {
                exerciseId = profile.exerciseId,
                side = side,
                start = start,
                end = end,
                pivot = pivot,
                startRotation = startRotation,
                endRotation = endRotation,
                calibratedTravelMetres = Vector3.Distance(start, end),
            };
            calibrations.Add(calibration);

            if (!Settings.devMode)
            {
                var arm = Settings.Arm(profile.exerciseId, side);
                arm.calibration = calibration.ToLocal(origin, frame);
                arm.automaticRange = false;
                arm.speed = arm.range = 1;
                Settings.Save();
                adaptiveCoach.Recalibrated(profile.exerciseId, side);
            }
            return true;
        }

        // After a capture the player returns to the start pose and holds it for two seconds.
        void UpdateReady(bool tracked, float dt)
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            Vector3 hand = handPosition[side];
            bool atStart = tracked && Vector3.Distance(hand, start) <= Mathf.Max(EndpointTolerance, .06f);
            if (atStart)
                clock -= dt;
            else
                clock = 2;

            string caption = "COPY THE START POSE - " + profile.displayName + " / " + (side == 0 ? "LEFT" : "RIGHT") + "\n"
                + (atStart ? "Start pose matched. Hold still." : "Move your hand to the shown starting position.") + "\n"
                + profile.instruction;
            replay.UpdateGuidance(hand, side, caption);

            if (clock > 0)
                return;

            replay.Close();
            calibrationIndex++;
            if (calibrationIndex < calibrationQueue.Count)
            {
                block = calibrationQueue[calibrationIndex];
                PrepareBlock();
            }
            else
            {
                StartMission();
            }
        }

        // Whether a hand position is a sensible start pose (or end pose) for the current exercise and arm.
        bool CalibrationPoseAllowed(Vector3 hand, bool endpoint)
        {
            var profile = ActiveProfile;
            if (endpoint)
                return RangeDirectionAllowed(profile.movement, Quaternion.Inverse(frame) * (hand - start), ActiveSide, profile.minimumTravel);

            Vector3 local = Quaternion.Inverse(frame) * (hand - origin);
            float sign = ActiveSide == 0 ? -1 : 1;
            if (local.x * sign < .02f)
                return false;

            switch (profile.movement)
            {
                case ExerciseMovement.Scaption:
                    return local.y < -.25f && local.y > -1.2f && local.z < .5f;
                case ExerciseMovement.SerratusReach:
                    return local.z > .15f && local.y > -.80f && local.y < .10f;
                default:
                    return local.y < -.20f && local.y > -.95f && local.z > .03f && local.z < .65f;
            }
        }

        // Whether a movement goes in the right direction for the exercise. The limits scale with the size
        // of the movement, so a small but correct movement is accepted.
        public static bool RangeDirectionAllowed(ExerciseMovement movement, Vector3 delta, int side, float minimumTravel)
        {
            float length = delta.magnitude;
            float sign = side == 0 ? -1 : 1;
            if (length < minimumTravel)
                return false;

            if (movement == ExerciseMovement.Scaption)
                return delta.y > length * .15f && delta.x * sign > length * .15f && delta.z > -length * .25f;

            float sideways = Mathf.Max(.015f, length * .35f);
            // Serratus: mostly forward (within about 53 degrees). The old extra limit of 35 % sideways and up/down
            // (1.75 cm on a 5 cm reach) rejected nearly every real reach, so the hold never started (headset test
            // 2026-11-18, left arm: the capture % never went up).
            if (movement == ExerciseMovement.SerratusReach)
                return delta.z >= length * .6f;

            return delta.x * sign > length * .5f && Mathf.Abs(delta.y) < sideways;
        }

        static string SideName(int side) => side == 0 ? "LEFT" : "RIGHT";
    }
}
