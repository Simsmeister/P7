using UnityEngine;
using UnityEditor;
using EmilVR;
public static class ExercisePathSetup
{
    public static void Build()
    {
        ShieldCoachSetup.Build();
        const string assetPath="Assets/VR/Shield/Exercise Path Stream.prefab";
        var root=new GameObject("Exercise Path Stream");var stream=root.AddComponent<ExercisePathStream>();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/VR/Shield/Energy Pulse.mat");
        var marker=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VR/Shield/Coach Hand Marker.prefab");
        for(int n=0;n<2;n++){
            var line=new GameObject(n==0?"Current exercise curve":"Incoming exercise curve",typeof(LineRenderer));line.transform.SetParent(root.transform,false);
            var l=line.GetComponent<LineRenderer>();l.sharedMaterial=material;l.useWorldSpace=true;l.widthMultiplier=.025f;l.numCapVertices=6;l.numCornerVertices=4;l.positionCount=0;stream.paths[n]=l;
            stream.starts[n]=((GameObject)PrefabUtility.InstantiatePrefab(marker,root.transform)).transform;stream.starts[n].name="Curve start "+n;stream.starts[n].localScale=Vector3.one*.045f;
            stream.ends[n]=((GameObject)PrefabUtility.InstantiatePrefab(marker,root.transform)).transform;stream.ends[n].name="Curve end "+n;stream.ends[n].localScale=Vector3.one*.045f;
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,assetPath);Object.DestroyImmediate(root);
        const string gamePath="Assets/VR/Shield/Shield Exercise Game.prefab";
        var contents=PrefabUtility.LoadPrefabContents(gamePath);
        try{var game=contents.GetComponent<ShieldExerciseGame>();if(!game.adaptiveCoach)game.adaptiveCoach=contents.GetComponent<AdaptiveExerciseCoach>()??contents.AddComponent<AdaptiveExerciseCoach>();game.adaptiveCoach.minimumRangeScale=.4f;game.adaptiveCoach.slowestDurationScale=2f;if(!game.pathStream){game.pathStream=((GameObject)PrefabUtility.InstantiatePrefab(prefab,contents.transform)).GetComponent<ExercisePathStream>();}game.pathStream.game=game;PrefabUtility.RecordPrefabInstancePropertyModifications(game.pathStream);PrefabUtility.SaveAsPrefabAsset(contents,gamePath);}
        finally{PrefabUtility.UnloadPrefabContents(contents);}
        AssetDatabase.SaveAssets();ExerciseMenuSetup.Build();Verify();
    }
    public static void Verify()
    {
        VerifyNeuralCoach();
        var a=new Vector3(-.4f,.2f,1.6f);
        if(ExercisePathStream.Approach(a,Vector3.forward,0,.55f)!=a)throw new System.Exception("Arrival must equal the scored 3D target");
        if(Vector3.Distance(ExercisePathStream.Approach(a,Vector3.forward,2.5f,.55f),a+Vector3.forward*1.375f)>.0001f)throw new System.Exception("Exercise gap");
        var mirrored=WandSessionReplay.MirrorPoint(new Vector3(-.3f,1,.5f));
        if(mirrored.x!=-.3f||mirrored.y!=1||mirrored.z!=-.5f)throw new System.Exception("Mirror coach must preserve left/right screen position");
        foreach(int side in new[]{0,1}){
            float sign=side==0?-1:1;
            foreach(float scale in new[]{.03f,.4f}){
                if(!ShieldExerciseGame.RangeDirectionAllowed(ShieldMovement.Scaption,new Vector3(sign*scale,scale,scale*.3f),side,.02f))throw new System.Exception("Small/large scaption range");
                if(!ShieldExerciseGame.RangeDirectionAllowed(ShieldMovement.SerratusReach,Vector3.forward*scale,side,.015f))throw new System.Exception("Small/large reach range");
                if(!ShieldExerciseGame.RangeDirectionAllowed(ShieldMovement.ExternalRotation,new Vector3(sign*scale,0,0),side,.02f))throw new System.Exception("Small/large rotation range");
            }
            if(ShieldExerciseGame.RangeDirectionAllowed(ShieldMovement.Scaption,Vector3.zero,side,.02f)||ShieldExerciseGame.RangeDirectionAllowed(ShieldMovement.SerratusReach,Vector3.back*.1f,side,.015f))throw new System.Exception("Reject stationary / wrong-direction calibration");
            if(Mathf.Abs(WandSessionReplay.TutorialYaw(true,side))!=90||WandSessionReplay.TutorialYaw(false,side)!=0)throw new System.Exception("Reach side view only");
        }
        VerifyAdaptiveCoach();
        Debug.Log("EXERCISE_PATH_CHECKS_PASSED");
    }
    static void VerifyAdaptiveCoach()
    {
        var root=new GameObject("Adaptive coach test");var coach=root.AddComponent<AdaptiveExerciseCoach>();
        var profile=ScriptableObject.CreateInstance<ShieldExerciseProfile>();profile.exerciseId="test";
        string path=System.IO.Path.Combine("/tmp","emil-preferences-test-"+System.Guid.NewGuid()+".json");
        var settings=new ExercisePlayerSettings{filePath=path};
        try {
            settings.Limit("test").fasterPercent=20;coach.Configure(settings);coach.ProfileFor(profile,0);
            var result=coach.Observe(profile,0,1,100,1,8,0,0,0);
            if(result.nextDurationScale>=1)throw new System.Exception("A single perfect attempt must immediately raise speed");
            var saved=ExercisePlayerSettings.Load(path);if(saved.Arm("test",0).speed<=1||saved.Arm("test",1).speed!=1)throw new System.Exception("Per-arm persistence");
            coach.Configure(saved);if(coach.ProfileFor(profile,0).outwardSeconds>=profile.outwardSeconds)throw new System.Exception("Restore saved pace");
            result=coach.Observe(profile,0,2,30,.4f,8,0,0,0);
            if(result.nextRangeScale>=1||result.nextDurationScale<=result.durationScaleUsed)throw new System.Exception("Immediate easing");
            result=coach.Observe(profile,0,3,100,1,8,0,0,0);
            if(result.nextRangeScale<=result.rangeScaleUsed||result.nextDurationScale>=result.durationScaleUsed)throw new System.Exception("Both speed and range progress after a strong attempt");
            result=coach.Observe(profile,0,4,80,.85f,8,0,0,0);
            if(result.nextDurationScale!=result.durationScaleUsed||result.nextRangeScale!=result.rangeScaleUsed)throw new System.Exception("Stable medium band");
            result=coach.Observe(profile,0,5,0,0,8,4,0,0);
            if(result.nextDurationScale!=result.durationScaleUsed||result.nextRangeScale!=result.rangeScaleUsed)throw new System.Exception("Tracking loss gate");
            result=coach.Observe(profile,0,6,100,1,8,0,0,85);
            if(result.nextDurationScale>=result.durationScaleUsed)throw new System.Exception("Rotation alone must allow progression");
            result=coach.Observe(profile,0,7,100,1,8,0,.3f,0,0,0,1);
            if(result.nextDurationScale!=result.durationScaleUsed||result.nextRangeScale!=result.rangeScaleUsed)throw new System.Exception("Position proxy must block increases");
            if(ShieldExerciseGame.HeadPositionSeverity(new Vector3(-.25f,-.2f,0),1)<.99f||ShieldExerciseGame.HeadPositionSeverity(new Vector3(.25f,-.2f,0),0)<.99f)throw new System.Exception("Opposite-side downward displacement");
            foreach(var v in new[]{Vector3.zero,new Vector3(-.25f,0,0),new Vector3(0,-.2f,0),new Vector3(.25f,-.2f,0),new Vector3(-.05f,-.04f,0)})if(ShieldExerciseGame.HeadPositionSeverity(v,1)!=0)throw new System.Exception("Natural movement/deadzone or wrong side penalized");
            if(Mathf.Abs(ShieldExerciseGame.HeadAdjustedScore(1,1)-.9f)>.0001f||ShieldExerciseGame.HeadAdjustedScore(.05f,1)<0)throw new System.Exception("Position penalty cap");
            result=coach.Observe(profile,0,7,34.6159f,.7471f,8,0,.0748f,26.556f);
            if(result.nextDurationScale<=result.durationScaleUsed||result.nextRangeScale>=result.rangeScaleUsed)throw new System.Exception("Actual headset regression: poor performance plus head turn must ease difficulty");
            for(int i=0;i<50;i++)result=coach.Observe(profile,0,i,100,1,8,0,0,0);
            if(1/result.nextDurationScale>1.2001f||result.nextRangeScale>1)throw new System.Exception("Per-exercise speed cap / calibrated range cap");
            for(int i=0;i<50;i++)result=coach.Observe(profile,0,i,0,0,8,0,0,0);
            if(result.nextDurationScale>2f||result.nextRangeScale<.4f)throw new System.Exception("Lower difficulty bounds");
            var persisted=ExercisePlayerSettings.Load(path);if(persisted.Arm("test",0).range>.4001f)throw new System.Exception("Reduced range must survive reload");
            saved.devMode=true;saved.Arm("test",0).devSpeed=1.7f;saved.Arm("test",0).devRange=.5f;saved.Save();string before=System.IO.File.ReadAllText(path);
            coach.Configure(saved);if(Mathf.Abs(coach.ProfileFor(profile,0).outwardSeconds-3/1.7f)>.001f||coach.RangeFor(profile,0,.4f)!=.5f)throw new System.Exception("Dev overrides");
            coach.Observe(profile,0,1,100,1,8,0,0,0);if(System.IO.File.ReadAllText(path)!=before)throw new System.Exception("Dev mode wrote normal progress");
            saved.devMode=false;coach.Configure(saved);if(Mathf.Abs(coach.ProfileFor(profile,0).outwardSeconds-3/saved.Arm("test",0).speed)>.001f)throw new System.Exception("Exit dev restores normal preset");
            var baseline=new ShieldExerciseGame.Calibration{exerciseId="test",start=new Vector3(0,0,.4f),end=new Vector3(.3f,.2f,.4f),pivot=Vector3.zero,startRotation=Quaternion.identity,endRotation=Quaternion.Euler(0,40,0)};
            var local=ExercisePlayerSettings.TransformCalibration(baseline,new Vector3(2,1,3),Quaternion.Euler(0,60,0),true);
            var restored=ExercisePlayerSettings.TransformCalibration(local,new Vector3(2,1,3),Quaternion.Euler(0,60,0),false);
            if(Vector3.Distance(restored.end,baseline.end)>.0001f||Quaternion.Angle(restored.endRotation,baseline.endRotation)>.01f)throw new System.Exception("Calibration coordinate roundtrip");
            foreach(ShieldMovement move in System.Enum.GetValues(typeof(ShieldMovement))){profile.movement=move;var adjusted=ShieldExerciseGame.ScaledCalibration(profile,baseline,.7f);if(Vector3.Distance(adjusted.end,ShieldExerciseProfile.Path(move,baseline.start,baseline.end,baseline.pivot,.7f))>.0001f)throw new System.Exception("Exercise curve scaling");}
            settings.totalRepetitions=7;settings.Arm("test",1).enabled=false;var playlist=settings.Playlist(new[]{profile});if(playlist.Count!=7||playlist.Exists(n=>n!=0))throw new System.Exception("Exact total / disabled arms");
            settings.Arm("test",0).enabled=false;if(settings.Playlist(new[]{profile}).Count!=0)throw new System.Exception("All disabled");
            settings.Arm("test",0).enabled=settings.Arm("test",1).enabled=true;settings.totalRepetitions=18;
            bool repeat=false,different=false;var previous=settings.Playlist(new[]{profile},0);
            for(int seed=1;seed<20;seed++){var random=settings.Playlist(new[]{profile},seed);if(random.Count!=18||random.FindAll(v=>v==0).Count!=9||random.FindAll(v=>v==1).Count!=9)throw new System.Exception("Random playlist must preserve counts");for(int i=1;i<random.Count;i++){repeat|=random[i]==random[i-1];different|=random[i]!=previous[i];}}
            if(!repeat||!different)throw new System.Exception("Random sequence must vary and permit repeats");
            Debug.Log("ADAPTIVE_COACH_AND_PERSISTENCE_CHECKS_PASSED");
        }finally{Object.DestroyImmediate(root);Object.DestroyImmediate(profile);if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
    }
    public static void VerifyNeuralCoach()
    {
        var asset=Resources.Load<TextAsset>("AdaptiveCoach/base-model");if(!asset)throw new System.Exception("Missing trained network");
        var model=JsonUtility.FromJson<AdaptiveExerciseCoach.Model>(asset.text);
        if(!AdaptiveExerciseCoach.ValidModel(model)||model.validationMse>.02f)throw new System.Exception("Invalid neural model");
        for(int score=0;score<=100;score+=2)for(int coverage=0;coverage<=100;coverage+=2){var delta=AdaptiveExerciseCoach.Predict(model,score/100f,coverage/100f);if(float.IsNaN(delta.x)||float.IsNaN(delta.y)||Mathf.Abs(delta.x)>AdaptiveExerciseCoach.RangeStep||Mathf.Abs(delta.y)>AdaptiveExerciseCoach.SpeedStep)throw new System.Exception("Network output bounds");}
        var near=AdaptiveExerciseCoach.Predict(model,.3f,.4f,0);var far=AdaptiveExerciseCoach.Predict(model,.3f,.4f,1);
        if(far.x>=near.x-.2f||far.y>=near.y-.1f||far.x>-.3f)throw new System.Exception("Network must ease substantially more for large sustained error");
        if(AdaptiveExerciseCoach.ErrorSeverity(.02f,.4f)!=0||AdaptiveExerciseCoach.ErrorSeverity(.25f,.4f)<.99f)throw new System.Exception("Position error normalization");
        var poor=AdaptiveExerciseCoach.Predict(model,.3f,.4f);var strong=AdaptiveExerciseCoach.Predict(model,1,1);
        if(poor.x>=0||poor.y>=0||strong.x<=0||strong.y<=0)throw new System.Exception("Learned direction");
        model.inputWeights[0]=float.NaN;if(AdaptiveExerciseCoach.ValidModel(model))throw new System.Exception("NaN weights accepted");
        var root=new GameObject("Neural persistence verification");var coach=root.AddComponent<AdaptiveExerciseCoach>();
        var profile=ScriptableObject.CreateInstance<ShieldExerciseProfile>();profile.exerciseId="neural-test";
        string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"neural-test-"+System.Guid.NewGuid()+".json");
        var settings=new ExercisePlayerSettings{filePath=path};settings.Arm(profile.exerciseId,0).range=.7f;
        try
        {
            coach.Configure(settings);coach.RangeFor(profile,0,.5f);var d=coach.Observe(profile,0,1,100,1,8,0,0,0);
            string personalPath=AdaptiveExerciseCoach.PersonalPath(path);if(!System.IO.File.Exists(personalPath))throw new System.Exception("Separate personal file missing");
            var personal=JsonUtility.FromJson<AdaptiveExerciseCoach.PersonalData>(System.IO.File.ReadAllText(personalPath));
            if(personal.arms.Count!=1||personal.arms[0].observations!=1||personal.arms[0].meanScore!=100)throw new System.Exception("Personal observations missing");
            var restored=ExercisePlayerSettings.Load(path);restored.Arm(profile.exerciseId,0).speed=1.99f;coach.Configure(restored);
            if(Mathf.Abs(coach.ProfileFor(profile,0).outwardSeconds-profile.outwardSeconds*d.nextDurationScale)>.001f)throw new System.Exception("Personal file is not authoritative after reload");
            string before=System.IO.File.ReadAllText(personalPath);restored.devMode=true;coach.Configure(restored);coach.Observe(profile,0,2,20,.2f,8,0,0,0);
            if(System.IO.File.ReadAllText(personalPath)!=before)throw new System.Exception("Dev mode altered personal data");
            restored.devMode=false;coach.Configure(restored);coach.Observe(profile,0,3,float.NaN,1,8,0,0,0);coach.Observe(profile,0,4,100,1,8,5,0,0);
            if(System.IO.File.ReadAllText(personalPath)!=before)throw new System.Exception("Invalid/tracking-loss data learned");
            if(Resources.Load<TextAsset>("AdaptiveCoach/base-model").text!=asset.text)throw new System.Exception("Gameplay changed base model");
            AdaptiveExerciseCoach.ResetPersonalData(restored);var empty=JsonUtility.FromJson<AdaptiveExerciseCoach.PersonalData>(System.IO.File.ReadAllText(personalPath));if(empty.arms.Count!=0)throw new System.Exception("Reset retained personal history");
            var fresh=new ExercisePlayerSettings();coach.Configure(fresh);
            var small=coach.Observe(profile,0,1,30,.4f,8,0,0,0,targetTravelMetres:.4f,meanHandErrorMetres:.02f);
            var large=coach.Observe(profile,1,1,30,.4f,8,0,0,0,targetTravelMetres:.4f,meanHandErrorMetres:.25f);
            if(large.nextRangeScale>=small.nextRangeScale-.2f||large.nextDurationScale<=small.nextDurationScale||large.nextRangeScale<.4f)throw new System.Exception("Measured distance must drive faster easing on the next attempt within limits");
            Debug.Log("NEURAL_COACH_CHECKS_PASSED model validation, bounded predictions, independent persistence, reload, dev mode, unreliable-input gate, reset");
        }
        finally{Object.DestroyImmediate(root);Object.DestroyImmediate(profile);System.IO.File.Delete(path);System.IO.File.Delete(AdaptiveExerciseCoach.PersonalPath(path));}
    }

}
