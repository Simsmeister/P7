using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EmilVR;
using System.IO;
[InitializeOnLoad]
public static class ShieldPreviewCapture
{
    static bool menuInitialized;static int menuStage;static ShieldExerciseGame game;static bool started,demo,calibration,reach,approach,restored;static double deadline;
    static ShieldPreviewCapture(){if(SessionState.GetBool("ShieldPreview",false))EditorApplication.update+=Tick;}
    public static void RunMenu(){SessionState.SetBool("MenuOnlyPreview",true);Run();}
    public static void Run(){ExercisePathSetup.Build();ConduitSetup.Build();EditorSceneManager.OpenScene("Assets/Scenes/Emil.unity");SessionState.SetBool("ShieldPreview",true);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;}
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
        if(!menuInitialized){game.PlayerSettings=new ExercisePlayerSettings();game.menu.Navigate("home");menuInitialized=true;return;}
        if(!started&&menuStage<5){
            if(menuStage==0){Capture("emil-menu-home");for(int i=0;i<ExerciseGameMenu.UnlockPresses;i++)game.menu.Navigate("settings");}
            else if(menuStage==1){Capture("emil-menu-settings");game.menu.Navigate("exercise:0");}
            else if(menuStage==2){Capture("emil-menu-exercise");game.menu.Change("dev",1);}
            else if(menuStage==3){Capture("emil-menu-dev");game.menu.Change("dev",0);game.menu.Navigate("reset");}
            else{Capture("emil-menu-reset");game.menu.Navigate("home");}
            menuStage++;if(menuStage==5&&SessionState.GetBool("MenuOnlyPreview",false)){SessionState.SetBool("MenuOnlyPreview",false);SessionState.SetBool("ShieldPreview",false);Debug.Log("MENU_PREVIEW_CAPTURED");EditorApplication.Exit(0);}return;
        }
        if(!started){game.PlayerSettings=new ExercisePlayerSettings{totalRepetitions=18};for(int e=1;e<game.exercises.Length;e++)for(int s=0;s<2;s++)game.PlayerSettings.Arm(game.exercises[e].exerciseId,s).enabled=false;for(int i=0;i<game.exercises.Length;i++){var p=Object.Instantiate(game.exercises[i]);p.outwardSeconds=1.2f;p.holdSeconds=.4f;p.returnSeconds=1.2f;p.restSeconds=.2f;game.exercises[i]=p;}game.TestMode=true;game.Begin();started=true;}
        if(!restored&&game.CurrentStep=="Tutorial"){foreach(var p in game.exercises){p.outwardSeconds=3;p.holdSeconds=1.5f;p.returnSeconds=3;p.restSeconds=1;}restored=true;}
        if(!approach&&game.CurrentStep=="Approaching"&&game.PathClock>-1.5f){Capture("emil-path-approaching");approach=true;}
        if(!calibration&&game.CurrentStep=="CaptureStart"&&game.AutomaticCaptureProgress>.3f){Capture("emil-path-calibration");calibration=true;}
        if(!reach&&game.ActiveProfile.movement==ShieldMovement.SerratusReach&&game.CurrentStep=="CaptureEnd"&&game.AutomaticCaptureProgress>.3f){Capture("emil-path-reach");reach=true;}
        if(!demo&&game.replay.IsDemo&&game.replay.PlaybackTime>.7f){Capture("emil-path-mirror");demo=true;}
        if(demo&&!game.replay.IsActive&&game.replay.Data!=null&&game.replay.Data.inputSource=="SIMULATED TEST"&&game.replay.Data.frames.Count>90&&game.CurrentStep=="Playing"&&game.PathClock>3.2f){Capture("emil-path-playing");SessionState.SetBool("ShieldPreview",false);Debug.Log("SHIELD_PREVIEW_CAPTURED");EditorApplication.Exit(0);}
    }
}
