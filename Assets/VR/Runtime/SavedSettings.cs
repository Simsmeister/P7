using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace P7
{
    [Serializable]
    public class ArmSettings
    {
        public string id;
        public int side;
        public bool enabled = true;
        public bool automaticRange;
        public float speed = 1, range = 1;
        public float devSpeed = 1, devRange = 1;
        // Stored relative to the head (see Calibration.ToLocal).
        public Calibration calibration;
    }

    [Serializable]
    public class SpeedLimit
    {
        public string id;
        public float fasterPercent = 20;  // ceiling 120 % speed (was 40, lowered 2026-11-18 for players with injuries)
    }

    // Everything the player or therapist can change, saved as player-settings.json.
    [Serializable]
    public class SavedSettings
    {
        public int version = 1;
        public int totalRepetitions = 18;
        public bool tutorialComplete;
        // Settings switch: run the tutorial on every START, not only the first time.
        public bool tutorialEveryStart;
        public bool devMode;
        public List<ArmSettings> arms = new List<ArmSettings>();
        public List<SpeedLimit> limits = new List<SpeedLimit>();

        [NonSerialized] public string filePath;
        [NonSerialized] public string error;

        public ArmSettings Arm(string id, int side)
        {
            var arm = arms.Find(a => a.id == id && a.side == side);
            if (arm == null)
            {
                arm = new ArmSettings { id = id, side = side };
                arms.Add(arm);
            }
            return arm;
        }

        public SpeedLimit Limit(string id)
        {
            var limit = limits.Find(l => l.id == id);
            if (limit == null)
            {
                limit = new SpeedLimit { id = id };
                limits.Add(limit);
            }
            return limit;
        }

        public static SavedSettings Load(string path)
        {
            SavedSettings data = null;
            string problem = null;
            try
            {
                if (File.Exists(path))
                    data = JsonUtility.FromJson<SavedSettings>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                problem = "Saved settings could not be read: " + e.Message;
            }

            if (data == null || data.version != 1)
            {
                data = new SavedSettings();
                if (File.Exists(path))
                    problem = "Saved settings unavailable. A gentle automatic start will be used.";
            }

            data.filePath = path;
            data.error = problem;
            data.arms ??= new List<ArmSettings>();
            data.limits ??= new List<SpeedLimit>();
            data.totalRepetitions = Mathf.Clamp(data.totalRepetitions, 1, 120);

            foreach (var arm in data.arms)
            {
                arm.speed = SafeClamp(arm.speed, .5f, 2, 1);
                arm.range = SafeClamp(arm.range, arm.automaticRange ? .15f : .4f, 1, 1);
                arm.devSpeed = SafeClamp(arm.devSpeed, .5f, 2, 1);
                arm.devRange = SafeClamp(arm.devRange, .3f, 1, 1);
            }
            foreach (var limit in data.limits)
                limit.fasterPercent = SafeClamp(limit.fasterPercent, 0, 100, 20);

            return data;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(filePath))
                return;
            try
            {
                JsonFile.Write(filePath, this);
                error = null;
            }
            catch (Exception e)
            {
                error = "Settings could not be saved: " + e.Message;
                Debug.LogWarning(error);
            }
        }

        // The order of exercises for a session. Each entry is exerciseIndex * 2 + side (left 0, right 1).
        // Every enabled exercise and arm gets the same number of turns, in a random order.
        public List<int> Playlist(ExerciseProfile[] profiles, int? seed = null)
        {
            var enabled = new List<int>();
            for (int side = 0; side < 2; side++)
                for (int e = 0; e < profiles.Length; e++)
                    if (Arm(profiles[e].exerciseId, side).enabled)
                        enabled.Add(e * 2 + side);

            var result = new List<int>();
            if (enabled.Count == 0)
                return result;

            var random = new System.Random(seed ?? Guid.NewGuid().GetHashCode());
            // Shuffle first so short sessions do not always favour the same exercise.
            Shuffle(enabled, random);
            int count = Mathf.Clamp(totalRepetitions, 1, 120);
            for (int i = 0; i < count; i++)
                result.Add(enabled[i % enabled.Count]);
            Shuffle(result, random);
            return result;
        }

        static void Shuffle(List<int> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        static float SafeClamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return Mathf.Clamp(value, min, max);
        }
    }

    public static class JsonFile
    {
        // Writes to a temporary file first, so a crash never leaves a half-written file behind.
        public static void Write(string path, object data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
    }
}
