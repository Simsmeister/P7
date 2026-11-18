using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace P7
{
    // The world-space menu. Controller rays (or the mouse in the editor) are hit-tested against the widgets here,
    // because the scene has no EventSystem.
    public class GameMenu : MonoBehaviour
    {
        enum Page { Home, Settings, ExerciseDetails, ResetConfirmation, Results, Therapist }

        static readonly Color ButtonNormal = new Color(.1f, .24f, .33f);
        static readonly Color ButtonHover = new Color(.15f, .55f, .65f);
        static readonly Color ButtonDisabled = new Color(.15f, .18f, .22f);
        static readonly Color Backdrop = new Color(.018f, .035f, .065f, .98f);
        static readonly Color ResultsBackdrop = new Color(.018f, .035f, .065f, .65f);

        [Serializable]
        public class Widget
        {
            public string key;
            public RectTransform rect;
            public Text label;
            public Button button;
            public Toggle toggle;
            public Slider slider;
        }

        public ExerciseGame game;
        public RectTransform canvasRoot;
        public GameObject home, settings, details, resetConfirmation, results;
        public Text resultTitle, resultScore, resultAccuracy, resultStreak, resultMoves, resultBonus;
        public Text homeStatus, detailTitle, detailStatus, footer;
        public LineRenderer pointer;
        public List<Widget> widgets = new List<Widget>();

        Page page;
        int exercise;
        bool wasVisible, wasDown;
        Widget dragging;
        float refreshTimer;
        bool resultsShown;
        float resultsAge;
        int recenterFrames;
        readonly List<XRInputSubsystem> xrSystems = new List<XRInputSubsystem>();

        // Leaving the results page takes two presses on the same button (outside dev mode), so a participant
        // cannot close the score by accident before the test leader has read it (pilot test 1, participant 3).
        public const float ConfirmMinSeconds = .4f, ConfirmMaxSeconds = 4;
        string armedKey = "";
        float armedAge;
        readonly Dictionary<string, string> buttonText = new Dictionary<string, string>();
        // Shown on the home page after a run, so the result can still be read if the results page was left.
        string lastResult = "";

        // Physiotherapist's view of the last session (CLAUDE.md section 14). Not shown to the player: it opens from a
        // THERAPIST VIEW button on the home page that takes two presses (outside dev mode), like the test leader's
        // buttons on the results page. It is drawn 0.8 m in front of the menu, so the text is large enough to read.
        // Built at runtime, so the menu prefab is unchanged.
        TherapistView therapistView;

        void Awake()
        {
            therapistView = new GameObject("Therapist View").AddComponent<TherapistView>();
            therapistView.transform.SetParent(transform, false);
            therapistView.transform.localPosition = new Vector3(-TherapistView.Width / 2, .6f, -.8f);
            therapistView.Hide();
            AddTherapistBack();
            AddTutorialToggle();
            AddTherapistButton();
            foreach (var widget in widgets)
            {
                string key = widget.key;
                if (widget.button)
                    widget.button.onClick.AddListener(() => Navigate(key));
                if (widget.toggle)
                    widget.toggle.onValueChanged.AddListener(on => Change(key, on ? 1 : 0));
                if (widget.slider)
                    widget.slider.onValueChanged.AddListener(value => Change(key, value));
            }
        }

        // The "TUTORIAL ALWAYS" switch is a runtime copy of the DEV MODE switch, placed beside it,
        // so the menu prefab does not need a new widget.
        void AddTutorialToggle()
        {
            var dev = widgets.Find(w => w.key == "dev");
            if (dev == null || !dev.rect || widgets.Exists(w => w.key == "tutorial"))
                return;

            var copy = Instantiate(dev.rect.gameObject, dev.rect.parent, false);
            copy.name = "tutorial";
            var rect = (RectTransform)copy.transform;
            float half = dev.rect.sizeDelta.x / 2 + 10;
            rect.anchoredPosition = dev.rect.anchoredPosition + new Vector2(half, 0);
            dev.rect.anchoredPosition -= new Vector2(half, 0);
            widgets.Add(new Widget
            {
                key = "tutorial",
                rect = rect,
                label = copy.GetComponentInChildren<Text>(true),
                toggle = copy.GetComponentInChildren<Toggle>(true),
            });
        }

        // BACK TO MENU, just above the therapist panel: a copy of a menu "back" button on its own small world-space
        // canvas (the panel is not a canvas), at the menu canvas's scale.
        void AddTherapistBack()
        {
            var back = widgets.Find(w => w.key == "back" && w.button && w.rect);
            if (back == null)
                return;
            var holder = new GameObject("Therapist Back", typeof(RectTransform), typeof(Canvas));
            holder.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            holder.transform.SetParent(therapistView.transform, false);
            holder.transform.localPosition = new Vector3(TherapistView.Width / 2, .09f, -.01f);
            holder.transform.localScale = canvasRoot.lossyScale;
            var copy = Instantiate(back.rect.gameObject, holder.transform, false);
            copy.name = "therapistBack";
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 320), rect.sizeDelta.y);
            var label = copy.GetComponentInChildren<Text>(true);
            if (label)
                label.text = "BACK TO MENU";
            widgets.Add(new Widget { key = "therapistBack", rect = rect, label = label, button = copy.GetComponentInChildren<Button>(true) });
        }

        // A runtime copy of the home page's LAST REPLAY button, which is split in two halves to make room.
        void AddTherapistButton()
        {
            var replay = widgets.Find(w => w.key == "replay" && w.rect && w.rect.IsChildOf(home.transform));
            if (replay == null || widgets.Exists(w => w.key == "therapist"))
                return;

            var copy = Instantiate(replay.rect.gameObject, replay.rect.parent, false);
            copy.name = "therapist";
            var rect = (RectTransform)copy.transform;
            float half = replay.rect.sizeDelta.x / 4 + 5;
            rect.sizeDelta = replay.rect.sizeDelta = new Vector2(replay.rect.sizeDelta.x / 2 - 10, replay.rect.sizeDelta.y);
            rect.anchoredPosition = replay.rect.anchoredPosition + new Vector2(half, 0);
            replay.rect.anchoredPosition -= new Vector2(half, 0);
            var label = copy.GetComponentInChildren<Text>(true);
            if (label)
                label.text = "THERAPIST VIEW";
            widgets.Add(new Widget { key = "therapist", rect = rect, label = label, button = copy.GetComponentInChildren<Button>(true) });
        }

        void OnEnable()
        {
            SubsystemManager.GetSubsystems(xrSystems);
            foreach (var xr in xrSystems)
                xr.trackingOriginUpdated += OnRecentered;
        }

        void OnDisable()
        {
            foreach (var xr in xrSystems)
                xr.trackingOriginUpdated -= OnRecentered;
        }

        // Wait a few frames after a headset recenter, so the head pose has settled.
        void OnRecentered(XRInputSubsystem xr) => recenterFrames = 3;

        void OnApplicationFocus(bool focus)
        {
            if (focus)
                recenterFrames = 3;
        }

        public void Recenter()
        {
            if (game.MenuVisible)
                PlaceInFrontOfPlayer();
        }

        void PlaceInFrontOfPlayer()
        {
            var yaw = Quaternion.Euler(0, game.head.eulerAngles.y, 0);
            transform.SetPositionAndRotation(game.head.position + yaw * new Vector3(0, -.2f, 2.3f), yaw);
        }

        // Whether a button on the results page needs a second press before it acts.
        public static bool NeedsConfirm(string key, bool onResults, bool devMode)
        {
            return !devMode && (key == "therapist" || onResults && (key == "home" || key == "again" || key == "replay"));
        }

        // A second press counts only if it comes on the same button, not too fast (a held or bouncing trigger)
        // and not too late.
        public static bool Confirmed(string armed, string key, float age)
        {
            return armed == key && age >= ConfirmMinSeconds && age <= ConfirmMaxSeconds;
        }

        // For the Play Mode test, which cannot wait between two presses.
        public void AgeConfirmForTest(float seconds) => armedAge += seconds;

        public void Navigate(string key)
        {
            if (NeedsConfirm(key, page == Page.Results, game.Settings.devMode))
            {
                if (!Confirmed(armedKey, key, armedAge))
                {
                    armedKey = key;
                    armedAge = 0;
                    Refresh();
                    return;
                }
            }
            armedKey = "";

            // Replays are for the team, not for participants: only in dev mode.
            if (key == "replay" && !game.Settings.devMode)
                return;

            switch (key)
            {
                case "therapist":
                    page = Page.Therapist;
                    break;
                case "recenter":
                    Recenter();
                    return;
                case "start":
                case "again":
                    resultsShown = false;
                    page = Page.Home;
                    game.Begin();
                    return;
                case "redo":
                    game.Begin(true);
                    return;
                case "replay":
                    game.ShowLastReplay();
                    return;
                case "confirmReset":
                    game.ResetSavedSettings();
                    page = Page.Home;
                    break;
                case "reset":
                    page = Page.ResetConfirmation;
                    break;
                case "settings":
                case "back":
                    page = Page.Settings;
                    break;
                default:
                    if (key.StartsWith("exercise:"))
                    {
                        exercise = int.Parse(key.Substring("exercise:".Length));
                        page = Page.ExerciseDetails;
                    }
                    else
                    {
                        page = Page.Home;
                    }
                    break;
            }
            Refresh();
        }

        public void Change(string key, float value)
        {
            var data = game.Settings;
            if (key == "total")
            {
                data.totalRepetitions = Mathf.RoundToInt(value);
            }
            else if (key == "dev")
            {
                data.devMode = value > .5f;
            }
            else if (key == "tutorial")
            {
                data.tutorialEveryStart = value > .5f;
            }
            else if (key.StartsWith("left:") || key.StartsWith("right:"))
            {
                int index = int.Parse(key.Split(':')[1]);
                int side = key.StartsWith("left:") ? 0 : 1;
                data.Arm(game.exercises[index].exerciseId, side).enabled = value > .5f;
            }
            else
            {
                string id = game.exercises[exercise].exerciseId;
                if (key == "limit")
                {
                    data.Limit(id).fasterPercent = value;
                }
                else if (data.devMode)
                {
                    var arm = data.Arm(id, key.EndsWith("Left") ? 0 : 1);
                    if (key.StartsWith("speed"))
                        arm.devSpeed = value / 100;
                    else if (key.StartsWith("range"))
                        arm.devRange = value / 100;
                }
            }
            data.Save();
            Refresh();
        }

        void Refresh()
        {
            var data = game.Settings;

            home.SetActive(page == Page.Home);
            settings.SetActive(page == Page.Settings);
            details.SetActive(page == Page.ExerciseDetails);
            resetConfirmation.SetActive(page == Page.ResetConfirmation);
            results.SetActive(page == Page.Results);
            canvasRoot.GetComponent<Image>().color = page == Page.Results ? ResultsBackdrop : Backdrop;

            if (page == Page.Results)
                RefreshResults();
            if (page == Page.Therapist)
                therapistView.Show(game.RepLog, resultTitle.font);
            else
                therapistView.Hide();

            homeStatus.text = "Participant " + game.Participant.Id + "  |  "
                + (data.devMode ? "DEV MODE - fixed overrides, normal progress protected" : "Your personal pace and range are saved automatically")
                + "\nEnergy fills after about " + data.totalRepetitions + " strong movements"
                + (lastResult != "" ? "\n" + lastResult : "");
            footer.text = FooterText(data);

            exercise = Mathf.Clamp(exercise, 0, game.exercises.Length - 1);
            var profile = game.exercises[exercise];
            var left = data.Arm(profile.exerciseId, 0);
            var right = data.Arm(profile.exerciseId, 1);
            detailTitle.text = profile.displayName.ToUpper();
            detailStatus.text = "Saved: Left " + Percent(left.speed) + " speed / " + Percent(left.range) + " range   |   Right "
                + Percent(right.speed) + " / " + Percent(right.range) + "\n"
                + (data.devMode ? "Dev sliders override normal presets. Learning is off." : "Dev sliders are disabled. Turn Dev mode on in Settings to use them.");

            foreach (var widget in widgets)
                RefreshWidget(widget, data, profile.exerciseId, left, right);
        }

        void RefreshResults()
        {
            string outcome = game.pathFeedback.Run.outcome;
            resultTitle.text = outcome == "victory" ? "LEVEL COMPLETE"
                : outcome == "session_limit" ? "SESSION COMPLETE"
                : outcome == "defeat" ? "RUN ENDED"
                : "SESSION STOPPED";
            resultAccuracy.text = game.Score.ToString("F0") + "%";
            resultStreak.text = game.scoreFeedback.BestCombo.ToString();
            resultMoves.text = game.CompletedRepetitions.ToString();
            resultBonus.text = game.scoreFeedback.TrackingBonusPoints.ToString("N0") + " POINTS FROM PERFECT FLOW";
            lastResult = "Last run: " + resultTitle.text + ", " + game.scoreFeedback.Points.ToString("N0") + " points, "
                + resultAccuracy.text + " accuracy, best streak " + resultStreak.text + ", " + resultMoves.text + " moves";
        }

        string FooterText(SavedSettings data)
        {
            if (page == Page.ResetConfirmation)
                return "Confirming starts a new participant: " + game.Participant.NextId;
            if (page == Page.Therapist)
                return "Therapist view of the last session. BACK TO MENU or B to go back.";
            if (!string.IsNullOrEmpty(data.error))
                return "Could not save settings. Progress may not persist.";
            if (!string.IsNullOrEmpty(game.Participant.error))
                return game.Participant.error;
            if (!game.AnyEnabled)
                return "Enable at least one exercise and arm in Settings.";
            if (page == Page.Results)
                return data.devMode ? "Point with either controller and press trigger."
                    : "Test leader: press a button twice to leave the results.";
            return "Point with either controller and press trigger. Drag sliders to adjust.";
        }

        void RefreshWidget(Widget widget, SavedSettings data, string exerciseId, ArmSettings left, ArmSettings right)
        {
            string key = widget.key;
            if (key == "start" || key == "again" || key == "redo")
                widget.button.interactable = game.AnyEnabled;
            if (key == "replay")
                widget.button.interactable = data.devMode;
            if (key == "therapist")
                widget.button.interactable = game.RepLog.Count > 0;

            if (widget.button && widget.label)
            {
                if (!buttonText.ContainsKey(key))
                    buttonText[key] = widget.label.text;
                bool armed = (page == Page.Results || key == "therapist") && armedKey == key && armedAge <= ConfirmMaxSeconds;
                widget.label.text = armed ? "PRESS AGAIN" : buttonText[key];
            }

            if (widget.toggle)
            {
                if (key == "dev")
                {
                    widget.toggle.SetIsOnWithoutNotify(data.devMode);
                }
                else if (key == "tutorial")
                {
                    widget.toggle.SetIsOnWithoutNotify(data.tutorialEveryStart);
                }
                else
                {
                    int index = int.Parse(key.Split(':')[1]);
                    int side = key.StartsWith("left:") ? 0 : 1;
                    widget.toggle.SetIsOnWithoutNotify(data.Arm(game.exercises[index].exerciseId, side).enabled);
                }
                string name = key == "dev" ? "DEV MODE" : key == "tutorial" ? "TUTORIAL ALWAYS" : key.StartsWith("left:") ? "LEFT" : "RIGHT";
                widget.label.text = name + "   " + (widget.toggle.isOn ? "ON" : "OFF");
            }

            if (!widget.slider)
                return;

            float value;
            string title;
            switch (key)
            {
                case "total": value = data.totalRepetitions; title = "Energy target (strong movements)"; break;
                case "limit": value = data.Limit(exerciseId).fasterPercent; title = "Maximum speed increase (normal mode)"; break;
                case "speedLeft": value = left.devSpeed * 100; title = "Dev left speed"; break;
                case "speedRight": value = right.devSpeed * 100; title = "Dev right speed"; break;
                case "rangeLeft": value = left.devRange * 100; title = "Dev left range"; break;
                default: value = right.devRange * 100; title = "Dev right range"; break;
            }
            widget.slider.SetValueWithoutNotify(value);
            widget.slider.interactable = key == "total" || key == "limit" || data.devMode;
            widget.label.text = title + ": " + value.ToString("F0") + (key == "total" ? "" : "%");
            widget.label.color = widget.slider.interactable ? Color.white : new Color(.5f, .55f, .6f);
        }

        static string Percent(float fraction) => (fraction * 100).ToString("F0") + "%";

        public static bool Hit(RectTransform rect, Ray ray, out Vector2 local)
        {
            local = Vector2.zero;
            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(ray, out float distance) || distance < 0)
                return false;

            Vector3 point = rect.InverseTransformPoint(ray.GetPoint(distance));
            local = new Vector2(point.x, point.y);
            return rect.rect.Contains(local);
        }

        void LateUpdate()
        {
            if (!game.Finished)
                resultsShown = false;
            if (recenterFrames > 0 && --recenterFrames == 0)
                Recenter();

            bool visible = game.MenuVisible;
            if (page == Page.Therapist && (game.CloseTherapistView || !visible))
            {
                page = Page.Home;
                Refresh();
            }
            game.therapistViewOpen = visible && page == Page.Therapist;
            canvasRoot.gameObject.SetActive(visible);
            pointer.gameObject.SetActive(false);
            if (!visible)
            {
                wasVisible = false;
                wasDown = false;
                dragging = null;
                return;
            }

            if (!wasVisible)
                OnMenuOpened();

            if (page == Page.Results)
                AnimateScore();
            armedAge += Time.unscaledDeltaTime;

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer > .3f)
            {
                refreshTimer = 0;
                Refresh();
            }

            if (GetPointer(out Ray ray, out bool down))
                HandlePointer(ray, down);
            else
            {
                wasDown = false;
                dragging = null;
            }
        }

        void OnMenuOpened()
        {
            // After a session the results page opens once; returning from a replay keeps the page.
            if (!game.Finished)
                page = Page.Home;
            else if (!resultsShown)
            {
                page = Page.Results;
                resultsShown = true;
                resultsAge = 0;
            }

            // Ignore a trigger that is still held from the last press.
            wasDown = true;
            armedKey = "";
            PlaceInFrontOfPlayer();
            Refresh();
            wasVisible = true;
        }

        // The final score counts up over about a second.
        void AnimateScore()
        {
            resultsAge += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(resultsAge / 1.1f);
            float eased = 1 - Mathf.Pow(1 - u, 3);
            resultScore.text = Mathf.RoundToInt(game.scoreFeedback.Points * eased).ToString("N0");
        }

        // The right controller is used unless only the left one is tracked or its trigger is pressed.
        // In the editor without a headset the mouse is used.
        bool GetPointer(out Ray ray, out bool down)
        {
            if (!XRSettings.isDeviceActive && Mouse.current != null)
            {
                var camera = game.head.GetComponent<Camera>();
                ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                down = Mouse.current.leftButton.isPressed;
                return true;
            }

            bool right = game.MenuRay(1, out ray, out down);
            bool left = game.MenuRay(0, out var leftRay, out bool leftDown);
            if (left && (!right || leftDown))
            {
                ray = leftRay;
                down = leftDown;
                return true;
            }
            return right;
        }

        void HandlePointer(Ray ray, bool down)
        {
            var plane = new Plane(canvasRoot.forward, canvasRoot.position);
            bool onCanvas = plane.Raycast(ray, out float distance) && distance > 0;
            if (onCanvas)
            {
                pointer.gameObject.SetActive(true);
                pointer.SetPosition(0, ray.origin);
                pointer.SetPosition(1, ray.GetPoint(distance));
            }

            bool pressed = down && !wasDown;
            wasDown = down;
            if (!down)
                dragging = null;

            // In the therapist view only BACK TO MENU (or B) goes back, so pointing around does not close it by accident.
            // If that button is missing, any trigger press goes back, so the view can never trap the test leader.
            if (page == Page.Therapist && pressed && !widgets.Exists(w => w.key == "therapistBack"))
            {
                page = Page.Home;
                Refresh();
                return;
            }

            foreach (var widget in widgets)
            {
                if (!widget.rect.gameObject.activeInHierarchy)
                    continue;

                bool hit = Hit(widget.rect, ray, out _);
                if (widget.button)
                {
                    var button = widget.button;
                    button.image.color = !button.interactable ? ButtonDisabled : hit ? ButtonHover : ButtonNormal;
                    if (hit && pressed && button.interactable)
                    {
                        button.onClick.Invoke();
                        break;
                    }
                }

                if (widget.toggle && hit && pressed)
                {
                    widget.toggle.isOn = !widget.toggle.isOn;
                    break;
                }

                if (widget.slider && widget.slider.interactable)
                {
                    if (hit && pressed)
                        dragging = widget;
                    if (dragging == widget && down)
                    {
                        Vector3 point = widget.rect.InverseTransformPoint(ray.GetPoint(Mathf.Max(0, distance)));
                        widget.slider.normalizedValue = Mathf.InverseLerp(widget.rect.rect.xMin, widget.rect.rect.xMax, point.x);
                        break;
                    }
                }
            }
        }
    }
}
