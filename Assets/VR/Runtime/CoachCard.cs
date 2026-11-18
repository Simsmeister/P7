using UnityEngine;

namespace P7
{
    // A stick figure below the line that shows the current movement. The body is illustrative, not measured.
    public class CoachCard : MonoBehaviour
    {
        public ExerciseGame game;

        Transform card;
        TextMesh title;
        LineRenderer body, arm, route, head;
        Material lineMaterial;

        void Awake()
        {
            lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };

            card = new GameObject("Movement card").transform;
            card.SetParent(transform, false);
            card.localPosition = Vector3.zero;

            title = new GameObject("View label", typeof(TextMesh)).GetComponent<TextMesh>();
            title.transform.SetParent(card, false);
            title.transform.localPosition = new Vector3(0, .12f, 0);
            title.anchor = TextAnchor.MiddleCenter;
            title.alignment = TextAlignment.Center;
            title.fontSize = 64;
            title.characterSize = .0075f;

            body = CreateLine("Illustrative body", new Color(.5f, .7f, .85f), .012f);
            arm = CreateLine("Active arm", Color.white, .021f);
            route = CreateLine("Movement route", new Color(.2f, .8f, 1), .008f);
            head = CreateLine("Head", new Color(.7f, .8f, .9f), .012f);
        }

        void OnDestroy()
        {
            Destroy(lineMaterial);
        }

        void LateUpdate()
        {
            bool visible = game.IsMission && !game.replay.IsActive;
            card.gameObject.SetActive(visible);
            if (!visible)
                return;

            transform.SetPositionAndRotation(game.Origin + game.Frame * CardPosition, game.Frame);

            var profile = game.ActiveProfile;
            int side = game.ActiveSide;
            float progress = game.CurrentMovementProgress;
            // Always the example movement on the figure's own body. The player's path used to be drawn here, but start
            // anchoring, the coach's start shift and the reach limit move it away from the figure's fixed shoulder, and
            // the coach shrinks it to 35 % at first, so the arm looked bent or barely moved (headset test 2026-11-18).
            // The figure shows the form; the line shows the player's own range.
            ExampleMovement(profile, side, progress, out var hand, out var start, out var end, out var pivot);

            title.text = (side == 0 ? "LEFT" : "RIGHT") + "\n" + profile.displayName;
            float viewAngle = profile.movement == ExerciseMovement.SerratusReach ? 90 : 0;
            Draw(profile, side, hand, start, end, pivot, viewAngle, FigureScale);
        }

        // Pilot test 1: the card was missed (it sat 2.25 m away and 0.8 m below the eyes, behind and under the line).
        // Now it is centred between the two arms' lines, just behind them and a little below eye height, so it is in
        // view while following the line. The active line starts 0.30 m to the side at 1.35 m, so the centre stays free.
        public static readonly Vector3 CardPosition = new Vector3(0, -.22f, 1.75f);
        public const float FigureScale = .26f;

        // A typical movement on an average body, used when nothing else is known.
        public static void ExampleMovement(ExerciseProfile profile, int side, float progress,
            out Vector3 hand, out Vector3 start, out Vector3 end, out Vector3 pivot)
        {
            float sign = side == 0 ? -1 : 1;
            pivot = new Vector3(sign * .18f, -.25f, 0);
            switch (profile.movement)
            {
                case ExerciseMovement.Scaption:
                    // Straight arm (.565 m) lifted in the scapular plane to 120 degrees, as the automatic estimate.
                    start = pivot + Vector3.down * .565f;
                    end = pivot + new Vector3(sign * .396f, .2825f, .288f);
                    break;
                case ExerciseMovement.SerratusReach:
                    start = pivot + Vector3.forward * .565f;
                    end = start + Vector3.forward * .06f;
                    break;
                default:
                    pivot += Vector3.down * .25f;
                    start = pivot + Vector3.forward * .25f;
                    end = pivot + Quaternion.Euler(0, sign * 40, 0) * Vector3.forward * .25f;
                    break;
            }
            hand = ExerciseProfile.Path(profile.movement, start, end, pivot, progress);
        }

        void Draw(ExerciseProfile profile, int side, Vector3 hand, Vector3 start, Vector3 end, Vector3 pivot, float viewAngle, float scale)
        {
            Vector3 P(Vector3 v) => Quaternion.Euler(0, viewAngle, 0) * v * scale;

            float sign = side == 0 ? -1 : 1;
            Vector3 shoulder = new Vector3(sign * .18f, -.25f, 0);
            Vector3 hip = new Vector3(0, -.85f, 0);
            Vector3 elbow = SessionReplay.EstimateElbow(shoulder, hand, new Vector3(sign * .3f, -.4f, -.2f));

            SetPoints(body,
                P(new Vector3(-.18f, -1.55f, 0)), P(hip), P(new Vector3(.18f, -1.55f, 0)), P(hip),
                P(new Vector3(0, -.25f, 0)), P(new Vector3(-.18f, -.25f, 0)), P(new Vector3(.18f, -.25f, 0)));

            SetPoints(arm, P(shoulder), P(elbow), P(hand));
            SetColor(arm, ExerciseGame.ArmColor(side));

            var circle = new Vector3[33];
            for (int i = 0; i < circle.Length; i++)
            {
                float angle = i * Mathf.PI / 16;
                circle[i] = new Vector3(Mathf.Cos(angle) * .09f, Mathf.Sin(angle) * .1f, 0) * scale;
            }
            SetPoints(head, circle);

            var path = new Vector3[31];
            for (int i = 0; i < path.Length; i++)
                path[i] = P(ExerciseProfile.Path(profile.movement, start, end, pivot, i / 30f));
            SetPoints(route, path);
        }

        LineRenderer CreateLine(string lineName, Color color, float width)
        {
            var line = new GameObject(lineName, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(card, false);
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            SetColor(line, color);
            return line;
        }

        static void SetColor(LineRenderer line, Color color)
        {
            line.startColor = line.endColor = color;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            line.SetPropertyBlock(block);
        }

        static void SetPoints(LineRenderer line, params Vector3[] points)
        {
            line.positionCount = points.Length;
            line.SetPositions(points);
        }
    }
}
