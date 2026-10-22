using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EmilVR;
[InitializeOnLoad]
public static class ShieldExerciseVerification
{
    static ShieldExerciseGame game;static double deadline;static bool started;
    static ShieldExerciseVerification(){if(SessionState.GetBool("ShieldTest",false))EditorApplication.update+=Tick;}
    public static void VerifiedBuild(){ShieldCoachSetup.Build();ShieldExerciseSetup.Verify();EmilVRBuild.Build();}
    public static void Run(){ShieldCoachSetup.Build();ShieldExerciseSetup.Verify();EditorSceneManager.OpenScene("Assets/Scenes/Emil.unity");SessionState.SetBool("ShieldTest",true);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try {
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+180;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Shield integration timeout");
            if(!game)game=UnityEngine.Object.FindFirstObjectByType<ShieldExerciseGame>();if(!game||Time.timeSinceLevelLoad<1)return;
            if(!started){for(int i=0;i<game.exercises.Length;i++){var p=UnityEngine.Object.Instantiate(game.exercises[i]);p.outwardSeconds=.6f;p.holdSeconds=.3f;p.returnSeconds=.6f;p.restSeconds=.2f;p.repetitions=2;game.exercises[i]=p;}game.TestMode=true;game.Begin();started=true;}
            if(game.Finished){if(game.CompletedRepetitions!=12||game.Score<99.9||game.coach==null||game.arcade==null||game.arcade.Points<=0||game.arcade.Combo!=12)throw new Exception("Expected 12 perfect repetitions; got "+game.CompletedRepetitions+" score "+game.Score);
                var summaries=Directory.GetFiles("/tmp/emil-shield-tests","summary.csv",SearchOption.AllDirectories);Array.Sort(summaries);var rows=File.ReadAllLines(summaries[summaries.Length-1]);
                if(rows.Length!=13||rows[0].Split(',').Length!=15)throw new Exception("Summary schema or row count");
                for(int i=2;i<rows.Length;i++)if(rows[i].Split(',')[0]==rows[i-1].Split(',')[0])throw new Exception("Exercises should alternate");
                var files=Directory.GetFiles("/tmp/emil-shield-tests","replay.json",SearchOption.AllDirectories);Array.Sort(files);if(!game.replay.LoadRecording(files[files.Length-1])||!game.replay.Open()||game.replay.Data.equipment!="shield")throw new Exception("Shield replay roundtrip");
                Debug.Log("SHIELD_INTEGRATION_PASSED reps="+game.CompletedRepetitions+" score="+game.Score+" frames="+game.replay.Data.frames.Count);SessionState.SetBool("ShieldTest",false);EditorApplication.Exit(0);
            }
        }catch(Exception e){Debug.LogException(e);SessionState.SetBool("ShieldTest",false);EditorApplication.Exit(1);}
    }
}
