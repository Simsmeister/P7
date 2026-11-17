using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using P7;
using Object = UnityEngine.Object;

// Plays a whole simulated session in Play Mode and checks the result. The result is written to Temp/p7-playmode-test.txt.
// "Manual calibration" uses the menu, captures every range by hand and mixes poor and good repetitions.
// "Automatic start" checks the default start without calibration.
[InitializeOnLoad]
public static class P7PlayModeTest
{
    const string ResultFile = "Temp/p7-playmode-test.txt";
    const string RunningKey = "P7PlayModeTest.Running";
    const string AutomaticKey = "P7PlayModeTest.Automatic";
    const string TestFolder = "/tmp/p7-tests";

    static ExerciseGame game;
    static bool started, skippedDemo, skippedTutorial, sawApproach, sawPractice, sawTutorialDemo;
    static int scoredRepetition;
    static float scoredDuration, finishedWait;
    static Vector3 scoredEndpoint;
    static double deadline;

    // Entering Play Mode reloads the scripts, so the test has to hook itself up again.
    static P7PlayModeTest()
    {
        if (SessionState.GetBool(RunningKey, false))
            EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    // Leaving Play Mode before the test has finished cancels it, so it never takes over a normal Play.
    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RunningKey, false))
            return;
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        File.WriteAllText(ResultFile, "PLAY MODE TEST STOPPED");
    }

    [MenuItem("Tools/P7/Run Play Mode Test (manual calibration)")]
    static void RunManual() => Run(false);

    [MenuItem("Tools/P7/Run Play Mode Test (automatic start)")]
    static void RunAutomatic() => Run(true);

    static void Run(bool automatic)
    {
        File.WriteAllText(ResultFile, "RUNNING");
        game = null;
        started = skippedDemo = skippedTutorial = sawApproach = sawPractice = sawTutorialDemo = false;
        scoredRepetition = -1;
        finishedWait = 0;
        deadline = 0;

        EditorSceneManager.OpenScene("Assets/Scenes/P7.unity");
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(AutomaticKey, automatic);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;
        try
        {
            if (deadline == 0)
                deadline = EditorApplication.timeSinceStartup + 360;
            Check(EditorApplication.timeSinceStartup < deadline, "The test timed out" + (game
                ? " in " + game.CurrentStep + " " + game.TutorialStepName + " after " + game.CompletedRepetitions + " repetitions, frames " + Time.frameCount
                : ""));

            if (!game)
                game = Object.FindFirstObjectByType<ExerciseGame>();
            if (!game || Time.timeSinceLevelLoad < 1)
                return;

            if (SessionState.GetBool(AutomaticKey, false))
                TickAutomatic();
            else
                TickManual();
        }
        catch (Exception e)
        {
            End("PLAY MODE TEST FAILED: " + e.Message + "\n" + e.StackTrace);
        }
    }

    static void End(string result)
    {
        File.WriteAllText(ResultFile, result);
        if (result.Contains("PASSED"))
            Debug.Log(result);
        else
            Debug.LogError(result);
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
    }

    static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
    }

    // Short movements, so a whole session runs in about a minute.
    static void SpeedUp(float gapSeconds, int totalRepetitions)
    {
        game.TestMode = true;
        game.exerciseGapSeconds = gapSeconds;
        for (int i = 0; i < game.exercises.Length; i++)
        {
            var copy = Object.Instantiate(game.exercises[i]);
            copy.outwardSeconds = 1.2f;
            copy.holdSeconds = .3f;
            copy.returnSeconds = 1.2f;
            copy.restSeconds = .2f;
            game.exercises[i] = copy;
        }
        game.Settings = new SavedSettings
        {
            totalRepetitions = totalRepetitions,
            filePath = "/tmp/p7-test-player-" + Guid.NewGuid() + ".json",
        };
    }

    static GameMenu.Widget Widget(string key) => game.menu.widgets.Find(w => w.key == key);

    // The difficulty and target must not change while a repetition is being scored.
    static void CheckAttemptIsStable(bool checkDuration)
    {
        game.PathDefinition(0, out var profile, out var calibration, out _);
        if (scoredRepetition != game.CompletedRepetitions)
        {
            scoredRepetition = game.CompletedRepetitions;
            scoredDuration = profile.outwardSeconds;
            scoredEndpoint = calibration.end;
            return;
        }
        Check(Vector3.Distance(scoredEndpoint, calibration.end) < .00001f, "The target moved during a repetition");
        if (checkDuration)
            Check(Mathf.Abs(scoredDuration - profile.outwardSeconds) < .00001f, "The speed changed during a repetition");
    }

    static void TickManual()
    {
        if (!started)
        {
            SpeedUp(.3f, 18);
            CheckMenu();
            Widget("redo").button.onClick.Invoke();
            Check(game.CurrentStep == "Demo", "Recalibrate starts with the demo");
            started = true;
        }

        if (!skippedDemo && game.CurrentStep == "Demo" && game.replay.IsDemo)
        {
            game.SkipTutorialPreview();
            Check(game.CurrentStep == "CaptureStart", "Skipping the demo goes on to calibration");
            skippedDemo = true;
        }
        if (game.CurrentStep == "Tutorial")
        {
            game.SkipTutorialPreview();
            Check(game.CurrentStep == "Tutorial", "A on the first intro page does not skip the tutorial");
            game.SkipTutorial();
            Check(game.CurrentStep == "Approaching", "Skipping the tutorial starts the first line");
            skippedTutorial = true;
        }

        // The first six repetitions are poor (short movement, head turned), the rest are perfect.
        bool poor = game.CompletedRepetitions < 6;
        game.TestControllerDegrees = 135;
        game.TestHandFraction = poor ? .35f : 1;
        game.TestHeadDegrees = poor ? 30 : 85;

        if (game.IsPlaying)
        {
            Check(!game.board.gameObject.activeSelf, "The instruction board is hidden while playing");
            Check(Quaternion.Angle(game.handMarkers[game.ActiveSide].rotation, game.Frame) < .01f, "The hand marker does not rotate with the controller");
            if (!poor && game.PathClock > .1f)
                Check(game.FollowQuality >= .999f, "Controller rotation does not lower the score");
            CheckAttemptIsStable(true);
        }
        Check(!(game.IsMission && game.CurrentStep == "Ready"), "No countdown between exercises");
        if (game.CurrentStep == "Approaching")
            sawApproach = true;

        if (!game.Finished)
            return;
        finishedWait += Time.unscaledDeltaTime;
        if (finishedWait < 1.5f)
            return;

        CheckResultsPage();
        CheckSessionFiles();
        CheckSavedStart();
        End("PLAY MODE TEST PASSED (manual calibration)");
    }

    static void CheckMenu()
    {
        var menu = game.menu;
        menu.Navigate("settings");
        Check(menu.settings.activeSelf, "Settings open on one click");

        var toggle = Widget("left:0").toggle;
        toggle.isOn = false;
        Check(!game.Settings.Arm(game.exercises[0].exerciseId, 0).enabled, "The arm toggle is wired up");
        toggle.isOn = true;

        var total = Widget("total").slider;
        total.value = 12;
        Check(game.Settings.totalRepetitions == 12, "The target slider is wired up");
        total.value = 18;

        menu.Navigate("reset");
        menu.Navigate("back");
        Check(game.Settings.totalRepetitions == 18, "Cancelling the reset changes nothing");

        game.Settings.totalRepetitions = 7;
        game.Settings.devMode = true;
        game.Settings.tutorialComplete = true;
        game.Settings.Arm(game.exercises[0].exerciseId, 0).speed = 1.5f;
        menu.Navigate("reset");
        Widget("confirmReset").button.onClick.Invoke();
        var reset = SavedSettings.Load(game.Settings.filePath);
        Check(reset.totalRepetitions == 18 && !reset.devMode && !reset.tutorialComplete
            && reset.Arm(game.exercises[0].exerciseId, 0).speed == 1 && !reset.arms.Exists(a => a.calibration != null),
            "Reset saves the defaults");

        menu.Navigate("home");
        var head = game.head;
        var oldPosition = head.position;
        var oldRotation = head.rotation;
        head.SetPositionAndRotation(oldPosition + Vector3.right, Quaternion.Euler(0, 60, 0));
        menu.Recenter();
        var expected = head.position + Quaternion.Euler(0, 60, 0) * new Vector3(0, -.2f, 2.3f);
        Check(Vector3.Distance(menu.transform.position, expected) < .001f, "Recenter moves the menu in front of the player");
        head.SetPositionAndRotation(oldPosition, oldRotation);
        menu.Recenter();

        var start = Widget("start").rect;
        Check(GameMenu.Hit(start, new Ray(start.position - start.forward, start.forward), out _), "A controller ray hits the menu");
    }

    static void CheckResultsPage()
    {
        var menu = game.menu;
        var score = game.scoreFeedback;
        Check(menu.results.activeInHierarchy && !menu.home.activeInHierarchy, "A finished session shows the results");
        Check(menu.resultScore.text == score.Points.ToString("N0"), "The results show the exact final score");
        menu.Navigate("replay");
        Check(menu.results.activeInHierarchy && !game.replay.IsActive, "One press does not leave the results");
        menu.Navigate("home");
        Check(menu.results.activeInHierarchy, "A press on another button only arms that button");
        menu.AgeConfirmForTest(GameMenu.ConfirmMinSeconds);
        menu.Navigate("home");
        Check(menu.home.activeInHierarchy && !menu.results.activeInHierarchy, "A second press on Main menu leaves the results");
        Check(menu.homeStatus.text.Contains("Last run:"), "The home page keeps the last result");
        menu.Navigate("replay");
        Check(!game.replay.IsActive, "Replay is locked outside dev mode");

        Check(score.Points >= 10000 && score.TrackingBonusPoints > 0 && score.TrackingBonus.Ticks == 0, "Perfect-flow bonus is paid and reset");
        Check(skippedDemo && skippedTutorial && sawApproach, "Demo skip, tutorial skip and approach all happened");
        Check(game.CompletedRepetitions >= 18 && game.pathFeedback.Run.outcome == "victory", "18 repetitions or more, ending in victory, got " + game.CompletedRepetitions);
        Check(game.Score > 0 && game.Score < 99, "The score mixes poor and good repetitions, got " + game.Score);
        Check(score.BestCombo >= Mathf.Max(1, score.Combo), "Best streak");
    }

    static void CheckSessionFiles()
    {
        var summaries = Directory.GetFiles(TestFolder, "summary.csv", SearchOption.AllDirectories);
        Array.Sort(summaries);
        string folder = Path.GetDirectoryName(summaries[summaries.Length - 1]);
        var rows = File.ReadAllLines(Path.Combine(folder, "summary.csv"));
        Check(rows.Length == game.CompletedRepetitions + 1 && rows[0].Split(',').Length == 18, "summary.csv has one row per repetition and 18 columns");

        var audit = JsonUtility.FromJson<AdaptiveCoach.Report>(File.ReadAllText(Path.Combine(folder, "adaptive-coach.json")));
        var decisions = audit.decisions;
        Check(decisions.Length == game.CompletedRepetitions, "Every repetition has a coach decision");
        Check(Array.Exists(decisions, d => d.durationScaleUsed < 1), "The speed was adapted during play");

        int eased = 0, increased = 0, applied = 0;
        for (int i = 0; i < decisions.Length; i++)
        {
            var d = decisions[i];
            if (i < 6)
            {
                Check(d.nextRangeScale < d.rangeScaleUsed && d.nextDurationScale > d.durationScaleUsed, "Poor attempts ease the next one");
                eased++;
            }
            if (d.nextRangeScale > d.rangeScaleUsed && d.nextDurationScale < d.durationScaleUsed)
                increased++;

            var next = Array.Find(decisions, n => Array.IndexOf(decisions, n) > i && n.exerciseId == d.exerciseId && n.side == d.side);
            if (next != null)
            {
                Check(Mathf.Abs(next.durationScaleUsed - d.nextDurationScale) < .0001f && Mathf.Abs(next.rangeScaleUsed - d.nextRangeScale) < .0001f,
                    "The change is used on the next attempt of the same exercise and arm");
                applied++;
            }
        }
        Check(eased == 6 && increased > 0 && applied > 0, "The coach both eased and increased");

        Check(game.replay.LoadRecording(Path.Combine(folder, "replay.json")) && game.replay.Open(), "The replay can be loaded again");
        game.replay.Close();
    }

    static void CheckSavedStart()
    {
        var restored = SavedSettings.Load(game.Settings.filePath);
        Check(restored.tutorialComplete && !restored.arms.Exists(a => a.calibration == null || a.automaticRange || a.calibration.automatic),
            "Calibration and tutorial are saved");

        game.Settings = restored;
        game.menu.Navigate("again");
        Check(game.CurrentStep == "Approaching", "A saved start skips calibration and tutorial");

        var profile = game.ActiveProfile;
        var original = Array.Find(game.exercises, p => p.exerciseId == profile.exerciseId);
        float savedSpeed = restored.Arm(profile.exerciseId, game.ActiveSide).speed;
        Check(Mathf.Abs(profile.outwardSeconds - original.outwardSeconds / savedSpeed) < .0001f, "The saved speed is used");
    }

    static void TickAutomatic()
    {
        if (!started)
        {
            SpeedUp(.6f, 6);
            Widget("start").button.onClick.Invoke();
            Check(game.CurrentStep == "Tutorial" || game.CurrentStep == "Approaching", "Start goes straight to the game without calibration");
            Check(!game.Settings.arms.Exists(a => !a.calibration.automatic || !a.automaticRange
                    || a.range != ExerciseGame.AutomaticStartRange || a.speed != ExerciseGame.AutomaticStartSpeed
                    || a.calibration.estimateVersion != ExerciseGame.AutomaticEstimateVersion),
                "The automatic start uses gentle levels on the current estimate");
            Check(game.CurrentStep == "Tutorial" && game.TutorialStepName == "Intro", "A first start opens the tutorial");
            started = true;
        }

        string step = game.CurrentStep;
        Check(step != "Demo" && step != "CaptureStart" && step != "CaptureEnd" && step != "Ready", "The automatic start never asks for calibration");
        // The automatic test plays the whole tutorial: intro, a demo and one practice repetition per exercise.
        // The intro pages and the wrap-up wait for A, so the test presses it (it only counts after a second).
        if (game.TutorialStepName == "Intro" || game.TutorialStepName == "Outro")
            game.SkipTutorialPreview();
        if (game.IsPractice)
            sawPractice = true;
        if (game.TutorialStepName == "Demo")
            sawTutorialDemo = true;
        if (game.IsPlaying && !game.IsPractice)
        {
            Check(sawPractice && sawTutorialDemo, "The tutorial showed a demo and a practice repetition");
            Check(game.Settings.tutorialComplete, "Finishing the tutorial is saved");
            CheckAttemptIsStable(false);
        }
        if (game.IsPractice && game.IsPlaying)
            Check(game.CompletedRepetitions == 0, "Practice repetitions are not scored");

        if (!game.Finished)
            return;

        Check(game.pathFeedback.Run.outcome == "victory", "The automatic session ends in victory, got " + game.pathFeedback.Run.outcome + " after " + game.CompletedRepetitions + " repetitions, score " + game.Score);
        var restored = SavedSettings.Load(game.Settings.filePath);
        Check(!restored.arms.Exists(a => !a.calibration.automatic || !a.calibration.startAnchored
                || a.speed <= ExerciseGame.AutomaticStartSpeed || a.range <= ExerciseGame.AutomaticStartRange),
            "Start poses are anchored, and progress is learned and saved");

        game.Settings = restored;
        game.Begin();
        Check(game.CurrentStep == "Approaching", "A saved automatic start skips calibration and tutorial");
        End("PLAY MODE TEST PASSED (automatic start)");
    }
}
