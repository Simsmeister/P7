using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using P7;
using Debug = UnityEngine.Debug;

// Everything under Tools > P7.
public static class P7Tools
{
    const string Scene = "Assets/Scenes/P7.unity";
    const string ApkPath = "Builds/P7-Quest3.apk";
    const string TrainingFile = "Assets/VR/Training/coach-training.json";
    const string ModelFile = "Assets/Resources/AdaptiveCoach/base-model.json";

    [MenuItem("Tools/P7/Switch to Quest Android")]
    static void SwitchToAndroid()
    {
        EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
    }

    [MenuItem("Tools/P7/Build Quest APK")]
    public static void Build() => Build(false);

    [MenuItem("Tools/P7/Clean Build Quest APK")]
    static void CleanBuild() => Build(true);

    static void Build(bool clean)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.LogWarning("Switch to Quest Android first, then build again.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        var options = BuildOptions.Development;
        if (clean)
            options |= BuildOptions.CleanBuildCache;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = options,
        });
        Debug.Log("P7 build: " + report.summary.result + ", " + report.summary.totalErrors + " errors");
    }

    // Copies the saved sessions from the headset over USB.
    [MenuItem("Tools/P7/Export Quest Recordings")]
    static void ExportRecordings()
    {
        string destination = EditorUtility.OpenFolderPanel("Export Quest sessions to this folder",
            Path.Combine(Directory.GetCurrentDirectory(), "Recordings"), "");
        if (string.IsNullOrEmpty(destination))
            return;

        string adb = FindAdb();
        if (adb == null)
        {
            EditorUtility.DisplayDialog("Export", "Android SDK not found. Install Android Build Support in Unity Hub.", "OK");
            return;
        }

        string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string source = "/sdcard/Android/data/" + package + "/files/Sessions";
        try
        {
            var info = new ProcessStartInfo(adb, "-d pull " + source + " \"" + destination.Replace("\"", "\\\"") + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    throw new Exception("Export timed out. Check the USB connection and headset authorization.");
                }
                if (process.ExitCode != 0)
                    throw new Exception(errors.Result + output.Result);
            }

            EditorUtility.RevealInFinder(destination);
            EditorUtility.DisplayDialog("Recordings exported", "Copied saved sessions to:\n" + destination
                + "\n\nsummary.csv: one row per repetition\nsamples.csv: raw movement\nsession.json: calibration and scoring rules"
                + "\nreplay.json: replay in Unity\n\nThe sessions also remain on the Quest.", "OK");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Export could not complete",
                "Connect the Quest by USB, enable developer mode and accept USB debugging in the headset.\n\n" + e.Message, "OK");
        }
    }

    static string FindAdb()
    {
        string sdk = EditorPrefs.GetString("AndroidSdkRoot");
        if (string.IsNullOrEmpty(sdk) || !Directory.Exists(sdk))
            sdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK");
        // On macOS the playback engines are installed next to the .app.
        if (!Directory.Exists(sdk))
            sdk = Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath, "../../PlaybackEngines/AndroidPlayer/SDK"));

        string adb = Path.Combine(sdk, "platform-tools", Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb");
        return File.Exists(adb) ? adb : null;
    }

    [MenuItem("Tools/P7/Load Replay File in Play Mode")]
    static void LoadReplay()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode first.");
            return;
        }

        string file = EditorUtility.OpenFilePanel("Select exported replay.json", "", "json");
        if (string.IsNullOrEmpty(file))
            return;

        var replay = UnityEngine.Object.FindFirstObjectByType<SessionReplay>();
        if (!replay.LoadRecording(file) || !replay.Open())
            Debug.LogError("Invalid replay file.");
    }

    // Retrains the adaptive coach network from the labelled examples. See ADAPTIVE_COACH.md.
    [MenuItem("Tools/P7/Train Coach Model")]
    static void TrainCoachModel()
    {
        var data = JsonUtility.FromJson<AdaptiveCoach.TrainingSet>(File.ReadAllText(TrainingFile));
        var model = AdaptiveCoach.Train(data);
        JsonFile.Write(ModelFile, model);
        AssetDatabase.Refresh();
        Debug.Log("Coach model trained: " + model.modelId + ", training MSE " + model.trainingMse + ", validation MSE " + model.validationMse);
    }
}
