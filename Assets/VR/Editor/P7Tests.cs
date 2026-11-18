using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using P7;
using Object = UnityEngine.Object;

// Checks of the game rules. Run them from Tools > P7 > Run Checks; the result is logged and written to Temp/p7-checks.txt.
public static class P7Tests
{
    public const string ResultFile = "Temp/p7-checks.txt";

    [MenuItem("Tools/P7/Run Checks")]
    public static void RunChecks()
    {
        try
        {
            CheckExerciseRules();
            CheckCalibration();
            CheckStartShift();
            CheckScoring();
            CheckSessionProgress();
            CheckParticipant();
            CheckNeuralModel();
            CheckAdaptiveCoach();
            CheckPlaylist();
            CheckIteration2();
            Report("CHECKS PASSED");
        }
        catch (Exception e)
        {
            Report("CHECKS FAILED: " + e.Message + "\n" + e.StackTrace);
        }
    }

    public static void Report(string result)
    {
        File.WriteAllText(ResultFile, result);
        if (result.Contains("PASSED"))
            Debug.Log(result);
        else
            Debug.LogError(result);
    }

    static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
    }

    static void CheckExerciseRules()
    {
        foreach (ExerciseMovement movement in Enum.GetValues(typeof(ExerciseMovement)))
        {
            var a = new Vector3(.2f, 0, .3f);
            var b = new Vector3(.4f, .3f, .1f);
            Check(Vector3.Distance(ExerciseProfile.Path(movement, a, b, Vector3.zero, 0), a) < .0001f, "Path starts at the start");
            Check(Vector3.Distance(ExerciseProfile.Path(movement, a, b, Vector3.zero, 1), b) < .0001f, "Path ends at the end");
        }

        var profile = ScriptableObject.CreateInstance<ExerciseProfile>();
        Check(profile.Quality(.02f) == 1 && profile.Quality(.2f) == 0, "Quality bands");
        Check(profile.Progress(0) == 0 && profile.Progress(profile.outwardSeconds) == 1 && profile.Progress(profile.Cycle) == 0, "Movement phases");
        Object.DestroyImmediate(profile);

        for (int side = 0; side < 2; side++)
            for (int i = -10; i <= 10; i++)
            {
                Vector3 moved = ExerciseGame.ProjectToFront(Vector3.zero, Vector3.one * i, side);
                Vector3 still = ExerciseGame.ProjectToFront(Vector3.zero, Vector3.zero, side);
                Check(Vector3.Distance(moved - still, Vector3.one * i) < .0001f, "The line in front keeps the hand's movement on all three axes");
            }

        var point = new Vector3(-.4f, .2f, 1.6f);
        Check(PathStream.Approach(point, Vector3.forward, 0, .55f) == point, "An arrived line lies on the scored target");
        Check(Vector3.Distance(PathStream.Approach(point, Vector3.forward, 2.5f, .55f), point + Vector3.forward * 1.375f) < .0001f, "Approach distance");

        var mirrored = SessionReplay.MirrorPoint(new Vector3(-.3f, 1, .5f));
        Check(mirrored == new Vector3(-.3f, 1, -.5f), "The mirrored demo keeps left and right on screen");
        for (int side = 0; side < 2; side++)
            Check(Mathf.Abs(SessionReplay.TutorialYaw(true, side)) == 90 && SessionReplay.TutorialYaw(false, side) == 0, "Only reaches use the side view");
    }

    static void CheckStartShift()
    {
        var shift = Vector3.zero;
        Check(AdaptiveCoach.NextStartShift(shift, new Vector3(0, .02f, 0), 1) == shift, "Small start offsets are ignored");
        Check(Mathf.Abs(AdaptiveCoach.NextStartShift(shift, new Vector3(0, .06f, 0), .5f).y - .03f) < .0001f, "The network's follow fraction scales the step");
        Check(AdaptiveCoach.NextStartShift(shift, new Vector3(0, .06f, 0), 0) == shift, "Follow 0 keeps the start");
        Check(Mathf.Abs(AdaptiveCoach.NextStartShift(shift, new Vector3(0, -.5f, 0), 1).magnitude - AdaptiveCoach.StartShiftStep) < .0001f, "One step is capped");
        for (int i = 0; i < 20; i++)
            shift = AdaptiveCoach.NextStartShift(shift, new Vector3(0, -.5f, 0), 1);
        Check(shift.magnitude <= AdaptiveCoach.StartShiftLimit + .0001f, "The total shift is capped");
        Check(AdaptiveCoach.NextStartShift(Vector3.zero, new Vector3(float.NaN, 0, 0), 1) == Vector3.zero
            && AdaptiveCoach.NextStartShift(Vector3.zero, Vector3.up * .1f, float.NaN) == Vector3.zero, "Invalid offsets are ignored");
        Check(AdaptiveCoach.StartOffsetSeverity(Vector3.up * .1f) == .5f && AdaptiveCoach.StartOffsetSeverity(Vector3.up) == 1, "Start offset normalization");

        Check(ExerciseGame.BodyScaleFor(ExerciseGame.ReferenceEyeHeight) == 1 && ExerciseGame.BodyScaleFor(.5f) == 1
            && ExerciseGame.BodyScaleFor(float.NaN) == 1, "Eye height outside the standing range uses the reference body");
        Check(ExerciseGame.PracticeSpeed(.2f) == .6f && ExerciseGame.PracticeSpeed(.9f) == 1 && ExerciseGame.PracticeSpeed(float.NaN) == .6f,
            "Practice sets a starting speed of 0.6-1");
        Check(ExerciseGame.PracticeReach(0, .7f) == .7f && ExerciseGame.PracticeReach(0, 1.3f) == 1
            && ExerciseGame.PracticeReach(float.PositiveInfinity, float.NegativeInfinity) == 0, "Practice reach is 0-1");
        Check(ExerciseGame.BodyScaleFor(1.4f) < 1 && ExerciseGame.BodyScaleFor(1.9f) > 1, "Shorter and taller bodies scale the estimate");
    }

    static void CheckCalibration()
    {
        var profile = ScriptableObject.CreateInstance<ExerciseProfile>();
        profile.exerciseId = "automatic-check";
        foreach (ExerciseMovement movement in Enum.GetValues(typeof(ExerciseMovement)))
            for (int side = 0; side < 2; side++)
            {
                profile.movement = movement;
                var estimate = ExerciseGame.AutomaticEstimate(profile, side, Vector3.zero, Quaternion.identity);
                Check(estimate.automatic && !estimate.startAnchored && estimate.estimateVersion == ExerciseGame.AutomaticEstimateVersion,
                    "Estimates are marked as automatic, with their version");
                var small = ExerciseGame.AutomaticEstimate(profile, side, Vector3.zero, Quaternion.identity, .8f);
                Check(Vector3.Distance(small.start, small.end) < Vector3.Distance(estimate.start, estimate.end)
                    && small.start.y > estimate.start.y, "A shorter body gets a smaller, higher-starting estimate");
                Check(ExerciseGame.RangeDirectionAllowed(movement, estimate.end - estimate.start, side, profile.minimumTravel), "Estimate direction");
                if (movement == ExerciseMovement.Scaption)  // Expert 2's sheet: lift to 90-120 degrees (section 7, 2026-11-18)
                    Check(Mathf.Abs(Vector3.Angle(Vector3.down, estimate.end - estimate.pivot) - 120) < 2, "Scaption estimate ends at about 120 degrees");
                Check(estimate.ToLocal(Vector3.zero, Quaternion.identity).automatic, "Saving keeps the automatic flag");
            }

        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1 : 1;
            foreach (float size in new[] { .03f, .4f })
            {
                Check(ExerciseGame.RangeDirectionAllowed(ExerciseMovement.Scaption, new Vector3(sign * size, size, size * .3f), side, .02f), "Small and large scaption");
                Check(ExerciseGame.RangeDirectionAllowed(ExerciseMovement.SerratusReach, Vector3.forward * size, side, .015f), "Small and large reach");
                Check(ExerciseGame.RangeDirectionAllowed(ExerciseMovement.ExternalRotation, new Vector3(sign * size, 0, 0), side, .02f), "Small and large rotation");
            }
            Check(!ExerciseGame.RangeDirectionAllowed(ExerciseMovement.Scaption, Vector3.zero, side, .02f), "No movement is rejected");
            Check(!ExerciseGame.RangeDirectionAllowed(ExerciseMovement.SerratusReach, Vector3.back * .1f, side, .015f), "Wrong direction is rejected");
        }

        var baseline = new Calibration
        {
            exerciseId = "test",
            start = new Vector3(0, 0, .4f),
            end = new Vector3(.3f, .2f, .4f),
            pivot = Vector3.zero,
            startRotation = Quaternion.identity,
            endRotation = Quaternion.Euler(0, 40, 0),
        };
        var origin = new Vector3(2, 1, 3);
        var frame = Quaternion.Euler(0, 60, 0);
        var restored = baseline.ToLocal(origin, frame).ToWorld(origin, frame);
        Check(Vector3.Distance(restored.end, baseline.end) < .0001f && Quaternion.Angle(restored.endRotation, baseline.endRotation) < .01f, "Saved calibration round trip");

        foreach (ExerciseMovement movement in Enum.GetValues(typeof(ExerciseMovement)))
        {
            var scaled = baseline.Scaled(movement, .7f);
            Check(Vector3.Distance(scaled.end, ExerciseProfile.Path(movement, baseline.start, baseline.end, baseline.pivot, .7f)) < .0001f, "Range scaling follows the path");
        }
        Object.DestroyImmediate(profile);
    }

    static void CheckScoring()
    {
        Check(ExerciseGame.MovementScore(1, 1, 1) == 1, "Perfect movement scores 1");
        Check(ExerciseGame.MovementScore(.8f, 1, .75f) >= .7f, "Partial credit");
        Check(ExerciseGame.MovementScore(1, 0, 1) <= .25f, "Standing still scores low");
        Check(ExerciseGame.TravelCoverage(2, 2) == 0 && ExerciseGame.TravelCoverage(float.PositiveInfinity, float.NegativeInfinity) == 0
            && ExerciseGame.TravelCoverage(0, 1) == 1, "Coverage");

        Check(ExerciseGame.HeadPositionSeverity(new Vector3(-.25f, -.2f, 0), 1) >= .99f, "Leaning down and across is detected (right arm)");
        Check(ExerciseGame.HeadPositionSeverity(new Vector3(.25f, -.2f, 0), 0) >= .99f, "Leaning down and across is detected (left arm)");
        foreach (var offset in new[] { Vector3.zero, new Vector3(-.25f, 0, 0), new Vector3(0, -.2f, 0), new Vector3(.25f, -.2f, 0), new Vector3(-.05f, -.04f, 0) })
            Check(ExerciseGame.HeadPositionSeverity(offset, 1) == 0, "Natural head movement is not penalized");
        Check(Mathf.Abs(ExerciseGame.HeadAdjustedScore(1, 1) - .9f) < .0001f && ExerciseGame.HeadAdjustedScore(.05f, 1) >= 0, "Head penalty is capped");

        Check(ExerciseGame.DueAtRepEnd(299, 299, 300, 900) == "", "No break before 5 minutes");
        Check(ExerciseGame.DueAtRepEnd(300, 300, 300, 900) == "break", "Break after 5 minutes");
        Check(ExerciseGame.DueAtRepEnd(900, 300, 300, 900) == "cap", "The session cap wins over a break");
        Check(ExerciseGame.DueAtRepEnd(5000, 5000, 0, 0) == "", "0 turns the limits off");

        Check(ScoreFeedback.Perfect(true, .9f, .9f) && !ScoreFeedback.Perfect(true, .89f, 1) && !ScoreFeedback.Perfect(true, 1, .89f)
            && !ScoreFeedback.Perfect(false, 1, 1), "Perfect threshold");
        Check(ScoreFeedback.StreakNote(0) == 0 && ScoreFeedback.StreakNote(1) == 0 && ScoreFeedback.StreakNote(2) == 1 && ScoreFeedback.StreakNote(99) == 5, "Streak notes rise and stop at the top");
        Check(ScoreFeedback.LineLevel(0) > 0 && ScoreFeedback.LineLevel(0) < ScoreFeedback.LineLevel(.5f) && ScoreFeedback.LineLevel(.5f) < ScoreFeedback.LineLevel(1), "Line tone volume");
        Check(ScoreFeedback.LineCutoff(0) < ScoreFeedback.LineCutoff(.5f) && ScoreFeedback.LineCutoff(.5f) < ScoreFeedback.LineCutoff(1), "Line tone filter");

        Check(!PathFeedback.NodeRepaired(.2f) && PathFeedback.NodeRepaired(.9f), "Node threshold");
        Check(PathFeedback.Glow(.4f).g <= PathFeedback.Glow(.65f).g && PathFeedback.Glow(.65f).g <= PathFeedback.Glow(.9f).g && PathFeedback.Glow(.9f) == Color.white, "Glow brightens with quality");
        Check(PathFeedback.SectionLit(1, 100, true) && !PathFeedback.SectionLit(1, 100, false) && !PathFeedback.SectionLit(1, .5f, true), "Lit sections skip tracking gaps");

        var bonus = new PerfectFlowBonus();
        int earned = 0;
        for (int i = 0; i < 19; i++)
            earned += bonus.Advance(true, 1, .1f);
        Check(earned == 0, "No bonus before two seconds");
        earned += bonus.Advance(true, .9f, .1f);
        Check(earned == 100 && bonus.Ticks == 1, "First tick at two seconds");
        for (int i = 0; i < 20; i++)
            earned += bonus.Advance(true, 1, .1f);
        Check(earned == 600 && bonus.Ticks == 5 && bonus.Multiplier == 2, "Half-second ticks and rising multiplier");
        bonus.Advance(true, .89f, .01f);
        Check(bonus.Ticks == 0 && bonus.Seconds == 0, "Going off the line resets the bonus");
        bonus.Advance(true, 1, 30);
        Check(bonus.Seconds <= .101f, "A long frame does not pay out several ticks");
        bonus.Reset();
        for (int i = 0; i < 400; i++)
            bonus.Advance(true, 1, .1f);
        Check(bonus.Multiplier == 5, "Bonus multiplier stops at 5");
    }

    static void CheckSessionProgress()
    {
        var good = new SessionProgress { target = 18 };
        for (int i = 0; i < 18; i++)
            good.Resolve(1);
        Check(good.outcome == "victory" && good.battery == 100, "Accurate movements fill the energy");

        int movements = good.movements;
        good.Resolve(0);
        Check(good.movements == movements, "A finished session does not change");

        var still = new SessionProgress { target = 1 };
        for (int i = 0; i < 100 && !still.Finished; i++)
            still.Resolve(.25f);
        Check(still.outcome == "defeat" && still.power == 0, "Standing still fails even a short session");

        var medium = new SessionProgress { target = 18 };
        for (int i = 0; i < 18; i++)
            medium.Resolve(.7f);
        Check(!medium.Finished, "Moderate movements need more than the target");
        for (int i = 0; i < 100 && !medium.Finished; i++)
            medium.Resolve(.7f);
        Check(medium.outcome == "victory", "Moderate movements succeed in the end");

        var capped = new SessionProgress { target = 18 };
        capped.Resolve(.7f, true);
        Check(capped.outcome == "session_limit" && capped.GoalReached, "Reaching the cap counts as a completed goal");
        var cappedWin = new SessionProgress { target = 1 };
        cappedWin.Resolve(1, true);
        var cappedLoss = new SessionProgress { target = 18, battery = 1 };
        cappedLoss.Resolve(.25f, true);
        Check(cappedWin.outcome == "victory" && cappedLoss.outcome == "defeat" && !cappedLoss.GoalReached, "Victory and defeat still count on the capped repetition");
    }

    static void CheckParticipant()
    {
        Check(Participant.FormatId(1) == "P001" && Participant.FormatId(1000) == "P1000", "ID format");
        Check(Participant.NextNumber(0) == 1 && Participant.NextNumber(3) == 4, "Next ID");
        Check(Participant.ParseId("P012") == 12 && Participant.ParseId("TEST") == 0 && Participant.ParseId(null) == 0, "ID parsing");
        Check(Participant.ParseFile("{\"number\":3}") == 3 && Participant.ParseFile("{oops") == 0 && Participant.ParseFile("{}") == 0 && Participant.ParseFile(null) == 0, "File parsing");

        string folder = Path.Combine(Path.GetTempPath(), "p7-participant-" + Guid.NewGuid());
        string file = Path.Combine(folder, "participant.json");
        string sessions = Path.Combine(folder, "Sessions");
        try
        {
            var first = Participant.Load(file, sessions);
            Check(first.Id == "P001" && first.error == null, "The first participant is P001 without a warning");
            first.Advance();
            Check(Participant.Load(file, sessions).Id == "P002", "The counter is saved");

            Directory.CreateDirectory(Path.Combine(sessions, "s"));
            File.WriteAllText(Path.Combine(sessions, "s", "session.json"), "{\"participantId\":\"P007\"}");
            File.WriteAllText(file, "{oops");
            var recovered = Participant.Load(file, sessions);
            Check(recovered.Id == "P008" && recovered.error != null, "A damaged file continues after the highest exported ID and warns");
            var reloaded = Participant.Load(file, sessions);
            Check(reloaded.Id == "P008" && reloaded.error == null, "The recovered ID is saved");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    static void CheckNeuralModel()
    {
        var asset = Resources.Load<TextAsset>(AdaptiveCoach.ModelResource);
        Check(asset, "The trained model exists");
        var model = JsonUtility.FromJson<AdaptiveCoach.Model>(asset.text);
        Check(AdaptiveCoach.IsValid(model) && model.validationMse <= .02f, "The model is valid");

        for (int score = 0; score <= 100; score += 2)
            for (int coverage = 0; coverage <= 100; coverage += 2)
            {
                var change = AdaptiveCoach.Predict(model, score / 100f, coverage / 100f);
                Check(!float.IsNaN(change.x) && !float.IsNaN(change.y)
                    && Mathf.Abs(change.x) <= AdaptiveCoach.RangeStep && Mathf.Abs(change.y) <= AdaptiveCoach.SpeedStep, "Predictions stay within limits");
            }

        var near = AdaptiveCoach.Predict(model, .3f, .4f, 0);
        var far = AdaptiveCoach.Predict(model, .3f, .4f, 1);
        Check(far.x < near.x - .2f && far.y < near.y - .1f && far.x <= -.3f, "Large errors ease much more");
        Check(AdaptiveCoach.ErrorSeverity(.02f, .4f) == 0 && AdaptiveCoach.ErrorSeverity(.25f, .4f) >= .99f, "Error normalization");

        // Start follow: none for a start that is already right, about half for a clearly misplaced one,
        // more when the hand error is also large. The start offset hardly changes range and speed.
        for (int score = 0; score <= 100; score += 10)
            for (int offset = 0; offset <= 10; offset++)
            {
                float follow = AdaptiveCoach.PredictStartFollow(model, score / 100f, .7f, .3f, offset / 10f);
                Check(follow >= 0 && follow <= 1, "Start follow stays within 0-1");
            }
        Check(AdaptiveCoach.PredictStartFollow(model, .8f, .8f, 0, 0) < .1f, "A start in the right place is not moved");
        float half = AdaptiveCoach.PredictStartFollow(model, .8f, .8f, 0, .5f);
        Check(half > .45f && half < .75f, "A 10 cm offset is followed more than halfway, got " + half);
        Check(AdaptiveCoach.PredictStartFollow(model, .3f, .4f, 1, .5f) > half + .08f, "A large hand error follows the start more");
        var withOffset = AdaptiveCoach.Predict(model, .3f, .4f, 0, 1);
        var withoutOffset = AdaptiveCoach.Predict(model, .3f, .4f, 0, 0);
        Check(Mathf.Abs(withOffset.x - withoutOffset.x) < .06f && Mathf.Abs(withOffset.y - withoutOffset.y) < .06f, "The start offset hardly changes range and speed");

        // A good attempt that is not perfect still makes the next one harder, above the runtime dead zone.
        var good = AdaptiveCoach.Predict(model, .8f, 1);
        Check(good.x > .04f && good.y > .04f, "A good attempt (score 80) makes it harder, got " + good);
        var perfect = AdaptiveCoach.Predict(model, 1, 1);
        Check(perfect.x > .09f && perfect.y > .09f, "A perfect attempt makes it clearly harder, got " + perfect);

        var poor = AdaptiveCoach.Predict(model, .3f, .4f);
        var strong = AdaptiveCoach.Predict(model, 1, 1);
        Check(poor.x < 0 && poor.y < 0 && strong.x > 0 && strong.y > 0, "Poor attempts ease, strong attempts increase");

        model.inputWeights[0] = float.NaN;
        Check(!AdaptiveCoach.IsValid(model), "NaN weights are rejected");
    }

    // Runs the coach on a test profile with settings in a temporary file.
    static void WithCoach(Action<AdaptiveCoach, ExerciseProfile, string> test)
    {
        var root = new GameObject("Coach test");
        var coach = root.AddComponent<AdaptiveCoach>();
        var profile = ScriptableObject.CreateInstance<ExerciseProfile>();
        profile.exerciseId = "test";
        string path = Path.Combine(Path.GetTempPath(), "p7-coach-test-" + Guid.NewGuid() + ".json");
        try
        {
            test(coach, profile, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
            File.Delete(path);
            File.Delete(AdaptiveCoach.PersonalPath(path));
        }
    }

    static void CheckAdaptiveCoach()
    {
        WithCoach((coach, profile, path) =>
        {
            var settings = new SavedSettings { filePath = path };
            settings.Limit("test").fasterPercent = 20;
            coach.Configure(settings);

            var result = coach.Observe(profile, 0, 1, 100, 1, 8, 0, 0, 0);
            Check(result.nextDurationScale < 1, "One perfect attempt raises the speed");
            var saved = SavedSettings.Load(path);
            Check(saved.Arm("test", 0).speed > 1 && saved.Arm("test", 1).speed == 1, "Each arm is saved separately");

            coach.Configure(saved);
            Check(coach.ProfileFor(profile, 0).outwardSeconds < profile.outwardSeconds, "The saved speed is restored");

            result = coach.Observe(profile, 0, 2, 30, .4f, 8, 0, 0, 0);
            Check(result.nextRangeScale < 1 && result.nextDurationScale > result.durationScaleUsed, "A poor attempt eases at once");
            result = coach.Observe(profile, 0, 3, 100, 1, 8, 0, 0, 0);
            Check(result.nextRangeScale > result.rangeScaleUsed && result.nextDurationScale < result.durationScaleUsed, "A strong attempt raises speed and range");
            result = coach.Observe(profile, 0, 4, 62, .85f, 8, 0, 0, 0);
            Check(result.nextDurationScale == result.durationScaleUsed && result.nextRangeScale == result.rangeScaleUsed, "A medium attempt keeps the level");
            result = coach.Observe(profile, 0, 5, 0, 0, 8, 4, 0, 0);
            Check(result.nextDurationScale == result.durationScaleUsed && result.nextRangeScale == result.rangeScaleUsed, "Lost tracking changes nothing");
            result = coach.Observe(profile, 0, 6, 100, 1, 8, 0, 0, 85);
            Check(result.nextDurationScale < result.durationScaleUsed, "Head rotation alone does not block progress");
            result = coach.Observe(profile, 0, 7, 100, 1, 8, 0, .3f, 0, 0, 0, 1);
            Check(result.nextDurationScale == result.durationScaleUsed && result.nextRangeScale == result.rangeScaleUsed, "Leaning blocks an increase");
            result = coach.Observe(profile, 0, 7, 34.6159f, .7471f, 8, 0, .0748f, 26.556f);
            Check(result.nextDurationScale > result.durationScaleUsed && result.nextRangeScale < result.rangeScaleUsed, "A poor attempt with head turn eases (seen on a headset)");

            for (int i = 0; i < 50; i++)
                result = coach.Observe(profile, 0, i, 100, 1, 8, 0, 0, 0);
            Check(1 / result.nextDurationScale <= 1.2001f && result.nextRangeScale <= 1, "Speed and range stop at their limits");
            for (int i = 0; i < 50; i++)
                result = coach.Observe(profile, 0, i, 0, 0, 8, 0, 0, 0);
            Check(result.nextDurationScale <= 2 && result.nextRangeScale >= .4f, "Lower limits");
            Check(SavedSettings.Load(path).Arm("test", 0).range <= .4001f, "A reduced range survives a reload");

            saved.devMode = true;
            saved.Arm("test", 0).devSpeed = 1.7f;
            saved.Arm("test", 0).devRange = .5f;
            saved.Save();
            string before = File.ReadAllText(path);
            coach.Configure(saved);
            Check(Mathf.Abs(coach.ProfileFor(profile, 0).outwardSeconds - 3 / 1.7f) < .001f && coach.RangeFor(profile, 0, .4f) == .5f, "Dev overrides");
            coach.Observe(profile, 0, 1, 100, 1, 8, 0, 0, 0);
            Check(File.ReadAllText(path) == before, "Dev mode does not save progress");

            saved.devMode = false;
            coach.Configure(saved);
            Check(Mathf.Abs(coach.ProfileFor(profile, 0).outwardSeconds - 3 / saved.Arm("test", 0).speed) < .001f, "Leaving dev mode restores the normal level");
        });

        WithCoach((coach, profile, path) =>
        {
            var settings = new SavedSettings { filePath = path };
            settings.Arm(profile.exerciseId, 0).range = .7f;
            coach.Configure(settings);
            coach.RangeFor(profile, 0, .5f);
            var decision = coach.Observe(profile, 0, 1, 100, 1, 8, 0, 0, 0);

            string personalPath = AdaptiveCoach.PersonalPath(path);
            Check(File.Exists(personalPath), "Personal levels have their own file");
            var personal = JsonUtility.FromJson<AdaptiveCoach.PersonalData>(File.ReadAllText(personalPath));
            Check(personal.arms.Count == 1 && personal.arms[0].observations == 1 && personal.arms[0].meanScore == 100, "Personal observations are saved");

            var restored = SavedSettings.Load(path);
            restored.Arm(profile.exerciseId, 0).speed = 1.99f;
            coach.Configure(restored);
            Check(Mathf.Abs(coach.ProfileFor(profile, 0).outwardSeconds - profile.outwardSeconds * decision.nextDurationScale) < .001f, "The personal file wins after a reload");

            string before = File.ReadAllText(personalPath);
            restored.devMode = true;
            coach.Configure(restored);
            coach.Observe(profile, 0, 2, 20, .2f, 8, 0, 0, 0);
            Check(File.ReadAllText(personalPath) == before, "Dev mode does not change personal levels");

            restored.devMode = false;
            coach.Configure(restored);
            coach.Observe(profile, 0, 3, float.NaN, 1, 8, 0, 0, 0);
            coach.Observe(profile, 0, 4, 100, 1, 8, 5, 0, 0);
            Check(File.ReadAllText(personalPath) == before, "Invalid data and lost tracking are not learned");

            AdaptiveCoach.ResetPersonalData(restored);
            Check(JsonUtility.FromJson<AdaptiveCoach.PersonalData>(File.ReadAllText(personalPath)).arms.Count == 0, "Reset clears personal levels");

            // Start shift: the path follows where the hand really starts, in bounded steps, and is saved.
            coach.Configure(restored);
            var low = new Vector3(0, -.15f, 0);
            decision = coach.Observe(profile, 0, 5, 80, .85f, 8, 0, 0, 0, 0, 0, 0, 0, low, 1);
            Check(decision.startShiftUsed == Vector3.zero && decision.predictedStartFollow > .35f
                && Mathf.Abs(decision.nextStartShift.y + AdaptiveCoach.StartShiftStep) < .0001f,
                "The network moves a low start down by one bounded step");
            coach.Configure(SavedSettings.Load(path));
            Check(Mathf.Abs(coach.StartShiftFor(profile, 0).y + AdaptiveCoach.StartShiftStep) < .0001f, "The start shift survives a reload");
            decision = coach.Observe(profile, 0, 6, 80, .85f, 8, 0, 0, 0, 0, 0, 0, 0, low, .1f);
            Check(decision.nextStartShift == decision.startShiftUsed, "Too few start samples change nothing");
            coach.ObserveStart(profile, 0, low, 1, 80, .85f, .05f, .3f, 8, 0);
            Check(Mathf.Abs(coach.StartShiftFor(profile, 0).y + 2 * AdaptiveCoach.StartShiftStep) < .0001f, "A practice repetition teaches the start shift");
            coach.Recalibrated(profile.exerciseId, 0);
            Check(coach.StartShiftFor(profile, 0) == Vector3.zero, "Recalibration clears the start shift");

            // Tutorial practice: the reach along the full movement sets the starting range, within the floor.
            coach.SetMeasuredLevel(profile, 0, .63f, .8f);
            Check(Mathf.Abs(SavedSettings.Load(path).Arm(profile.exerciseId, 0).range - .63f) < .001f, "The practice reach becomes the saved range");
            coach.SetMeasuredLevel(profile, 0, .01f, .8f);
            Check(coach.RangeFor(profile, 0, .4f) >= coach.minimumRangeScale - .0001f, "A tiny reach stays above the range floor");

            coach.Configure(new SavedSettings());
            var small = coach.Observe(profile, 0, 1, 30, .4f, 8, 0, 0, 0, targetTravelMetres: .4f, meanHandErrorMetres: .02f);
            var large = coach.Observe(profile, 1, 1, 30, .4f, 8, 0, 0, 0, targetTravelMetres: .4f, meanHandErrorMetres: .25f);
            Check(large.nextRangeScale < small.nextRangeScale - .2f && large.nextDurationScale > small.nextDurationScale && large.nextRangeScale >= .4f,
                "A larger distance from the line eases more");
        });
    }

    static void CheckPlaylist()
    {
        var profile = ScriptableObject.CreateInstance<ExerciseProfile>();
        profile.exerciseId = "test";
        var profiles = new[] { profile };
        var settings = new SavedSettings { totalRepetitions = 7 };

        settings.Arm("test", 1).enabled = false;
        var playlist = settings.Playlist(profiles);
        Check(playlist.Count == 7 && playlist.TrueForAll(entry => entry == 0), "Exact length, disabled arm skipped");
        settings.Arm("test", 0).enabled = false;
        Check(settings.Playlist(profiles).Count == 0, "Nothing enabled gives an empty playlist");

        settings.Arm("test", 0).enabled = settings.Arm("test", 1).enabled = true;
        settings.totalRepetitions = 18;
        bool repeats = false, varies = false;
        var first = settings.Playlist(profiles, 0);
        for (int seed = 1; seed < 20; seed++)
        {
            var shuffled = settings.Playlist(profiles, seed);
            Check(shuffled.Count == 18 && shuffled.FindAll(v => v == 0).Count == 9, "Both arms get the same number of turns");
            for (int i = 1; i < shuffled.Count; i++)
            {
                repeats |= shuffled[i] == shuffled[i - 1];
                varies |= shuffled[i] != first[i];
            }
        }
        Check(repeats && varies, "The order is random and may repeat");
        Object.DestroyImmediate(profile);
    }

    // Iteration 2 (pilot test 1 fixes): reach limit, standing still, results confirmation, tutorial pages.
    static void CheckIteration2()
    {
        var profile = ScriptableObject.CreateInstance<ExerciseProfile>();
        profile.exerciseId = "reach-check";
        foreach (ExerciseMovement movement in Enum.GetValues(typeof(ExerciseMovement)))
            for (int side = 0; side < 2; side++)
                foreach (float scale in new[] { .8f, 1f, 1.2f })
                {
                    profile.movement = movement;
                    var estimate = ExerciseGame.AutomaticEstimate(profile, side, Vector3.zero, Quaternion.identity, scale);
                    Vector3 shoulder = ExerciseGame.ShoulderOffset(side, scale);
                    float reach = ExerciseGame.ArmReachMetres * scale;
                    var same = ExerciseGame.ClampToReach(movement, estimate, shoulder, reach);
                    Check(Vector3.Distance(same.start, estimate.start) < .0001f && Vector3.Distance(same.end, estimate.end) < .0001f,
                        "The automatic estimate is already in reach and is not changed");

                    // A start shift 40 cm forward and 20 cm up must be pulled back into reach.
                    var pushed = estimate.Copy();
                    Vector3 shift = new Vector3(0, .2f, .4f);
                    pushed.start += shift;
                    pushed.end += shift;
                    pushed.pivot += shift;
                    var clamped = ExerciseGame.ClampToReach(movement, pushed, shoulder, reach);
                    for (int i = 0; i <= 20; i++)
                    {
                        Vector3 p = ExerciseProfile.Path(movement, clamped.start, clamped.end, clamped.pivot, i / 20f);
                        Check(Vector3.Distance(p, shoulder) <= reach + .002f, "Every point of a clamped path is in reach");
                    }
                    Check(Vector3.Distance(clamped.start, clamped.end) > .5f * Vector3.Distance(estimate.start, estimate.end),
                        "Clamping moves the movement rather than shrinking it to nothing");
                }
        Object.DestroyImmediate(profile);

        Check(Mathf.Abs(ExerciseGame.StepDistance(new Vector3(.3f, 1.7f, .4f), new Vector3(0, 1.2f, 0)) - .5f) < .0001f, "Step distance ignores height");
        Check(!ExerciseGame.OffSpot(false, .3f, .35f, .2f) && ExerciseGame.OffSpot(false, .4f, .35f, .2f), "Off the floor ring past the limit");
        Check(ExerciseGame.OffSpot(true, .3f, .35f, .2f) && !ExerciseGame.OffSpot(true, .15f, .35f, .2f), "Back on the ring only below the return distance");

        Check(GameMenu.NeedsConfirm("home", true, false) && GameMenu.NeedsConfirm("again", true, false)
            && !GameMenu.NeedsConfirm("home", true, true) && !GameMenu.NeedsConfirm("home", false, false)
            && !GameMenu.NeedsConfirm("start", true, false), "Only results-page buttons outside dev mode need a second press");
        Check(GameMenu.NeedsConfirm("therapist", false, false) && !GameMenu.NeedsConfirm("therapist", false, true),
            "The therapist view needs two presses outside dev mode");
        Check(GameMenu.Confirmed("home", "home", 1) && !GameMenu.Confirmed("home", "home", .1f)
            && !GameMenu.Confirmed("home", "home", 5) && !GameMenu.Confirmed("again", "home", 1), "A second press must be on the same button, 0.4-4 s later");

        Check(ExerciseGame.TutorialIntroPages.Length >= 3, "The tutorial intro has pages");
        bool energy = false, flow = false;
        foreach (var page in ExerciseGame.TutorialIntroPages)
        {
            Check(page.Split('\n').Length <= 5, "Each tutorial page is short");
            energy |= page.Contains("ENERGY");
            flow |= page.Contains("FLOW");
        }
        Check(energy && flow, "The tutorial explains ENERGY and FLOW");
        // Therapist view: counts per exercise and arm, in play order, first and last three means.
        var log = new System.Collections.Generic.List<TherapistView.Rep>();
        float[] scores = { .5f, .6f, .7f, .8f, .9f, .95f };
        foreach (float s in scores)
            log.Add(new TherapistView.Rep { exerciseId = "a", side = 1, score = s, complete = s >= .7f, perfect = s >= .9f });
        log.Insert(2, new TherapistView.Rep { exerciseId = "b", side = 0, score = 1, complete = true, perfect = true });
        var arms = TherapistView.Summarise(log);
        Check(arms.Count == 2 && arms[0].exerciseId == "a" && arms[0].tries == 6 && arms[0].complete == 4 && arms[0].perfect == 2
            && Mathf.Abs(arms[0].firstMean - 60) < .01f && Mathf.Abs(arms[0].lastMean - 88.33f) < .01f
            && arms[1].tries == 1 && Mathf.Abs(arms[1].firstMean - 100) < .01f && Mathf.Abs(arms[1].lastMean - 100) < .01f,
            "The therapist view counts tries, complete and PERFECT reps per exercise and arm");
        Check(TherapistView.Rep.Learned("Network eases next attempt") && TherapistView.Rep.Learned("Maintain difficulty")
            && !TherapistView.Rep.Learned("Tracking unreliable; keep difficulty") && !TherapistView.Rep.Learned("Adaptation disabled"),
            "The therapist view marks reps the coach did not learn from");
        string csv = TherapistView.Csv(log);
        string[] csvLines = csv.Split('\n');
        Check(csv.StartsWith(TherapistView.CsvHeader) && csvLines.Length == log.Count + 1
            && csvLines[1].Split(',').Length == TherapistView.CsvHeader.Split(',').Length,
            "therapist-view.csv has a header and one row per repetition");

        string colours = Array.Find(ExerciseGame.TutorialIntroPages, p => p.StartsWith("COLOURS")) ?? "";
        foreach (var c in new[] { ExerciseGame.FarColor, ExerciseGame.NearColor, ExerciseGame.OnTargetColor, ExerciseGame.ArmColor(0),
                     ExerciseGame.ArmColor(1), new Color(.2f, .85f, 1), new Color(1, .55f, .1f) })
            Check(colours.Contains("#" + ColorUtility.ToHtmlStringRGB(c)), "The tutorial colour page uses the game's colour #" + ColorUtility.ToHtmlStringRGB(c));

        Check(ExerciseGame.DepthDirection(.1f) == 1 && ExerciseGame.DepthDirection(-.1f) == -1 && ExerciseGame.DepthDirection(.01f) == 0,
            "The depth cue says reach forward, come closer, or nothing");
        // Green must mean "counts towards PERFECT": never wider than the hold tolerance, and a rep held at that distance
        // the whole time, with every phase reached and full coverage, scores at least 90.
        var ringProfile = ScriptableObject.CreateInstance<ExerciseProfile>();
        foreach (float tolerance in new[] { .025f, .039f, .06f })
        {
            float green = ExerciseGame.GreenDistanceFor(tolerance, ringProfile.fullCreditMetres, ringProfile.zeroCreditMetres);
            Check(green <= tolerance && ExerciseGame.MovementScore(ringProfile.Quality(green), 1, 1) >= .9f - .0001f,
                "Green is within the hold tolerance and good enough for PERFECT");
            Check(ExerciseGame.TargetColor(green, green, ringProfile.zeroCreditMetres, true) == ExerciseGame.OnTargetColor
                && ExerciseGame.TargetColor(green + .001f, green, ringProfile.zeroCreditMetres, false) != ExerciseGame.OnTargetColor
                && ExerciseGame.TargetColor(.2f, green, ringProfile.zeroCreditMetres, false) == ExerciseGame.FarColor,
                "The ring is red far away, yellow close, and green only when in");
        }
        Check(ExerciseGame.InTarget(.02f, .04f, false, 0) && !ExerciseGame.InTarget(.05f, .04f, false, 1)
            && !ExerciseGame.InTarget(.02f, .04f, true, .85f) && ExerciseGame.InTarget(.02f, .04f, true, .92f),
            "In the hold the ring is green only when the end is also reached (90 % coverage)");
        UnityEngine.Object.DestroyImmediate(ringProfile);

        foreach (var shape in new[] { ExerciseGame.TorusMesh(.05f, .007f, 48, 10), ExerciseGame.SphereMesh(.018f, 12, 18) })
        {
            bool outward = true;
            var tris = shape.triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = shape.vertices[tris[t]], b = shape.vertices[tris[t + 1]], c = shape.vertices[tris[t + 2]];
                Vector3 n = shape.normals[tris[t]] + shape.normals[tris[t + 1]] + shape.normals[tris[t + 2]];
                outward &= Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0;
            }
            bool shaded = shape.subMeshCount == 3;
            for (int k = 0; k < shape.subMeshCount; k++)
                shaded &= shape.GetTriangles(k).Length > 0;
            Check(outward && shaded && shape.bounds.size.z > .013f, shape.name + " has depth, three shading bands and outward faces");
            UnityEngine.Object.DestroyImmediate(shape);
        }
        Check(Mathf.Abs(CoachCard.CardPosition.x) < .15f && CoachCard.CardPosition.z < 2 && CoachCard.CardPosition.y > -.4f,
            "The coach card sits in view, between the two lines");
    }
}
