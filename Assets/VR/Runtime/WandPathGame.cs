using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace EmilVR
{
    public class WandPathGame : MonoBehaviour
    {
        public Transform trackingSpace, head;
        public WandSessionReplay replay;
[NonSerialized] public bool RecordSimulationForTesting;
        public Transform[] wands = new Transform[2], ghosts = new Transform[2], gates = new Transform[2];
        public PathTube[] tubes = new PathTube[2];
        public TextMesh display;
        public Transform board;
        public float wandLength=.60f, amplitude=.10f, tipTolerance=.16f, handTolerance=.12f, angleTolerance=35f, roundSeconds=20f;
        public Quaternion FrameRotation { get; private set; } = Quaternion.identity;
        public Vector3 FrameOrigin { get; private set; }
        InputActionMap input;
        readonly Vector3[] baseline=new Vector3[2], positions=new Vector3[2];
        readonly Quaternion[] rotations=new Quaternion[2], baselineRotation=new Quaternion[2];
        readonly bool[] valid=new bool[2];
        readonly double[,] total=new double[3,2], posTotal=new double[3,2], tipTotal=new double[3,2], angleTotal=new double[3,2];
        readonly double[] duration=new double[3];
        float elapsed, calibrationHold, countdown, sampleTime, uiTime, flushTime, pausedSeconds;
        bool calibrated, running, counting, finished, simulated, previousA, completed, recordingFailed, previousB;
        string status="", sessionId="", savedPath="";
        StreamWriter csv;
        Quaternion neutralHead;
        EmilVRInput rigInput;
        bool oldModeSwitch;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        public float Score { get { double sum=0,d=0; for(int r=0;r<3;r++){sum+=total[r,0]+total[r,1];d+=duration[r]*2;} return d>0?(float)(sum/d*100):0; } }
        void Awake()
        {
            input=new InputActionMap("Wand Path Game");
            foreach(string side in new[]{"Left","Right"})
            {
                string b="<XRController>{"+side+"Hand}/";
                input.AddAction(side+"Position",InputActionType.Value,b+"devicePosition",expectedControlLayout:"Vector3");
                input.AddAction(side+"Rotation",InputActionType.Value,b+"deviceRotation",expectedControlLayout:"Quaternion");
                input.AddAction(side+"Tracked",InputActionType.Value,b+"isTracked",expectedControlLayout:"Button");
                input.AddAction(side+"State",InputActionType.Value,b+"trackingState",expectedControlLayout:"Integer");
                input.AddAction(side+"Trigger",InputActionType.Value,b+"trigger");
            }
            input.AddAction("Replay",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");
            input.AddAction("Restart",InputActionType.Button,"<XRController>{RightHand}/primaryButton");
        }
        void OnEnable(){ input.Enable(); }
        void Start()
        {
#if UNITY_EDITOR
            simulated=!XRSettings.isDeviceActive;
#endif
            rigInput=FindFirstObjectByType<EmilVRInput>();
            if(rigInput){oldModeSwitch=rigInput.allowModeSwitch;rigInput.allowModeSwitch=false;rigInput.mode=EmilVRInput.InputMode.Controllers;}
            for(int side=0;side<2;side++){ResizeWand(wands[side],wandLength);ResizeWand(ghosts[side],wandLength);}
            ResetPreview();
        }
        void OnDisable(){input.Disable();Finish(false);if(rigInput)rigInput.allowModeSwitch=oldModeSwitch;}
        void OnDestroy(){csv?.Dispose();input.Dispose();}
        void OnApplicationPause(bool pause){if(pause&&running){Finish(false);status="Session interrupted. Press A to restart.";}csv?.Flush();}
        void ResetPreview()
        {
            FrameOrigin=head.position; FrameRotation=Quaternion.Euler(0,head.eulerAngles.y,0);neutralHead=head.rotation;
            for(int s=0;s<2;s++){baseline[s]=new Vector3(s==0?-.24f:.24f,-.32f,.40f);baselineRotation[s]=Quaternion.identity;}
            board.SetPositionAndRotation(FrameOrigin+FrameRotation*new Vector3(0,.22f,1.6f),FrameRotation);
        }
        bool HeadValid()
        {
            if(simulated)return true;
            var d=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool t)&&t && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,out InputTrackingState state)&& (state&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);
        }
        void Update()
        {
            if(!head||!trackingSpace)return;
            for(int s=0;s<2;s++)
            {
                string side=s==0?"Left":"Right";
                var state=(InputTrackingState)input[side+"State"].ReadValue<int>();
                valid[s]=input[side+"Tracked"].IsPressed()&&(state&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);
                positions[s]=trackingSpace.TransformPoint(input[side+"Position"].ReadValue<Vector3>());
                rotations[s]=trackingSpace.rotation*input[side+"Rotation"].ReadValue<Quaternion>();
                if(simulated){valid[s]=true;Target(s,elapsed,out positions[s],out rotations[s]);}
                wands[s].gameObject.SetActive(valid[s]);
                if(valid[s])wands[s].SetPositionAndRotation(positions[s],rotations[s]);
            }
            if(replay&&replay.IsDemo&&replay.IsActive)
            {
                board.gameObject.SetActive(false);
                for(int side=0;side<2;side++){wands[side].gameObject.SetActive(false);ghosts[side].gameObject.SetActive(false);gates[side].gameObject.SetActive(false);tubes[side].gameObject.SetActive(false);}
                return;
            }
            bool b=input["Replay"].IsPressed() || (simulated&&Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame);
            if(b&&!previousB&&!running&&!counting&&replay)
            {
                if(replay.IsActive)replay.Close();else if(!replay.Open())status="No recorded session yet.";
            }
            previousB=b;
            bool reviewing=replay&&replay.IsActive;
            board.gameObject.SetActive(!reviewing);
            for(int s=0;s<2;s++){ghosts[s].gameObject.SetActive(!reviewing);gates[s].gameObject.SetActive(!reviewing);tubes[s].gameObject.SetActive(!reviewing);if(reviewing)wands[s].gameObject.SetActive(false);}
            if(reviewing)return;
            bool a=input["Restart"].IsPressed() || (simulated&&Keyboard.current!=null&&(Keyboard.current.spaceKey.isPressed||Keyboard.current.spaceKey.wasPressedThisFrame));
            if(a&&!previousA&&!running&&!counting){finished=false;calibrated=false;ResetPreview();if(simulated)Calibrate();}
            previousA=a;
            if(replay&&replay.IsDemo)return;
            bool all=valid[0]&&valid[1]&&HeadValid();
            if(!calibrated&&!finished)
            {
                bool held=input["LeftTrigger"].ReadValue<float>()>.7f&&input["RightTrigger"].ReadValue<float>()>.7f;
                if(all&&held){calibrationHold+=Time.unscaledDeltaTime;if(calibrationHold>1f)Calibrate();}else calibrationHold=0;
            }
            if(replay&&replay.IsDemo)return;
            if(counting)
            {
                if(all)countdown-=Time.unscaledDeltaTime;
                if(countdown<=0){counting=false;running=true;OpenRecording();}
            }
            if(running)
            {
                float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
                if(all)
                {
                    int round=Mathf.Clamp((int)(elapsed/roundSeconds),0,2);
                    for(int s=0;s<2;s++){Errors(s,out float pe,out float te,out float ae);total[round,s]+=Quality(pe,te,ae,handTolerance,tipTolerance,angleTolerance)*dt;posTotal[round,s]+=pe*dt;tipTotal[round,s]+=te*dt;angleTotal[round,s]+=ae*dt;}
                    duration[round]+=dt;
                }
                else pausedSeconds+=Time.unscaledDeltaTime;
                sampleTime+=Time.unscaledDeltaTime;
                if(sampleTime>=1f/60f){sampleTime%=1f/60f;CaptureReplay();Record(all);}
                if(all)elapsed+=dt;
                if(elapsed>=roundSeconds*3)Finish(true);
            }
            for(int s=0;s<2;s++)
            {
                Target(s,elapsed,out Vector3 p,out Quaternion q);ghosts[s].SetPositionAndRotation(p,q);
                gates[s].SetPositionAndRotation(p+q*Vector3.forward*wandLength,FrameRotation);
                tubes[s].Draw(this,s,elapsed);
            }
            uiTime+=Time.unscaledDeltaTime;
            if(uiTime>.1f){uiTime=0;UpdateDisplay(all);}
        }
        void Calibrate()
        {
            var yaw=Quaternion.Euler(0,head.eulerAngles.y,0);
            for(int s=0;s<2;s++)
            {
                Vector3 local=Quaternion.Inverse(yaw)*(positions[s]-head.position);
                if(local.z<.20f||local.z>.75f||local.y>-.05f||local.y<-.75f || (s==0?local.x>-.06f:local.x<.06f))
                {status="Hold hands apart and comfortably IN FRONT of you.";calibrationHold=0;return;}
                if(Vector3.Angle(rotations[s]*Vector3.forward,yaw*Vector3.forward)>45f)
                {status="Point both wands forward, then hold both triggers.";calibrationHold=0;return;}
            }
            ResetPreview();
            for(int s=0;s<2;s++){baseline[s]=Quaternion.Inverse(FrameRotation)*(positions[s]-FrameOrigin);baselineRotation[s]=Quaternion.Inverse(FrameRotation)*rotations[s];}
            Array.Clear(total,0,total.Length);Array.Clear(posTotal,0,posTotal.Length);Array.Clear(tipTotal,0,tipTotal.Length);Array.Clear(angleTotal,0,angleTotal.Length);Array.Clear(duration,0,duration.Length);
            elapsed=pausedSeconds=sampleTime=0;calibrated=true;counting=true;countdown=3;status="";savedPath="";
            if(replay&&!RecordSimulationForTesting)replay.OpenDemo(this,head.position,head.rotation);
        }
        public void Target(int side,float t,out Vector3 position,out Quaternion rotation)
        {
            t=Mathf.Clamp(t,0,roundSeconds*3);
            int stage=Mathf.Clamp((int)(t/roundSeconds),0,2);
            float phase=t-stage*roundSeconds;
            float wave=Mathf.Sin(phase*Mathf.PI*2/5f)*Mathf.SmoothStep(0,1,Mathf.Min(phase,roundSeconds-phase));
            float height=amplitude*wave*(stage==1&&side==1?-1:1);
            if(stage==2)height*=.65f;
            position=FrameOrigin+FrameRotation*(baseline[side]+Vector3.up*height);
            rotation=FrameRotation*baselineRotation[side];
        }
        public static float Quality(float positionError,float tipError,float angleError,float positionLimit,float tipLimit,float angleLimit)
        {
            // Full credit within an explicit acceptance zone; correlated errors are not multiplied.
            return Mathf.Min(Band(positionError,.04f,positionLimit),Mathf.Min(Band(tipError,.065f,tipLimit),Band(angleError,12f,angleLimit)));
        }
        static float Band(float error,float fullCredit,float zeroCredit)
        {return 1-Mathf.InverseLerp(Mathf.Min(fullCredit,zeroCredit*.8f),zeroCredit,error);}
        public static void ResizeWand(Transform wand,float length)
        {
            if(!wand)return;
            var shaft=wand.Find("Short Wand");if(!shaft)shaft=wand.Find("Sword Wand");
            if(shaft){shaft.localPosition=new Vector3(0,0,length*.5f);shaft.localScale=new Vector3(.018f,length*.5f,.018f);}
            var tip=wand.Find("Tip");if(tip)tip.localPosition=new Vector3(0,0,length);
        }
        void Errors(int side,out float pe,out float te,out float ae)
        {
            Target(side,elapsed,out Vector3 p,out Quaternion q);
            pe=Vector3.Distance(positions[side],p);te=Vector3.Distance(positions[side]+rotations[side]*Vector3.forward*wandLength,p+q*Vector3.forward*wandLength);
            ae=Vector3.Angle(rotations[side]*Vector3.forward,q*Vector3.forward);
        }
        void UpdateDisplay(bool all)
        {
            string prefix=simulated?"DESKTOP SIMULATION - NOT RECORDED\n":"WAND PATH LAB\n";
            if(finished){display.text=prefix+(completed?"SESSION COMPLETE     ":"SESSION INTERRUPTED     ")+Score.ToString("F0")+" / 100\n"+"Left RED   |   Right GREEN\n"+Results()+"\n"+(savedPath!=""&&!recordingFailed?"Movement data saved on headset.\n":recordingFailed?"Recording failed - check headset storage.\n":"")+"A: new session   |   B: watch replay";return;}
            if(!calibrated){display.text=prefix+"LEFT RED  /  RIGHT GREEN\nHold hands comfortably in front. Point wands forward.\nHold BOTH triggers for 1 second to calibrate.\nFollow the ghost HANDLES and put the tips in the rings.\n"+(simulated?"SPACE: simulate   |   R: replay":(status+"\nB: replay last saved session"));return;}
            if(counting){display.text=prefix+(all?"STARTING IN "+Mathf.CeilToInt(countdown):"Waiting for headset and both controllers...")+"\nMatch the ghost handles and wand direction.";return;}
            int stage=Mathf.Clamp((int)(elapsed/roundSeconds),0,2);
            string[] labels={"TOGETHER","ALTERNATING","SMALL CONTROLLED MOVEMENTS"};
            var text=new StringBuilder(prefix+labels[stage]+"   "+Mathf.CeilToInt(roundSeconds*3-elapsed)+"s\nScore "+Score.ToString("F0")+" / 100\n");
            if(!all)text.Append("PAUSED - tracking lost. Bring controllers into view.\n");
            else for(int s=0;s<2;s++){Errors(s,out float p,out float tip,out float angle);text.Append((s==0?"LEFT  ":"RIGHT ")+"Hand "+(p*100).ToString("F1")+"cm  Tip "+(tip*100).ToString("F1")+"cm  Angle "+angle.ToString("F0")+" deg\n");}
            float headMove=Vector3.Distance(head.position,FrameOrigin),headAngle=Quaternion.Angle(neutralHead,head.rotation);
            if(headMove>.15f||headAngle>20)text.Append("Head moved from start - keep a comfortable posture.\n");
            text.Append(recordingFailed?"Recording unavailable - check storage.":"Match handles + direction, not only the tip.");display.text=text.ToString();
        }
        string Results(){var s=new StringBuilder();for(int r=0;r<3;r++)s.Append("Challenge "+(r+1)+": "+(duration[r]>0?((total[r,0]+total[r,1])/duration[r]*50).ToString("F0"):"--")+" / 100\n");return s.ToString();}
        double startedAt;
        void OpenRecording()
        {
            startedAt=Time.realtimeSinceStartupAsDouble;recordingFailed=false;
            if(replay)replay.BeginRecording(FrameOrigin,FrameRotation,wandLength,simulated);
#if UNITY_EDITOR
            if(simulated&&!RecordSimulationForTesting)return;
#else
            if(simulated)return;
#endif
            try
            {
                sessionId=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");savedPath=Path.Combine(Application.persistentDataPath,"Sessions",sessionId);
#if UNITY_EDITOR
                if(simulated)savedPath=Path.Combine("/tmp/emil-wand-prototype-test",sessionId);
#endif
                Directory.CreateDirectory(savedPath);
                csv=new StreamWriter(Path.Combine(savedPath,"samples.csv"),false,Encoding.UTF8);
                csv.WriteLine("timestamp_s,exercise_time_s,challenge,side,valid,scorable,px,py,pz,qx,qy,qz,qw,target_px,target_py,target_pz,target_qx,target_qy,target_qz,target_qw,head_px,head_py,head_pz,head_qx,head_qy,head_qz,head_qw,hand_error_m,tip_error_m,pointing_error_deg,head_displacement_m,head_rotation_deg");
                var settings=new SessionSettings{sessionId=sessionId,frameOrigin=FrameOrigin,frameRotation=FrameRotation,leftStart=baseline[0],rightStart=baseline[1],leftRotation=baselineRotation[0],rightRotation=baselineRotation[1],amplitude=amplitude,handTolerance=handTolerance,tipTolerance=tipTolerance,angleTolerance=angleTolerance,wandLength=wandLength,roundSeconds=roundSeconds};
                if(simulated)settings.inputSource="SIMULATED TEST - not participant data";
                File.WriteAllText(Path.Combine(savedPath,"session.json"),JsonUtility.ToJson(settings,true));
            }
            catch(Exception e){recordingFailed=true;status="Recording unavailable";savedPath="";Debug.LogError("Recording: "+e.Message);csv?.Dispose();csv=null;}
        }
        void CaptureReplay()
        {
            if(!replay)return;
            Target(0,elapsed,out Vector3 lp,out Quaternion lq);Target(1,elapsed,out Vector3 rp,out Quaternion rq);
            replay.Capture(new WandReplayFrame{time=(float)(Time.realtimeSinceStartupAsDouble-startedAt),headValid=HeadValid(),leftValid=valid[0],rightValid=valid[1],head=head.position,headRotation=head.rotation,left=positions[0],right=positions[1],leftRotation=rotations[0],rightRotation=rotations[1],targetLeft=lp,targetRight=rp,targetLeftRotation=lq,targetRightRotation=rq});
        }
        static string F(double n)=>n.ToString("G9",Inv);
        static void Pose(StringBuilder s,Vector3 p,Quaternion q){s.Append(',').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',').Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w));}
        void Record(bool all)
        {
            if(csv==null)return;
            try{
                for(int side=0;side<2;side++)
                {
                    Target(side,elapsed,out Vector3 p,out Quaternion q);Errors(side,out float pe,out float te,out float ae);
                    var s=new StringBuilder(F(Time.realtimeSinceStartupAsDouble-startedAt)+","+F(elapsed)+","+Mathf.Clamp((int)(elapsed/roundSeconds)+1,1,3)+","+(side==0?"left":"right")+","+(valid[side]?"1":"0")+","+(all?"1":"0"));
                    if(valid[side])Pose(s,positions[side],rotations[side]);else s.Append(",,,,,,,");
                    Pose(s,p,q);if(HeadValid())Pose(s,head.position,head.rotation);else s.Append(",,,,,,,");
                    s.Append(',').Append(valid[side]?F(pe):"").Append(',').Append(valid[side]?F(te):"").Append(',').Append(valid[side]?F(ae):"").Append(',').Append(HeadValid()?F(Vector3.Distance(head.position,FrameOrigin)):"").Append(',').Append(HeadValid()?F(Quaternion.Angle(neutralHead,head.rotation)):"");csv.WriteLine(s);
                }
                if(Time.unscaledTime-flushTime>1){flushTime=Time.unscaledTime;csv.Flush();}
            }catch(Exception e){recordingFailed=true;Debug.LogError("Recording: "+e.Message);csv.Dispose();csv=null;}
        }
        void Finish(bool complete)
        {
            if(!running)return;running=false;finished=true;completed=complete;csv?.Flush();csv?.Dispose();csv=null;
            if(savedPath!="")try{
                var s=new StringBuilder("challenge,side,score,mean_hand_error_m,mean_tip_error_m,mean_pointing_error_deg,valid_seconds,session_paused_seconds,complete\n");
                for(int r=0;r<3;r++)for(int side=0;side<2;side++){double d=duration[r];s.AppendLine((r+1)+","+(side==0?"left":"right")+","+(d>0?F(total[r,side]/d*100)+","+F(posTotal[r,side]/d)+","+F(tipTotal[r,side]/d)+","+F(angleTotal[r,side]/d):",,,")+","+F(d)+","+F(pausedSeconds)+","+(complete?"1":"0"));}
                File.WriteAllText(Path.Combine(savedPath,"summary.csv"),s.ToString());if(replay)replay.Save(savedPath);
            }catch(Exception e){Debug.LogError(e.Message);}
        }
        [Serializable] class SessionSettings
        {
            public string sessionId;public string schemaVersion="1",scoreVersion="prototype-2-tolerance-bands",units="metres-seconds-degrees",inputSource="controller-grip";
            public Vector3 frameOrigin,leftStart,rightStart;public Quaternion frameRotation,leftRotation,rightRotation;
            public float fullCreditHandMetres=.04f,fullCreditTipMetres=.065f,fullCreditPointingDegrees=12f;
            public string exerciseIntent="whole-arm vertical hand translation with comfortable soft elbow bend; elbow not measured";
            public float amplitude,handTolerance,tipTolerance,angleTolerance,wandLength,roundSeconds;
        }
    }
}
