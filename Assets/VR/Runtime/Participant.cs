using System;
using System.IO;
using UnityEngine;

namespace P7
{
    // Anonymous participant number per headset (P001, P002, ...), saved as participant.json.
    // It lives in its own file so resetting the settings never loses it.
    [Serializable]
    public class Participant
    {
        public int number;

        [NonSerialized] public string filePath;
        [NonSerialized] public string error;

        [Serializable]
        class ExportedSession
        {
            public string participantId;
        }

        public string Id => FormatId(number);
        public string NextId => FormatId(NextNumber(number));

        public static string FormatId(int number) => "P" + number.ToString("000");

        public static int NextNumber(int current) => Mathf.Max(current, 0) + 1;

        public static int ParseId(string id)
        {
            if (id == null || id.Length < 2 || id[0] != 'P')
                return 0;
            return int.TryParse(id.Substring(1), out int n) && n > 0 ? n : 0;
        }

        public static int ParseFile(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<Participant>(json);
                return data != null && data.number > 0 ? data.number : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static Participant Load(string path, string sessionsFolder)
        {
            string text = null;
            bool existed = false;
            try
            {
                existed = File.Exists(path);
                if (existed)
                    text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }

            var participant = new Participant { filePath = path, number = ParseFile(text) };
            if (participant.number > 0)
                return participant;

            // The file is missing or damaged: continue after the highest ID already exported, so no ID is reused.
            int highest = HighestExportedNumber(sessionsFolder);
            participant.number = NextNumber(highest);
            participant.Save();
            if (participant.error == null && (existed || highest > 0))
                participant.error = "Participant ID file was missing or damaged. Continuing at " + participant.Id + ".";
            return participant;
        }

        public void Advance()
        {
            number = NextNumber(number);
            Save();
        }

        void Save()
        {
            try
            {
                JsonFile.Write(filePath, this);
                error = null;
            }
            catch (Exception e)
            {
                error = "Could not save participant ID. IDs may repeat after a restart.";
                Debug.LogWarning(error + " " + e.Message);
            }
        }

        static int HighestExportedNumber(string sessionsFolder)
        {
            int highest = 0;
            try
            {
                if (!Directory.Exists(sessionsFolder))
                    return 0;

                foreach (string folder in Directory.GetDirectories(sessionsFolder))
                {
                    try
                    {
                        string file = Path.Combine(folder, "session.json");
                        if (!File.Exists(file))
                            continue;
                        var session = JsonUtility.FromJson<ExportedSession>(File.ReadAllText(file));
                        highest = Mathf.Max(highest, ParseId(session.participantId));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
            return highest;
        }
    }
}
