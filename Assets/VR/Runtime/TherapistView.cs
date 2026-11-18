using System.Collections.Generic;
using UnityEngine;

namespace P7
{
    // Read-only session view for the physiotherapist, shown beside the results page (CLAUDE.md section 14, version 1).
    // Per exercise and arm: tries, complete and PERFECT reps, mean score of the first and last three reps, and a graph
    // per rep of the coach's range and speed and the rep's score. The physiotherapist judges the movement; the game
    // only shows what it measured. PERFECT means the line was followed, not that the movement was clinically correct.
    public class TherapistView : MonoBehaviour
    {
        public class Rep
        {
            public string exerciseId, exerciseName;
            public int side;
            public float score, coverage, range, speed, minutes;
            public bool complete, perfect, learned, headFlag, devMode;

            public static Rep From(ExerciseProfile profile, int side, float score, float coverage, bool complete, AdaptiveCoach.Decision decision,
                float secondsIntoSession = 0)
            {
                return new Rep
                {
                    exerciseId = profile.exerciseId,
                    exerciseName = profile.displayName,
                    side = side,
                    score = score,
                    coverage = coverage,
                    complete = complete,
                    perfect = ScoreFeedback.Perfect(complete, score, coverage),
                    range = decision != null ? decision.rangeScaleUsed : 1,
                    speed = decision != null && decision.durationScaleUsed > 0 ? 1 / decision.durationScaleUsed : 1,
                    learned = decision != null && Learned(decision.reason),
                    headFlag = decision != null && decision.headPositionSeverity > .2f,
                    devMode = decision != null && decision.developerMode,
                    minutes = secondsIntoSession / 60,
                };
            }

            // The coach learned from the rep when the network ran (it eased, increased or kept the level).
            public static bool Learned(string reason) => reason != null && (reason.StartsWith("Network") || reason.StartsWith("Maintain"));
        }

        public class Arm
        {
            public string exerciseId, exerciseName;
            public int side, tries, complete, perfect;
            public float firstMean, lastMean;
            public readonly List<Rep> reps = new List<Rep>();
        }

        // Groups the reps by exercise and arm, in the order they were first played.
        public static List<Arm> Summarise(IList<Rep> log, int window = 3)
        {
            var arms = new List<Arm>();
            foreach (var rep in log)
            {
                var arm = arms.Find(a => a.exerciseId == rep.exerciseId && a.side == rep.side);
                if (arm == null)
                    arms.Add(arm = new Arm { exerciseId = rep.exerciseId, exerciseName = rep.exerciseName, side = rep.side });
                arm.reps.Add(rep);
                arm.tries++;
                arm.complete += rep.complete ? 1 : 0;
                arm.perfect += rep.perfect ? 1 : 0;
            }
            foreach (var arm in arms)
            {
                arm.firstMean = MeanScore(arm.reps, 0, window);
                arm.lastMean = MeanScore(arm.reps, Mathf.Max(0, arm.reps.Count - window), window);
            }
            return arms;
        }

        static float MeanScore(List<Rep> reps, int from, int count)
        {
            float sum = 0;
            int n = 0;
            for (int i = from; i < reps.Count && i < from + count; i++, n++)
                sum += reps[i].score;
            return n > 0 ? sum / n * 100 : 0;
        }

        // Layout in metres, panel-local: x right, y up, the panel's top-left corner at the origin.
        public const float Width = 1.2f;
        const float HeaderHeight = .2f, MaxRowHeight = .3f, MaxBodyHeight = 1.15f, TextColumn = .5f;
        const float GraphTop = 1.5f;  // the graph's y axis goes to 150 %, so a speed above 100 % still fits
        public static readonly Color RangeColor = new Color(.2f, .85f, 1), SpeedColor = new Color(1, .55f, .1f),
            ScoreColor = new Color(.95f, .95f, .95f), NotLearnedColor = new Color(.5f, .5f, .55f), HeadColor = new Color(1, .2f, .2f);

        Material material;
        Font font;
        Transform content;
        int shownCount = -1;

        public void Show(IList<Rep> log, Font textFont)
        {
            gameObject.SetActive(true);
            if (log.Count == shownCount && font == textFont)
                return;
            shownCount = log.Count;
            font = textFont;
            if (!material)
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (content)
                Destroy(content.gameObject);
            content = new GameObject("Content").transform;
            content.SetParent(transform, false);

            var arms = Summarise(log);
            float rowHeight = Mathf.Min(MaxRowHeight, MaxBodyHeight / Mathf.Max(1, arms.Count));
            float height = HeaderHeight + rowHeight * arms.Count + .04f;
            Background(height);

            bool dev = log.Count > 0 && log[log.Count - 1].devMode;
            Label("THERAPIST VIEW - THIS SESSION\n"
                + Paint("range", RangeColor) + "  " + Paint("speed", SpeedColor) + "  " + Paint("score", ScoreColor)
                + ": coach level per repetition, x = minutes into the session, dashed line = 100 %\n"
                + Paint("grey dot", NotLearnedColor) + ": coach did not learn from it   " + Paint("red dot", HeadColor) + ": head moved"
                + (dev ? "\nDEV MODE: adaptation was off, so the level is flat" : ""),
                new Vector2(.03f, -.03f), .005f);

            // One time axis for every row, so the exercises can be compared over the session.
            float endMinutes = Mathf.Max(1, log.Count > 0 ? log[log.Count - 1].minutes : 1);
            for (int i = 0; i < arms.Count; i++)
                DrawArm(arms[i], -HeaderHeight - i * rowHeight, rowHeight, endMinutes);
        }

        public void Hide() => gameObject.SetActive(false);

        void DrawArm(Arm arm, float top, float rowHeight, float endMinutes)
        {
            var last = arm.reps[arm.reps.Count - 1];
            Label(arm.exerciseName.ToUpper() + "  " + (arm.side == 0 ? "LEFT" : "RIGHT") + "\n"
                + arm.tries + " tries, " + arm.complete + " complete, " + arm.perfect + " PERFECT\n"
                + "Score first 3: " + arm.firstMean.ToString("F0") + "  last 3: " + arm.lastMean.ToString("F0") + "\n"
                + "Level now: range " + (last.range * 100).ToString("F0") + " %, speed " + (last.speed * 100).ToString("F0") + " %",
                new Vector2(.03f, top - .015f), Mathf.Min(.0055f, rowHeight * .018f));

            float left = TextColumn + .02f, right = Width - .03f, bottom = top - rowHeight + .04f, graphTop = top - .03f;
            Vector3 P(int i, float value)
            {
                float x = Mathf.Lerp(left, right, Mathf.Clamp01(arm.reps[i].minutes / endMinutes));
                return new Vector3(x, Mathf.Lerp(bottom, graphTop, Mathf.Clamp01(value / GraphTop)), -.01f);
            }

            Line(new Color(.35f, .4f, .45f), .003f, new Vector3(left, graphTop, -.01f), new Vector3(left, bottom, -.01f), new Vector3(right, bottom, -.01f));
            float hundred = Mathf.Lerp(bottom, graphTop, 1 / GraphTop);
            Label("0", new Vector2(left - .01f, bottom - .035f), .0035f);
            Label(endMinutes.ToString("F0") + " min", new Vector2(right - .07f, bottom - .035f), .0035f);
            for (float x = left; x < right; x += .03f)
                Line(new Color(.3f, .35f, .4f), .002f, new Vector3(x, hundred, -.01f), new Vector3(Mathf.Min(x + .015f, right), hundred, -.01f));

            var range = new Vector3[arm.reps.Count];
            var speed = new Vector3[arm.reps.Count];
            var score = new Vector3[arm.reps.Count];
            for (int i = 0; i < arm.reps.Count; i++)
            {
                range[i] = P(i, arm.reps[i].range);
                speed[i] = P(i, arm.reps[i].speed);
                score[i] = P(i, arm.reps[i].score);
                if (!arm.reps[i].learned)
                    Dot(NotLearnedColor, new Vector3(range[i].x, bottom - .012f, -.012f));
                if (arm.reps[i].headFlag)
                    Dot(HeadColor, new Vector3(range[i].x, bottom - .028f, -.012f));
            }
            if (arm.reps.Count > 1)
            {
                Line(ScoreColor, .003f, score);
                Line(RangeColor, .006f, range);
                Line(SpeedColor, .006f, speed);
            }
            else
            {
                Dot(ScoreColor, score[0]);
                Dot(RangeColor, range[0]);
                Dot(SpeedColor, speed[0]);
            }
        }

        static string Paint(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        void Background(float height)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Background";
            quad.transform.SetParent(content, false);
            quad.transform.localPosition = new Vector3(Width / 2, -height / 2, 0);
            quad.transform.localScale = new Vector3(Width, height, 1);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Tint(renderer, new Color(.02f, .04f, .07f));
        }

        void Label(string text, Vector2 at, float characterSize)
        {
            var label = new GameObject("Label", typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(content, false);
            label.transform.localPosition = new Vector3(at.x, at.y, -.005f);
            if (font)
            {
                label.font = font;
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            label.fontSize = 64;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.UpperLeft;
            label.richText = true;
            label.text = text;
        }

        void Line(Color color, float width, params Vector3[] points)
        {
            var line = new GameObject("Line", typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(content, false);
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startColor = line.endColor = color;
            Tint(line, color);
        }

        void Dot(Color color, Vector3 at) => Line(color, .012f, at - new Vector3(.006f, 0, 0), at + new Vector3(.006f, 0, 0));

        static void Tint(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        void OnDestroy()
        {
            if (material)
                Destroy(material);
        }
    }
}
