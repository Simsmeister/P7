using System;
using UnityEngine;

namespace P7
{
    // The start and end pose of one exercise for one arm.
    [Serializable]
    public class Calibration
    {
        public string exerciseId;
        public int side;

        // True when the range is an automatic estimate rather than measured on the player.
        public bool automatic;
        // True once an automatic estimate has been moved to the player's real starting hand position.
        public bool startAnchored;
        // Which version of ExerciseGame.AutomaticEstimate made an automatic range. Older estimates are replaced.
        public int estimateVersion;

        public Vector3 start, end, pivot;
        public float calibratedTravelMetres;
        public Quaternion startRotation, endRotation;

        public Calibration Copy()
        {
            return (Calibration)MemberwiseClone();
        }

        // Saved calibrations are stored relative to the head, so they still fit when the player stands somewhere else.
        public Calibration ToLocal(Vector3 origin, Quaternion frame)
        {
            var inverse = Quaternion.Inverse(frame);
            var local = Copy();
            local.start = inverse * (start - origin);
            local.end = inverse * (end - origin);
            local.pivot = inverse * (pivot - origin);
            local.startRotation = inverse * startRotation;
            local.endRotation = inverse * endRotation;
            return local;
        }

        public Calibration ToWorld(Vector3 origin, Quaternion frame)
        {
            var world = Copy();
            world.start = origin + frame * start;
            world.end = origin + frame * end;
            world.pivot = origin + frame * pivot;
            world.startRotation = frame * startRotation;
            world.endRotation = frame * endRotation;
            return world;
        }

        public bool IsUsableFor(ExerciseProfile profile)
        {
            if (exerciseId != profile.exerciseId)
                return false;

            float travel = Vector3.Distance(start, end);
            bool travelOk = !float.IsNaN(travel) && !float.IsInfinity(travel)
                && travel >= profile.minimumTravel && travel < 2;
            bool rotationsOk = Quaternion.Dot(startRotation, startRotation) > .5f
                && Quaternion.Dot(endRotation, endRotation) > .5f;
            return travelOk && rotationsOk;
        }

        // Same start, but the end pulled back along the path to the given fraction of the full range.
        public Calibration Scaled(ExerciseMovement movement, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            var scaled = Copy();
            scaled.end = ExerciseProfile.Path(movement, start, end, pivot, fraction);
            scaled.endRotation = Quaternion.Slerp(startRotation, endRotation, fraction);
            return scaled;
        }
    }
}
