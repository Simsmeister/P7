using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace P7
{
    // Adjusts speed and range for each exercise and arm after every repetition, using a small neural network.
    // The network is trained in the editor on synthetic examples (see ADAPTIVE_COACH.md) and never changes during play.
    // It is not clinically validated.
    public class AdaptiveCoach : MonoBehaviour
    {
        public const int Hidden = 16;
        public const float RangeStep = .45f;
        public const float SpeedStep = .35f;
        public const string ModelResource = "AdaptiveCoach/base-model";

        // Start shift: the whole path (start, end and pivot) moves towards where the player's hand really is
        // at the start and during the rest. The network's third output says which fraction (0-1) of the measured
        // offset to follow; the direction is the measured one. Hard guards: at most 5 cm per step and 20 cm in total,
        // offsets under 2.5 cm ignored. Prototype values, not clinically validated.
        public const float StartShiftStep = .05f;
        public const float StartShiftLimit = .20f;
        public const float StartShiftDeadZone = .025f;
        // Seconds of start/rest samples a repetition needs before its offset is trusted.
        public const float StartShiftMinimumSeconds = .3f;
        public const int Inputs = 4, Outputs = 3;
        const string InputSchema = "score01,coverage01,errorSeverity01,startOffset01";
        // Start offsets are given to the network as 0-1 over 0-20 cm.
        public const float StartOffsetFullScale = .20f;

        public bool adaptationEnabled = true;
        [Range(.25f, 1f)] public float minimumRangeScale = .4f;
        [Range(.75f, 1f)] public float fastestDurationScale = .85f;
        [Range(1f, 2f)] public float slowestDurationScale = 2f;
        public const float MaxSpeedStepUp = .05f;

        // One adjustment, saved in adaptive-coach.json.
        [Serializable]
        public class Decision
        {
            public string policy = "neural-range-tempo-start-v3", exerciseId, side, reason;
            public string modelId;
            public float predictedRangeChange, predictedSpeedChange, meanHandErrorMetres, errorSeverity;
            public int repetition;
            public bool enabled, developerMode;
            public float maximumSpeedIncreasePercent, headPositionSeverity;
            public float score, coverage, trackingLostSeconds, headDisplacementMetres, headRotationDegrees;
            public float rangeScaleUsed, nextRangeScale, calibratedTravelMetres, targetTravelMetres;
            public float durationScaleUsed, nextDurationScale, outwardSecondsUsed, returnSecondsUsed;
            // Added 2026-11-10. Head-relative metres (x right, y up, z forward), see StartShiftStep.
            public Vector3 measuredStartOffset, startShiftUsed, nextStartShift;
            public float startOffsetSeverity, predictedStartFollow;
        }

        [Serializable]
        public class Report
        {
            public Decision[] decisions;
        }

        // Network weights, saved in Resources/AdaptiveCoach/base-model.json.
        [Serializable]
        public class Model
        {
            public int version = 3, hidden = Hidden, seed = 42, epochs, trainingExamples, validationExamples;
            public string modelId, inputSchema = InputSchema, provenance;
            public float trainingMse, validationMse;
            public float[] inputWeights = new float[Hidden * Inputs], hiddenBias = new float[Hidden];
            public float[] outputWeights = new float[Hidden * Outputs], outputBias = new float[Outputs];
        }

        [Serializable]
        public class Example
        {
            public float score, coverage, errorSeverity, startOffset, rangeChange, speedChange, startFollow;
            public bool validation;
        }

        [Serializable]
        public class TrainingSet
        {
            public int version = 3;
            public string provenance;
            public Example[] examples;
        }

        // A player's own level per exercise and arm, saved next to the settings as *-adaptive.json.
        [Serializable]
        public class PersonalLevel
        {
            public string exerciseId, lastModelId;
            public int side, observations;
            public float speed = 1, range = 1, meanScore, meanCoverage;
            // Head-relative shift of the whole path, see StartShiftStep. Older files read as zero.
            public Vector3 startShift;
        }

        [Serializable]
        public class PersonalData
        {
            public int version = 1;
            public List<PersonalLevel> arms = new List<PersonalLevel>();
        }

        // The current difficulty of one exercise and arm. The profile is a copy with the adjusted timing.
        class ArmState
        {
            public ExerciseProfile profile;
            public float baseOutward, baseReturn;
            public float durationScale = 1, range = 1, rangeFloor = .4f, maxSpeed = 1.4f;
            public Vector3 startShift;
        }

        readonly Dictionary<string, ArmState> states = new Dictionary<string, ArmState>();
        readonly List<Decision> decisions = new List<Decision>();
        SavedSettings settings;
        bool developerMode;
        Model model;
        PersonalData personal = new PersonalData();

        bool Learning => adaptationEnabled && !developerMode;

        public void Configure(SavedSettings value)
        {
            ResetSession();
            settings = value;
            developerMode = value != null && value.devMode;
            LoadModel();
            LoadPersonalData();
        }

        void OnDestroy() => ResetSession();

        void ResetSession()
        {
            foreach (var state in states.Values)
                DestroyProfile(state.profile);
            states.Clear();
            decisions.Clear();
        }

        // The exercise with this arm's current speed applied.
        public ExerciseProfile ProfileFor(ExerciseProfile source, int side)
        {
            return StateFor(source, side).profile;
        }

        // The fraction of the calibrated range this arm should use now.
        public float RangeFor(ExerciseProfile source, int side, float calibratedTravelMetres)
        {
            var state = StateFor(source, side);
            // Never shrink a small movement below what tracking can tell apart.
            float noiseFloor = source.minimumTravel / Mathf.Max(.00001f, calibratedTravelMetres);
            state.rangeFloor = Mathf.Clamp(Mathf.Max(LowestRange(source.exerciseId, side), noiseFloor), 0, 1);
            state.range = Mathf.Clamp(state.range, state.rangeFloor, 1);
            return state.range;
        }

        // How far (head-relative metres) this arm's path is moved from its calibration. Zero in developer mode.
        public Vector3 StartShiftFor(ExerciseProfile source, int side)
        {
            return developerMode ? Vector3.zero : StateFor(source, side).startShift;
        }

        // The next start shift after one measured offset between the hand and the start of the path.
        // follow is the network's fraction of the offset to move (0-1).
        public static Vector3 NextStartShift(Vector3 current, Vector3 measuredOffset, float follow)
        {
            if (!Finite(measuredOffset.x) || !Finite(measuredOffset.y) || !Finite(measuredOffset.z) || !Finite(follow)
                || measuredOffset.magnitude < StartShiftDeadZone)
                return current;
            Vector3 step = Vector3.ClampMagnitude(measuredOffset * Mathf.Clamp01(follow), StartShiftStep);
            return Vector3.ClampMagnitude(current + step, StartShiftLimit);
        }

        // The tutorial practice measures how far the player gets along the full movement and how well they keep up;
        // that becomes the arm's starting range and speed (within the usual limits). The network adapts from there.
        public void SetMeasuredLevel(ExerciseProfile source, int side, float range, float speed)
        {
            if (!Learning || settings == null || !Finite(range) || !Finite(speed))
                return;
            var state = StateFor(source, side);
            state.range = Mathf.Clamp(range, state.rangeFloor, 1);
            state.durationScale = 1 / Mathf.Clamp(speed, 1 / slowestDurationScale, state.maxSpeed);
            ApplyTiming(state);
            var arm = settings.Arm(source.exerciseId, side);
            arm.range = state.range;
            arm.speed = 1 / state.durationScale;
            var level = PersonalLevelFor(source.exerciseId, side);
            level.range = arm.range;
            level.speed = arm.speed;
            settings.Save();
            SavePersonalData();
        }

        // A practice repetition in the tutorial teaches the start shift (and, through SetMeasuredRange, the range).
        public void ObserveStart(ExerciseProfile source, int side, Vector3 measuredStartOffset, float startSeconds,
            float score, float coverage, float meanHandErrorMetres, float targetTravelMetres,
            float validSeconds, float trackingLostSeconds)
        {
            if (!Learning || settings == null || model == null || startSeconds < StartShiftMinimumSeconds
                || !IsReliable(score, coverage, validSeconds, trackingLostSeconds, 0, meanHandErrorMetres))
                return;
            var state = StateFor(source, side);
            float follow = PredictStartFollow(model, score / 100f, coverage,
                ErrorSeverity(meanHandErrorMetres, targetTravelMetres), StartOffsetSeverity(measuredStartOffset));
            state.startShift = NextStartShift(state.startShift, measuredStartOffset, follow);
            PersonalLevelFor(source.exerciseId, side).startShift = state.startShift;
            SavePersonalData();
        }

        ArmState StateFor(ExerciseProfile source, int side)
        {
            string key = source.exerciseId + ":" + side;
            if (states.TryGetValue(key, out var state))
                return state;

            state = new ArmState
            {
                profile = Instantiate(source),
                baseOutward = source.outwardSeconds,
                baseReturn = source.returnSeconds,
            };

            if (settings != null)
            {
                var arm = settings.Arm(source.exerciseId, side);
                state.rangeFloor = LowestRange(source.exerciseId, side);
                state.maxSpeed = 1 + settings.Limit(source.exerciseId).fasterPercent / 100;
                float speed = developerMode ? arm.devSpeed : arm.speed;
                state.durationScale = 1 / Mathf.Clamp(speed, .5f, developerMode ? 2 : state.maxSpeed);
                state.range = Mathf.Clamp(developerMode ? arm.devRange : arm.range, state.rangeFloor, 1);
                if (!developerMode)
                    state.startShift = Vector3.ClampMagnitude(PersonalLevelFor(source.exerciseId, side).startShift, StartShiftLimit);
            }
            else
            {
                state.maxSpeed = 1 / fastestDurationScale;
            }

            ApplyTiming(state);
            states.Add(key, state);
            return state;
        }

        float LowestRange(string exerciseId, int side)
        {
            if (developerMode)
                return .3f;
            if (settings != null && settings.Arm(exerciseId, side).automaticRange)
                return .15f;
            return minimumRangeScale;
        }

        static void ApplyTiming(ArmState state)
        {
            state.profile.outwardSeconds = state.baseOutward * state.durationScale;
            state.profile.returnSeconds = state.baseReturn * state.durationScale;
        }

        // Called after every repetition. Changes only the next attempt of the same exercise and arm.
        public Decision Observe(ExerciseProfile source, int side, int repetition, float score, float coverage,
            float validSeconds, float trackingLostSeconds, float headMetres, float headDegrees,
            float calibratedTravelMetres = 0, float targetTravelMetres = 0, float headPositionSeverity = 0, float meanHandErrorMetres = 0,
            Vector3 measuredStartOffset = default, float startSeconds = 0)
        {
            var state = StateFor(source, side);
            float errorSeverity = ErrorSeverity(meanHandErrorMetres, targetTravelMetres);
            var decision = new Decision
            {
                exerciseId = source.exerciseId,
                side = side == 0 ? "left" : "right",
                repetition = repetition,
                modelId = model != null ? model.modelId : "unavailable",
                enabled = Learning,
                developerMode = developerMode,
                maximumSpeedIncreasePercent = (state.maxSpeed - 1) * 100,
                score = score,
                coverage = coverage,
                meanHandErrorMetres = meanHandErrorMetres,
                errorSeverity = errorSeverity,
                trackingLostSeconds = trackingLostSeconds,
                headPositionSeverity = headPositionSeverity,
                headDisplacementMetres = headMetres,
                headRotationDegrees = headDegrees,
                calibratedTravelMetres = calibratedTravelMetres,
                targetTravelMetres = targetTravelMetres,
                rangeScaleUsed = state.range,
                durationScaleUsed = state.durationScale,
                outwardSecondsUsed = state.profile.outwardSeconds,
                returnSecondsUsed = state.profile.returnSeconds,
                measuredStartOffset = measuredStartOffset,
                startShiftUsed = state.startShift,
            };

            bool reliable = IsReliable(score, coverage, validSeconds, trackingLostSeconds, headPositionSeverity, meanHandErrorMetres);
            if (developerMode)
                decision.reason = "Developer overrides; no learning or preset writes";
            else if (!adaptationEnabled)
                decision.reason = "Adaptation disabled";
            else if (!reliable)
                decision.reason = "Tracking unreliable; keep difficulty";
            else if (model == null)
                decision.reason = "Base model unavailable; keep difficulty";
            else
            {
                Adjust(state, decision, score, coverage, errorSeverity, headPositionSeverity);
                decision.startOffsetSeverity = StartOffsetSeverity(measuredStartOffset);
                decision.predictedStartFollow = PredictStartFollow(model, score / 100f, coverage, errorSeverity, decision.startOffsetSeverity);
                if (startSeconds >= StartShiftMinimumSeconds)
                    state.startShift = NextStartShift(state.startShift, measuredStartOffset, decision.predictedStartFollow);
            }

            decision.nextRangeScale = state.range;
            decision.nextStartShift = state.startShift;
            decision.nextDurationScale = state.durationScale;
            ApplyTiming(state);

            if (settings != null && Learning && reliable && model != null)
                SaveProgress(source.exerciseId, side, state, score, coverage);

            decisions.Add(decision);
            return decision;
        }

        void Adjust(ArmState state, Decision decision, float score, float coverage, float errorSeverity, float headPositionSeverity)
        {
            Vector2 change = Predict(model, score / 100f, coverage, errorSeverity);
            decision.predictedRangeChange = change.x;
            decision.predictedSpeedChange = change.y;

            // Ignore tiny changes so the level does not drift.
            float rangeChange = Mathf.Abs(change.x) < .02f ? 0 : change.x;
            float speedChange = Mathf.Abs(change.y) < .0225f ? 0 : change.y;

            // Moving the head to compensate may block an increase, but never stops the game from easing.
            if (headPositionSeverity > .2f)
            {
                rangeChange = Mathf.Min(0, rangeChange);
                speedChange = Mathf.Min(0, speedChange);
            }

            // Speed rises at most MaxSpeedStepUp per attempt (added 2026-11-18: a headset test went from 100 to 140 %
            // speed in four reps, too fast for players with injuries). Easing is not limited.
            speedChange = Mathf.Min(speedChange, MaxSpeedStepUp);

            state.range = Mathf.Clamp(state.range + rangeChange, state.rangeFloor, 1);
            float speed = Mathf.Clamp(1 / state.durationScale * (1 + speedChange), 1 / slowestDurationScale, state.maxSpeed);
            state.durationScale = 1 / speed;

            if (rangeChange < 0 || speedChange < 0)
                decision.reason = "Network eases next attempt";
            else if (rangeChange > 0 || speedChange > 0)
                decision.reason = "Network increases next attempt within limits";
            else
                decision.reason = "Maintain difficulty";
        }

        static bool IsReliable(float score, float coverage, float validSeconds, float trackingLostSeconds,
            float headPositionSeverity, float meanHandErrorMetres)
        {
            if (!Finite(score) || !Finite(coverage) || !Finite(validSeconds) || !Finite(trackingLostSeconds)
                || !Finite(headPositionSeverity) || !Finite(meanHandErrorMetres))
                return false;
            if (score < 0 || score > 100 || coverage < 0 || coverage > 1 || meanHandErrorMetres < 0 || trackingLostSeconds < 0)
                return false;
            if (validSeconds <= 0)
                return false;

            // Learn nothing from a repetition where tracking was lost more than 10% of the time.
            float lostFraction = trackingLostSeconds / Mathf.Max(.001f, validSeconds + trackingLostSeconds);
            return lostFraction <= .1f;
        }

        void SaveProgress(string exerciseId, int side, ArmState state, float score, float coverage)
        {
            // The settings file keeps a copy of speed and range for the menu.
            var arm = settings.Arm(exerciseId, side);
            arm.speed = 1 / state.durationScale;
            arm.range = state.range;

            var level = PersonalLevelFor(exerciseId, side);
            level.speed = arm.speed;
            level.range = arm.range;
            level.startShift = state.startShift;
            level.meanScore = level.observations == 0 ? score : Mathf.Lerp(level.meanScore, score, .25f);
            level.meanCoverage = level.observations == 0 ? coverage : Mathf.Lerp(level.meanCoverage, coverage, .25f);
            level.observations++;
            level.lastModelId = model.modelId;

            settings.Save();
            SavePersonalData();
        }

        public string ExportJson()
        {
            return JsonUtility.ToJson(new Report { decisions = decisions.ToArray() }, true);
        }

        // A new calibration starts the arm's level over.
        public void Recalibrated(string exerciseId, int side, float speed = 1, float range = 1)
        {
            if (settings == null || developerMode)
                return;

            var level = PersonalLevelFor(exerciseId, side);
            level.speed = speed;
            level.range = range;
            level.observations = 0;
            level.meanScore = level.meanCoverage = 0;
            level.startShift = Vector3.zero;

            if (states.Remove(exerciseId + ":" + side, out var previous))
                DestroyProfile(previous.profile);
            SavePersonalData();
        }

        static void DestroyProfile(ExerciseProfile profile)
        {
            if (!profile)
                return;
            if (Application.isPlaying)
                Destroy(profile);
            else
                DestroyImmediate(profile);
        }

        // Turns the average distance from the line into 0-1. Both the distance itself and the distance
        // compared to the size of the movement count; small tracking noise gives 0.
        public static float StartOffsetSeverity(Vector3 offset)
        {
            float m = offset.magnitude;
            return Finite(m) ? Mathf.Clamp01(m / StartOffsetFullScale) : 0;
        }

        public static float ErrorSeverity(float metres, float targetTravel)
        {
            if (!Finite(metres) || !Finite(targetTravel))
                return 0;
            float absolute = Mathf.InverseLerp(.04f, .18f, metres);
            float relative = Mathf.InverseLerp(.15f, .6f, metres / Mathf.Max(.08f, targetTravel));
            return Mathf.Min(absolute, relative);
        }

        // --- Network: 4 inputs, 16 tanh neurons, 3 tanh outputs (range change, speed change, start follow). ---

        public static Vector2 Predict(Model model, float score, float coverage, float errorSeverity = 0, float startOffset = 0)
        {
            if (!IsValid(model) || !Finite(score) || !Finite(coverage) || !Finite(errorSeverity) || !Finite(startOffset))
                return Vector2.zero;

            Vector3 output = Forward(model, Mathf.Clamp01(score), Mathf.Clamp01(coverage), Mathf.Clamp01(errorSeverity),
                Mathf.Clamp01(startOffset), new float[Hidden]);
            return new Vector2(output.x * RangeStep, output.y * SpeedStep);
        }

        // The fraction (0-1) of the measured start offset the path should move towards the hand.
        public static float PredictStartFollow(Model model, float score, float coverage, float errorSeverity, float startOffset)
        {
            if (!IsValid(model) || !Finite(score) || !Finite(coverage) || !Finite(errorSeverity) || !Finite(startOffset))
                return 0;
            Vector3 output = Forward(model, Mathf.Clamp01(score), Mathf.Clamp01(coverage), Mathf.Clamp01(errorSeverity),
                Mathf.Clamp01(startOffset), new float[Hidden]);
            return FollowFromOutput(output.z);
        }

        // The third output is tanh (-1..1); follow is 0..1.
        static float FollowFromOutput(float output) => Mathf.Clamp01((output + 1) / 2);
        static float OutputFromFollow(float follow) => Mathf.Clamp01(follow) * 2 - 1;

        static Vector3 Forward(Model model, float score, float coverage, float errorSeverity, float startOffset, float[] hidden)
        {
            // Inputs are moved from 0..1 to -1..1.
            float x = score * 2 - 1, y = coverage * 2 - 1, z = errorSeverity * 2 - 1, w = startOffset * 2 - 1;
            for (int h = 0; h < Hidden; h++)
            {
                float sum = model.hiddenBias[h]
                    + model.inputWeights[h * Inputs] * x
                    + model.inputWeights[h * Inputs + 1] * y
                    + model.inputWeights[h * Inputs + 2] * z
                    + model.inputWeights[h * Inputs + 3] * w;
                hidden[h] = Tanh(sum);
            }

            float range = model.outputBias[0], speed = model.outputBias[1], follow = model.outputBias[2];
            for (int h = 0; h < Hidden; h++)
            {
                range += model.outputWeights[h] * hidden[h];
                speed += model.outputWeights[Hidden + h] * hidden[h];
                follow += model.outputWeights[2 * Hidden + h] * hidden[h];
            }
            return new Vector3(Tanh(range), Tanh(speed), Tanh(follow));
        }

        public static bool IsValid(Model m)
        {
            return m != null && m.version == 3 && m.hidden == Hidden && m.inputSchema == InputSchema
                && !string.IsNullOrEmpty(m.modelId)
                && ValidWeights(m.inputWeights, Hidden * Inputs) && ValidWeights(m.hiddenBias, Hidden)
                && ValidWeights(m.outputWeights, Hidden * Outputs) && ValidWeights(m.outputBias, Outputs);
        }

        void LoadModel()
        {
            model = null;
            var asset = Resources.Load<TextAsset>(ModelResource);
            if (!asset)
            {
                Debug.LogWarning("The adaptive coach model is missing. Train it with Tools > P7 > Train Coach Model.");
                return;
            }

            var loaded = JsonUtility.FromJson<Model>(asset.text);
            if (IsValid(loaded))
                model = loaded;
            else
                Debug.LogWarning("The adaptive coach model is invalid.");
        }

        // --- Personal levels ---

        public static string PersonalPath(string settingsPath)
        {
            if (string.IsNullOrEmpty(settingsPath))
                return null;
            string name = Path.GetFileNameWithoutExtension(settingsPath) + "-adaptive.json";
            return Path.Combine(Path.GetDirectoryName(settingsPath), name);
        }

        PersonalLevel PersonalLevelFor(string exerciseId, int side)
        {
            var level = personal.arms.Find(a => a.exerciseId == exerciseId && a.side == side);
            if (level == null)
            {
                var arm = settings.Arm(exerciseId, side);
                level = new PersonalLevel { exerciseId = exerciseId, side = side, speed = arm.speed, range = arm.range };
                personal.arms.Add(level);
            }
            return level;
        }

        void LoadPersonalData()
        {
            personal = new PersonalData();
            if (settings == null || developerMode)
                return;

            string path = PersonalPath(settings.filePath);
            try
            {
                if (path == null || !File.Exists(path))
                {
                    // Older saves only have the settings file: copy their progress over.
                    foreach (var arm in settings.arms)
                        PersonalLevelFor(arm.id, arm.side);
                    return;
                }

                var loaded = JsonUtility.FromJson<PersonalData>(File.ReadAllText(path));
                if (!IsValid(loaded))
                    throw new InvalidDataException("Invalid personal coach data");

                personal = loaded;
                foreach (var level in personal.arms)
                {
                    var arm = settings.Arm(level.exerciseId, level.side);
                    arm.speed = Mathf.Clamp(level.speed, .5f, 2);
                    arm.range = Mathf.Clamp(level.range, arm.automaticRange ? .15f : minimumRangeScale, 1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Personal coach file could not be loaded; using the settings file instead. " + e.Message);
            }
        }

        static bool IsValid(PersonalData data)
        {
            return data != null && data.version == 1 && data.arms != null
                && !data.arms.Exists(a => a == null || string.IsNullOrEmpty(a.exerciseId) || (a.side != 0 && a.side != 1)
                    || !Finite(a.speed) || !Finite(a.range) || !Finite(a.meanScore) || !Finite(a.meanCoverage)
                    || !Finite(a.startShift.x) || !Finite(a.startShift.y) || !Finite(a.startShift.z));
        }

        void SavePersonalData()
        {
            string path = PersonalPath(settings.filePath);
            if (path == null)
                return;
            try
            {
                JsonFile.Write(path, personal);
            }
            catch (Exception e)
            {
                settings.error = "Could not save personal coach progress: " + e.Message;
                Debug.LogWarning(settings.error);
            }
        }

        public static void ResetPersonalData(SavedSettings settings)
        {
            string path = PersonalPath(settings.filePath);
            if (path == null)
                return;
            try
            {
                JsonFile.Write(path, new PersonalData());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not reset personal coach data: " + e.Message);
            }
        }

        // --- Training (editor only, see Tools > P7 > Train Coach Model) ---

        public static Model Train(TrainingSet data, int epochs = 1800)
        {
            ValidateTrainingSet(data);

            var random = new System.Random(42);
            var training = Array.FindAll(data.examples, e => !e.validation);
            var trained = new Model
            {
                epochs = epochs,
                provenance = data.provenance,
                trainingExamples = training.Length,
                validationExamples = data.examples.Length - training.Length,
            };
            if (trained.trainingExamples < 5 || trained.validationExamples < 5)
                throw new InvalidDataException("Need separate training and validation examples");

            for (int i = 0; i < trained.inputWeights.Length; i++)
                trained.inputWeights[i] = (float)(random.NextDouble() - .5);
            for (int i = 0; i < trained.outputWeights.Length; i++)
                trained.outputWeights[i] = (float)(random.NextDouble() - .5);

            var hidden = new float[Hidden];
            const float rate = .025f;
            for (int epoch = 0; epoch < epochs; epoch++)
            {
                for (int i = training.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (training[i], training[j]) = (training[j], training[i]);
                }
                foreach (var example in training)
                    TrainStep(trained, example, hidden, rate);
            }

            foreach (var example in data.examples)
            {
                Vector3 output = Forward(trained, example.score, example.coverage, example.errorSeverity, example.startOffset, hidden);
                float rangeError = output.x - example.rangeChange / RangeStep;
                float speedError = output.y - example.speedChange / SpeedStep;
                float followError = output.z - OutputFromFollow(example.startFollow);
                float error = (rangeError * rangeError + speedError * speedError + followError * followError) / 3;
                if (example.validation)
                    trained.validationMse += error;
                else
                    trained.trainingMse += error;
            }
            trained.trainingMse /= trained.trainingExamples;
            trained.validationMse /= trained.validationExamples;

            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data) + epochs);
                trained.modelId = "nn3-" + BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").Substring(0, 16);
            }

            if (!IsValid(trained) || !Finite(trained.validationMse) || trained.validationMse > .02f)
                throw new InvalidDataException("Training failed validation; the existing model was not replaced");
            return trained;
        }

        // One step of backpropagation for one example.
        static void TrainStep(Model m, Example example, float[] hidden, float rate)
        {
            Vector3 output = Forward(m, example.score, example.coverage, example.errorSeverity, example.startOffset, hidden);

            // Increases are rare in the examples, so they count six times as much.
            float weight = example.rangeChange > 0 || example.speedChange > 0 ? 6 : 1;
            float rangeGradient = weight * (output.x - example.rangeChange / RangeStep) * (1 - output.x * output.x);
            float speedGradient = weight * (output.y - example.speedChange / SpeedStep) * (1 - output.y * output.y);
            float followGradient = (output.z - OutputFromFollow(example.startFollow)) * (1 - output.z * output.z);

            float x = example.score * 2 - 1, y = example.coverage * 2 - 1, z = example.errorSeverity * 2 - 1, w = example.startOffset * 2 - 1;
            for (int h = 0; h < Hidden; h++)
            {
                float hiddenGradient = (rangeGradient * m.outputWeights[h] + speedGradient * m.outputWeights[Hidden + h]
                    + followGradient * m.outputWeights[2 * Hidden + h]) * (1 - hidden[h] * hidden[h]);
                m.inputWeights[h * Inputs] -= rate * hiddenGradient * x;
                m.inputWeights[h * Inputs + 1] -= rate * hiddenGradient * y;
                m.inputWeights[h * Inputs + 2] -= rate * hiddenGradient * z;
                m.inputWeights[h * Inputs + 3] -= rate * hiddenGradient * w;
                m.hiddenBias[h] -= rate * hiddenGradient;
                m.outputWeights[h] -= rate * rangeGradient * hidden[h];
                m.outputWeights[Hidden + h] -= rate * speedGradient * hidden[h];
                m.outputWeights[2 * Hidden + h] -= rate * followGradient * hidden[h];
            }
            m.outputBias[0] -= rate * rangeGradient;
            m.outputBias[1] -= rate * speedGradient;
            m.outputBias[2] -= rate * followGradient;
        }

        static void ValidateTrainingSet(TrainingSet data)
        {
            if (data == null || data.version != 3 || data.examples == null || data.examples.Length < 10)
                throw new InvalidDataException("Need a version-3 labelled training set");

            foreach (var e in data.examples)
            {
                bool valid = e != null
                    && InRange(e.score, 0, 1) && InRange(e.coverage, 0, 1) && InRange(e.errorSeverity, 0, 1)
                    && InRange(e.startOffset, 0, 1) && InRange(e.startFollow, 0, 1)
                    && InRange(e.rangeChange, -RangeStep, RangeStep) && InRange(e.speedChange, -SpeedStep, SpeedStep);
                if (!valid)
                    throw new InvalidDataException("Invalid example: inputs must be 0..1 and changes within per-rep limits");
            }
        }

        static bool InRange(float value, float min, float max) => Finite(value) && value >= min && value <= max;

        static bool ValidWeights(float[] values, int size)
        {
            return values != null && values.Length == size && Array.TrueForAll(values, x => Finite(x) && Mathf.Abs(x) <= 100);
        }

        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

        static float Tanh(float x) => (float)Math.Tanh(x);
    }
}
