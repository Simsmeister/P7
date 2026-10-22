using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WandDataExport
{
    [MenuItem("Tools/Emil VR/Export Quest Recordings")]
    public static void Export()
    {
        string destination=EditorUtility.OpenFolderPanel("Export Quest sessions to this folder",Path.Combine(Directory.GetCurrentDirectory(),"Recordings"),"");
        if(string.IsNullOrEmpty(destination))return;
        string sdk=EditorPrefs.GetString("AndroidSdkRoot");
        if(string.IsNullOrEmpty(sdk)||!Directory.Exists(sdk))sdk=Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines/AndroidPlayer/SDK");
        // macOS installs playback engines beside the .app; Windows installs under Editor/Data.
        if(!Directory.Exists(sdk))sdk=Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath,"../../PlaybackEngines/AndroidPlayer/SDK"));
        string adb=Path.Combine(sdk,"platform-tools",Application.platform==RuntimePlatform.WindowsEditor?"adb.exe":"adb");
        if(!File.Exists(adb)){EditorUtility.DisplayDialog("Export","Android SDK not found. Install Android Build Support in Unity Hub.","OK");return;}
        try
        {
            var info=new ProcessStartInfo(adb,"-d pull /sdcard/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/Sessions \""+destination.Replace("\"","\\\"")+"\"") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var process=Process.Start(info))
            {
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(60000)){process.Kill();throw new Exception("Export timed out. Check USB connection and headset authorization.");}
                if(process.ExitCode!=0)throw new Exception(errors.Result+output.Result);
            }
            EditorUtility.RevealInFinder(destination);
            EditorUtility.DisplayDialog("Recordings exported","Copied saved sessions to:\n"+destination+"\n\nsummary.csv: challenge results\nsamples.csv: raw movement\nsession.json: calibration and scoring rules\nreplay.json: replay in Unity\n\nOriginal sessions remain on the Quest.","OK");
        }
        catch(Exception e){EditorUtility.DisplayDialog("Export could not complete","Connect the Quest by USB, enable developer mode and accept USB debugging in the headset.\n\n"+e.Message,"OK");}
    }
}
