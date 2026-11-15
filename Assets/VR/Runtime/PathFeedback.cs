using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace P7
{
    // Visual feedback on the line: the nodes along the path, the sections that light up behind the hand,
    // the ring at the endpoint, the hand trail and the floor lights. Also keeps the ENERGY/FLOW progress.
    public class PathFeedback : MonoBehaviour
    {
        const int Bins = 120;
        const float NodeFadeSeconds = 1.3f;

        static readonly Color Missed = new Color(1, .48f, .12f);
        static readonly Color FloorLit = new Color(.15f, .9f, 1);
        static readonly Color FloorDark = new Color(.06f, .12f, .19f);
        static readonly Color Background = new Color(.018f, .03f, .055f);

        public ExerciseGame game;
        public Transform holdRing;
        public Renderer[] nodes;
        public Renderer[] floorLights;
        public LineRenderer[] litSections;

        public SessionProgress Run { get; private set; } = new SessionProgress();

        readonly List<string> progressRows = new List<string>();
        readonly List<string> nodeRows = new List<string>();

        // Hand quality sampled along the current repetition, one value per time bin.
        readonly float[] binQuality = new float[Bins];
        readonly bool[] binSampled = new bool[Bins];
        int repetition = -1;
        int lastBin = -1;
        float lastClock;
        float holdCharge;

        TrailRenderer[] trails;
        MaterialPropertyBlock tint;

        public static bool NodeRepaired(float quality) => quality >= .65f;

        public static bool SectionLit(float sectionEnd, float clock, bool sampled) => sectionEnd <= clock && sampled;

        // Dim amber far off the line, white on it.
        public static Color Glow(float quality)
        {
            return Color.Lerp(Missed * .35f, Color.white, Mathf.InverseLerp(.4f, .9f, quality));
        }

        public void Begin(int target)
        {
            Run = new SessionProgress { target = Mathf.Max(1, target) };
            progressRows.Clear();
            nodeRows.Clear();
            repetition = -1;
        }

        public void Resolve(float accuracy, bool sessionLimitReached)
        {
            Run.Resolve(accuracy, sessionLimitReached);
            progressRows.Add(string.Join(",", Run.movements, F(accuracy), F(Run.power), F(Run.battery), Run.outcome));
        }

        public string ExportProgress()
        {
            return "movement,accuracy,ship_power,battery,outcome\n" + string.Join("\n", progressRows);
        }

        public string ExportNodes()
        {
            return "movement,exercise_id,side,exercise_time_s,path_quality,node_repaired\n" + string.Join("\n", nodeRows);
        }

        void Awake()
        {
            tint = new MaterialPropertyBlock();
        }

        void Start()
        {
            var camera = game.head.GetComponent<Camera>();
            if (camera)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Background;
            }
        }

        void LateUpdate()
        {
            transform.SetPositionAndRotation(game.Origin, game.Frame);
            UpdateFloorLights();
            holdRing.gameObject.SetActive(false);

            bool visible = game.PathsVisible && !game.replay.IsActive;
            UpdateTrails(visible);
            if (!visible || !game.PathDefinition(0, out var profile, out var calibration, out int side))
            {
                SetAllActive(nodes, false);
                foreach (var section in litSections)
                    section.gameObject.SetActive(false);
                return;
            }

            float clock = game.PathClock;
            float duration = profile.outwardSeconds + profile.holdSeconds + profile.returnSeconds;
            if (repetition != game.CompletedRepetitions || clock < lastClock)
                StartRepetition();
            float deltaTime = Mathf.Max(0, clock - lastClock);
            lastClock = clock;

            if (game.IsPlaying && game.IsTracked)
                SampleHand(profile, side, clock, duration, deltaTime);

            Color armColor = ExerciseGame.ArmColor(side);
            DrawNodes(profile, calibration, side, clock, duration, armColor);
            DrawSections(profile, calibration, side, clock, duration);
            DrawHoldRing(profile, calibration, side, clock, armColor);
        }

        void StartRepetition()
        {
            Array.Clear(binSampled, 0, Bins);
            Array.Clear(binQuality, 0, Bins);
            repetition = game.CompletedRepetitions;
            lastBin = -1;
            holdCharge = 0;
        }

        // Records one sample per time bin. Tracking gaps are left empty, never filled in.
        void SampleHand(ExerciseProfile profile, int side, float clock, float duration, float deltaTime)
        {
            float quality = game.FollowQuality;
            int bin = BinAt(clock, duration, false);
            if (bin != lastBin)
            {
                binQuality[bin] = quality;
                binSampled[bin] = true;
                lastBin = bin;
                nodeRows.Add(string.Join(",", repetition + 1, profile.exerciseId, side == 0 ? "left" : "right",
                    F(clock), F(quality), NodeRepaired(quality) ? 1 : 0));
            }

            if (profile.Phase(clock) == "hold" && NodeRepaired(quality))
                holdCharge = Mathf.Min(1, holdCharge + deltaTime / Mathf.Max(.1f, profile.holdSeconds));
        }

        void DrawNodes(ExerciseProfile profile, Calibration calibration, int side, float clock, float duration, Color armColor)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                float t = duration * i / (nodes.Length - 1f);
                bool show = t >= clock - NodeFadeSeconds;
                node.gameObject.SetActive(show);
                if (!show)
                    continue;

                bool passed = t <= clock;
                int bin = BinAt(t, duration, true);
                Color color;
                if (!passed)
                    color = armColor * .35f;
                else if (binSampled[bin])
                    color = Glow(binQuality[bin]) * Mathf.Clamp01(1 - (clock - t) / NodeFadeSeconds);
                else
                    color = Missed * .35f;

                node.transform.position = ApproachingPoint(profile, calibration, side, t, clock);
                node.transform.localScale = Vector3.one * (passed ? .025f : .035f);
                SetColor(node, color);
            }
        }

        // The part of the line already followed stays lit until the repetition ends.
        void DrawSections(ExerciseProfile profile, Calibration calibration, int side, float clock, float duration)
        {
            for (int i = 0; i < litSections.Length; i++)
            {
                var section = litSections[i];
                float t = duration * i / litSections.Length;
                float next = duration * (i + 1) / litSections.Length;
                int bin = BinAt(t, duration, true);
                bool show = SectionLit(next, clock, binSampled[bin]);
                section.gameObject.SetActive(show);
                if (!show)
                    continue;

                // The return goes back over the outward line, so draw it thinner and slightly in front.
                bool returning = t >= profile.outwardSeconds + profile.holdSeconds;
                Vector3 lift = returning ? game.Frame * Vector3.back * .01f : Vector3.zero;
                section.SetPosition(0, game.PathVisualPoint(profile, calibration, side, t) + lift);
                section.SetPosition(1, game.PathVisualPoint(profile, calibration, side, next) + lift);
                section.widthMultiplier = returning ? .01f : .018f;
                SetColor(section, Glow(binQuality[bin]));
            }
        }

        // A ring at the endpoint that grows while the player holds the end pose.
        void DrawHoldRing(ExerciseProfile profile, Calibration calibration, int side, float clock, Color armColor)
        {
            if (clock > profile.outwardSeconds + profile.holdSeconds)
                return;

            float t = Mathf.Max(profile.outwardSeconds, clock);
            holdRing.gameObject.SetActive(true);
            holdRing.position = ApproachingPoint(profile, calibration, side, t, clock);
            holdRing.rotation = game.Frame;
            holdRing.localScale = Vector3.one * (.14f + .045f * holdCharge);
            SetColor(holdRing.GetComponent<Renderer>(), Color.Lerp(armColor * .5f, armColor, holdCharge));
        }

        void UpdateFloorLights()
        {
            int lit = Mathf.FloorToInt(Run.power * floorLights.Length);
            for (int i = 0; i < floorLights.Length; i++)
                SetColor(floorLights[i], i < lit ? FloorLit : FloorDark);
        }

        // A short light trail behind the hand marker, tinted by how well the hand follows the line.
        void UpdateTrails(bool visible)
        {
            if (trails == null)
                CreateTrails();

            foreach (var trail in trails)
            {
                bool on = visible && game.IsPlaying && game.IsTracked && trail.gameObject.activeInHierarchy;
                if (on && !trail.emitting)
                    trail.Clear();
                trail.emitting = on;
                if (on)
                {
                    tint.SetColor("_BaseColor", Glow(game.FollowQuality));
                    trail.SetPropertyBlock(tint);
                }
            }
        }

        void CreateTrails()
        {
            trails = new TrailRenderer[game.handMarkers.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                var trail = game.handMarkers[i].gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = game.pathStream.paths[0].sharedMaterial;
                trail.time = .25f;
                trail.minVertexDistance = .005f;
                trail.widthMultiplier = .03f;
                trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                trail.emitting = false;
                trails[i] = trail;
            }
        }

        Vector3 ApproachingPoint(ExerciseProfile profile, Calibration calibration, int side, float t, float clock)
        {
            Vector3 point = game.PathVisualPoint(profile, calibration, side, t);
            return PathStream.Approach(point, game.Frame * Vector3.forward, t - clock, game.pathStream.approachSpeed);
        }

        static int BinAt(float time, float duration, bool round)
        {
            float position = time / duration * (Bins - 1);
            int bin = round ? Mathf.RoundToInt(position) : Mathf.FloorToInt(position);
            return Mathf.Clamp(bin, 0, Bins - 1);
        }

        void SetColor(Renderer target, Color color)
        {
            tint.SetColor("_BaseColor", color);
            target.SetPropertyBlock(tint);
        }

        static void SetAllActive(Renderer[] renderers, bool active)
        {
            foreach (var r in renderers)
                r.gameObject.SetActive(active);
        }

        static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
    }
}
