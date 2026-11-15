using UnityEngine;

namespace P7
{
    public enum ExerciseMovement { Scaption, SerratusReach, ExternalRotation }

    [CreateAssetMenu(menuName = "P7/Exercise")]
    public class ExerciseProfile : ScriptableObject
    {
        public string exerciseId;
        public string displayName;
        [TextArea] public string instruction;
        public ExerciseMovement movement;
        public int repetitions = 3;

        public float outwardSeconds = 3;
        public float holdSeconds = 1.5f;
        public float returnSeconds = 3;
        public float restSeconds = 1;

        public float fullCreditMetres = .04f;
        public float zeroCreditMetres = .14f;

        // Rotation is not scored. These two values are only written to session.json.
        public float fullCreditDegrees = 12;
        public float zeroCreditDegrees = 40;

        public float minimumTravel = .025f;

        public float Cycle => outwardSeconds + holdSeconds + returnSeconds + restSeconds;

        // How far along the movement the target is at a given time: 0 at the start pose, 1 at the end pose.
        public float Progress(float time)
        {
            if (time < outwardSeconds)
                return Mathf.SmoothStep(0, 1, time / outwardSeconds);

            if (time < outwardSeconds + holdSeconds)
                return 1;

            float returnTime = time - outwardSeconds - holdSeconds;
            return 1 - Mathf.SmoothStep(0, 1, returnTime / returnSeconds);
        }

        public string Phase(float time)
        {
            if (time < outwardSeconds) return "outward";
            if (time < outwardSeconds + holdSeconds) return "hold";
            if (time < outwardSeconds + holdSeconds + returnSeconds) return "return";
            return "rest";
        }

        // Point on the movement path. Reaches go in a straight line, the other movements swing around the pivot.
        public static Vector3 Path(ExerciseMovement movement, Vector3 start, Vector3 end, Vector3 pivot, float u)
        {
            if (movement == ExerciseMovement.SerratusReach)
                return Vector3.Lerp(start, end, u);

            Vector3 fromPivot = start - pivot;
            Vector3 toPivot = end - pivot;
            if (fromPivot.magnitude < .05f || toPivot.magnitude < .05f)
                return Vector3.Lerp(start, end, u);

            Vector3 direction = Vector3.Slerp(fromPivot.normalized, toPivot.normalized, u);
            float radius = Mathf.Lerp(fromPivot.magnitude, toPivot.magnitude, u);
            return pivot + direction * radius;
        }

        // 1 when the hand is on the line, falling to 0 as it gets further away.
        public float Quality(float distanceMetres)
        {
            return 1 - Mathf.InverseLerp(fullCreditMetres, zeroCreditMetres, distanceMetres);
        }
    }
}
