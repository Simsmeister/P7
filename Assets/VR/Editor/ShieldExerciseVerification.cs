using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EmilVR;
[InitializeOnLoad]
public static class ShieldExerciseVerification
{
    static int activeRep=-1;static float activeDuration;static Vector3 activeEndpoint;static ShieldExerciseGame game;static double deadline;static bool started;static bool sawApproach,skippedDemo,skippedTutorial;
    static ShieldExerciseVerification(){if(SessionState.GetBool("ShieldTest",false))EditorApplication.update+=Tick;}
    public static void VerifiedBuild(){ExercisePathSetup.Build();ConduitSetup.Build();ShieldExerciseSetup.Verify();EmilVRBuild.Build();}
    public static void Run(){ExercisePathSetup.Build();ConduitSetup.Build();ShieldExerciseSetup.Verify();EditorSceneManager.OpenScene("Assets/Scenes/Emil.unity");SessionState.SetBool("ShieldTest",true);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try {
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+240;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Shield integration timeout");
            if(!game)game=UnityEngine.Object.FindFirstObjectByType<ShieldExerciseGame>();if(!game||Time.timeSinceLevelLoad<1)return;
            if(!started){for(int i=0;i<game.exercises.Length;i++){var p=UnityEngine.Object.Instantiate(game.exercises[i]);p.outwardSeconds=.6f;p.holdSeconds=.3f;p.returnSeconds=.6f;p.restSeconds=.2f;p.repetitions=3;game.exercises[i]=p;}game.TestMode=true;game.PlayerSettings=new ExercisePlayerSettings{totalRepetitions=18,filePath="/tmp/emil-integration-player-"+Guid.NewGuid()+".json"};game.menu.Navigate("settings");
                var toggle=game.menu.widgets.Find(w=>w.key=="left:0").toggle;toggle.isOn=false;if(game.PlayerSettings.Arm(game.exercises[0].exerciseId,0).enabled)throw new Exception("Menu toggle not wired");toggle.isOn=true;
                var total=game.menu.widgets.Find(w=>w.key=="total").slider;total.value=12;if(game.PlayerSettings.totalRepetitions!=12)throw new Exception("Menu slider not wired");total.value=18;
                game.menu.Navigate("reset");game.menu.Navigate("back");if(game.PlayerSettings.totalRepetitions!=18)throw new Exception("Cancel reset changed settings");
                game.PlayerSettings.totalRepetitions=7;game.PlayerSettings.devMode=true;game.PlayerSettings.tutorialComplete=true;game.PlayerSettings.Arm(game.exercises[0].exerciseId,0).speed=1.5f;
                game.menu.Navigate("reset");game.menu.widgets.Find(w=>w.key=="confirmReset").button.onClick.Invoke();
                var reset=ExercisePlayerSettings.Load(game.PlayerSettings.filePath);if(reset.totalRepetitions!=18||reset.devMode||reset.tutorialComplete||reset.Arm(game.exercises[0].exerciseId,0).speed!=1||reset.arms.Exists(a=>a.calibration!=null))throw new Exception("Reset settings did not persist defaults");
                game.menu.Navigate("home");var oldHead=game.head.position;var oldRotation=game.head.rotation;game.head.SetPositionAndRotation(oldHead+Vector3.right,Quaternion.Euler(0,60,0));game.menu.Recenter();if(Vector3.Distance(game.menu.transform.position,game.head.position+Quaternion.Euler(0,60,0)*new Vector3(0,-.2f,2.3f))>.001f)throw new Exception("Menu recenter pose");game.head.SetPositionAndRotation(oldHead,oldRotation);game.menu.Recenter();var button=game.menu.widgets.Find(w=>w.key=="start");if(!ExerciseGameMenu.Hit(button.rect,new Ray(button.rect.position-button.rect.forward,button.rect.forward),out _))throw new Exception("VR ray menu hit");button.button.onClick.Invoke();if(game.CurrentStep!="Demo")throw new Exception("Start button must calibrate first time");started=true;}
            if(!skippedDemo&&game.CurrentStep=="Demo"&&game.replay.IsDemo){game.SkipTutorialPreview();if(game.CurrentStep!="CaptureStart")throw new Exception("Preview skip must retain calibration");skippedDemo=true;}
            if(game.CurrentStep=="Tutorial"){game.SkipTutorialPreview();if(game.CurrentStep!="Approaching")throw new Exception("Tutorial skip should start approach");skippedTutorial=true;}
            game.TestControllerDegrees=135;game.TestHandFraction=game.CompletedRepetitions<6?.35f:1;game.TestHeadDegrees=game.CompletedRepetitions<6?30:85;
            if(game.CurrentStep=="Playing"){
                if(Quaternion.Angle(game.shields[game.ActiveSide].rotation,game.CoachRotation)>.01f)throw new Exception("Tool must not rotate with controller");
                if(game.CompletedRepetitions>=6&&game.PathClock>.1f&&game.RepairQuality<.999f)throw new Exception("Controller rotation reduced position accuracy");
                game.PathDefinition(0,out var current,out var calibration,out _);
                if(activeRep!=game.CompletedRepetitions){activeRep=game.CompletedRepetitions;activeDuration=current.outwardSeconds;activeEndpoint=calibration.end;}
                else if(Mathf.Abs(activeDuration-current.outwardSeconds)>.00001f||Vector3.Distance(activeEndpoint,calibration.end)>.00001f)throw new Exception("Difficulty changed during an attempt");
            }
            if(game.IsMission&&game.CurrentStep=="Ready")throw new Exception("Mission must not stop for a between-exercise countdown");
            if(game.CurrentStep=="Approaching")sawApproach=true;
            if(game.Finished){if(!skippedDemo||!skippedTutorial||!game.adaptiveCoach)throw new Exception("Missing skip or adaptive coach integration");if(!sawApproach)throw new Exception("Missing finite-path approach gap");if(game.CompletedRepetitions<18||!game.conduit||game.conduit.Run.outcome!="victory"||(game.Score<=0||game.Score>=99)||game.pathStream==null||game.coach==null||game.arcade==null||game.arcade.Points<=0)throw new Exception("Expected 18 mixed poor/good repetitions; got "+game.CompletedRepetitions+" score "+game.Score);
                var summaries=Directory.GetFiles("/tmp/emil-shield-tests","summary.csv",SearchOption.AllDirectories);Array.Sort(summaries);var rows=File.ReadAllLines(summaries[summaries.Length-1]);
                var audit=JsonUtility.FromJson<AdaptiveExerciseCoach.Report>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(summaries[summaries.Length-1]),"adaptive-coach.json")));if(audit.decisions.Length!=game.CompletedRepetitions)throw new Exception("Every repetition needs a coach audit row");
                if(!Array.Exists(audit.decisions,d=>d.durationScaleUsed<1))throw new Exception("Adaptive pace was not exercised in gameplay");
                if(rows.Length!=game.CompletedRepetitions+1||rows[0].Split(',').Length!=18)throw new Exception("Summary schema or row count");
                int reductions=0,increases=0,applied=0;
                for(int i=0;i<audit.decisions.Length;i++){
                    var d=audit.decisions[i];if(i<6){if(d.nextRangeScale>=d.rangeScaleUsed||d.nextDurationScale<=d.durationScaleUsed)throw new Exception("Poor/head-turn attempt failed to ease");reductions++;}
                    if(d.nextRangeScale>d.rangeScaleUsed&&d.nextDurationScale<d.durationScaleUsed)increases++;
                    for(int j=i+1;j<audit.decisions.Length;j++){var next=audit.decisions[j];if(next.exerciseId!=d.exerciseId||next.side!=d.side)continue;
                        if(Mathf.Abs(next.durationScaleUsed-d.nextDurationScale)>.0001f||Mathf.Abs(next.rangeScaleUsed-d.nextRangeScale)>.0001f)throw new Exception("Adaptation was not applied to the next matching attempt");applied++;break;}
                }
                if(reductions!=6||increases==0||applied==0)throw new Exception("Missing down/up/next-attempt coverage");
                var files=Directory.GetFiles("/tmp/emil-shield-tests","replay.json",SearchOption.AllDirectories);Array.Sort(files);if(!game.replay.LoadRecording(files[files.Length-1])||!game.replay.Open()||game.replay.Data.equipment!="shield")throw new Exception("Shield replay roundtrip");
                game.replay.Close();var restoredSettings=ExercisePlayerSettings.Load(game.PlayerSettings.filePath);if(!restoredSettings.tutorialComplete||restoredSettings.arms.Exists(a=>a.calibration==null))throw new Exception("Calibration/tutorial persistence");game.PlayerSettings=restoredSettings;game.Begin();if(game.CurrentStep!="Approaching"||Mathf.Abs(game.ActiveProfile.outwardSeconds-game.exercises[Array.FindIndex(game.exercises,p=>p.exerciseId==game.ActiveProfile.exerciseId)].outwardSeconds/restoredSettings.Arm(game.ActiveProfile.exerciseId,game.ActiveSide).speed)>.0001f)throw new Exception("Saved start must skip tutorial and retain adaptation");
                Debug.Log("SHIELD_INTEGRATION_PASSED conduit reaches victory after extended sequence; poor/head-turn easing, successful recovery, next-attempt application, stable in-progress targets, saved settings");SessionState.SetBool("ShieldTest",false);EditorApplication.Exit(0);
            }
        }catch(Exception e){Debug.LogException(e);SessionState.SetBool("ShieldTest",false);EditorApplication.Exit(1);}
    }
}
