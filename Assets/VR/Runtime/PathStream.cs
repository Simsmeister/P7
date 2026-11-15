using UnityEngine;

namespace P7
{
    // Draws the current exercise line and the next one, which travel towards the player.
    // A line's distance in front of its arrival point is proportional to the time left until it arrives,
    // so when it arrives it lies exactly on the scored target.
    public class PathStream : MonoBehaviour
    {
        const int Samples = 180;

        public ExerciseGame game;
        public LineRenderer[] paths = new LineRenderer[2];
        public Transform[] ends = new Transform[2];
        public float approachSpeed = .55f;

        readonly Vector3[] points = new Vector3[Samples];
        MaterialPropertyBlock tint;
        // A dim line from the end of the current path to the start of the next, so the stream reads as one line.
        LineRenderer bridge;

        public static Vector3 Approach(Vector3 point, Vector3 forward, float secondsUntilArrival, float speed)
        {
            return point + forward * (secondsUntilArrival * speed);
        }

        void Awake()
        {
            tint = new MaterialPropertyBlock();

            bridge = new GameObject("Path Bridge").AddComponent<LineRenderer>();
            bridge.transform.SetParent(transform, false);
            bridge.sharedMaterial = paths[0].sharedMaterial;
            bridge.widthMultiplier = paths[0].widthMultiplier * .5f;
            bridge.positionCount = 2;
            bridge.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            bool visible = game.PathsVisible && !game.replay.IsActive;
            int shown = 0;

            for (int n = 0; n < paths.Length; n++)
            {
                if (!visible || !game.PathDefinition(n, out var profile, out var calibration, out int side))
                {
                    paths[n].gameObject.SetActive(false);
                    ends[n].gameObject.SetActive(false);
                    continue;
                }
                paths[n].gameObject.SetActive(true);
                ends[n].gameObject.SetActive(true);

                // The current path only shows what is still ahead; the next one arrives after this one and the gap.
                float arrivalDelay = n == 0 ? 0 : game.ActiveProfile.Cycle + game.exerciseGapSeconds;
                float firstTime = n == 0 ? Mathf.Clamp(game.PathClock, 0, profile.Cycle) : 0;
                for (int i = 0; i < Samples; i++)
                {
                    float t = Mathf.Lerp(firstTime, profile.Cycle, i / (Samples - 1f));
                    Vector3 point = game.PathVisualPoint(profile, calibration, side, t);
                    float secondsUntilArrival = arrivalDelay + t - game.PathClock;
                    points[i] = Approach(point, game.Frame * Vector3.forward, secondsUntilArrival, approachSpeed);
                }
                paths[n].positionCount = Samples;
                paths[n].SetPositions(points);
                ends[n].position = points[Samples - 1];

                Color color = ExerciseGame.ArmColor(side) * (n == 0 ? .55f : .30f);
                color.a = 1;
                SetColor(paths[n], color);
                SetColor(ends[n].GetComponent<Renderer>(), color);

                if (n == 0)
                {
                    bridge.SetPosition(0, points[Samples - 1]);
                    shown++;
                }
                else if (shown == 1)
                {
                    bridge.SetPosition(1, points[0]);
                    SetColor(bridge, color * .5f);
                    shown++;
                }
            }

            bridge.gameObject.SetActive(shown == 2);
        }

        void SetColor(Renderer target, Color color)
        {
            tint.SetColor("_BaseColor", color);
            target.SetPropertyBlock(tint);
        }
    }
}
