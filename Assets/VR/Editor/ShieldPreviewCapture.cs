using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EmilVR;
using System.IO;
[InitializeOnLoad]
public static class ShieldPreviewCapture
{
    static ShieldExerciseGame game;static bool started,demo,calibration,reach;static double deadline;
    static ShieldPreviewCapture(){if(SessionState.GetBool("ShieldPreview",false))EditorApplication.update+=Tick;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Emil.unity");SessionState.SetBool("ShieldPreview",true);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;}
    static void Capture(string name)
    {
        var camera=game.head.GetComponent<Camera>();var rt=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("/tmp/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        if(deadline==0)deadline=EditorApplication.timeSinceStartup+180;
        if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool("ShieldPreview",false);EditorApplication.Exit(1);}
        if(!game)game=Object.FindFirstObjectByType<ShieldExerciseGame>();if(!game||Time.timeSinceLevelLoad<1)return;
        if(!started){for(int i=0;i<game.exercises.Length;i++){var p=Object.Instantiate(game.exercises[i]);p.outwardSeconds=1.2f;p.holdSeconds=.4f;p.returnSeconds=1.2f;p.restSeconds=.2f;game.exercises[i]=p;}game.TestMode=true;game.Begin();started=true;}
        if(!calibration&&game.CurrentStep=="CaptureStart"&&game.AutomaticCaptureProgress>.3f){Capture("emil-auto-calibration");calibration=true;}
        if(!reach&&game.ActiveProfile.movement==ShieldMovement.SerratusReach&&game.CurrentStep=="CaptureEnd"&&game.AutomaticCaptureProgress>.3f){Capture("emil-four-view-reach");reach=true;}
        if(!demo&&game.replay.IsDemo&&game.replay.PlaybackTime>.7f){Capture("emil-shield-demo");demo=true;}
        if(demo&&!game.replay.IsActive&&game.replay.Data!=null&&game.replay.Data.inputSource=="SIMULATED TEST"&&game.replay.Data.frames.Count>20){Capture("emil-shield-play");SessionState.SetBool("ShieldPreview",false);Debug.Log("SHIELD_PREVIEW_CAPTURED");EditorApplication.Exit(0);}
    }
}
