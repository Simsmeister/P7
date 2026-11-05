using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
namespace EmilVR
{
    public class ShieldExerciseGame : MonoBehaviour
    {
        public Transform head,trackingSpace,board,ghost,projectile;
        public Transform[] shields=new Transform[2];
        public TextMesh display;
        public ShieldExerciseProfile[] exercises;
        public WandSessionReplay replay;
        public ShieldCoachDisplay coach;
        public ExercisePathStream pathStream;
        public AdaptiveExerciseCoach adaptiveCoach;
        public ExerciseGameMenu menu;
        [UnityEngine.Serialization.FormerlySerializedAs("convoy")] public ConduitRepairGame conduit;
        public string SessionResult=>conduit?conduit.Result:"SESSION COMPLETE";
        [NonSerialized] public ExercisePlayerSettings PlayerSettings;
        readonly List<int> calibrationQueue=new List<int>();int calibrationIndex;bool showTutorial;
        public bool MenuVisible=>(state==State.Welcome||state==State.Finished)&&!(replay&&replay.IsActive);
        public bool AnyEnabled {get{EnsurePlayerSettings();foreach(var p in exercises)for(int s=0;s<2;s++)if(PlayerSettings.Arm(p.exerciseId,s).enabled)return true;return false;}}
        public void EnsurePlayerSettings(){if(PlayerSettings==null)PlayerSettings=ExercisePlayerSettings.Load(Application.isEditor?"/tmp/emil-editor-player-settings.json":Path.Combine(Application.persistentDataPath,"player-settings.json"));}
        ExerciseParticipant participant;
        public ExerciseParticipant Participant=>participant??(participant=ExerciseParticipant.Load(Application.isEditor?"/tmp/emil-editor-participant.json":Path.Combine(Application.persistentDataPath,"participant.json"),Path.Combine(Application.persistentDataPath,"Sessions")));
        public bool MenuRay(int side,out Ray ray,out bool down)
        {
            string n=side==0?"Left":"Right";down=input[n+"Trigger"].ReadValue<float>()>.55f;
            ray=new Ray(pos[side],rot[side]*Vector3.forward);return valid[side];
        }
        // TestMode tools press the real confirm button, so they must not advance the real counter.
        public void ResetSavedSettings(){if(!MenuVisible)return;EnsurePlayerSettings();PlayerSettings=new ExercisePlayerSettings{filePath=PlayerSettings.filePath};PlayerSettings.Save();if(adaptiveCoach)adaptiveCoach.Configure(PlayerSettings);if(!TestMode)Participant.Advance();}
        // Position proxy only: require both downward and opposite-side translation.
        public static float HeadPositionSeverity(Vector3 localDelta,int side)
        {float opposite=side==1?-localDelta.x:localDelta.x;return Mathf.Min(Mathf.InverseLerp(.10f,.25f,opposite),Mathf.InverseLerp(.08f,.20f,-localDelta.y));}
        public static float HeadAdjustedScore(float raw,float severity)=>Mathf.Max(0,raw-.1f*Mathf.Clamp01(severity));
        // Asked at a rep boundary: the cap wins over a break, and RepairRun.Resolve settles victory and defeat before the cap. A limit of 0 is off.
        public static string DueAtRepEnd(float active,float sinceBreak,float breakAfter,float cap)=>cap>0&&active>=cap?"cap":breakAfter>0&&sinceBreak>=breakAfter?"break":"";
        string Due=>DueAtRepEnd(activeSeconds,activeSinceBreak,breakAfterSeconds,sessionCapSeconds);
        public void ShowLastReplay(){if(replay)replay.Open();}
        float repTrackingPause,repHeadMetres,repHeadDegrees,headDisplacementHold,repHeadSeverity;
        public float exerciseGapSeconds=2.5f;
        // Prototype values, not clinical guidance; a physiotherapist must confirm them. Seconds of active play, except the break length.
        public float breakAfterSeconds=300,breakSeconds=45,sessionCapSeconds=900;
        float activeSeconds,activeSinceBreak;int breaksTaken;
        public float PathClock=>clock;
        public bool RepairTracked=>valid[Side]&&HeadValid();
        float repairQuality;
        public float RepairQuality=>state==State.Playing&&RepairTracked?repairQuality:0;
        public bool PathsVisible=>state==State.Playing||state==State.Approaching;
        readonly List<int> playlist=new List<int>();int playlistIndex;bool missionStarted;
        float minimumProgress,maximumProgress;
        public string GuidanceCaption {get;private set;}
        public int ActiveSide=>Side;
        public ShieldExerciseProfile ActiveProfile=>Profile;
        public float CurrentMovementProgress=>state==State.Playing?Profile.Progress(clock):0;
        public bool IsCalibrating=>!missionStarted&&state!=State.Welcome&&state!=State.Finished;
        public bool IsMission=>missionStarted&&state!=State.Finished&&state!=State.Break;
        public Vector3 CoachOrigin=>origin;public Quaternion CoachRotation=>frame;
        public Vector3 LiveHand=>pos[Side];
        public ShieldExerciseProfile NextProfile=>playlistIndex+1<playlist.Count?MissionProfile(playlist[playlistIndex+1]):null;
        public int NextSide=>playlistIndex+1<playlist.Count?playlist[playlistIndex+1]%2:Side;
        public ShieldArcadeFeedback arcade;
        WandReplayData demonstration;
        Vector3 stablePosition,positionSum;Quaternion stableRotation;
        int stableSamples;
        float poseWait;
        bool stabilityStarted;
        public string CurrentStep=>state.ToString();
        public float AutomaticCaptureProgress=>Mathf.Clamp01(holdInput/1.25f);
        public float EndpointTolerance=>Mathf.Clamp(Vector3.Distance(start,end)*.3f,.025f,.06f);
        [NonSerialized] public bool TestMode;
        [NonSerialized] public float TestHandFraction=1,TestHeadDegrees,TestControllerDegrees;
        public bool Finished=>state==State.Finished;
        public int CompletedRepetitions {get;private set;}
        public float Score=>CompletedRepetitions>0?100*scoreSum/CompletedRepetitions:0;
        enum State { Welcome, Demo, CaptureStart, CaptureEnd, Ready, Tutorial, Approaching, Playing, Finished, Break }
        State state;
        InputActionMap input;
        MaterialPropertyBlock targetTint;Renderer[] targetRenderers;
        Vector3 origin;Quaternion frame;
        Vector3 start,end,pivot;
        Quaternion startQ,endQ;
        readonly Vector3[] pos=new Vector3[2];readonly Quaternion[] rot=new Quaternion[2];readonly bool[] valid=new bool[2];
        int block,rep;float repPositionError,repAngleError;
        float clock,holdInput,sampleClock,uiClock,trackingPause,repQuality,repDuration,heldSeconds,scoreSum;
        bool recordingOpen;
        bool startedPose,returnedPose,impact,impactHit,previousA,previousB,previousX,simulated;
        string message="",folder="",lastResult="";
        StreamWriter csv,tsv;
        double recordingStart;
        readonly List<string> summaries=new List<string>();
        readonly List<Calibration> calibrations=new List<Calibration>();
        int Side=>block%2;
        ShieldExerciseProfile MissionProfile(int entry)=>adaptiveCoach?adaptiveCoach.ProfileFor(exercises[entry/2],entry%2):exercises[entry/2];
        ShieldExerciseProfile Profile=>missionStarted?MissionProfile(block):exercises[Mathf.Clamp(block/2,0,exercises.Length-1)];
        [Serializable] public class Calibration {public string exerciseId;public int side;public Vector3 start,end,pivot;public float calibratedTravelMetres;public Quaternion startRotation,endRotation;}
        [Serializable] class Settings {public bool developerMode;public bool handRotationScored=false;public string schemaVersion="shield-6",participantId,scoreVersion="position-only-hand-5",equipment="shield",inputSource;public string calibrationMode="personal-range-stable-pose-2",controlMapping="front-translation-2";public string presentation="finite-3d-paths-mirror-coach-1";public float exerciseGapSeconds,breakAfterSeconds,breakSeconds,sessionCapSeconds;public int[] challengeOrder;public float pathWeight=.7f,phaseWeight=.3f,stationaryScoreFactor=.25f,minimumCompletionCoverage=.65f,endpointFraction=.3f,minimumEndpointMetres=.025f,maximumEndpointMetres=.06f;public string anatomy="Shoulder pivot is estimated. Elbow angle, scapular motion and torso compensation are NOT measured."; public Vector3 frameOrigin;public Quaternion frameRotation;public Calibration[] calibrations;public ExerciseSettings[] exercises;}
        [Serializable] class ExerciseSettings {public string id;public int repetitions;public float outward,hold,returnTime,rest,fullCreditMetres,zeroCreditMetres,fullCreditDegrees,zeroCreditDegrees;}
        public static readonly string Header="timestamp_s,exercise_time_s,exercise_id,repetition,phase,active_side,side,valid,scorable,px,py,pz,qx,qy,qz,qw,target_px,target_py,target_pz,target_qx,target_qy,target_qz,target_qw,head_px,head_py,head_pz,head_qx,head_qy,head_qz,head_qw,hand_error_m,shield_normal_error_deg,head_displacement_m,head_rotation_deg,visual_shield_px,visual_shield_py,visual_shield_pz,visual_target_px,visual_target_py,visual_target_pz";
        Quaternion neutralHead;
        void Awake()
        {
            targetTint=new MaterialPropertyBlock();targetRenderers=ghost.GetComponentsInChildren<Renderer>();
            input=new InputActionMap("Shield exercises");
            foreach(string s in new[]{"Left","Right"}){string p="<XRController>{"+s+"Hand}/";input.AddAction(s+"Position",InputActionType.Value,p+"devicePosition");input.AddAction(s+"Rotation",InputActionType.Value,p+"deviceRotation");input.AddAction(s+"Tracked",InputActionType.Value,p+"isTracked");input.AddAction(s+"State",InputActionType.Value,p+"trackingState");input.AddAction(s+"Trigger",InputActionType.Value,p+"trigger");}
            input.AddAction("A",InputActionType.Button,"<XRController>{RightHand}/primaryButton");input.AddAction("B",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");input.AddAction("X",InputActionType.Button,"<XRController>{LeftHand}/primaryButton");
        }
        void OnEnable(){input.Enable();}
        void Start()
        {
#if UNITY_EDITOR
            simulated=!XRSettings.isDeviceActive;
#endif
            var rig=FindFirstObjectByType<EmilVRInput>();if(rig){rig.mode=EmilVRInput.InputMode.Controllers;rig.allowModeSwitch=false;}
            EnsurePlayerSettings();SetFrame();
        }
        void OnDisable(){input.Disable();if(arcade)arcade.StopLineTone();Finish(false);}
        void OnDestroy(){csv?.Dispose();tsv?.Dispose();input.Dispose();}
        void OnApplicationPause(bool pause){if(pause&&csv!=null)Finish(false);}
        void SetFrame(){origin=head.position;frame=Quaternion.Euler(0,head.eulerAngles.y,0);neutralHead=head.rotation;board.SetPositionAndRotation(origin+frame*new Vector3(0,.70f,2.3f),frame);}
        bool HeadValid(){if(simulated)return true;var d=InputDevices.GetDeviceAtXRNode(XRNode.Head);return d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool t)&&t&&d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,out InputTrackingState s)&&(s&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);}
        void Target(float t,out Vector3 p,out Quaternion q){float u=Profile.Progress(t);p=ShieldExerciseProfile.Path(Profile.movement,start,end,pivot,u);q=Quaternion.Slerp(startQ,endQ,u);}
        void Update()
        {
            if(!head||!trackingSpace||exercises.Length==0)return;
            for(int s=0;s<2;s++)
            {
                string n=s==0?"Left":"Right";var flags=(InputTrackingState)input[n+"State"].ReadValue<int>();valid[s]=input[n+"Tracked"].IsPressed()&&(flags&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);
                pos[s]=trackingSpace.TransformPoint(input[n+"Position"].ReadValue<Vector3>());rot[s]=trackingSpace.rotation*input[n+"Rotation"].ReadValue<Quaternion>();
                if(simulated){valid[s]=true;pos[s]=origin+frame*new Vector3(s==0?-.25f:.25f,-.35f,.3f);rot[s]=frame;if(s==Side){if(state==State.CaptureStart||state==State.Ready||state==State.Approaching){pos[s]=start;rot[s]=startQ;}else if(state==State.CaptureEnd){pos[s]=end;rot[s]=endQ;}else {Target(clock,out pos[s],out rot[s]);if(TestMode&&state==State.Playing)pos[s]=Vector3.Lerp(start,pos[s],TestHandFraction);}}}
                if(TestMode&&simulated&&state==State.Playing)rot[s]*=Quaternion.Euler(TestControllerDegrees,TestControllerDegrees,0);
                shields[s].gameObject.SetActive(!MenuVisible&&state!=State.Break&&valid[s]&&s==Side&&!(replay&&replay.IsActive));if(valid[s])shields[s].SetPositionAndRotation(MapToFront(pos[s],s),frame);
            }
            targetTint.SetColor("_BaseColor",Side==0?new Color(1,.2f,.3f):new Color(.1f,1,.45f));foreach(var r in targetRenderers)r.SetPropertyBlock(targetTint);
            bool a=input["A"].IsPressed()||(simulated&&Keyboard.current!=null&&Keyboard.current.spaceKey.isPressed),b=input["B"].IsPressed()||(simulated&&Keyboard.current!=null&&Keyboard.current.rKey.isPressed),x=input["X"].IsPressed();
            bool pressA=a&&!previousA,pressB=b&&!previousB,pressX=x&&!previousX;previousA=a;previousB=b;previousX=x;
            if(pressX&&state!=State.Welcome&&state!=State.Finished){Finish(false);message="Session stopped. A: new session";}
            if(pressB&&(state==State.Finished||state==State.Welcome)){if(replay.IsActive)replay.Close();else replay.Open();}
            bool calibrating=state==State.CaptureStart||state==State.CaptureEnd||state==State.Ready;
            if(pressA)SkipTutorialPreview();
            bool reviewing=replay&&replay.IsActive;
            if(arcade){arcade.Show(!conduit&&!reviewing&&state==State.Playing,block,rep,Profile.repetitions);arcade.core.gameObject.SetActive(false);arcade.LineTone(!reviewing&&state==State.Playing&&RepairTracked,RepairQuality);}
            board.gameObject.SetActive(!reviewing&&!(menu&&MenuVisible));ghost.gameObject.SetActive(!reviewing&&state!=State.Welcome&&state!=State.Finished&&state!=State.Break);if(reviewing&&!calibrating){projectile.gameObject.SetActive(false);return;}
            bool tracked=valid[Side]&&HeadValid();float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            // Active play is when the game clock runs: never calibration, tutorial, break or tracking pauses.
            if(tracked&&(state==State.Approaching||state==State.Playing)){activeSeconds+=dt;activeSinceBreak+=dt;}
            if(!menu&&(state==State.Welcome||state==State.Finished)&&pressA){Begin();return;}
            if(state==State.Demo){StartAutomaticPose(false);}
            else if(state==State.CaptureStart||state==State.CaptureEnd)
            {
                UpdateAutomaticPose(tracked,dt);
            }
            else if(state==State.Ready){bool near=tracked&&Vector3.Distance(pos[Side],start)<=Mathf.Max(EndpointTolerance,.06f);if(near)clock-=dt;else clock=2;
                GuidanceCaption="COPY THE START POSE - "+Profile.displayName+" / "+(Side==0?"LEFT":"RIGHT")+"\n"+(near?"Start pose matched. Hold still.":"Move your hand to the shown starting position.")+"\n"+Profile.instruction;
                if(!missionStarted)replay.UpdateGuidance(pos[Side],Side,GuidanceCaption);
                else ghost.SetPositionAndRotation(MapToFront(start,Side),frame);
                if(clock<=0){replay.Close();if(!missionStarted){if(++calibrationIndex<calibrationQueue.Count){block=calibrationQueue[calibrationIndex];PrepareBlock();}else StartMission();}else{ResetRep();state=State.Playing;if(!recordingOpen)OpenRecording();}}}
            else if(state==State.Tutorial){clock-=dt;ghost.SetPositionAndRotation(MapToFront(start,Side),frame);if(clock<=0)CompleteTutorial();}
            else if(state==State.Break){clock-=dt;if(clock<=0)LoadChallenge();}
            else if(state==State.Approaching){ghost.SetPositionAndRotation(MapToFront(start,Side),frame);if(tracked)clock+=dt;else trackingPause+=Time.unscaledDeltaTime;
                if(clock>=0){ResetRep();state=State.Playing;if(!recordingOpen)OpenRecording();}}
            else if(state==State.Playing)
            {
                Target(clock,out Vector3 p,out Quaternion q);ghost.SetPositionAndRotation(MapToFront(p,Side),frame);
                float pe=Vector3.Distance(pos[Side],p),ae=Vector3.Angle(rot[Side]*Vector3.forward,q*Vector3.forward);
                repairQuality=tracked?Profile.Quality(pe,ae):0;
                if(tracked)
                {
                    float progress=Vector3.Dot(pos[Side]-start,end-start)/Mathf.Max(.000001f,(end-start).sqrMagnitude);
                    minimumProgress=Mathf.Min(minimumProgress,progress);maximumProgress=Mathf.Max(maximumProgress,progress);
                    repHeadMetres=Mathf.Max(repHeadMetres,Vector3.Distance(head.position,origin));repHeadDegrees=Mathf.Max(repHeadDegrees,TestMode?TestHeadDegrees:Quaternion.Angle(head.rotation,neutralHead));
                    float headSeverity=Profile.Phase(clock)=="rest"?0:HeadPositionSeverity(Quaternion.Inverse(frame)*(head.position-origin),Side);
                    headDisplacementHold=headSeverity>0?headDisplacementHold+dt:0;
                    if(headDisplacementHold>.5f)repHeadSeverity+=headSeverity*dt;
                    repQuality+=Profile.Quality(pe,ae)*dt;repDuration+=dt;repPositionError+=pe*dt;repAngleError+=ae*dt;
                    if(clock<.8f&&Vector3.Distance(pos[Side],start)<=EndpointTolerance)startedPose=true;
                    if(Profile.Phase(clock)=="hold"&&pe<=EndpointTolerance)heldSeconds+=dt;
                    if(clock>=Profile.outwardSeconds+Profile.holdSeconds+Profile.returnSeconds&&Vector3.Distance(pos[Side],start)<=EndpointTolerance)returnedPose=true;
                    if(!impactHit&&clock>=Profile.outwardSeconds&&clock<Profile.outwardSeconds+Profile.holdSeconds){impact=true;impactHit=Vector3.Distance(pos[Side],end)<=.14f;message=impactHit?"Endpoint reached. Hold, then follow the return curve.":"Follow the line to the endpoint.";if(impactHit&&arcade)arcade.Block(MapToFront(end,Side));if(impactHit&&!simulated)InputDevices.GetDeviceAtXRNode(Side==0?XRNode.LeftHand:XRNode.RightHand).SendHapticImpulse(0,.3f,.08f);}
                }else {headDisplacementHold=0;trackingPause+=Time.unscaledDeltaTime;repTrackingPause+=Time.unscaledDeltaTime;}
                sampleClock+=Time.unscaledDeltaTime;if(sampleClock>=1f/60){sampleClock%=1f/60;Record(tracked,p,q);}
                if(tracked)clock+=dt;
                if(clock>=Profile.Cycle){CompleteRep();
                    if(!conduit&&(playlistIndex+1>=playlist.Count||Due=="cap")){Finish(true);}
                    else if(conduit&&conduit.Run.Finished){message=conduit.Result;Finish(conduit.Run.GoalReached);}
                    else {playlistIndex++;if(playlistIndex+1>=playlist.Count)playlist.AddRange(PlayerSettings.Playlist(exercises));if(Due=="break"){activeSinceBreak=0;breaksTaken++;state=State.Break;clock=breakSeconds;}else LoadChallenge();}}
            }
            projectile.gameObject.SetActive(false);
            if(arcade)arcade.drone.gameObject.SetActive(false);
            uiClock+=Time.unscaledDeltaTime;if(uiClock>.1f){uiClock=0;DrawText(tracked);}
        }
        public void SkipTutorialPreview()
        {
            if(state==State.Demo&&replay.IsDemo){replay.Close();StartAutomaticPose(false);}
            else if(state==State.Tutorial)CompleteTutorial();
            else if(state==State.Break)LoadChallenge();
        }
        void CompleteTutorial(){if(!PlayerSettings.devMode){PlayerSettings.tutorialComplete=true;PlayerSettings.Save();}LoadChallenge();}
        public void Begin(){Begin(false);}
        public void Begin(bool recalibrate)
        {
            EnsurePlayerSettings();if(!AnyEnabled)return;
            if(replay.IsActive)replay.Close();Finish(false);SetFrame();
            if(adaptiveCoach)adaptiveCoach.Configure(PlayerSettings);
            recordingOpen=false;CompletedRepetitions=0;scoreSum=trackingPause=activeSeconds=activeSinceBreak=0;breaksTaken=0;block=0;lastResult="";missionStarted=false;playlistIndex=0;playlist.Clear();folder="";
            if(arcade){arcade.transform.SetPositionAndRotation(origin,frame);arcade.ResetMission();}
            summaries.Clear();calibrations.Clear();calibrationQueue.Clear();calibrationIndex=0;
            showTutorial=recalibrate||!PlayerSettings.tutorialComplete;
            for(int e=0;e<exercises.Length;e++)for(int side=0;side<2;side++){
                var arm=PlayerSettings.Arm(exercises[e].exerciseId,side);if(!arm.enabled)continue;
                if(!recalibrate&&ExercisePlayerSettings.UsableCalibration(arm.calibration,exercises[e]))calibrations.Add(ExercisePlayerSettings.TransformCalibration(arm.calibration,origin,frame,false));
                else calibrationQueue.Add(e*2+side);
            }
            if(calibrationQueue.Count>0){showTutorial=true;block=calibrationQueue[0];PrepareBlock();}else StartMission();
        }
        void PrepareBlock()
        {
            clock=holdInput=0;state=State.Demo;message="";float sign=Side==0?-1:1;
            pivot=origin+frame*new Vector3(sign*.18f,-.25f,0);
            startQ=endQ=frame;
            if(Profile.movement==ShieldMovement.Scaption){start=pivot-Vector3.up*.565f;end=pivot+frame*new Vector3(sign*.457f,0,.332f);}
            else if(Profile.movement==ShieldMovement.SerratusReach){start=pivot+frame*Vector3.forward*.565f;end=start+frame*Vector3.forward*.06f;}
            else {pivot-=Vector3.up*.25f;start=pivot+frame*Vector3.forward*.25f;end=pivot+frame*(Quaternion.Euler(0,sign*40,0)*Vector3.forward*.25f);endQ=frame*Quaternion.Euler(0,sign*40,0);}
            var demo=new WandReplayData{origin=origin,rotation=frame,wandLength=0,equipment="shield",inputSource="EXERCISE DEMONSTRATION"};
            for(int i=0;i<=Mathf.CeilToInt(Profile.Cycle*60);i++){
                float t=Mathf.Min(i/60f,Profile.Cycle);Target(t,out var p,out var q);q=frame;Vector3 rest=origin+frame*new Vector3(Side==0?.2f:-.2f,-.7f,0);
                demo.frames.Add(new WandReplayFrame{time=t,headValid=true,leftValid=true,rightValid=true,head=origin,headRotation=neutralHead,left=Side==0?p:rest,right=Side==1?p:rest,leftRotation=Side==0?q:frame,rightRotation=Side==1?q:frame,targetLeft=Side==0?p:rest,targetRight=Side==1?p:rest,targetLeftRotation=Side==0?q:frame,targetRightRotation=Side==1?q:frame,activeSide=Side,exerciseId=Profile.exerciseId,phase=Profile.Phase(t),illustrativeShoulderOffset=Profile.movement==ShieldMovement.SerratusReach?Profile.Progress(t)*.06f:0});
            }
            demonstration=demo;replay.OpenExerciseDemo(demo,Profile.displayName+" - "+(Side==0?"LEFT":"RIGHT")+(Profile.movement==ShieldMovement.SerratusReach?" / SIDE VIEW":" / MIRROR")+"\n"+Profile.instruction+"\nExample only. Next: capture your own comfortable range.\nA / Space: skip preview. Calibration follows.",Profile.movement==ShieldMovement.SerratusReach);
        }
        // A fixed translation preserves all three axes, including overshoot. Inputs are in the calibrated body frame.
        public static Vector3 ProjectShield(ShieldMovement movement,Vector3 start,Vector3 end,Vector3 hand,int side)
        {
            float sign=side==0?-1:1;
            return new Vector3(sign*.30f,-.32f,1.35f)+(hand-start);
        }
        Vector3 MapToFront(Vector3 hand,int side)=>origin+frame*ProjectShield(Profile.movement,Quaternion.Inverse(frame)*(start-origin),Quaternion.Inverse(frame)*(end-origin),Quaternion.Inverse(frame)*(hand-origin),side);
        void StartMission()
        {
            missionStarted=true;playlist.Clear();if(conduit)conduit.Begin(PlayerSettings.totalRepetitions);
            playlist.AddRange(PlayerSettings.Playlist(exercises));
            if(playlist.Count==0){state=State.Welcome;return;}
            playlistIndex=0;LoadChallenge();if(showTutorial){state=State.Tutorial;clock=9;}
        }
        void LoadChallenge()
        {
            block=playlist[playlistIndex];rep=0;for(int i=0;i<playlistIndex;i++)if(playlist[i]==block)rep++;
            var c=EffectiveCalibration(Profile,calibrations.Find(v=>v.exerciseId==Profile.exerciseId&&v.side==Side),Side);
            start=c.start;end=c.end;pivot=c.pivot;startQ=c.startRotation;endQ=c.endRotation;
            state=State.Approaching;clock=-exerciseGapSeconds;message="Follow the arriving line.";GuidanceCaption="CURRENT: "+Profile.displayName+" / "+(Side==0?"LEFT":"RIGHT")+"\nCopy the start pose. Your marker moves freely in 3D.";
        }
        public bool PathDefinition(int offset,out ShieldExerciseProfile profile,out Calibration calibration,out int side)
        {
            int index=playlistIndex+offset;profile=null;calibration=null;side=0;
            if(index<0||index>=playlist.Count)return false;
            int entry=playlist[index];side=entry%2;profile=MissionProfile(entry);
            string id=profile.exerciseId;int arm=side;calibration=EffectiveCalibration(profile,calibrations.Find(c=>c.exerciseId==id&&c.side==arm),side);return calibration!=null;
        }
        public Calibration EffectiveCalibration(ShieldExerciseProfile profile,Calibration baseline,int side)
        {
            if(baseline==null)return null;
            float fraction=adaptiveCoach?adaptiveCoach.RangeFor(profile,side,Vector3.Distance(baseline.start,baseline.end)):1;
            return ScaledCalibration(profile,baseline,fraction);
        }
        public static Calibration ScaledCalibration(ShieldExerciseProfile profile,Calibration baseline,float fraction)
        {
            fraction=Mathf.Clamp01(fraction);
            return new Calibration{exerciseId=baseline.exerciseId,side=baseline.side,start=baseline.start,
                end=ShieldExerciseProfile.Path(profile.movement,baseline.start,baseline.end,baseline.pivot,fraction),
                pivot=baseline.pivot,startRotation=baseline.startRotation,endRotation=Quaternion.Slerp(baseline.startRotation,baseline.endRotation,fraction),
                calibratedTravelMetres=baseline.calibratedTravelMetres};
        }
        public Vector3 PathVisualPoint(ShieldExerciseProfile profile,Calibration c,int side,float time)
        {
            Vector3 hand=ShieldExerciseProfile.Path(profile.movement,c.start,c.end,c.pivot,profile.Progress(time));
            return origin+frame*ProjectShield(profile.movement,Quaternion.Inverse(frame)*(c.start-origin),Quaternion.Inverse(frame)*(c.end-origin),Quaternion.Inverse(frame)*(hand-origin),side);
        }
        public void CoachPose(bool endpoint,out Vector3 p,out Quaternion q){Target(endpoint?Profile.outwardSeconds:0,out p,out q);}
        void StartAutomaticPose(bool endpoint)
        {
            state=endpoint?State.CaptureEnd:State.CaptureStart;holdInput=poseWait=0;stableSamples=0;stabilityStarted=false;message="";
            if(!replay.IsActive)replay.OpenExerciseDemo(demonstration,"",Profile.movement==ShieldMovement.SerratusReach);
            replay.HoldDemoAt(endpoint?Profile.outwardSeconds:0,"");
        }
        // Direction is proportional to travel so a small valid movement is not rejected
        // by a fixed 8-10 cm requirement. Minimum travel only rejects ambiguous/noisy captures.
        public static bool RangeDirectionAllowed(ShieldMovement movement,Vector3 delta,int side,float minimumTravel)
        {
            float length=delta.magnitude,sign=side==0?-1:1;
            if(length<minimumTravel)return false;
            if(movement==ShieldMovement.Scaption)return delta.y>length*.15f&&delta.x*sign>length*.15f&&delta.z>-length*.25f;
            float transverse=Mathf.Max(.015f,length*.35f);
            if(movement==ShieldMovement.SerratusReach)return delta.z>=length*.6f&&Mathf.Abs(delta.x)<transverse&&Mathf.Abs(delta.y)<transverse;
            return delta.x*sign>length*.5f&&Mathf.Abs(delta.y)<transverse;
        }
        public bool CalibrationPoseAllowed(Vector3 hand,bool endpoint)
        {
            Vector3 local=Quaternion.Inverse(frame)*(hand-origin);float sign=Side==0?-1:1;
            if(!endpoint){
                if(local.x*sign<.02f)return false;
                if(Profile.movement==ShieldMovement.Scaption)return local.y<-.25f&&local.y>-1.2f&&local.z<.5f;
                if(Profile.movement==ShieldMovement.SerratusReach)return local.z>.15f&&local.y>-.80f&&local.y<.10f;
                return local.y<-.20f&&local.y>-.95f&&local.z>.03f&&local.z<.65f;
            }
            return RangeDirectionAllowed(Profile.movement,Quaternion.Inverse(frame)*(hand-start),Side,Profile.minimumTravel);
        }
        void UpdateAutomaticPose(bool tracked,float dt)
        {
            poseWait+=dt;bool endpoint=state==State.CaptureEnd;bool allowed=tracked&&CalibrationPoseAllowed(pos[Side],endpoint)&&poseWait>.6f;
            if(!allowed){stabilityStarted=false;holdInput=0;stableSamples=0;positionSum=Vector3.zero;}
            else{
                if(!stabilityStarted||Vector3.Distance(stablePosition,pos[Side])>.025f){stablePosition=pos[Side];stableRotation=rot[Side];stabilityStarted=true;holdInput=0;positionSum=Vector3.zero;stableSamples=0;}
                holdInput+=dt;positionSum+=pos[Side];stableSamples++;
            }
            string posture=endpoint?(Profile.movement==ShieldMovement.Scaption?"Lift diagonally outward to a comfortable height.":Profile.movement==ShieldMovement.SerratusReach?"Reach a little farther forward; keep elbow straight.":"Turn forearm outward; keep elbow beside your ribs."):(Profile.movement==ShieldMovement.Scaption?"Let your arm hang comfortably by your side.":Profile.movement==ShieldMovement.SerratusReach?"Hold your arm forward at your chosen starting height.":"Keep elbow beside your ribs and forearm forward.");
            GuidanceCaption="SET YOUR RANGE - YOUR "+(Side==0?"LEFT (RED)":"RIGHT (GREEN)")+" ARM\n"+posture+"\n"+(!tracked?"Waiting for tracking...":allowed?"Hold still... "+Mathf.RoundToInt(AutomaticCaptureProgress*100)+"%":"Move in the shown direction, then hold still.")+"\nCoach = movement example. Stop at your own comfortable limit.\n"+(endpoint&&Profile.movement==ShieldMovement.SerratusReach?"Reach change: "+((Quaternion.Inverse(frame)*(pos[Side]-start)).z*100).ToString("F1")+" cm forward. ":"")+message;replay.UpdateGuidance(pos[Side],Side,GuidanceCaption);
            if(holdInput<1.25f)return;
            if(!endpoint){start=positionSum/stableSamples;startQ=rot[Side];if(arcade)arcade.Captured();StartAutomaticPose(true);}
            else if(CaptureEnd()){if(arcade)arcade.Captured();replay.HoldDemoAt(0,"");state=State.Ready;clock=3;message="Pose learned! Return to the starting pose.";}
            else {holdInput=0;stabilityStarted=false;}
        }
        bool CaptureEnd()
        {
            Vector3 candidate=stableSamples>0?positionSum/stableSamples:pos[Side];Quaternion cq=rot[Side];
            if(Vector3.Distance(start,candidate)<Profile.minimumTravel){message="Movement too small to distinguish reliably. Ask your trainer to adjust this exercise.";return false;}
            if(Vector3.Distance(head.position,origin)>.25f){message="Return to your starting position, then hold still.";return false;}
            if(!RangeDirectionAllowed(Profile.movement,Quaternion.Inverse(frame)*(candidate-start),Side,Profile.minimumTravel)){message="Use your own range in the shown direction, then hold still.";return false;}
            end=candidate;endQ=cq;
            if(Profile.movement==ShieldMovement.ExternalRotation)pivot=start-frame*Vector3.forward*.25f;
            calibrations.Add(new Calibration{exerciseId=Profile.exerciseId,side=Side,start=start,end=end,pivot=pivot,calibratedTravelMetres=Vector3.Distance(start,end),startRotation=startQ,endRotation=endQ});
            if(!PlayerSettings.devMode){var arm=PlayerSettings.Arm(Profile.exerciseId,Side);arm.calibration=ExercisePlayerSettings.TransformCalibration(calibrations[calibrations.Count-1],origin,frame,true);arm.speed=arm.range=1;PlayerSettings.Save();}
            return true;
        }
        void ResetRep(){repTrackingPause=repHeadMetres=repHeadDegrees=headDisplacementHold=repHeadSeverity=0;minimumProgress=float.PositiveInfinity;maximumProgress=float.NegativeInfinity;clock=0;repPositionError=repAngleError=repQuality=repDuration=heldSeconds=0;startedPose=returnedPose=impact=impactHit=false;}
        void CompleteRep()
        {
            bool complete=startedPose&&returnedPose&&heldSeconds>=Profile.holdSeconds*.65f&&impactHit;
            float pathQuality=repDuration>0?repQuality/repDuration:0;
            float coverage=TravelCoverage(minimumProgress,maximumProgress);
            float phases=((startedPose?1:0)+(returnedPose?1:0)+(impactHit?1:0)+Mathf.Clamp01(heldSeconds/(Profile.holdSeconds*.65f)))/4;
            complete&=coverage>=.65f;
            float rawQuality=MovementScore(pathQuality,coverage,phases);float headSeverity=repDuration>0?repHeadSeverity/repDuration:0;float quality=HeadAdjustedScore(rawQuality,headSeverity);scoreSum+=quality;CompletedRepetitions++;
            summaries.Add(string.Join(",",Profile.exerciseId,Side==0?"left":"right",rep+1,F(quality*100),complete?"1":"0",F(heldSeconds),startedPose?"1":"0",returnedPose?"1":"0",impactHit?"1":"0",F(repDuration),F(repDuration>0?repPositionError/repDuration:0),F(repDuration>0?repAngleError/repDuration:0),F(pathQuality*100),F(coverage),F(phases),F(rawQuality*100),F(headSeverity),F((rawQuality-quality)*100)));
            lastResult="Last move: "+Mathf.RoundToInt(quality*100)+" / 100";
            if(arcade)arcade.Reward(block,complete,ShieldArcadeFeedback.Perfect(complete,quality,coverage),quality);
            if(conduit)conduit.Resolve(quality,Due=="cap");
            if(adaptiveCoach){var baseline=calibrations.Find(c=>c.exerciseId==Profile.exerciseId&&c.side==Side);
                var decision=adaptiveCoach.Observe(Profile,Side,rep+1,quality*100,coverage,repDuration,repTrackingPause,repHeadMetres,repHeadDegrees,Vector3.Distance(baseline.start,baseline.end),Vector3.Distance(start,end),headSeverity);
                bool easier=decision.nextRangeScale<decision.rangeScaleUsed||decision.nextDurationScale>decision.durationScaleUsed;
                bool harder=decision.nextRangeScale>decision.rangeScaleUsed||decision.nextDurationScale<decision.durationScaleUsed;
                lastResult=Profile.displayName+" / "+(Side==0?"L":"R")+": "+Mathf.RoundToInt(quality*100)+" -> "+(easier?"easier next time":harder?"harder next time":"unchanged");
            }
            message=complete?"PATH COMPLETE!":"Missed phase: "+(coverage<.65f?"movement range":!startedPose?"start pose":!impactHit?"endpoint":heldSeconds<Profile.holdSeconds*.65f?"hold too short":"return to start");
        }
        public static float TravelCoverage(float minimum,float maximum)=>float.IsInfinity(minimum)||float.IsInfinity(maximum)?0:Mathf.Clamp01(maximum-minimum);
        public static float MovementScore(float pathQuality,float coverage,float phases)=>Mathf.Clamp01((.7f*pathQuality+.3f*phases)*Mathf.Lerp(.25f,1,Mathf.Clamp01(coverage)));
        void DrawText(bool tracked)
        {
            string prefix=simulated?"MOVEMENT PATHS - SIMULATION\n":"MOVEMENT PATHS\n";
            if(state==State.Welcome){display.text=prefix+"Scaption / Serratus reach / External rotation\nOne arm at a time. A: begin | B: last replay\nLearn 3 moves, then follow the line through a mixed sequence.\nUse the comfortable range prescribed for you.";return;}
            if(state==State.Finished){display.text=prefix+"MOVEMENT ACCURACY   "+Score.ToString("F0")+" / 100\n"+CompletedRepetitions+" repetitions scored   "+(arcade?arcade.Points:0)+" game points\n"+message+"\nA: new session | B: replay\nCSV + tab-separated data saved for completed repetitions.";return;}
            if(state==State.Tutorial){display.text="PRACTICE - NO POINTS YET\nMove up/down, left/right, then forward/back.\nFollow the line. Solid centre = you; hollow ring = target.\nHold at the large node, then follow the line back.\nThe CURRENT card shows the movement. A / Space: skip.";return;}
            if(state==State.Break){display.text="BREAK - GREAT WORK\n"+CompletedRepetitions+" moves done. This rest is part of your session.\nNext line in "+Mathf.CeilToInt(clock)+" s   |   A: skip";return;}
            if(state==State.Approaching){display.text=Profile.displayName+" - "+(Side==0?"LEFT RED":"RIGHT GREEN")+"\nGet into the starting pose as the line approaches.\n"+(!tracked?"PAUSED - tracking unavailable":PositionHint(start))+"\nSpeed "+(100*exercises[block/2].outwardSeconds/Profile.outwardSeconds).ToString("F0")+"%  |  Range "+(adaptiveCoach?100*adaptiveCoach.RangeFor(Profile,Side,calibrations.Find(c=>c.exerciseId==Profile.exerciseId&&c.side==Side).calibratedTravelMetres):100).ToString("F0")+"%"+(lastResult.Length>0?"\n"+lastResult:"");return;}
            if(state==State.Ready&&missionStarted){display.text=GuidanceCaption+"\n"+PositionHint(start)+"  |  "+lastResult;return;}
            string side=Side==0?"LEFT RED":"RIGHT GREEN";
            string prompt=state==State.Demo?"WATCH THE COACH - demo only":state==State.CaptureStart?"Copy the resting pose and hold still":state==State.CaptureEnd?"Copy the raised/reached pose and hold still":state==State.Ready?"Return to the starting pose and hold still":"Rep "+(rep+1)+" / "+Profile.repetitions+"   "+Profile.Phase(clock).ToUpper()+"   "+Score.ToString("F0")+" /100";
            if(state==State.Playing){Target(clock,out var p,out var q);float quality=Profile.Quality(Vector3.Distance(pos[Side],p),Vector3.Angle(rot[Side]*Vector3.forward,q*Vector3.forward));string phase=Profile.Phase(clock);string cue=phase=="outward"?(Profile.movement==ShieldMovement.Scaption?"LIFT YOUR ARM DIAGONALLY":Profile.movement==ShieldMovement.SerratusReach?"REACH FORWARD":"ROTATE FOREARM OUTWARD"):phase=="hold"?"HOLD ON THE LINE":phase=="return"?"FOLLOW THE RETURN CURVE":"REST";display.text=Profile.displayName+" - "+side+"   "+(playlistIndex+1)+" moves"+"\n"+(!tracked?"PAUSED - tracking unavailable":cue)+"\nPath match "+Mathf.RoundToInt(quality*100)+"%  |  "+PositionHint(p);return;}
            display.text=prefix+Profile.displayName+" - "+side+"\n"+Profile.instruction+"\n"+prompt+"\n"+(!tracked?"PAUSED - tracking unavailable":message)+"\nElbow / shoulder-blade form not measured. X: stop.";
        }
        string PositionHint(Vector3 target)
        {
            Vector3 d=Quaternion.Inverse(frame)*(target-pos[Side]);
            if(d.magnitude<=EndpointTolerance)return "On target";
            float x=Mathf.Abs(d.x),y=Mathf.Abs(d.y),z=Mathf.Abs(d.z);
            if(z>=x&&z>=y)return (d.z>0?"Forward ":"Back ")+Mathf.RoundToInt(z*100)+" cm";
            if(y>=x)return (d.y>0?"Up ":"Down ")+Mathf.RoundToInt(y*100)+" cm";
            return (d.x>0?"Right ":"Left ")+Mathf.RoundToInt(x*100)+" cm";
        }
        static string F(double v)=>v.ToString("G9",CultureInfo.InvariantCulture);
        static void Pose(StringBuilder b,Vector3 p,Quaternion q){b.Append(',').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',').Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w));}
        void OpenRecording()
        {
            recordingOpen=true;replay.BeginRecording(origin,frame,0,simulated);replay.Data.equipment="shield";recordingStart=Time.realtimeSinceStartupAsDouble;
            if(simulated&&!TestMode)return;
            try {folder=Path.Combine(simulated?"/tmp/emil-shield-tests":Path.Combine(Application.persistentDataPath,"Sessions"),DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));Directory.CreateDirectory(folder);csv=new StreamWriter(Path.Combine(folder,"samples.csv"),false,Encoding.UTF8);tsv=new StreamWriter(Path.Combine(folder,"samples.tsv"),false,Encoding.UTF8);csv.WriteLine(Header);tsv.WriteLine(Header.Replace(',','\t'));}
            catch(Exception e){message="Recording failed: "+e.Message;Debug.LogError(message);}
        }
        void Record(bool tracked,Vector3 p,Quaternion q)
        {
            Vector3 lp=Side==0?p:pos[0],rp=Side==1?p:pos[1];Quaternion lq=Side==0?q:rot[0],rq=Side==1?q:rot[1];float time=(float)(Time.realtimeSinceStartupAsDouble-recordingStart);
            replay.Capture(new WandReplayFrame{time=time,headValid=HeadValid(),leftValid=valid[0],rightValid=valid[1],head=head.position,headRotation=head.rotation,left=pos[0],right=pos[1],leftRotation=rot[0],rightRotation=rot[1],targetLeft=lp,targetRight=rp,targetLeftRotation=lq,targetRightRotation=rq,exerciseId=Profile.exerciseId,phase=Profile.Phase(clock),activeSide=Side});
            if(csv==null)return;
            try {for(int s=0;s<2;s++){
                var b=new StringBuilder(F(time)+","+F(clock)+","+Profile.exerciseId+","+(rep+1)+","+Profile.Phase(clock)+","+(Side==0?"left":"right")+","+(s==0?"left":"right")+","+(valid[s]?1:0)+","+(tracked&&s==Side?1:0));
                if(valid[s])Pose(b,pos[s],rot[s]);else b.Append(",,,,,,,");if(s==Side)Pose(b,p,q);else b.Append(",,,,,,,");if(HeadValid())Pose(b,head.position,head.rotation);else b.Append(",,,,,,,");
                b.Append(',').Append(s==Side&&valid[s]?F(Vector3.Distance(pos[s],p)):"").Append(',').Append(s==Side&&valid[s]?F(Vector3.Angle(rot[s]*Vector3.forward,q*Vector3.forward)):"").Append(',').Append(HeadValid()?F(Vector3.Distance(head.position,origin)):"").Append(',').Append(HeadValid()?F(Quaternion.Angle(head.rotation,neutralHead)):"");if(s==Side&&valid[s]){Vector3 v=MapToFront(pos[s],s),vt=MapToFront(p,s);b.Append(',').Append(F(v.x)).Append(',').Append(F(v.y)).Append(',').Append(F(v.z)).Append(',').Append(F(vt.x)).Append(',').Append(F(vt.y)).Append(',').Append(F(vt.z));}else b.Append(",,,,,,");csv.WriteLine(b);tsv.WriteLine(b.ToString().Replace(',','\t'));
            }if(Time.frameCount%60==0){csv.Flush();tsv.Flush();}}
            catch(Exception e){message="Recording failed: "+e.Message;Debug.LogError(message);csv?.Dispose();tsv?.Dispose();csv=tsv=null;}
        }
        void Finish(bool complete)
        {
            if(state==State.Welcome||state==State.Finished)return;
            if(replay&&replay.IsDemo)replay.Close();
            if(conduit&&!conduit.Run.Finished)conduit.Run.outcome="aborted";
            csv?.Dispose();tsv?.Dispose();csv=tsv=null;
            if(folder!="")try {
                string participantId=TestMode?"TEST":Participant.Id;
                var settings=new Settings{participantId=participantId,developerMode=PlayerSettings!=null&&PlayerSettings.devMode,exerciseGapSeconds=exerciseGapSeconds,breakAfterSeconds=breakAfterSeconds,breakSeconds=breakSeconds,sessionCapSeconds=sessionCapSeconds,challengeOrder=playlist.ToArray(),inputSource=simulated?"SIMULATED TEST":"controller-grip",frameOrigin=origin,frameRotation=frame,calibrations=calibrations.ToArray(),exercises=Array.ConvertAll(exercises,e=>new ExerciseSettings{id=e.exerciseId,repetitions=e.repetitions,outward=e.outwardSeconds,hold=e.holdSeconds,returnTime=e.returnSeconds,rest=e.restSeconds,fullCreditMetres=e.fullCreditMetres,zeroCreditMetres=e.zeroCreditMetres,fullCreditDegrees=e.fullCreditDegrees,zeroCreditDegrees=e.zeroCreditDegrees})};
                if(conduit){File.WriteAllText(Path.Combine(folder,"conduit.json"),JsonUtility.ToJson(conduit.Run,true));File.WriteAllText(Path.Combine(folder,"conduit.csv"),conduit.Export());File.WriteAllText(Path.Combine(folder,"conduit.tsv"),conduit.Export().Replace(',','\t'));File.WriteAllText(Path.Combine(folder,"conduit-path.csv"),conduit.ExportNodes());File.WriteAllText(Path.Combine(folder,"conduit-path.tsv"),conduit.ExportNodes().Replace(',','\t'));}
                if(adaptiveCoach)File.WriteAllText(Path.Combine(folder,"adaptive-coach.json"),adaptiveCoach.ExportJson());
                File.WriteAllText(Path.Combine(folder,"session.json"),JsonUtility.ToJson(settings,true));string text="exercise_id,side,repetition,score,complete_movement,hold_seconds,start_reached,return_reached,endpoint_reached,valid_seconds,mean_hand_error_m,mean_shield_normal_error_deg,path_accuracy_percent,travel_coverage,phase_completion,unpenalized_score,head_position_severity,head_penalty_points\n"+string.Join("\n",summaries);File.WriteAllText(Path.Combine(folder,"summary.csv"),text);File.WriteAllText(Path.Combine(folder,"summary.tsv"),text.Replace(',','\t'));File.WriteAllText(Path.Combine(folder,"outcome.json"),"{\"participant_id\":\""+participantId+"\",\"complete\":"+(complete?"true":"false")+",\"outcome\":\""+(conduit?conduit.Run.outcome:complete?"complete":"aborted")+"\",\"tracking_pause_seconds\":"+F(trackingPause)+",\"active_seconds\":"+F(activeSeconds)+",\"breaks\":"+breaksTaken+"}");replay.Save(folder);
            }catch(Exception e){message="Save failed: "+e.Message;Debug.LogError(message);}
            state=State.Finished;ghost.gameObject.SetActive(false);projectile.gameObject.SetActive(false);
        }
    }
}
