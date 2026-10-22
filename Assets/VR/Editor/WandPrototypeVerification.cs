using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EmilVR;

[InitializeOnLoad]
public static class WandPrototypeVerification
{
    static double deadline;
    static bool demoSeen,started,opened;
    static WandPathGame game;
    static WandPrototypeVerification(){if(SessionState.GetBool("WandV2Test",false))EditorApplication.update+=Tick;}
    public static void Run()
    {
        WandPathPrototypeSetup.Verify();
        EditorSceneManager.OpenScene("Assets/Scenes/Emil.unity");
        SessionState.SetBool("WandV2Test",true);
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+100;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Demo/session verification timed out");
            if(!game)game=UnityEngine.Object.FindFirstObjectByType<WandPathGame>();
            if(!game)return;
            if(!started)
            {
                if(Time.timeSinceLevelLoad<1)return;
                game.roundSeconds=5;game.SendMessage("Calibrate");started=true;
            }
            if(game.replay.IsDemo){demoSeen=true;game.RecordSimulationForTesting=true;}
            if(!demoSeen)return;
            if(!game.replay.IsDemo && game.replay.Data!=null && game.replay.Data.frames.Count>1 && !opened)
            {
                // Wait for the complete 15-second session, not an in-progress frame count.
                var files=Directory.GetFiles("/tmp/emil-wand-prototype-test","replay.json",SearchOption.AllDirectories);
                Array.Sort(files);if(files.Length==0)return;
                var file=files[files.Length-1];if(File.GetLastWriteTimeUtc(file)<DateTime.UtcNow.AddSeconds(-10))return;
                if(!game.replay.LoadRecording(file)||!game.replay.Open())throw new Exception("Replay disk round trip failed");
                if(Mathf.Abs(game.replay.Data.wandLength-.6f)>.001f)throw new Exception("Sword recording length");
                if(Mathf.Abs(game.replay.wands[0].Find("Tip").localPosition.z-.6f)>.001f)throw new Exception("Replay sword geometry");
                if(game.Score<99.9f)throw new Exception("Perfect simulated score below 100");
                opened=true;Debug.Log("WAND_V2_PLAY_TEST_PASSED demo=18s session=15s score="+game.Score+" frames="+game.replay.Data.frames.Count);
                SessionState.SetBool("WandV2Test",false);EditorApplication.Exit(0);
            }
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetBool("WandV2Test",false);EditorApplication.Exit(1);}
    }
}
