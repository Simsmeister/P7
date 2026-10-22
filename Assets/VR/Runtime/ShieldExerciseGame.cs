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
        readonly List<int> playlist=new List<int>();int playlistIndex;bool missionStarted;
        float minimumProgress,maximumProgress;
        public string GuidanceCaption {get;private set;}
        public int ActiveSide=>Side;
        public ShieldExerciseProfile ActiveProfile=>Profile;
        public float CurrentMovementProgress=>state==State.Playing?Profile.Progress(clock):0;
        public bool IsCalibrating=>!missionStarted&&state!=State.Welcome&&state!=State.Finished;
        public bool IsMission=>missionStarted&&state!=State.Finished;
        public Vector3 CoachOrigin=>origin;public Quaternion CoachRotation=>frame;
        public Vector3 LiveHand=>pos[Side];
        public ShieldExerciseProfile NextProfile=>playlistIndex+1<playlist.Count?exercises[playlist[playlistIndex+1]/2]:null;
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
        public bool Finished=>state==State.Finished;
        public int CompletedRepetitions {get;private set;}
        public float Score=>CompletedRepetitions>0?100*scoreSum/CompletedRepetitions:0;
        enum State { Welcome, Demo, CaptureStart, CaptureEnd, Ready, Tutorial, Playing, Finished }
        State state;
        InputActionMap input;
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
        ShieldExerciseProfile Profile=>exercises[Mathf.Clamp(block/2,0,exercises.Length-1)];
        [Serializable] public class Calibration {public string exerciseId;public int side;public Vector3 start,end,pivot;public Quaternion startRotation,endRotation;}
        [Serializable] class Settings {public string schemaVersion="shield-3",scoreVersion="shield-partial-coverage-3",equipment="shield",inputSource;public string calibrationMode="automatic-stable-pose",controlMapping="front-translation-2";public int[] challengeOrder;public float pathWeight=.7f,phaseWeight=.3f,stationaryScoreFactor=.25f,minimumCompletionCoverage=.65f,endpointFraction=.3f,minimumEndpointMetres=.025f,maximumEndpointMetres=.06f;public string anatomy="Shoulder pivot is estimated. Elbow angle, scapular motion and torso compensation are NOT measured."; public Vector3 frameOrigin;public Quaternion frameRotation;public Calibration[] calibrations;public ExerciseSettings[] exercises;}
        [Serializable] class ExerciseSettings {public string id;public int repetitions;public float outward,hold,returnTime,rest,fullCreditMetres,zeroCreditMetres,fullCreditDegrees,zeroCreditDegrees;}
        public static readonly string Header="timestamp_s,exercise_time_s,exercise_id,repetition,phase,active_side,side,valid,scorable,px,py,pz,qx,qy,qz,qw,target_px,target_py,target_pz,target_qx,target_qy,target_qz,target_qw,head_px,head_py,head_pz,head_qx,head_qy,head_qz,head_qw,hand_error_m,shield_normal_error_deg,head_displacement_m,head_rotation_deg,visual_shield_px,visual_shield_py,visual_shield_pz,visual_target_px,visual_target_py,visual_target_pz";
        Quaternion neutralHead;
        void Awake()
        {
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
            SetFrame();
        }
        void OnDisable(){input.Disable();Finish(false);}
        void OnDestroy(){csv?.Dispose();tsv?.Dispose();input.Dispose();}
        void OnApplicationPause(bool pause){if(pause&&csv!=null)Finish(false);}
        void SetFrame(){origin=head.position;frame=Quaternion.Euler(0,head.eulerAngles.y,0);neutralHead=head.rotation;board.SetPositionAndRotation(origin+frame*new Vector3(0,.52f,2.3f),frame);}
        bool HeadValid(){if(simulated)return true;var d=InputDevices.GetDeviceAtXRNode(XRNode.Head);return d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool t)&&t&&d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,out InputTrackingState s)&&(s&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);}
        void Target(float t,out Vector3 p,out Quaternion q){float u=Profile.Progress(t);p=ShieldExerciseProfile.Path(Profile.movement,start,end,pivot,u);q=Quaternion.Slerp(startQ,endQ,u);}
        void Update()
        {
            if(!head||!trackingSpace||exercises.Length==0)return;
            for(int s=0;s<2;s++)
            {
                string n=s==0?"Left":"Right";var flags=(InputTrackingState)input[n+"State"].ReadValue<int>();valid[s]=input[n+"Tracked"].IsPressed()&&(flags&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);
                pos[s]=trackingSpace.TransformPoint(input[n+"Position"].ReadValue<Vector3>());rot[s]=trackingSpace.rotation*input[n+"Rotation"].ReadValue<Quaternion>();
                if(simulated){valid[s]=true;pos[s]=origin+frame*new Vector3(s==0?-.25f:.25f,-.35f,.3f);rot[s]=frame;if(s==Side){if(state==State.CaptureStart||state==State.Ready){pos[s]=start;rot[s]=startQ;}else if(state==State.CaptureEnd){pos[s]=end;rot[s]=endQ;}else Target(clock,out pos[s],out rot[s]);}}
                shields[s].gameObject.SetActive(valid[s]&&s==Side&&!(replay&&replay.IsActive));if(valid[s]){Target(clock,out _,out var intended);var tilt=Quaternion.FromToRotation(Vector3.forward,(Quaternion.Inverse(intended)*rot[s])*Vector3.forward);shields[s].SetPositionAndRotation(MapToFront(pos[s],s),frame*Quaternion.RotateTowards(Quaternion.identity,tilt,20));}
            }
            bool a=input["A"].IsPressed()||(simulated&&Keyboard.current!=null&&Keyboard.current.spaceKey.isPressed),b=input["B"].IsPressed()||(simulated&&Keyboard.current!=null&&Keyboard.current.rKey.isPressed),x=input["X"].IsPressed();
            bool pressA=a&&!previousA,pressB=b&&!previousB,pressX=x&&!previousX;previousA=a;previousB=b;previousX=x;
            if(pressX&&state!=State.Welcome&&state!=State.Finished){Finish(false);message="Session stopped. A: new session";}
            if(pressB&&(state==State.Finished||state==State.Welcome)){if(replay.IsActive)replay.Close();else replay.Open();}
            bool calibrating=state==State.CaptureStart||state==State.CaptureEnd||state==State.Ready;
            bool reviewing=replay&&replay.IsActive;
            if(arcade)arcade.Show(!reviewing&&state==State.Playing,block,rep,Profile.repetitions);
            board.gameObject.SetActive(!reviewing);ghost.gameObject.SetActive(!reviewing&&state!=State.Welcome&&state!=State.Finished);if(reviewing&&!calibrating){projectile.gameObject.SetActive(false);return;}
            bool tracked=valid[Side]&&HeadValid();float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if((state==State.Welcome||state==State.Finished)&&pressA){Begin();return;}
            if(state==State.Demo){StartAutomaticPose(false);}
            else if(state==State.CaptureStart||state==State.CaptureEnd)
            {
                UpdateAutomaticPose(tracked,dt);
            }
            else if(state==State.Ready){bool near=tracked&&Vector3.Distance(pos[Side],start)<=Mathf.Max(EndpointTolerance,.06f);if(near)clock-=dt;else clock=2;
                GuidanceCaption="COPY THE START POSE - "+Profile.displayName+" / "+(Side==0?"LEFT":"RIGHT")+"\n"+(near?"Starting in "+Mathf.CeilToInt(clock):"Move your hand to the blue target.")+"\n"+Profile.instruction;
                if(!missionStarted)replay.UpdateGuidance(pos[Side],Side,GuidanceCaption);
                else ghost.SetPositionAndRotation(MapToFront(start,Side),frame);
                if(clock<=0){replay.Close();if(!missionStarted){if(++block<exercises.Length*2)PrepareBlock();else StartMission();}else{ResetRep();state=State.Playing;if(!recordingOpen)OpenRecording();}}}
            else if(state==State.Tutorial){clock-=dt;ghost.SetPositionAndRotation(MapToFront(start,Side),frame);if(clock<=0)LoadChallenge();}
            else if(state==State.Playing)
            {
                Target(clock,out Vector3 p,out Quaternion q);ghost.SetPositionAndRotation(MapToFront(p,Side),frame);
                float pe=Vector3.Distance(pos[Side],p),ae=Vector3.Angle(rot[Side]*Vector3.forward,q*Vector3.forward);
                if(tracked)
                {
                    float progress=Vector3.Dot(pos[Side]-start,end-start)/Mathf.Max(.000001f,(end-start).sqrMagnitude);
                    minimumProgress=Mathf.Min(minimumProgress,progress);maximumProgress=Mathf.Max(maximumProgress,progress);
                    repQuality+=Profile.Quality(pe,ae)*dt;repDuration+=dt;repPositionError+=pe*dt;repAngleError+=ae*dt;
                    if(clock<.8f&&Vector3.Distance(pos[Side],start)<=EndpointTolerance)startedPose=true;
                    if(Profile.Phase(clock)=="hold"&&pe<=EndpointTolerance&&ae<=Profile.fullCreditDegrees+8)heldSeconds+=dt;
                    if(clock>=Profile.outwardSeconds+Profile.holdSeconds+Profile.returnSeconds&&Vector3.Distance(pos[Side],start)<=EndpointTolerance)returnedPose=true;
                    if(!impactHit&&clock>=Profile.outwardSeconds&&clock<Profile.outwardSeconds+Profile.holdSeconds){impact=true;impactHit=Vector3.Distance(pos[Side],end)<=.14f&&ae<=Profile.zeroCreditDegrees;message=impactHit?"BLOCK! Hold to absorb energy, then return to recharge.":"Miss - follow the target shield on the next pulse.";if(impactHit&&arcade)arcade.Block(MapToFront(end,Side));if(impactHit&&!simulated)InputDevices.GetDeviceAtXRNode(Side==0?XRNode.LeftHand:XRNode.RightHand).SendHapticImpulse(0,.3f,.08f);}
                }else trackingPause+=Time.unscaledDeltaTime;
                sampleClock+=Time.unscaledDeltaTime;if(sampleClock>=1f/60){sampleClock%=1f/60;Record(tracked,p,q);}
                if(tracked)clock+=dt;
                if(clock>=Profile.Cycle){CompleteRep();if(++playlistIndex>=playlist.Count)Finish(true);else LoadChallenge();}
            }
            projectile.gameObject.SetActive(state==State.Playing&&clock<Profile.outwardSeconds+.25f);
            if(projectile.gameObject.activeSelf){Vector3 target=MapToFront(end,Side);Vector3 launch=target+frame*new Vector3(Mathf.Sin(rep*2.1f)*.2f,.15f,2.5f);projectile.position=Vector3.Lerp(launch,target-frame*Vector3.forward*.25f,Mathf.Clamp01(clock/(Profile.outwardSeconds+.25f)));projectile.rotation=frame;if(arcade){arcade.drone.gameObject.SetActive(true);arcade.drone.SetPositionAndRotation(launch,frame);}}
            else if(arcade)arcade.drone.gameObject.SetActive(false);
            uiClock+=Time.unscaledDeltaTime;if(uiClock>.1f){uiClock=0;DrawText(tracked);}
        }
        public void Begin(){if(replay.IsActive)replay.Close();Finish(false);SetFrame();recordingOpen=false;CompletedRepetitions=0;scoreSum=trackingPause=0;block=0;lastResult="";missionStarted=false;playlistIndex=0;playlist.Clear();folder="";if(arcade){arcade.transform.SetPositionAndRotation(origin,frame);arcade.ResetMission();}summaries.Clear();calibrations.Clear();PrepareBlock();}
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
                float t=Mathf.Min(i/60f,Profile.Cycle);Target(t,out var p,out var q);Vector3 rest=origin+frame*new Vector3(Side==0?.2f:-.2f,-.7f,0);
                demo.frames.Add(new WandReplayFrame{time=t,headValid=true,leftValid=true,rightValid=true,head=origin,headRotation=neutralHead,left=Side==0?p:rest,right=Side==1?p:rest,leftRotation=Side==0?q:frame,rightRotation=Side==1?q:frame,targetLeft=Side==0?p:rest,targetRight=Side==1?p:rest,targetLeftRotation=Side==0?q:frame,targetRightRotation=Side==1?q:frame,activeSide=Side,exerciseId=Profile.exerciseId,phase=Profile.Phase(t),illustrativeShoulderOffset=Profile.movement==ShieldMovement.SerratusReach?Profile.Progress(t)*.06f:0});
            }
            demonstration=demo;replay.OpenExerciseDemo(demo,"WATCH: "+Profile.displayName+" - "+(Side==0?"LEFT":"RIGHT")+"\n"+Profile.instruction+"\nIllustrative body only; elbow and shoulder blade are not measured.\nNext: copy the character and hold still. No triggers needed.");
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
            missionStarted=true;playlist.Clear();
            int rounds=0;foreach(var p in exercises)rounds=Mathf.Max(rounds,p.repetitions);
            // Alternating exercises and arms, with a different order each round; every configured rep occurs once.
            for(int r=0;r<rounds;r++)for(int i=0;i<exercises.Length*2;i++){
                int exercise=(i+r)%exercises.Length,side=i%2;
                if(r<exercises[exercise].repetitions)playlist.Add(exercise*2+side);
            }
            playlistIndex=0;LoadChallenge();state=State.Tutorial;clock=9;
        }
        void LoadChallenge()
        {
            block=playlist[playlistIndex];rep=0;for(int i=0;i<playlistIndex;i++)if(playlist[i]==block)rep++;
            var c=calibrations.Find(v=>v.exerciseId==Profile.exerciseId&&v.side==Side);
            start=c.start;end=c.end;pivot=c.pivot;startQ=c.startRotation;endQ=c.endRotation;
            state=State.Ready;clock=2;message="Next: "+Profile.displayName;GuidanceCaption="NEXT: "+Profile.displayName+" / "+(Side==0?"LEFT":"RIGHT")+"\nCopy the start pose. Your shield moves freely in 3D.";
        }
        public void CoachPose(bool endpoint,out Vector3 p,out Quaternion q){Target(endpoint?Profile.outwardSeconds:0,out p,out q);}
        void StartAutomaticPose(bool endpoint)
        {
            state=endpoint?State.CaptureEnd:State.CaptureStart;holdInput=poseWait=0;stableSamples=0;stabilityStarted=false;message="";
            if(!replay.IsActive)replay.OpenExerciseDemo(demonstration,"");
            replay.HoldDemoAt(endpoint?Profile.outwardSeconds:0,"");
        }
        public bool CalibrationPoseAllowed(Vector3 hand,bool endpoint)
        {
            Vector3 local=Quaternion.Inverse(frame)*(hand-origin);float sign=Side==0?-1:1;
            if(!endpoint){
                if(local.x*sign<.05f)return false;
                if(Profile.movement==ShieldMovement.Scaption)return local.y<-.42f&&local.z<.4f;
                if(Profile.movement==ShieldMovement.SerratusReach)return local.z>.25f&&local.y>-.50f&&local.y<.08f;
                return local.y<-.20f&&local.y>-.85f&&local.z>.05f&&local.z<.6f;
            }
            Vector3 d=Quaternion.Inverse(frame)*(hand-start);
            if(Profile.movement==ShieldMovement.Scaption)return d.y>.1f&&d.x*sign>.08f&&d.z>-.03f;
            if(Profile.movement==ShieldMovement.SerratusReach)return d.z>=Profile.minimumTravel&&Mathf.Abs(d.x)<.05f&&Mathf.Abs(d.y)<.05f;
            return d.x*sign>.05f&&Mathf.Abs(d.y)<.10f;
        }
        void UpdateAutomaticPose(bool tracked,float dt)
        {
            poseWait+=dt;bool endpoint=state==State.CaptureEnd;bool allowed=tracked&&CalibrationPoseAllowed(pos[Side],endpoint)&&poseWait>.6f;
            if(!allowed){stabilityStarted=false;holdInput=0;stableSamples=0;positionSum=Vector3.zero;}
            else{
                if(!stabilityStarted||Vector3.Distance(stablePosition,pos[Side])>.025f||Quaternion.Angle(stableRotation,rot[Side])>12){stablePosition=pos[Side];stableRotation=rot[Side];stabilityStarted=true;holdInput=0;positionSum=Vector3.zero;stableSamples=0;}
                holdInput+=dt;positionSum+=pos[Side];stableSamples++;
            }
            string posture=endpoint?(Profile.movement==ShieldMovement.Scaption?"Lift diagonally outward to a comfortable height.":Profile.movement==ShieldMovement.SerratusReach?"Reach a little farther forward; keep elbow straight.":"Turn forearm outward; keep elbow beside your ribs."):(Profile.movement==ShieldMovement.Scaption?"Let your arm hang comfortably by your side.":Profile.movement==ShieldMovement.SerratusReach?"Hold your arm forward near shoulder height.":"Keep elbow beside your ribs and forearm forward.");
            GuidanceCaption="COPY THE COACH - YOUR "+(Side==0?"LEFT (RED)":"RIGHT (GREEN)")+" ARM\n"+posture+"\n"+(!tracked?"Waiting for tracking...":allowed?"Hold still... "+Mathf.RoundToInt(AutomaticCaptureProgress*100)+"%":"Move into the pose. No buttons needed.")+"\nSmall glowing orb = your hand. Use your comfortable range.\n"+(endpoint&&Profile.movement==ShieldMovement.SerratusReach?"Reach change: "+((Quaternion.Inverse(frame)*(pos[Side]-start)).z*100).ToString("F1")+" cm forward. ":"")+message;replay.UpdateGuidance(pos[Side],Side,GuidanceCaption);
            if(holdInput<1.25f)return;
            if(!endpoint){start=positionSum/stableSamples;startQ=rot[Side];if(arcade)arcade.Captured();StartAutomaticPose(true);}
            else if(CaptureEnd()){if(arcade)arcade.Captured();replay.HoldDemoAt(0,"");state=State.Ready;clock=3;message="Pose learned! Return to the starting pose.";}
            else {holdInput=0;stabilityStarted=false;}
        }
        bool CaptureEnd()
        {
            Vector3 candidate=stableSamples>0?positionSum/stableSamples:pos[Side];Quaternion cq=rot[Side];
            if(Vector3.Distance(start,candidate)<Profile.minimumTravel){message="Start/end too close. Repeat within your comfortable prescribed range.";return false;}
            if(Vector3.Distance(head.position,origin)>.25f){message="Return to your starting standing position, then hold still.";return false;}
            Vector3 delta=Quaternion.Inverse(frame)*(candidate-start);float side=Side==0?-1:1;
            if(Profile.movement==ShieldMovement.Scaption&&(delta.y<.1f||delta.x*side<.08f||delta.z<-.03f)){message="Lift diagonally OUT and UP, slightly forward. Repeat end pose.";return false;}
            if(Profile.movement==ShieldMovement.SerratusReach&&(delta.z<Profile.minimumTravel||Mathf.Abs(delta.x)>.05f||Mathf.Abs(delta.y)>.05f)){message="Reach gently FORWARD at the same height. Repeat end pose.";return false;}
            if(Profile.movement==ShieldMovement.ExternalRotation&&(delta.x*side<.05f||Mathf.Abs(delta.y)>.10f)){message="Rotate forearm OUTWARD at elbow height. Repeat end pose.";return false;}
            end=candidate;endQ=cq;
            if(Profile.movement==ShieldMovement.ExternalRotation)pivot=start-frame*Vector3.forward*.25f;
            calibrations.Add(new Calibration{exerciseId=Profile.exerciseId,side=Side,start=start,end=end,pivot=pivot,startRotation=startQ,endRotation=endQ});
            return true;
        }
        void ResetRep(){minimumProgress=float.PositiveInfinity;maximumProgress=float.NegativeInfinity;clock=0;repPositionError=repAngleError=repQuality=repDuration=heldSeconds=0;startedPose=returnedPose=impact=impactHit=false;}
        void CompleteRep()
        {
            bool complete=startedPose&&returnedPose&&heldSeconds>=Profile.holdSeconds*.65f&&impactHit;
            float pathQuality=repDuration>0?repQuality/repDuration:0;
            float coverage=TravelCoverage(minimumProgress,maximumProgress);
            float phases=((startedPose?1:0)+(returnedPose?1:0)+(impactHit?1:0)+Mathf.Clamp01(heldSeconds/(Profile.holdSeconds*.65f)))/4;
            complete&=coverage>=.65f;
            float quality=MovementScore(pathQuality,coverage,phases);scoreSum+=quality;CompletedRepetitions++;
            summaries.Add(string.Join(",",Profile.exerciseId,Side==0?"left":"right",rep+1,F(quality*100),complete?"1":"0",F(heldSeconds),startedPose?"1":"0",returnedPose?"1":"0",impactHit?"1":"0",F(repDuration),F(repDuration>0?repPositionError/repDuration:0),F(repDuration>0?repAngleError/repDuration:0),F(pathQuality*100),F(coverage),F(phases)));
            lastResult="Last move: "+Mathf.RoundToInt(quality*100)+" / 100";
            if(arcade)arcade.Reward(block,complete,quality);
            message=complete?"BLOCKED + RETURNED!":"Missed phase: "+(coverage<.65f?"movement range":!startedPose?"start pose":!impactHit?"shield block":heldSeconds<Profile.holdSeconds*.65f?"hold too short":"return to start");
        }
        public static float TravelCoverage(float minimum,float maximum)=>float.IsInfinity(minimum)||float.IsInfinity(maximum)?0:Mathf.Clamp01(maximum-minimum);
        public static float MovementScore(float pathQuality,float coverage,float phases)=>Mathf.Clamp01((.7f*pathQuality+.3f*phases)*Mathf.Lerp(.25f,1,Mathf.Clamp01(coverage)));
        void DrawText(bool tracked)
        {
            string prefix=simulated?"REACTOR RESCUE - SIMULATION\n":"REACTOR RESCUE\n";
            if(state==State.Welcome){display.text=prefix+"Scaption / Serratus reach / External rotation\nOne arm at a time. A: begin | B: last replay\nLearn 3 moves, then defend against a mixed sequence.\nUse the comfortable range prescribed for you.";return;}
            if(state==State.Finished){display.text=prefix+"MOVEMENT ACCURACY   "+Score.ToString("F0")+" / 100\n"+CompletedRepetitions+" repetitions scored   "+(arcade?arcade.Points:0)+" game points\n"+message+"\nA: new session | B: replay\nCSV + tab-separated data saved for completed repetitions.";return;}
            if(state==State.Tutorial){display.text="TRY YOUR SHIELD - NO POINTS YET\nMove up/down, left/right, then forward/back.\nYour shield follows every direction. Blue = target.\nNOW shows your move. NEXT previews the next one.\nMixed mission begins in "+Mathf.CeilToInt(clock);return;}
            if(state==State.Ready&&missionStarted){display.text=GuidanceCaption+"\n"+PositionHint(start)+"  |  "+lastResult;return;}
            string side=Side==0?"LEFT RED":"RIGHT GREEN";
            string prompt=state==State.Demo?"WATCH THE GHOST SHIELD - demo only":state==State.CaptureStart?"Copy the resting pose and hold still":state==State.CaptureEnd?"Copy the raised/reached pose and hold still":state==State.Ready?"Return to the starting pose. Launch in "+Mathf.CeilToInt(clock):"Rep "+(rep+1)+" / "+Profile.repetitions+"   "+Profile.Phase(clock).ToUpper()+"   "+Score.ToString("F0")+" /100";
            if(state==State.Playing){Target(clock,out var p,out var q);float quality=Profile.Quality(Vector3.Distance(pos[Side],p),Vector3.Angle(rot[Side]*Vector3.forward,q*Vector3.forward));string phase=Profile.Phase(clock);string cue=phase=="outward"?(Profile.movement==ShieldMovement.Scaption?"LIFT YOUR ARM DIAGONALLY":Profile.movement==ShieldMovement.SerratusReach?"REACH FORWARD":"ROTATE FOREARM OUTWARD"):phase=="hold"?"HOLD - ABSORB ENERGY":phase=="return"?"RETURN - RECHARGE":"REST";display.text=Profile.displayName+" - "+side+"   "+(playlistIndex+1)+" / "+playlist.Count+"\n"+(!tracked?"PAUSED - tracking unavailable":cue)+"\nPath match "+Mathf.RoundToInt(quality*100)+"%  |  "+PositionHint(p);return;}
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
            csv?.Dispose();tsv?.Dispose();csv=tsv=null;
            if(folder!="")try {
                var settings=new Settings{challengeOrder=playlist.ToArray(),inputSource=simulated?"SIMULATED TEST":"controller-grip",frameOrigin=origin,frameRotation=frame,calibrations=calibrations.ToArray(),exercises=Array.ConvertAll(exercises,e=>new ExerciseSettings{id=e.exerciseId,repetitions=e.repetitions,outward=e.outwardSeconds,hold=e.holdSeconds,returnTime=e.returnSeconds,rest=e.restSeconds,fullCreditMetres=e.fullCreditMetres,zeroCreditMetres=e.zeroCreditMetres,fullCreditDegrees=e.fullCreditDegrees,zeroCreditDegrees=e.zeroCreditDegrees})};
                File.WriteAllText(Path.Combine(folder,"session.json"),JsonUtility.ToJson(settings,true));string text="exercise_id,side,repetition,score,complete_movement,hold_seconds,start_reached,return_reached,projectile_blocked,valid_seconds,mean_hand_error_m,mean_shield_normal_error_deg,path_accuracy_percent,travel_coverage,phase_completion\n"+string.Join("\n",summaries);File.WriteAllText(Path.Combine(folder,"summary.csv"),text);File.WriteAllText(Path.Combine(folder,"summary.tsv"),text.Replace(',','\t'));File.WriteAllText(Path.Combine(folder,"outcome.json"),"{\"complete\":"+(complete?"true":"false")+",\"tracking_pause_seconds\":"+F(trackingPause)+"}");replay.Save(folder);
            }catch(Exception e){message="Save failed: "+e.Message;Debug.LogError(message);}
            state=State.Finished;ghost.gameObject.SetActive(false);projectile.gameObject.SetActive(false);
        }
    }
}
