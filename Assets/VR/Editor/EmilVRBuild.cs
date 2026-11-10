using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class EmilVRBuild
{
    static bool cleanBuild;
    [MenuItem("Tools/Emil VR/Clean Build Quest APK")]
    public static void CleanBuild()
    {
        cleanBuild = true;
        try { Build(); } finally { cleanBuild = false; }
    }
    [MenuItem("Tools/Emil VR/Restart Editor for Build")]
    public static void RestartEditor()
    {
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
    }
    [MenuItem("Tools/Emil VR/Switch to Quest Android")]
    public static void Switch() { EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android); }
    [MenuItem("Tools/Emil VR/Build Quest APK")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        { Debug.LogWarning("Switch to Quest Android first, then build again."); return; }
        Directory.CreateDirectory("Builds");
        var log = new System.Text.StringBuilder();
        Application.LogCallback cb = (m, st, t) => { if (t != LogType.Log) log.AppendLine(t + ": " + m + "\n" + st); };
        Application.logMessageReceived += cb;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Emil.unity" }, locationPathName = "Builds/Emil-Quest3.apk",
            target = BuildTarget.Android, options = BuildOptions.Development | (cleanBuild ? BuildOptions.CleanBuildCache : BuildOptions.None) });
        Application.logMessageReceived -= cb;
        foreach (var step in report.steps) foreach (var msg in step.messages) if (msg.type == LogType.Error || msg.type == LogType.Exception) log.AppendLine("STEP " + step.name + ": " + msg.content);
        File.WriteAllText("Builds/build-log.txt", "RESULT " + report.summary.result + " errors=" + report.summary.totalErrors + "\n" + log);
        Debug.Log("EMIL_VR_BUILD " + report.summary.result + " errors=" + report.summary.totalErrors);
    }
}
