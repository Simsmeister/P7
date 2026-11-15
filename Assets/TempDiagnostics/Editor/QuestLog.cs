using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

static class QuestLog
{
    const string OutFile = "Temp/quest-log.txt";

    static string Adb()
    {
        string sdk = EditorPrefs.GetString("AndroidSdkRoot");
        if (string.IsNullOrEmpty(sdk) || !Directory.Exists(sdk))
            sdk = Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath, "../../PlaybackEngines/AndroidPlayer/SDK"));
        if (!Directory.Exists(sdk))
            sdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK");
        return Path.Combine(sdk, "platform-tools", "adb");
    }

    static string Run(string adb, string args, int timeoutMs = 120000)
    {
        var info = new ProcessStartInfo(adb, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var p = Process.Start(info))
        {
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs)) { p.Kill(); return "TIMEOUT " + args; }
            return "$ adb " + args + "\n" + o.Result + e.Result + "\n";
        }
    }

    [MenuItem("Tools/Temp/Install, Launch and Log")]
    static void InstallLaunchLog()
    {
        string adb = Adb();
        string apk = Path.GetFullPath("Builds/P7-Quest3.apk");
        string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        File.WriteAllText(OutFile, "RUNNING\n");
        new Thread(() =>
        {
            var sb = new StringBuilder();
            try
            {
                sb.Append(Run(adb, "devices -l", 20000));
                sb.Append(Run(adb, "-d logcat -c", 20000));
                sb.Append(Run(adb, "-d install -r \"" + apk + "\"", 300000));
                sb.Append(Run(adb, "-d shell monkey -p " + package + " -c android.intent.category.LAUNCHER 1", 30000));
                Thread.Sleep(15000);
                sb.Append(Run(adb, "-d shell pidof " + package, 20000));
                sb.Append(Run(adb, "-d logcat -d -v time", 60000));
                sb.Append("DONE\n");
            }
            catch (System.Exception ex) { sb.Append("ERROR " + ex + "\n"); }
            File.WriteAllText(OutFile, sb.ToString());
        }).Start();
    }

    [MenuItem("Tools/Temp/Clear Quest Log")]
    static void ClearLog() { File.WriteAllText(OutFile, Run(Adb(), "-d logcat -c", 20000)); }

    [MenuItem("Tools/Temp/Save Quest Log")]
    static void SaveLog()
    {
        string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string adb = Adb();
        File.WriteAllText(OutFile, Run(adb, "-d shell pidof " + package, 20000) + Run(adb, "-d logcat -d -v time", 60000)
            + Run(adb, "-d shell ls -la /data/tombstones", 20000) + "DONE\n");
    }
}
