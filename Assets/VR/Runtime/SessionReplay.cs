using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace P7
{
    // Records the session and plays it back on a small mannequin. The same mannequin shows the exercise demo
    // and guides the player during calibration. Only head and hands are measured; the body is estimated.
    public class SessionReplay : MonoBehaviour
    {
        const float Scale = .65f;
        const int MaxFrames = 18000; // About 5 minutes at 60 Hz.

        public Transform viewer;
        public Transform figure, headModel, chest, pelvis, targetHead, neck, waist;
        public Transform[] hands = new Transform[2];
        public Transform[] upperArms = new Transform[2], forearms = new Transform[2];
        public Transform[] thighs = new Transform[2], shins = new Transform[2], feet = new Transform[2];
        public Transform[] targetHands = new Transform[2];
        public Transform[] recordedMarkers = new Transform[2];
        public TextMesh label;

        public ReplayData Data { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsDemo { get; private set; }
        public float PlaybackTime { get; private set; }
        public bool Paused { get; private set; }

        InputActionMap input;
        bool triggerWasDown;

        // The real recording is kept aside while a demo is playing.
        ReplayData savedRecording;
        string caption;
        bool holdingPose;
        Vector3 guidanceHand;
        int guidanceSide;

        Vector3 viewOrigin, anchor;
        Quaternion viewerYaw, viewRotation;
        float viewDistance = 2, orbit, demoYaw;

        public static Vector3 MirrorPoint(Vector3 p) => new Vector3(p.x, p.y, -p.z);

        // Reaches are shown from the side, everything else as a mirror image.
        public static float TutorialYaw(bool sideView, int side) => sideView ? (side == 0 ? -90 : 90) : 0;

        // Two-bone estimate of where the elbow is, bending towards the pole direction.
        public static Vector3 EstimateElbow(Vector3 shoulder, Vector3 hand, Vector3 pole)
        {
            const float upperArm = .30f, forearm = .27f;
            Vector3 direction = hand - shoulder;
            float distance = Mathf.Clamp(direction.magnitude, .04f, upperArm + forearm - .001f);
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.down;

            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            if (bend.sqrMagnitude < .01f)
                bend = Vector3.Cross(direction, Vector3.right).normalized;

            float along = (upperArm * upperArm - forearm * forearm + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, upperArm * upperArm - along * along));
            return shoulder + direction * along + bend * height;
        }

        void Awake()
        {
            input = new InputActionMap("Session Replay");
            input.AddAction("Pause", InputActionType.Value, "<XRController>{RightHand}/trigger");
            input.AddAction("View", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
            input.AddAction("Seek", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        }

        void OnEnable()
        {
            input.Enable();
            figure.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            input.Disable();
            Close();
        }

        void OnDestroy() => input.Dispose();

        public void BeginRecording(Vector3 origin, Quaternion rotation, bool simulated)
        {
            Data = new ReplayData
            {
                origin = origin,
                rotation = rotation,
                inputSource = simulated ? "SIMULATED TEST" : "XR headset and controllers",
            };
        }

        public void Capture(ReplayFrame frame)
        {
            if (Data != null && Data.frames.Count < MaxFrames)
                Data.frames.Add(frame);
        }

        public void Save(string folder)
        {
            if (Data != null && Data.frames.Count > 0)
                File.WriteAllText(Path.Combine(folder, "replay.json"), JsonUtility.ToJson(Data));
        }

        public bool LoadRecording(string file)
        {
            try
            {
                var data = JsonUtility.FromJson<ReplayData>(File.ReadAllText(file));
                if (data == null || data.frames == null || data.frames.Count < 2 || data.frames.Count > MaxFrames)
                    return false;
                for (int i = 1; i < data.frames.Count; i++)
                    if (data.frames[i].time < data.frames[i - 1].time)
                        return false;

                Close();
                Data = data;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Replay load: " + e.Message);
                return false;
            }
        }

        bool LoadLatest()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Sessions");
            if (!Directory.Exists(folder))
                return false;

            var files = Directory.GetFiles(folder, "replay.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = files.Length - 1; i >= 0; i--)
                if (LoadRecording(files[i]))
                    return true;
            return false;
        }

        public bool Open()
        {
            if ((Data == null || Data.frames.Count < 2) && !LoadLatest())
                return false;

            viewOrigin = viewer.position;
            viewerYaw = Quaternion.Euler(0, viewer.eulerAngles.y, 0);
            viewDistance = 2;
            orbit = 0;
            UpdateView();

            PlaybackTime = 0;
            Paused = false;
            IsActive = true;
            triggerWasDown = false;
            figure.gameObject.SetActive(true);
            return true;
        }

        public void Close()
        {
            demoYaw = 0;
            holdingPose = false;
            IsActive = false;
            if (figure)
                figure.gameObject.SetActive(false);
            if (IsDemo)
            {
                Data = savedRecording;
                savedRecording = null;
                IsDemo = false;
            }
        }

        public void OpenExerciseDemo(ReplayData demonstration, string demoCaption, bool sideView)
        {
            savedRecording = Data;
            Data = demonstration;
            caption = demoCaption;
            demoYaw = TutorialYaw(sideView, demonstration.frames[0].activeSide);
            Open();
            IsDemo = true;
        }

        // Freezes the demo at a pose, used while the player copies it during calibration.
        public void HoldDemoAt(float time, string demoCaption)
        {
            PlaybackTime = time;
            Paused = true;
            holdingPose = true;
            caption = demoCaption;
            orbit = 0;
        }

        public void UpdateGuidance(Vector3 hand, int side, string guidanceCaption)
        {
            guidanceHand = hand;
            guidanceSide = side;
            caption = guidanceCaption;
        }

        void UpdateView()
        {
            anchor = viewOrigin + viewerYaw * new Vector3(0, -.05f, viewDistance);
            viewRotation = viewerYaw * Quaternion.Euler(0, 180f + orbit, 0);
        }

        void Update()
        {
            if (!IsActive || Data == null)
                return;

            if (!IsDemo)
                HandleViewerInput();
            else if (!Paused)
                PlaybackTime += Time.unscaledDeltaTime;
            UpdateView();

            float end = Data.frames[Data.frames.Count - 1].time;
            if (PlaybackTime >= end)
            {
                if (IsDemo)
                {
                    Close();
                    return;
                }
                PlaybackTime = end;
                Paused = true;
            }
            PlaybackTime = Mathf.Clamp(PlaybackTime, 0, end);

            FindFrames(out var a, out var b, out float t);
            DrawFigure(a, b, t);
            UpdateLabel(a, end);
        }

        void HandleViewerInput()
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 view = input["View"].ReadValue<Vector2>();
            if (Mathf.Abs(view.x) > .2f)
                orbit += view.x * 65 * dt;
            if (Mathf.Abs(view.y) > .2f)
                viewDistance = Mathf.Clamp(viewDistance - view.y * .7f * dt, .65f, 3.5f);

            bool trigger = input["Pause"].ReadValue<float>() > .5f;
            if (trigger && !triggerWasDown)
                Paused = !Paused;
            triggerWasDown = trigger;

            float seek = input["Seek"].ReadValue<Vector2>().x;
            if (Mathf.Abs(seek) > .25f)
                PlaybackTime += seek * 4 * dt;
            else if (!Paused)
                PlaybackTime += dt;
        }

        // The two recorded frames around the playback time, and how far between them we are.
        void FindFrames(out ReplayFrame a, out ReplayFrame b, out float t)
        {
            var frames = Data.frames;
            int low = 0, high = frames.Count - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (frames[middle].time <= PlaybackTime)
                    low = middle;
                else
                    high = middle - 1;
            }
            a = frames[low];
            b = frames[Mathf.Min(low + 1, frames.Count - 1)];

            // Do not interpolate across a recording gap.
            bool shortGap = b.time > a.time && b.time - a.time < .2f;
            t = shortGap ? Mathf.InverseLerp(a.time, b.time, PlaybackTime) : 0;
        }

        void DrawFigure(ReplayFrame a, ReplayFrame b, float t)
        {
            Vector3 up = Data.rotation * Vector3.up;
            Vector3 right = Data.rotation * Vector3.right;
            Vector3 forward = Data.rotation * Vector3.forward;

            Vector3 head = Vector3.Lerp(a.head, b.head, t);
            Quaternion headRotation = Quaternion.Slerp(a.headRotation, b.headRotation, t);
            bool headOk = a.headValid && b.headValid;

            headModel.gameObject.SetActive(headOk);
            chest.gameObject.SetActive(headOk);
            pelvis.gameObject.SetActive(headOk);
            neck.gameObject.SetActive(headOk);
            waist.gameObject.SetActive(headOk);
            if (headOk)
                headModel.SetPositionAndRotation(Map(head), Map(headRotation));

            Vector3 chestCentre = head - up * .35f;
            Vector3 hip = Data.origin - up * .85f + Vector3.ProjectOnPlane(head - Data.origin, up) * .35f;
            Segment(neck, Map(head - up * .10f), Map(chestCentre + up * .12f), .09f);
            Segment(waist, Map(chestCentre - up * .12f), Map(hip + up * .05f), .18f);
            chest.SetPositionAndRotation(Map(chestCentre), Map(Data.rotation));
            pelvis.SetPositionAndRotation(Map(hip), Map(Data.rotation));
            targetHead.position = Map(Data.origin);

            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                float sign = left ? -1 : 1;
                bool handOk = headOk && (left ? a.leftValid && b.leftValid : a.rightValid && b.rightValid);
                Vector3 hand = Vector3.Lerp(left ? a.left : a.right, left ? b.left : b.right, t);
                Quaternion handRotation = Quaternion.Slerp(left ? a.leftRotation : a.rightRotation, left ? b.leftRotation : b.rightRotation, t);
                Vector3 target = Vector3.Lerp(left ? a.targetLeft : a.targetRight, left ? b.targetLeft : b.targetRight, t);
                bool isActiveArm = s == a.activeSide;

                hands[s].gameObject.SetActive(handOk);
                upperArms[s].gameObject.SetActive(handOk);
                forearms[s].gameObject.SetActive(handOk);
                recordedMarkers[s].gameObject.SetActive(handOk && isActiveArm);
                Quaternion markerRotation = IsDemo ? Quaternion.Euler(0, viewer.eulerAngles.y, 0) : Map(handRotation);
                recordedMarkers[s].SetPositionAndRotation(Map(hand), markerRotation);

                Vector3 shoulder = chestCentre + up * .10f + right * sign * .18f;
                if (IsDemo && isActiveArm)
                    shoulder += forward * Mathf.Lerp(a.illustrativeShoulderOffset, b.illustrativeShoulderOffset, t);
                Vector3 pole = right * sign * .3f - forward * .2f - up * .4f;
                Vector3 elbow = EstimateElbow(shoulder, hand, pole);
                if (handOk)
                {
                    hands[s].SetPositionAndRotation(Map(hand), Map(handRotation));
                    Segment(upperArms[s], Map(shoulder), Map(elbow), .035f);
                    Segment(forearms[s], Map(elbow), Map(hand), .028f);
                }

                targetHands[s].gameObject.SetActive(isActiveArm);
                targetHands[s].position = Map(holdingPose && s == guidanceSide ? guidanceHand : target);

                Vector3 legTop = hip + right * sign * .11f;
                Vector3 foot = Data.origin - up * 1.55f + right * sign * .12f + forward * .04f;
                Vector3 knee = (legTop + foot) * .5f + forward * .04f;
                thighs[s].gameObject.SetActive(headOk);
                shins[s].gameObject.SetActive(headOk);
                feet[s].gameObject.SetActive(headOk);
                Segment(thighs[s], Map(legTop), Map(knee), .045f);
                Segment(shins[s], Map(knee), Map(foot), .035f);
                feet[s].SetPositionAndRotation(Map(foot), Map(Data.rotation));
            }
        }

        void UpdateLabel(ReplayFrame frame, float end)
        {
            label.transform.SetPositionAndRotation(anchor + Vector3.up * .40f, Quaternion.Euler(0, viewer.eulerAngles.y, 0));
            if (IsDemo)
            {
                label.text = caption ?? "";
                return;
            }

            string title = Data.inputSource == "SIMULATED TEST" ? "SIMULATED REPLAY  " : "REPLAY  ";
            label.text = title + PlaybackTime.ToString("F1") + " / " + end.ToString("F1") + "s  " + (Paused ? "PAUSED" : "PLAYING")
                + "\nHead + hands recorded. BODY / ELBOWS / LEGS ESTIMATED."
                + "\nTransparent spheres = targets."
                + "\nRight trigger: pause/play  |  Right stick: seek  |  B: return"
                + "\nLeft stick: rotate / closer-further. You can also walk around."
                + "\n" + frame.exerciseId + "  " + frame.phase + "  " + (frame.activeSide == 0 ? "LEFT" : "RIGHT")
                + " | gaps show last recorded pose";
        }

        Quaternion DemoRotation => viewerYaw * Quaternion.Euler(0, demoYaw, 0);

        // From recorded world space to the mannequin in front of the viewer. The demo is mirrored.
        Vector3 Map(Vector3 p)
        {
            Vector3 local = Quaternion.Inverse(Data.rotation) * (p - Data.origin);
            Vector3 placed = IsDemo ? DemoRotation * MirrorPoint(local) : viewRotation * local;
            return anchor + placed * Scale;
        }

        Quaternion Map(Quaternion q)
        {
            Quaternion local = Quaternion.Inverse(Data.rotation) * q;
            if (!IsDemo)
                return viewRotation * local;
            return DemoRotation * Quaternion.LookRotation(MirrorPoint(local * Vector3.forward), MirrorPoint(local * Vector3.up));
        }

        static void Segment(Transform bone, Vector3 from, Vector3 to, float radius)
        {
            bone.position = (from + to) * .5f;
            bone.rotation = Quaternion.FromToRotation(Vector3.up, to - from);
            bone.localScale = new Vector3(radius * Scale, Vector3.Distance(from, to) * .5f, radius * Scale);
        }
    }
}
