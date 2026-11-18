using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace P7
{
    // Runs a session: calibration, the stream of exercises, scoring each repetition, breaks and the end of the session.
    // Calibration is in ExerciseGame.Calibration.cs, recording and export in ExerciseGame.Recording.cs.
    public partial class ExerciseGame : MonoBehaviour
    {
        enum State { Welcome, Demo, CaptureStart, CaptureEnd, Ready, Tutorial, Approaching, Playing, Finished, Break }

        public Transform head, trackingSpace;
        public Transform board;
        public TextMesh boardText;
        // The marker that follows the player's hand, one per arm.
        public Transform[] handMarkers = new Transform[2];
        // The hollow ring that moves along the line and shows where the hand should be.
        public Transform targetMarker;
        public ExerciseProfile[] exercises;

        public SessionReplay replay;
        public CoachCard coachCard;
        public PathStream pathStream;
        public AdaptiveCoach adaptiveCoach;
        public GameMenu menu;
        public PathFeedback pathFeedback;
        public ScoreFeedback scoreFeedback;

        public float exerciseGapSeconds = 2.5f;
        // Seconds of active play. Prototype values, not clinical guidance. 0 turns a limit off.
        public float breakAfterSeconds = 300;
        public float breakSeconds = 45;
        public float sessionCapSeconds = 900;

        // Safety (pilot test 1: players stepped forward and one nearly fell). The player should stand on the floor ring
        // placed where they stood at START. If the head moves further than stepLimitMetres sideways or forward from
        // there, the line waits and the board asks them to step back; it goes on below stepReturnMetres.
        public float stepLimitMetres = .35f;
        public float stepReturnMetres = .2f;
        // No target is placed further from the estimated shoulder than this, for the reference body (scaled by eye height).
        // Shoulder to the controller at a full forward reach with the shoulder blade forward, adult of 1.75 m.
        public const float ArmReachMetres = .66f;
        public const float FloorRingRadius = .3f;

        // Used by the editor tests to simulate a player.
        [System.NonSerialized] public bool TestMode;
        [System.NonSerialized] public float TestHandFraction = 1, TestHeadDegrees, TestControllerDegrees;

        State state;
        bool simulated;
        InputActionMap input;
        bool previousA, previousB, previousX;

        // The player's position and facing when the session started. Everything is placed relative to this.
        Vector3 origin;
        Quaternion frame;
        Quaternion neutralHead;

        readonly Vector3[] handPosition = new Vector3[2];
        readonly Quaternion[] handRotation = new Quaternion[2];
        readonly bool[] handTracked = new bool[2];

        SavedSettings settings;
        Participant participant;
        readonly List<Calibration> calibrations = new List<Calibration>();

        // The order of exercises. Each entry is exerciseIndex * 2 + side.
        readonly List<int> playlist = new List<int>();
        int playlistIndex;
        int block;
        int repetition;
        bool missionStarted;
        bool showTutorial;

        // First-time tutorial: an introduction, then for each exercise a demo on the figure and one practice
        // repetition without points, then a short wrap-up. A / Space goes on, B / R skips the rest of it.
        enum TutorialStep { Intro, Demo, Practice, Outro }
        TutorialStep tutorialStep;
        readonly List<int> tutorialBlocks = new List<int>();
        int tutorialIndex;
        bool tutorialSkipDemos;
        // True while a tutorial practice repetition runs: no points, no recording, no FLOW and no range/speed change.
        bool practicing;
        // During the tutorial the demo and the practice line use the full range, so the player sees and tries the
        // whole movement, not the small starting level.
        bool tutorialFullRange;
        string practiceFeedback = "";
        // The starting range is this share of the furthest point reached in the practice repetition.
        public const float PracticeRangeShare = .9f;
        // Where the hand waits during the last part of the approach, compared to the path start. Together with the
        // start of the repetition this is the start offset the network uses to move the start point.
        public const float ApproachMeasureSeconds = .8f;
        public const float ApproachMeasureRadius = .30f;
        Vector3 approachOffsetSum;
        float approachSeconds;
        // The intro and wrap-up wait for A instead of a timer (pilot test 1: the text disappeared too fast).
        // A press only counts after a page has been shown this long, so one press cannot skip two pages.
        public const float TutorialPageMinSeconds = 1;
        int introPage;
        public static readonly string[] TutorialIntroPages =
        {
            "WELCOME - HOW TO PLAY\n"
            + "A glowing line flies towards you.\n"
            + "Your hand is the solid ball. Keep it inside the hollow ring.\n"
            + "Follow the line out, hold at the end, then follow it back.",

            // Colours must match FarColor, NearColor, OnTargetColor, ArmColor and the depth guide (checked in P7Tests).
            "COLOURS - AM I IN?\n"
            + "Ball = your hand: <color=#FF334C>red</color> is the left arm, <color=#1AFF73>green</color> is the right arm.\n"
            + "Ring: <color=#FF3326>red</color> = far, <color=#FFD91A>yellow</color> = close, <color=#26FF4C>GREEN = you are in</color>.\n"
            + "Keep the ring green the whole way to get PERFECT.\n"
            + "Thin line: <color=#33D9FF>blue</color> = reach further, <color=#FF8C1A>orange</color> = come closer.",

            "STAND STILL\n"
            + "Stay on the floor ring. You never need to step or lean.\n"
            + "Every target is within reach of your arm.\n"
            + "If one feels too far, go only as far as is comfortable.",

            "ENERGY AND FLOW\n"
            + "ENERGY (upper bar) fills with every good movement. Fill it to finish the level.\n"
            + "FLOW (lower bar) drops when you are far from the line\n"
            + "and rises when you follow it well. If FLOW runs out, the run ends.",

            "YOUR TURN SOON\n"
            + "The figure shows each movement once, then you try it.\n"
            + "No points yet, and it is fine to miss.\n"
            + "How you do sets where the game starts.",
        };

        // The current exercise path in world space.
        Vector3 start, end, pivot;
        Quaternion startRotation, endRotation;

        // Seconds into the current repetition. Negative while the line is still approaching.
        float clock;
        float followQuality;
        float scoreSum;
        float activeSeconds, activeSinceBreak, trackingPause;
        int breaksTaken;
        // Eye-height scale of this session, read once in SetFrame so targets never move during a repetition.
        float bodyScale = 1;
        bool offSpot;
        float offSpotSeconds;
        LineRenderer floorRing;

        RepetitionResult current;
        MaterialPropertyBlock targetTint;

        public static Color ArmColor(int side) => side == 0 ? new Color(1, .2f, .3f) : new Color(.1f, 1, .45f);

        public SavedSettings Settings
        {
            get
            {
                if (settings == null)
                    settings = SavedSettings.Load(DataPath("player-settings.json"));
                return settings;
            }
            set => settings = value;
        }

        public Participant Participant
        {
            get
            {
                if (participant == null)
                    participant = Participant.Load(DataPath("participant.json"), SessionsFolder);
                return participant;
            }
        }

        public Vector3 Origin => origin;
        public Quaternion Frame => frame;
        public int ActiveSide => block % 2;
        public ExerciseProfile ActiveProfile => missionStarted ? MissionProfile(block) : exercises[Mathf.Clamp(block / 2, 0, exercises.Length - 1)];
        public float PathClock => clock;
        public string CurrentStep => state.ToString();
        public bool IsPlaying => state == State.Playing;
        public bool IsPractice => practicing;
        public string TutorialStepName => state == State.Tutorial || practicing ? tutorialStep.ToString() : "";
        public bool Finished => state == State.Finished;
        public bool IsMission => missionStarted && state != State.Finished && state != State.Break;
        public bool PathsVisible => state == State.Playing || state == State.Approaching;
        public bool MenuVisible => (state == State.Welcome || state == State.Finished) && !replay.IsActive;
        public bool IsTracked => handTracked[ActiveSide] && HeadTracked();
        public float FollowQuality => IsPlaying && IsTracked ? followQuality : 0;
        public float CurrentMovementProgress => IsPlaying ? ActiveProfile.Progress(clock) : 0;
        public int CompletedRepetitions { get; private set; }
        public float Score => CompletedRepetitions > 0 ? 100 * scoreSum / CompletedRepetitions : 0;
        public float EndpointTolerance => Mathf.Clamp(Vector3.Distance(start, end) * .3f, .025f, .06f);

        public bool AnyEnabled
        {
            get
            {
                foreach (var profile in exercises)
                    for (int side = 0; side < 2; side++)
                        if (Settings.Arm(profile.exerciseId, side).enabled)
                            return true;
                return false;
            }
        }

        string SessionsFolder => Path.Combine(Application.persistentDataPath, "Sessions");

        // In the editor the files go to /tmp, so testing never touches real data.
        static string DataPath(string fileName)
        {
            return Application.isEditor ? "/tmp/p7-editor-" + fileName : Path.Combine(Application.persistentDataPath, fileName);
        }

        void Awake()
        {
            targetTint = new MaterialPropertyBlock();
            BuildTargetVisuals();

            input = new InputActionMap("Exercises");
            foreach (string side in new[] { "Left", "Right" })
            {
                string path = "<XRController>{" + side + "Hand}/";
                input.AddAction(side + "Position", InputActionType.Value, path + "devicePosition");
                input.AddAction(side + "Rotation", InputActionType.Value, path + "deviceRotation");
                input.AddAction(side + "Tracked", InputActionType.Value, path + "isTracked");
                input.AddAction(side + "State", InputActionType.Value, path + "trackingState");
                input.AddAction(side + "Trigger", InputActionType.Value, path + "trigger");
            }
            input.AddAction("A", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            input.AddAction("B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            input.AddAction("X", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        }

        void OnEnable() => input.Enable();

        void Start()
        {
#if UNITY_EDITOR
            // Without a headset the editor runs a labelled simulation where the hand follows the line.
            simulated = !XRSettings.isDeviceActive;
#endif
            SetFrame();
        }

        void OnDisable()
        {
            input.Disable();
            scoreFeedback.StopLineTone();
            Finish(false);
        }

        void OnDestroy()
        {
            CloseSampleFiles();
            input.Dispose();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && samplesCsv != null)
                Finish(false);
        }

        void SetFrame()
        {
            origin = head.position;
            frame = Quaternion.Euler(0, head.eulerAngles.y, 0);
            neutralHead = head.rotation;
            board.SetPositionAndRotation(origin + frame * new Vector3(0, .70f, 2.3f), frame);
            bodyScale = BodyScale();
            offSpot = false;
            PlaceFloorRing();
        }

        // A ring on the floor under where the player stood at START. Created at runtime, so no prefab change is needed.
        void PlaceFloorRing()
        {
            if (!floorRing)
            {
                floorRing = new GameObject("Floor Ring").AddComponent<LineRenderer>();
                floorRing.transform.SetParent(transform, false);
                if (pathStream && pathStream.paths.Length > 0 && pathStream.paths[0])
                    floorRing.sharedMaterial = pathStream.paths[0].sharedMaterial;
                floorRing.useWorldSpace = true;
                floorRing.loop = true;
                floorRing.positionCount = 48;
                floorRing.widthMultiplier = .025f;
            }
            float floor = simulated || !trackingSpace ? origin.y - ReferenceEyeHeight : trackingSpace.position.y;
            Vector3 centre = new Vector3(origin.x, floor + .01f, origin.z);
            for (int i = 0; i < floorRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / floorRing.positionCount;
                floorRing.SetPosition(i, centre + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * FloorRingRadius);
            }
        }

        // Horizontal distance of the head from where the player stood at START.
        public static float StepDistance(Vector3 headPosition, Vector3 startPosition)
        {
            Vector3 d = headPosition - startPosition;
            d.y = 0;
            return d.magnitude;
        }

        // With hysteresis, so standing near the limit does not flicker between paused and running.
        public static bool OffSpot(bool wasOff, float distance, float limit, float returnDistance)
        {
            return wasOff ? distance > returnDistance : distance > limit;
        }

        bool HeadTracked()
        {
            if (simulated)
                return true;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var both = InputTrackingState.Position | InputTrackingState.Rotation;
            return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked
                && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState, out InputTrackingState flags)
                && (flags & both) == both;
        }

        public bool MenuRay(int side, out Ray ray, out bool triggerDown)
        {
            string name = side == 0 ? "Left" : "Right";
            triggerDown = input[name + "Trigger"].ReadValue<float>() > .55f;
            ray = new Ray(handPosition[side], handRotation[side] * Vector3.forward);
            return handTracked[side];
        }

        public void ShowLastReplay() => replay.Open();

        public void ResetSavedSettings()
        {
            if (!MenuVisible)
                return;
            Settings = new SavedSettings { filePath = Settings.filePath };
            Settings.Save();
            AdaptiveCoach.ResetPersonalData(Settings);
            adaptiveCoach.Configure(Settings);
            // Tests press the real button, so they must not advance the real participant counter.
            if (!TestMode)
                Participant.Advance();
        }

        void Update()
        {
            ReadControllers();
            UpdateMarkers();
            HandleButtons();

            bool reviewing = replay.IsActive;
            if (reviewing || !IsPlaying)
                scoreFeedback.TrackPerfect(false, 0, 0);
            scoreFeedback.LineTone(!reviewing && IsPlaying && IsTracked, FollowQuality);

            bool lineShown = PathsVisible && !reviewing;
            offSpot = lineShown && OffSpot(offSpot, StepDistance(head.position, origin), stepLimitMetres, stepReturnMetres);
            if (floorRing)
            {
                floorRing.gameObject.SetActive(!reviewing && !MenuVisible);
                Tint(floorRing, offSpot ? new Color(1, .55f, .1f) : new Color(.1f, .55f, .65f));
            }

            board.gameObject.SetActive(!reviewing && !MenuVisible && (!PathsVisible || practicing || offSpot));
            // While practising, the board drops closer to the line so the hints are in view.
            board.SetPositionAndRotation(origin + frame * new Vector3(0, practicing || offSpot ? PracticeBoardHeight : .70f, 2.3f), frame);
            if (offSpot && !reviewing)
                boardText.text = "STEP BACK ONTO THE FLOOR RING\nThe line waits for you.\nEvery target can be reached from the ring.";
            else if (practicing && !reviewing)
                boardText.text = PracticeHint();
            targetMarker.gameObject.SetActive(!reviewing && state != State.Welcome && state != State.Finished && state != State.Break
                && state != State.Tutorial);

            // The exercise demo plays on the replay figure; only calibration keeps running underneath it.
            bool calibrating = state == State.CaptureStart || state == State.CaptureEnd || state == State.Ready;
            if (reviewing && !calibrating)
                return;

            // Off the floor ring counts like a tracking pause for the line, but is logged separately.
            bool tracked = IsTracked && !offSpot;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (offSpot)
                offSpotSeconds += Time.unscaledDeltaTime;
            if (tracked && PathsVisible && !practicing)
            {
                activeSeconds += dt;
                activeSinceBreak += dt;
            }

            switch (state)
            {
                case State.Demo:
                    StartPoseCapture(false);
                    break;
                case State.CaptureStart:
                case State.CaptureEnd:
                    UpdatePoseCapture(tracked, dt);
                    break;
                case State.Ready:
                    UpdateReady(tracked, dt);
                    break;
                case State.Tutorial:
                    UpdateTutorial(dt);
                    break;
                case State.Break:
                    UpdateBreak(dt);
                    break;
                case State.Approaching:
                    UpdateApproaching(tracked, dt);
                    break;
                case State.Playing:
                    UpdatePlaying(tracked, dt);
                    break;
            }
        }

        void ReadControllers()
        {
            var both = InputTrackingState.Position | InputTrackingState.Rotation;
            for (int s = 0; s < 2; s++)
            {
                string name = s == 0 ? "Left" : "Right";
                var flags = (InputTrackingState)input[name + "State"].ReadValue<int>();
                handTracked[s] = input[name + "Tracked"].IsPressed() && (flags & both) == both;
                handPosition[s] = trackingSpace.TransformPoint(input[name + "Position"].ReadValue<Vector3>());
                handRotation[s] = trackingSpace.rotation * input[name + "Rotation"].ReadValue<Quaternion>();

                if (simulated)
                    SimulateHand(s);
            }
        }

        // Without a headset the active hand follows the target exactly; the other rests by the side.
        void SimulateHand(int side)
        {
            handTracked[side] = true;
            handPosition[side] = origin + frame * new Vector3(side == 0 ? -.25f : .25f, -.35f, .3f);
            handRotation[side] = frame;

            if (side == ActiveSide)
            {
                if (state == State.CaptureStart || state == State.Ready || state == State.Approaching)
                {
                    handPosition[side] = start;
                    handRotation[side] = startRotation;
                }
                else if (state == State.CaptureEnd)
                {
                    handPosition[side] = end;
                    handRotation[side] = endRotation;
                }
                else
                {
                    Target(clock, out handPosition[side], out handRotation[side]);
                    if (TestMode && IsPlaying)
                        handPosition[side] = Vector3.Lerp(start, handPosition[side], TestHandFraction);
                }
            }

            if (TestMode && IsPlaying)
                handRotation[side] *= Quaternion.Euler(TestControllerDegrees, TestControllerDegrees, 0);
        }

        void UpdateMarkers()
        {
            for (int s = 0; s < 2; s++)
            {
                bool show = !MenuVisible && state != State.Break && handTracked[s] && s == ActiveSide && !replay.IsActive;
                handMarkers[s].gameObject.SetActive(show);
                if (handTracked[s])
                    handMarkers[s].SetPositionAndRotation(MapToFront(handPosition[s], s), frame);
            }
        }

        // Target ring and hand ball. Pilot test 1: "the rings are very 2D". Headset test 2026-11-18: the first torus
        // went green from 9 cm and fully at 5 cm, but the hold only counts within EndpointTolerance and PERFECT also
        // needs 90 % of the path covered, so the ring said "in" on a rep that was not PERFECT. Now:
        // - green means exactly "this counts towards PERFECT": within GreenDistance in 3D (the scored distance), and in
        //   the hold also at least 90 % of the way to the end. Before that the ring goes red, orange, yellow with distance;
        // - the ring's hole has the radius GreenDistance, so "ball in the hole" and "green" agree when the depth is right;
        // - both are shaded (three brightness bands on the unlit material) so they read as 3D, and the ball is a small
        //   sphere in the arm colour instead of the flat 11 cm disc.
        // Scoring is unchanged.
        public const float PerfectFrameQuality = .86f;  // a rep at this mean quality with every phase reached scores 90
        public static readonly Color FarColor = new Color(1, .2f, .15f), NearColor = new Color(1, .85f, .1f),
            OnTargetColor = new Color(.15f, 1, .3f), IdleTargetColor = new Color(.85f, .9f, 1);
        const float TargetTubeRadius = .007f, BallRadius = .018f;
        static readonly float[] BandBrightness = { 1, .6f, .3f };
        static readonly Vector3 BandLight = new Vector3(-.35f, .75f, -.55f).normalized;
        Transform targetTorus;
        MeshFilter torusFilter;
        MeshRenderer torusRenderer;
        readonly MeshRenderer[] handBalls = new MeshRenderer[2];
        float torusHole = -1;
        bool targetHold;

        public static float GreenDistanceFor(float endpointTolerance, float fullCreditMetres, float zeroCreditMetres)
            => Mathf.Min(endpointTolerance, Mathf.Lerp(fullCreditMetres, zeroCreditMetres, 1 - PerfectFrameQuality));

        public static bool InTarget(float distance, float greenDistance, bool hold, float progress)
            => distance <= greenDistance && (!hold || progress >= .9f);

        public static Color TargetColor(float distance, float greenDistance, float zeroCreditMetres, bool inTarget)
            => inTarget ? OnTargetColor : Color.Lerp(FarColor, NearColor, Mathf.InverseLerp(zeroCreditMetres, greenDistance, distance));

        void BuildTargetVisuals()
        {
            var flat = targetMarker.GetComponentInChildren<LineRenderer>();
            var torus = new GameObject("Target Torus", typeof(MeshFilter), typeof(MeshRenderer));
            torus.transform.SetParent(targetMarker, false);
            torusFilter = torus.GetComponent<MeshFilter>();
            torusRenderer = torus.GetComponent<MeshRenderer>();
            if (flat)
            {
                torusRenderer.sharedMaterials = new[] { flat.sharedMaterial, flat.sharedMaterial, flat.sharedMaterial };
                flat.enabled = false;
            }
            targetTorus = torus.transform;

            for (int s = 0; s < 2; s++)
            {
                Material material = null;
                foreach (var r in handMarkers[s].GetComponentsInChildren<Renderer>(true))
                {
                    if (r is TrailRenderer)
                        continue;
                    material ??= r.sharedMaterial;
                    r.enabled = false;
                }
                var ball = new GameObject("Hand Ball", typeof(MeshFilter), typeof(MeshRenderer));
                ball.transform.SetParent(handMarkers[s], false);
                ball.GetComponent<MeshFilter>().sharedMesh = SphereMesh(BallRadius, 12, 18);
                handBalls[s] = ball.GetComponent<MeshRenderer>();
                handBalls[s].sharedMaterials = new[] { material, material, material };
            }
        }

        // Runs in LateUpdate, after the ring and the ball have moved this frame. The marker distance equals the scored
        // distance, because MapToFront shifts both by the same offset.
        void UpdateTargetVisuals()
        {
            int side = ActiveSide;
            if (handBalls[side])
                Shade(handBalls[side], ArmColor(side));
            if (!targetTorus || !targetMarker.gameObject.activeInHierarchy)
                return;

            var profile = ActiveProfile;
            float green = GreenDistanceFor(EndpointTolerance, profile.fullCreditMetres, profile.zeroCreditMetres);
            if (Mathf.Abs(green - torusHole) > .002f)
            {
                if (torusFilter.sharedMesh)
                    Destroy(torusFilter.sharedMesh);
                torusFilter.sharedMesh = TorusMesh(green + TargetTubeRadius, TargetTubeRadius, 48, 10);
                torusHole = green;
            }

            bool live = handTracked[side] && handMarkers[side].gameObject.activeInHierarchy && !replay.IsActive;
            float distance = Vector3.Distance(handMarkers[side].position, targetMarker.position);
            float progress = Vector3.Dot(handPosition[side] - start, end - start) / Mathf.Max(.000001f, (end - start).sqrMagnitude);
            bool inTarget = live && InTarget(distance, green, IsPlaying && targetHold, progress);
            Shade(torusRenderer, live ? TargetColor(distance, green, profile.zeroCreditMetres, inTarget) : IdleTargetColor);
            targetTorus.localScale = Vector3.one * (inTarget ? 1.08f : 1);
        }

        void Shade(Renderer renderer, Color color)
        {
            for (int i = 0; i < BandBrightness.Length; i++)
            {
                Color c = color * BandBrightness[i];
                c.a = 1;
                targetTint.SetColor("_BaseColor", c);
                renderer.SetPropertyBlock(targetTint, i);
            }
        }

        // Splits a mesh into three submeshes by how much each face turns towards a light above, left and in front,
        // so an unlit material can be drawn bright, medium and dark and the shape reads as 3D.
        static Mesh BandedMesh(string meshName, Vector3[] vertices, Vector3[] normals, List<int> triangles)
        {
            var bands = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 n = normals[triangles[t]] + normals[triangles[t + 1]] + normals[triangles[t + 2]];
                float d = Vector3.Dot(n.normalized, BandLight);
                bands[d > .3f ? 0 : d > -.25f ? 1 : 2].AddRange(new[] { triangles[t], triangles[t + 1], triangles[t + 2] });
            }
            var mesh = new Mesh { name = meshName, vertices = vertices, normals = normals, subMeshCount = 3 };
            for (int k = 0; k < 3; k++)
                mesh.SetTriangles(bands[k], k);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A torus around the local z axis, facing the player. Faces are clockwise seen from outside (Unity's front).
        public static Mesh TorusMesh(float radius, float tube, int segments, int sides)
        {
            var vertices = new Vector3[segments * sides];
            var normals = new Vector3[vertices.Length];
            var triangles = new List<int>(segments * sides * 6);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int j = 0; j < sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides;
                    var normal = outward * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    vertices[i * sides + j] = outward * radius + normal * tube;
                    normals[i * sides + j] = normal;
                    int i2 = (i + 1) % segments, j2 = (j + 1) % sides;
                    int v00 = i * sides + j, v10 = i2 * sides + j, v01 = i * sides + j2, v11 = i2 * sides + j2;
                    triangles.AddRange(new[] { v00, v10, v01, v10, v11, v01 });
                }
            }
            return BandedMesh("Target Torus", vertices, normals, triangles);
        }

        public static Mesh SphereMesh(float radius, int rings, int segments)
        {
            int row = segments + 1;
            var vertices = new Vector3[(rings + 1) * row];
            var normals = new Vector3[vertices.Length];
            var triangles = new List<int>(rings * segments * 6);
            for (int r = 0; r <= rings; r++)
            {
                float theta = Mathf.PI * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = Mathf.PI * 2 * s / segments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    vertices[r * row + s] = n * radius;
                    normals[r * row + s] = n;
                    if (r == rings || s == segments)
                        continue;
                    int a = r * row + s, b = a + row, c = a + 1, d = b + 1;
                    if (r > 0)
                        triangles.AddRange(new[] { a, c, b });
                    if (r < rings - 1)
                        triangles.AddRange(new[] { c, d, b });
                }
            }
            return BandedMesh("Hand Ball", vertices, normals, triangles);
        }

        // Depth cue (pilot test 1: "the rings are very 2D", players could not tell if they were too far or too close).
        // A thin line from the ball to the ring, shown when the hand is off by more than DepthCueMetres in depth:
        // cyan means reach further forward, orange means bring the hand closer. Drawn in LateUpdate, after the
        // ring has moved this frame. It does not change scoring.
        public const float DepthCueMetres = .03f;
        static readonly Color ReachColor = new Color(.2f, .85f, 1), CloserColor = new Color(1, .55f, .1f);
        LineRenderer depthGuide;

        // +1: the target is further away than the hand, -1: closer, 0: within the cue's dead zone.
        public static int DepthDirection(float targetDepthMinusHand)
        {
            if (targetDepthMinusHand > DepthCueMetres)
                return 1;
            if (targetDepthMinusHand < -DepthCueMetres)
                return -1;
            return 0;
        }

        void LateUpdate()
        {
            UpdateTargetVisuals();
            if (!depthGuide)
            {
                depthGuide = new GameObject("Depth Guide").AddComponent<LineRenderer>();
                depthGuide.transform.SetParent(transform, false);
                if (pathStream && pathStream.paths.Length > 0 && pathStream.paths[0])
                    depthGuide.sharedMaterial = pathStream.paths[0].sharedMaterial;
                depthGuide.useWorldSpace = true;
                depthGuide.positionCount = 2;
                depthGuide.widthMultiplier = .006f;
            }

            int side = ActiveSide;
            bool show = PathsVisible && !replay.IsActive && handTracked[side] && targetMarker.gameObject.activeInHierarchy
                && handMarkers[side].gameObject.activeInHierarchy;
            Vector3 ball = handMarkers[side].position, ring = targetMarker.position;
            int direction = DepthDirection((Quaternion.Inverse(frame) * (ring - ball)).z);
            show &= direction != 0;
            depthGuide.gameObject.SetActive(show);
            if (!show)
                return;
            depthGuide.SetPosition(0, ball);
            depthGuide.SetPosition(1, ring);
            Color color = direction > 0 ? ReachColor : CloserColor;
            Tint(depthGuide, color);
        }

        // Runtime lines use the path material, which reads _BaseColor from a property block.
        MaterialPropertyBlock lineTint;
        void Tint(LineRenderer line, Color color)
        {
            lineTint ??= new MaterialPropertyBlock();
            line.startColor = line.endColor = color;
            lineTint.SetColor("_BaseColor", color);
            line.SetPropertyBlock(lineTint);
        }

        // A: skip the demo or break, or go on in the tutorial. B: skip the tutorial, or (dev mode, in the menu)
        // show or hide the last replay. X: stop the session. In the editor simulation Space is A and R is B.
        // Set by GameMenu while the therapist view is open: B then closes it instead of opening a replay.
        [System.NonSerialized] public bool therapistViewOpen;
        public bool CloseTherapistView { get; private set; }

        void HandleButtons()
        {
            var keyboard = simulated ? Keyboard.current : null;
            bool a = input["A"].IsPressed() || (keyboard != null && keyboard.spaceKey.isPressed);
            bool b = input["B"].IsPressed() || (keyboard != null && keyboard.rKey.isPressed);
            bool x = input["X"].IsPressed();
            bool pressedA = a && !previousA, pressedB = b && !previousB, pressedX = x && !previousX;
            previousA = a;
            previousB = b;
            previousX = x;

            CloseTherapistView = pressedB && therapistViewOpen;
            if (CloseTherapistView)
                pressedB = false;

            if (pressedX && state != State.Welcome && state != State.Finished)
                Finish(false);

            // B opens the replay only in dev mode (pilot test 1: a participant opened it by accident); it can always close one.
            if (pressedB && (state == State.Finished || state == State.Welcome) && (Settings.devMode || replay.IsActive))
            {
                if (replay.IsActive)
                    replay.Close();
                else
                    replay.Open();
            }

            if (pressedA)
                SkipTutorialPreview();
            if (pressedB && (state == State.Tutorial || practicing))
                SkipTutorial();
        }

        // A: skip the calibration demo or a break, or go on in the tutorial (next page, end of a demo, start the game).
        public void SkipTutorialPreview()
        {
            if (state == State.Demo && replay.IsDemo)
            {
                replay.Close();
                StartPoseCapture(false);
            }
            else if (state == State.Tutorial && tutorialStep == TutorialStep.Demo)
            {
                if (replay.IsDemo)
                    replay.Close();
                StartPractice();
            }
            else if (state == State.Tutorial)
            {
                AdvanceTutorial();
            }
            else if (state == State.Break)
            {
                LoadChallenge();
            }
        }

        // B: leave the whole tutorial and start the game.
        public void SkipTutorial()
        {
            if (state == State.Tutorial || practicing)
                CompleteTutorial();
        }

        public void Begin() => Begin(false);

        // Starts a session. With recalibrate the player first sets their own range for every enabled exercise.
        public void Begin(bool recalibrate)
        {
            if (!AnyEnabled)
                return;
            if (replay.IsActive)
                replay.Close();
            Finish(false);
            SetFrame();
            adaptiveCoach.Configure(Settings);

            CompletedRepetitions = 0;
            scoreSum = trackingPause = activeSeconds = activeSinceBreak = offSpotSeconds = 0;
            breaksTaken = 0;
            block = 0;
            missionStarted = false;
            playlist.Clear();
            playlistIndex = 0;
            recordingOpen = false;
            sessionFolder = "";
            summaryRows.Clear();
            RepLog.Clear();
            sessionStartTime = Time.realtimeSinceStartup;
            calibrations.Clear();
            calibrationQueue.Clear();
            calibrationIndex = 0;
            scoreFeedback.transform.SetPositionAndRotation(origin, frame);
            scoreFeedback.ResetMission();

            showTutorial = recalibrate || !Settings.tutorialComplete || Settings.tutorialEveryStart;
            practicing = tutorialFullRange = false;
            for (int e = 0; e < exercises.Length; e++)
                for (int side = 0; side < 2; side++)
                    PrepareCalibration(e, side, recalibrate);

            if (!Settings.devMode)
                Settings.Save();

            if (calibrationQueue.Count > 0)
            {
                showTutorial = true;
                block = calibrationQueue[0];
                PrepareBlock();
            }
            else
            {
                StartMission();
            }
        }

        void StartMission()
        {
            missionStarted = true;
            pathFeedback.Begin(Settings.totalRepetitions);
            playlist.Clear();
            playlist.AddRange(Settings.Playlist(exercises));
            if (playlist.Count == 0)
            {
                state = State.Welcome;
                return;
            }

            playlistIndex = 0;
            if (showTutorial)
                BeginTutorial(calibrationQueue.Count > 0);
            else
                LoadChallenge();
        }

        // One tutorial exercise per enabled exercise, on the arm it first appears with in the playlist.
        // After a manual calibration the demos were just shown, so only the practice repetitions are left.
        void BeginTutorial(bool skipDemos)
        {
            // Every exercise in the order it first appears, each followed by all its enabled arms.
            tutorialBlocks.Clear();
            foreach (int entry in playlist)
            {
                if (tutorialBlocks.Exists(b => b / 2 == entry / 2))
                    continue;
                tutorialBlocks.Add(entry);
                int otherArm = entry ^ 1;
                if (playlist.Contains(otherArm))
                    tutorialBlocks.Add(otherArm);
            }
            tutorialFullRange = true;
            tutorialIndex = 0;
            tutorialSkipDemos = skipDemos;
            practiceFeedback = "";
            practicing = false;
            block = tutorialBlocks[0];
            tutorialStep = TutorialStep.Intro;
            state = State.Tutorial;
            introPage = 0;
            clock = 0;
        }

        void UpdateTutorial(float dt)
        {
            switch (tutorialStep)
            {
                case TutorialStep.Intro:
                    clock += dt;
                    boardText.text = TutorialIntroPages[introPage] + "\n\n"
                        + "Page " + (introPage + 1) + "/" + TutorialIntroPages.Length
                        + "   |   A / Space: next   |   B / R: skip tutorial";
                    break;
                case TutorialStep.Demo:
                    // The demo closes itself after one playthrough; then the player tries it.
                    StartPractice();
                    break;
                case TutorialStep.Outro:
                    clock += dt;
                    boardText.text = "TUTORIAL DONE\n" + practiceFeedback
                        + "\nFrom now on the game counts points and adapts to you.\n"
                        + "Fill ENERGY to finish. Keep FLOW up by following the line.\n"
                        + "A / Space: start";
                    break;
            }
        }

        // A in the tutorial: the next intro page, the end of a demo, or the start of the game after the wrap-up.
        void AdvanceTutorial()
        {
            if (tutorialStep == TutorialStep.Intro)
            {
                if (clock < TutorialPageMinSeconds)
                    return;
                introPage++;
                clock = 0;
                if (introPage >= TutorialIntroPages.Length)
                    ShowTutorialExercise();
            }
            else if (tutorialStep == TutorialStep.Outro)
            {
                if (clock >= TutorialPageMinSeconds)
                    CompleteTutorial();
            }
        }

        // Places the tutorial exercise and shows it on the figure, or goes straight to practice.
        void ShowTutorialExercise()
        {
            block = tutorialBlocks[tutorialIndex];
            SetPathForBlock();
            // Each exercise is shown once; its second arm goes straight to trying it.
            bool sameExercise = tutorialIndex > 0 && tutorialBlocks[tutorialIndex - 1] / 2 == block / 2;
            if (tutorialSkipDemos || sameExercise)
            {
                StartPractice();
                return;
            }

            var profile = ActiveProfile;
            int side = ActiveSide;
            tutorialStep = TutorialStep.Demo;
            state = State.Tutorial;
            // The figure shows the mastered movement (the full automatic estimate) at normal speed,
            // whatever this player's current level or calibration.
            var mastered = AutomaticEstimate(profile, side, origin, frame, bodyScale);
            start = mastered.start;
            end = mastered.end;
            pivot = mastered.pivot;
            startRotation = mastered.startRotation;
            endRotation = mastered.endRotation;
            demonstration = BuildDemonstration(profile, side);
            SetPathForBlock();
            bool sideView = profile.movement == ExerciseMovement.SerratusReach;
            string caption = "TUTORIAL " + (tutorialIndex + 1) + "/" + tutorialBlocks.Count + " - " + profile.displayName
                + " - YOUR " + SideName(side) + " ARM" + (sideView ? " / SIDE VIEW" : " / MIRROR")
                + "\n" + profile.instruction
                + "\nThis is the goal movement at full speed. Then you try it - it is fine if you cannot do it all yet."
                + "\nA / Space: try it now   |   B / R: skip tutorial";
            replay.OpenExerciseDemo(demonstration, caption, sideView);
        }

        void StartPractice()
        {
            tutorialStep = TutorialStep.Practice;
            practicing = true;
            ResetAnchor();
            approachOffsetSum = Vector3.zero;
            approachSeconds = 0;
            SetPathForBlock();
            state = State.Approaching;
            clock = -exerciseGapSeconds;
        }

        // Ends a practice repetition: a short comment, the start shift is learned, then the next exercise or the wrap-up.
        void EndPractice()
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            var r = current;
            practicing = false;
            float coverage = TravelCoverage(r.minimumProgress, r.maximumProgress);
            adaptiveCoach.ObserveStart(profile, side, r.MeanStartOffset, r.startSeconds,
                MovementScore(r.PathQuality, coverage, r.endpointReached ? 1 : .5f) * 100, coverage,
                r.MeanError, Vector3.Distance(start, end), r.validSeconds, r.trackingLostSeconds);

            // How far along the full movement the hand got becomes the starting range; the coach adapts from there.
            float reached = PracticeReach(r.minimumProgress, r.maximumProgress);
            bool usable = r.validSeconds > 0 && r.trackingLostSeconds <= .1f * (r.validSeconds + r.trackingLostSeconds);
            if (usable)
                adaptiveCoach.SetMeasuredLevel(profile, side, reached * PracticeRangeShare, PracticeSpeed(r.PathQuality));

            string comment = !usable ? "Tracking was lost - the game keeps a gentle start."
                : r.PathQuality >= .7f ? "Great, you followed the line"
                : "Good, the line will fit you better as you play";
            practiceFeedback += profile.displayName + " " + SideName(side) + ": " + comment
                + (usable ? " (reached about " + Mathf.RoundToInt(reached * 100) + "%)." : "") + "\n";

            tutorialIndex++;
            if (tutorialIndex < tutorialBlocks.Count)
            {
                ShowTutorialExercise();
            }
            else
            {
                tutorialStep = TutorialStep.Outro;
                state = State.Tutorial;
                clock = 0;
            }
        }

        void CompleteTutorial()
        {
            if (replay.IsDemo)
                replay.Close();
            practicing = false;
            tutorialFullRange = false;
            if (!Settings.devMode)
            {
                Settings.tutorialComplete = true;
                Settings.Save();
            }
            LoadChallenge();
        }

        const float PracticeBoardHeight = .38f;

        // Live coaching while the player copies the movement: where to move, or that it looks right.
        string PracticeHint()
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            string header = "YOUR TURN - " + profile.displayName + " - " + SideName(side) + " ARM\n";
            if (!IsTracked)
                return header + "Move your hand where the headset can see it.";

            Vector3 hand = handPosition[side];
            Vector3 target;
            if (state == State.Approaching)
            {
                target = start;
                if (Vector3.Distance(hand, target) <= .07f)
                    return header + "Ready - hand at the start. Copy the line when it arrives.";
                return header + "Put your hand at the start of the line: " + Direction(target - hand) + ".";
            }

            Target(clock, out target, out _);
            string phase = profile.Phase(clock);
            if (Vector3.Distance(hand, target) <= .07f)
                return header + (phase == "hold" ? "Good - hold it here."
                    : phase == "return" ? "Good - follow it back."
                    : phase == "rest" ? "Well done. Rest."
                    : "Good - keep the ball in the ring. Go as far as is comfortable.");
            if (phase == "outward" || phase == "hold")
                return header + "Try to " + Direction(target - hand) + " - only if comfortable. Not reaching it is fine.";
            return header + "Follow it back: " + Direction(target - hand) + ".";
        }

        // The largest direction from the hand to the target, as words the player can follow.
        string Direction(Vector3 worldDelta)
        {
            Vector3 d = Quaternion.Inverse(frame) * worldDelta;
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            if (ay >= ax && ay >= az)
                return d.y > 0 ? "move higher" : "move lower";
            if (ax >= az)
                return d.x > 0 ? "move to the right" : "move to the left";
            return d.z > 0 ? "reach further forward" : "bring it closer";
        }

        // Starting speed from how well the practice kept up with the full-speed line: 0.6 to 1.
        public const float PracticeSpeedMax = .8f;

        public static float PracticeSpeed(float pathQuality)
        {
            if (float.IsNaN(pathQuality))
                return .6f;
            // At most 80 % speed after the tutorial (was 100 %, 2026-11-18): players with injuries start slowly and the coach speeds up gradually.
            return Mathf.Lerp(.6f, PracticeSpeedMax, Mathf.InverseLerp(.3f, .8f, pathQuality));
        }

        // The share of the full movement the hand covered from the start, 0-1.
        public static float PracticeReach(float minimumProgress, float maximumProgress)
        {
            if (float.IsInfinity(maximumProgress) || float.IsNaN(maximumProgress))
                return 0;
            return Mathf.Clamp01(maximumProgress);
        }

        void UpdateBreak(float dt)
        {
            clock -= dt;
            boardText.text = "BREAK - GREAT WORK\n" + CompletedRepetitions + " moves done. This rest is part of your session.\n"
                + "Next line in " + Mathf.CeilToInt(clock) + " s   |   A: skip";
            if (clock <= 0)
                LoadChallenge();
        }

        // Sets up the next exercise in the playlist. The line arrives after the gap.
        void LoadChallenge()
        {
            ResetAnchor();
            approachOffsetSum = Vector3.zero;
            approachSeconds = 0;
            block = playlist[playlistIndex];
            repetition = 0;
            for (int i = 0; i < playlistIndex; i++)
                if (playlist[i] == block)
                    repetition++;

            SetPathForBlock();
            state = State.Approaching;
            clock = -exerciseGapSeconds;
        }

        // The current block's path with this arm's current range and start shift.
        void SetPathForBlock()
        {
            var calibration = EffectiveCalibration(ActiveProfile, FindCalibration(ActiveProfile.exerciseId, ActiveSide), ActiveSide);
            start = calibration.start;
            end = calibration.end;
            pivot = calibration.pivot;
            startRotation = calibration.startRotation;
            endRotation = calibration.endRotation;
        }

        void UpdateApproaching(bool tracked, float dt)
        {
            TryAnchorAutomaticStart(tracked, dt);
            targetMarker.SetPositionAndRotation(MapToFront(start, ActiveSide), frame);

            // The player's own choice of start: where the hand waits just before the line arrives.
            Vector3 waiting = handPosition[ActiveSide] - start;
            if (tracked && clock > -ApproachMeasureSeconds && waiting.magnitude <= ApproachMeasureRadius)
            {
                approachOffsetSum += Quaternion.Inverse(frame) * waiting * dt;
                approachSeconds += dt;
            }

            // The line only moves while the player is tracked and on the floor ring.
            if (tracked)
                clock += dt;
            else if (!offSpot)
                trackingPause += Time.unscaledDeltaTime;

            if (clock >= 0)
                StartRepetition();
        }

        void StartRepetition()
        {
            current = new RepetitionResult();
            if (approachSeconds > 0)
                current.TrackStart(approachOffsetSum / approachSeconds, approachSeconds);
            approachOffsetSum = Vector3.zero;
            approachSeconds = 0;
            clock = 0;
            state = State.Playing;
            if (!recordingOpen && !practicing)
                OpenRecording();
        }

        void UpdatePlaying(bool tracked, float dt)
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            string phase = profile.Phase(clock);
            Target(clock, out Vector3 target, out Quaternion targetRotation);
            targetMarker.SetPositionAndRotation(MapToFront(target, side), frame);

            float distance = Vector3.Distance(handPosition[side], target);
            float angle = Vector3.Angle(handRotation[side] * Vector3.forward, targetRotation * Vector3.forward);
            followQuality = tracked ? profile.Quality(distance) : 0;
            targetHold = phase == "hold";
            scoreFeedback.TrackPerfect(tracked && phase != "rest" && !practicing, followQuality, dt);

            if (tracked)
            {
                MeasureFrame(profile, side, phase, target, distance, angle, dt);
            }
            else
            {
                // A rep with more than 10 % lost time is not learned from; stepping off the ring counts as lost time.
                current.TrackingLost(Time.unscaledDeltaTime);
                if (!offSpot)
                    trackingPause += Time.unscaledDeltaTime;
            }

            if (!practicing)
                RecordAt60Hz(tracked, target, targetRotation);

            // The clock pauses while tracking is lost.
            if (tracked)
                clock += dt;
            if (clock >= profile.Cycle)
                EndRepetition();
        }

        void MeasureFrame(ExerciseProfile profile, int side, string phase, Vector3 target, float distance, float angle, float dt)
        {
            Vector3 hand = handPosition[side];
            // Where the hand rests compared to the path start: the first fifth of the outward phase and the rest.
            if (clock < profile.outwardSeconds * .2f || phase == "rest")
                current.TrackStart(Quaternion.Inverse(frame) * (hand - target), dt);
            Vector3 headOffset = Quaternion.Inverse(frame) * (head.position - origin);
            float headDegrees = TestMode ? TestHeadDegrees : Quaternion.Angle(head.rotation, neutralHead);
            float headSeverity = phase == "rest" ? 0 : HeadPositionSeverity(headOffset, side);

            current.AddFrame(dt, profile.Quality(distance), distance, angle);
            float progress = Vector3.Dot(hand - start, end - start) / Mathf.Max(.000001f, (end - start).sqrMagnitude);
            current.TrackTravel(progress);
            // Time the ring was green, by the same rule as the ring (UpdateTargetVisuals); PERFECT needs 90 % of it.
            if (phase != "rest")
            {
                current.scoredSeconds += dt;
                float green = GreenDistanceFor(EndpointTolerance, profile.fullCreditMetres, profile.zeroCreditMetres);
                if (InTarget(distance, green, phase == "hold", progress))
                    current.greenSeconds += dt;
            }
            current.TrackHead(Vector3.Distance(head.position, origin), headDegrees, headSeverity, dt);

            float tolerance = EndpointTolerance;
            if (clock < .8f && Vector3.Distance(hand, start) <= tolerance)
                current.startReached = true;
            if (phase == "hold" && distance <= tolerance)
                current.heldSeconds += dt;
            if (clock >= profile.outwardSeconds + profile.holdSeconds + profile.returnSeconds && Vector3.Distance(hand, start) <= tolerance)
                current.returnReached = true;

            // The endpoint is checked once, when the hold phase starts.
            bool holdPhase = clock >= profile.outwardSeconds && clock < profile.outwardSeconds + profile.holdSeconds;
            if (holdPhase && !current.endpointReached && Vector3.Distance(hand, end) <= .14f)
            {
                current.endpointReached = true;
                scoreFeedback.EndpointReached(MapToFront(end, side));
                if (!simulated)
                {
                    var controller = InputDevices.GetDeviceAtXRNode(side == 0 ? XRNode.LeftHand : XRNode.RightHand);
                    controller.SendHapticImpulse(0, .3f, .08f);
                }
            }
        }

        void EndRepetition()
        {
            if (practicing)
            {
                EndPractice();
                return;
            }

            ScoreRepetition();

            if (pathFeedback.Run.Finished)
            {
                Finish(pathFeedback.Run.GoalReached);
                return;
            }

            playlistIndex++;
            if (playlistIndex + 1 >= playlist.Count)
                playlist.AddRange(Settings.Playlist(exercises));

            if (DueAtRepEnd(activeSeconds, activeSinceBreak, breakAfterSeconds, sessionCapSeconds) == "break")
            {
                activeSinceBreak = 0;
                breaksTaken++;
                state = State.Break;
                clock = breakSeconds;
            }
            else
            {
                LoadChallenge();
            }
        }

        void ScoreRepetition()
        {
            var profile = ActiveProfile;
            int side = ActiveSide;
            var r = current;

            float neededHold = profile.holdSeconds * .65f;
            float pathQuality = r.PathQuality;
            float coverage = TravelCoverage(r.minimumProgress, r.maximumProgress);
            float phases = ((r.startReached ? 1 : 0) + (r.returnReached ? 1 : 0) + (r.endpointReached ? 1 : 0)
                + Mathf.Clamp01(r.heldSeconds / neededHold)) / 4;
            bool complete = r.startReached && r.returnReached && r.endpointReached && r.heldSeconds >= neededHold && coverage >= .65f;

            float rawScore = MovementScore(pathQuality, coverage, phases);
            float headSeverity = r.HeadSeverity;
            float score = HeadAdjustedScore(rawScore, headSeverity);
            scoreSum += score;
            CompletedRepetitions++;

            AddSummaryRow(profile, side, score, complete, pathQuality, coverage, phases, rawScore, headSeverity);

            bool capReached = DueAtRepEnd(activeSeconds, activeSinceBreak, breakAfterSeconds, sessionCapSeconds) == "cap";
            scoreFeedback.Reward(complete, ScoreFeedback.Perfect(complete, score, coverage, r.GreenShare), score);
            pathFeedback.Resolve(score, capReached);

            var baseline = FindCalibration(profile.exerciseId, side);
            var decision = adaptiveCoach.Observe(profile, side, repetition + 1, score * 100, coverage, r.validSeconds, r.trackingLostSeconds,
                r.headMetres, r.headDegrees, Vector3.Distance(baseline.start, baseline.end), Vector3.Distance(start, end),
                headSeverity, r.MeanError, r.MeanStartOffset, r.startSeconds);
            RepLog.Add(TherapistView.Rep.From(profile, side, score, coverage, complete, decision,
                Time.realtimeSinceStartup - sessionStartTime, r.GreenShare));
        }

        // Every scored repetition of this session, for the therapist view on the results page. Not exported.
        public readonly List<TherapistView.Rep> RepLog = new List<TherapistView.Rep>();
        float sessionStartTime;  // wall clock at START, for the view's time axis (breaks and pauses included)

        // Partial credit: following the line counts most, then reaching each phase, all scaled by how much of the range was used.
        public static float MovementScore(float pathQuality, float coverage, float phases)
        {
            return Mathf.Clamp01((.7f * pathQuality + .3f * phases) * Mathf.Lerp(.25f, 1, Mathf.Clamp01(coverage)));
        }

        // How much of the start-to-end range the hand actually covered.
        public static float TravelCoverage(float minimum, float maximum)
        {
            if (float.IsInfinity(minimum) || float.IsInfinity(maximum))
                return 0;
            return Mathf.Clamp01(maximum - minimum);
        }

        // Rough check for compensating by leaning: the head moves both down and towards the other side.
        public static float HeadPositionSeverity(Vector3 localOffset, int side)
        {
            float towardsOtherSide = side == 1 ? -localOffset.x : localOffset.x;
            return Mathf.Min(Mathf.InverseLerp(.10f, .25f, towardsOtherSide), Mathf.InverseLerp(.08f, .20f, -localOffset.y));
        }

        public static float HeadAdjustedScore(float rawScore, float severity) => Mathf.Max(0, rawScore - .1f * Mathf.Clamp01(severity));

        // Checked at the end of a repetition. The session cap wins over a break.
        public static string DueAtRepEnd(float active, float sinceBreak, float breakAfter, float cap)
        {
            if (cap > 0 && active >= cap)
                return "cap";
            if (breakAfter > 0 && sinceBreak >= breakAfter)
                return "break";
            return "";
        }

        void Finish(bool complete)
        {
            if (state == State.Welcome || state == State.Finished)
                return;
            if (replay.IsDemo)
                replay.Close();
            if (!pathFeedback.Run.Finished)
                pathFeedback.Run.outcome = "aborted";

            CloseSampleFiles();
            if (sessionFolder != "")
                WriteSessionFiles(complete);

            scoreFeedback.TrackPerfect(false, 0, 0);
            state = State.Finished;
            if (targetMarker)
                targetMarker.gameObject.SetActive(false);
        }

        // In the tutorial the demo and the practice run at normal speed; afterwards the coach's pace applies.
        ExerciseProfile MissionProfile(int entry) => tutorialFullRange ? exercises[entry / 2] : adaptiveCoach.ProfileFor(exercises[entry / 2], entry % 2);

        Calibration FindCalibration(string exerciseId, int side) => calibrations.Find(c => c.exerciseId == exerciseId && c.side == side);

        void Target(float time, out Vector3 position, out Quaternion rotation)
        {
            var profile = ActiveProfile;
            float u = profile.Progress(time);
            position = ExerciseProfile.Path(profile.movement, start, end, pivot, u);
            rotation = Quaternion.Slerp(startRotation, endRotation, u);
        }

        // The path for the current (offset 0) or next (offset 1) exercise, with this arm's current range.
        public bool PathDefinition(int offset, out ExerciseProfile profile, out Calibration calibration, out int side)
        {
            profile = null;
            calibration = null;
            side = 0;
            int index = playlistIndex + offset;
            if (index < 0 || index >= playlist.Count)
                return false;

            // A practice repetition shows only its own line.
            if (practicing && offset != 0)
                return false;
            int entry = practicing ? block : playlist[index];
            side = entry % 2;
            profile = MissionProfile(entry);
            calibration = EffectiveCalibration(profile, FindCalibration(profile.exerciseId, side), side);
            return calibration != null;
        }

        public Calibration EffectiveCalibration(ExerciseProfile profile, Calibration baseline, int side)
        {
            if (baseline == null)
                return null;
            float fraction = adaptiveCoach.RangeFor(profile, side, Vector3.Distance(baseline.start, baseline.end));
            if (tutorialFullRange)
                fraction = 1;
            var scaled = baseline.Scaled(profile.movement, fraction);
            // The coach moves the whole path to where this player's hand actually starts.
            Vector3 shift = frame * adaptiveCoach.StartShiftFor(profile, side);
            scaled.start += shift;
            scaled.end += shift;
            scaled.pivot += shift;
            return ClampToReach(profile.movement, scaled, origin + frame * ShoulderOffset(side, bodyScale), ArmReachMetres * bodyScale);
        }

        // The estimated shoulder relative to the eyes, the same point the automatic estimate swings around.
        public static Vector3 ShoulderOffset(int side, float scale)
        {
            return new Vector3(side == 0 ? -.18f : .18f, -.25f, 0) * Mathf.Clamp(scale, .8f, 1.2f);
        }

        // Keeps every point of the path within reach of the shoulder. First the whole path moves towards the shoulder,
        // so the movement keeps its size and only its place changes; if that is not enough, the end is cut back along
        // the path. Works on any calibration (automatic, anchored, start-shifted or manual), so no target asks for a step
        // or a lean. A path that is already in reach is returned unchanged.
        public static Calibration ClampToReach(ExerciseMovement movement, Calibration path, Vector3 shoulder, float reach)
        {
            const int Steps = 40;
            var clamped = path.Copy();
            for (int pass = 0; pass < 6; pass++)
            {
                Vector3 furthest = clamped.start;
                for (int i = 1; i <= Steps; i++)
                {
                    Vector3 p = ExerciseProfile.Path(movement, clamped.start, clamped.end, clamped.pivot, i / (float)Steps);
                    if (Vector3.Distance(p, shoulder) > Vector3.Distance(furthest, shoulder))
                        furthest = p;
                }
                Vector3 outward = furthest - shoulder;
                if (outward.magnitude <= reach + .001f)
                    return clamped;
                Vector3 pull = outward.normalized * (outward.magnitude - reach);
                clamped.start -= pull;
                clamped.end -= pull;
                clamped.pivot -= pull;
            }

            float allowed = 1;
            for (int i = 1; i <= Steps; i++)
            {
                float u = i / (float)Steps;
                if (Vector3.Distance(ExerciseProfile.Path(movement, clamped.start, clamped.end, clamped.pivot, u), shoulder) > reach + .001f)
                {
                    allowed = (i - 1) / (float)Steps;
                    break;
                }
            }
            if (allowed < 1)
            {
                var cut = clamped.Scaled(movement, allowed);
                cut.calibratedTravelMetres = clamped.calibratedTravelMetres;
                return cut;
            }
            return clamped;
        }

        // The line is drawn in front of the player instead of at the hand: the hand's movement from the start
        // is copied to a fixed point in front, so all three directions (and overshoot) are kept.
        public static Vector3 ProjectToFront(Vector3 localStart, Vector3 localHand, int side)
        {
            float sign = side == 0 ? -1 : 1;
            return new Vector3(sign * .30f, -.32f, 1.35f) + (localHand - localStart);
        }

        Vector3 MapToFront(Vector3 hand, int side)
        {
            var toLocal = Quaternion.Inverse(frame);
            return origin + frame * ProjectToFront(toLocal * (start - origin), toLocal * (hand - origin), side);
        }

        public Vector3 PathVisualPoint(ExerciseProfile profile, Calibration calibration, int side, float time)
        {
            var toLocal = Quaternion.Inverse(frame);
            Vector3 hand = ExerciseProfile.Path(profile.movement, calibration.start, calibration.end, calibration.pivot, profile.Progress(time));
            return origin + frame * ProjectToFront(toLocal * (calibration.start - origin), toLocal * (hand - origin), side);
        }

        // Everything measured during one repetition.
        class RepetitionResult
        {
            public float validSeconds, trackingLostSeconds;
            public float qualitySum, errorSum, angleSum;
            public float minimumProgress = float.PositiveInfinity, maximumProgress = float.NegativeInfinity;
            public float headMetres, headDegrees, headSeveritySum, headDisplacedSeconds;
            public float heldSeconds;
            public float scoredSeconds, greenSeconds;
            public float GreenShare => scoredSeconds > 0 ? greenSeconds / scoredSeconds : 0;
            public bool startReached, returnReached, endpointReached;
            public Vector3 startOffsetSum;
            public float startSeconds;

            // Mean head-relative offset from the path start while the hand should be at the start.
            public Vector3 MeanStartOffset => startSeconds > 0 ? startOffsetSum / startSeconds : Vector3.zero;

            public void TrackStart(Vector3 offset, float dt)
            {
                startOffsetSum += offset * dt;
                startSeconds += dt;
            }

            public float PathQuality => validSeconds > 0 ? qualitySum / validSeconds : 0;
            public float MeanError => validSeconds > 0 ? errorSum / validSeconds : 0;
            public float MeanAngle => validSeconds > 0 ? angleSum / validSeconds : 0;
            public float HeadSeverity => validSeconds > 0 ? headSeveritySum / validSeconds : 0;

            public void AddFrame(float dt, float quality, float error, float angle)
            {
                validSeconds += dt;
                qualitySum += quality * dt;
                errorSum += error * dt;
                angleSum += angle * dt;
            }

            public void TrackTravel(float progress)
            {
                minimumProgress = Mathf.Min(minimumProgress, progress);
                maximumProgress = Mathf.Max(maximumProgress, progress);
            }

            // Head displacement only counts after it has lasted half a second.
            public void TrackHead(float metres, float degrees, float severity, float dt)
            {
                headMetres = Mathf.Max(headMetres, metres);
                headDegrees = Mathf.Max(headDegrees, degrees);
                headDisplacedSeconds = severity > 0 ? headDisplacedSeconds + dt : 0;
                if (headDisplacedSeconds > .5f)
                    headSeveritySum += severity * dt;
            }

            public void TrackingLost(float seconds)
            {
                headDisplacedSeconds = 0;
                trackingLostSeconds += seconds;
            }
        }
    }
}
