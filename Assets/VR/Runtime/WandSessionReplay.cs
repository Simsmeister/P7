using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EmilVR
{
    [Serializable] public class WandReplayFrame
    {
        public string exerciseId,phase;public int activeSide;public float illustrativeShoulderOffset;
        public float time;
        public bool headValid,leftValid,rightValid;
        public Vector3 head,left,right,targetLeft,targetRight;
        public Quaternion headRotation,leftRotation,rightRotation,targetLeftRotation,targetRightRotation;
    }
    [Serializable] public class WandReplayData
    {
        public string equipment="wand";
        public string inputSource="XR headset and controllers";
        public string schemaVersion="1",bodySource="estimated mannequin - elbows, torso and legs not measured";
        public Vector3 origin;public Quaternion rotation;public float wandLength=.18f;
        public List<WandReplayFrame> frames=new List<WandReplayFrame>();
    }
    public class WandSessionReplay : MonoBehaviour
    {
        public Transform viewer,figure,headModel,chest,pelvis,targetHead,neck,waist;
        public Transform[] hands=new Transform[2],wands=new Transform[2],upperArms=new Transform[2],forearms=new Transform[2],thighs=new Transform[2],shins=new Transform[2],feet=new Transform[2],targetHands=new Transform[2],targetTips=new Transform[2],targetElbows=new Transform[2];
        public Transform[] replayShields=new Transform[2];
        string demoCaption;bool guidedPose;Vector3 guidanceHand;int guidanceSide;
        public TextMesh label;
        public WandReplayData Data { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsDemo { get; private set; }
        WandReplayData savedRecording;
        float viewDistance=2f,orbit,demoYaw;Vector3 viewOrigin;Quaternion viewerYaw;
        public float PlaybackTime { get; private set; }
        public bool Paused { get; private set; }
        InputActionMap input;
        Vector3 anchor;Quaternion viewRotation;
        const float Scale=.65f;
        bool triggerPrevious;
        void Awake(){input=new InputActionMap("Session Replay");input.AddAction("Pause",InputActionType.Value,"<XRController>{RightHand}/trigger");input.AddAction("View",InputActionType.Value,"<XRController>{LeftHand}/primary2DAxis");input.AddAction("Seek",InputActionType.Value,"<XRController>{RightHand}/primary2DAxis");}
        void OnEnable(){input.Enable();if(figure)figure.gameObject.SetActive(false);}
        void OnDisable(){input.Disable();Close();}
        void OnDestroy(){input.Dispose();}
        public void BeginRecording(Vector3 origin,Quaternion rotation,float length,bool simulated=false){Data=new WandReplayData{origin=origin,rotation=rotation,wandLength=length,inputSource=simulated?"SIMULATED TEST":"XR headset and controllers"};}
        public void Capture(WandReplayFrame frame){if(Data!=null&&Data.frames.Count<18000)Data.frames.Add(frame);}
        public void Save(string folder){if(Data!=null&&Data.frames.Count>0)File.WriteAllText(Path.Combine(folder,"replay.json"),JsonUtility.ToJson(Data));}
        public bool LoadRecording(string file)
        {
            try
            {
                var d=JsonUtility.FromJson<WandReplayData>(File.ReadAllText(file));
                if(d==null||d.frames==null||d.frames.Count<2||d.frames.Count>18000)return false;
                for(int i=1;i<d.frames.Count;i++)if(d.frames[i].time<d.frames[i-1].time)return false;
                Close();Data=d;return true;
            }catch(Exception e){Debug.LogWarning("Replay load: "+e.Message);return false;}
        }
        public bool LoadLatest()
        {
            string folder=Path.Combine(Application.persistentDataPath,"Sessions");if(!Directory.Exists(folder))return false;
            var files=Directory.GetFiles(folder,"replay.json",SearchOption.AllDirectories);Array.Sort(files,StringComparer.Ordinal);
            for(int i=files.Length-1;i>=0;i--)if(LoadRecording(files[i]))return true;
            return false;
        }
        public bool Open()
        {
            if((Data==null||Data.frames.Count<2)&&!LoadLatest())return false;
            viewOrigin=viewer.position;viewerYaw=Quaternion.Euler(0,viewer.eulerAngles.y,0);viewDistance=2f;orbit=0;
            UpdateView();
            for(int side=0;side<2;side++)WandPathGame.ResizeWand(wands[side],Data.wandLength);
            PlaybackTime=0;Paused=false;IsActive=true;triggerPrevious=false;figure.gameObject.SetActive(true);return true;
        }
        public void Close(){demoYaw=0;guidedPose=false;IsActive=false;if(figure)figure.gameObject.SetActive(false);if(IsDemo){Data=savedRecording;savedRecording=null;IsDemo=false;}}
        void UpdateView()
        {
            anchor=viewOrigin+viewerYaw*new Vector3(0,-.05f,viewDistance);
            viewRotation=viewerYaw*Quaternion.Euler(0,180f+orbit,0);
        }
        public void HoldDemoAt(float time,string caption){PlaybackTime=time;Paused=true;guidedPose=true;demoCaption=caption;orbit=0;}
        public void UpdateGuidance(Vector3 hand,int side,string caption){guidanceHand=hand;guidanceSide=side;demoCaption=caption;}
        public static float TutorialYaw(bool sideView,int side)=>sideView?(side==0?-90:90):0;
        public void OpenExerciseDemo(WandReplayData demonstration,string caption,bool sideView=false)
        {savedRecording=Data;Data=demonstration;demoCaption=caption;demoYaw=TutorialYaw(sideView,demonstration.frames[0].activeSide);Open();IsDemo=true;}
        public void OpenDemo(WandPathGame game,Vector3 head,Quaternion rotation)
        {
            demoCaption=null;savedRecording=Data;
            Data=new WandReplayData{origin=game.FrameOrigin,rotation=game.FrameRotation,wandLength=game.wandLength,inputSource="EXERCISE DEMONSTRATION"};
            for(int i=0;i<=1080;i++)
            {
                float t=i/60f;int stage=Mathf.Min((int)(t/6),2);float phase=Mathf.Min(t-stage*6,5f);
                game.Target(0,stage*game.roundSeconds+phase,out var left,out var lq);
                game.Target(1,stage*game.roundSeconds+phase,out var right,out var rq);
                Data.frames.Add(new WandReplayFrame{time=t,headValid=true,leftValid=true,rightValid=true,head=head,headRotation=rotation,left=left,right=right,leftRotation=lq,rightRotation=rq,targetLeft=left,targetRight=right,targetLeftRotation=lq,targetRightRotation=rq});
            }
            Open();IsDemo=true;
        }
        Quaternion DemoRotation=>viewerYaw*Quaternion.Euler(0,demoYaw,0);
        public static Vector3 MirrorPoint(Vector3 p)=>new Vector3(p.x,p.y,-p.z);
        Vector3 Map(Vector3 p){var local=Quaternion.Inverse(Data.rotation)*(p-Data.origin);return anchor+(IsDemo?DemoRotation*MirrorPoint(local):viewRotation*local)*Scale;}
        Quaternion Map(Quaternion q){var local=Quaternion.Inverse(Data.rotation)*q;return IsDemo?DemoRotation*Quaternion.LookRotation(MirrorPoint(local*Vector3.forward),MirrorPoint(local*Vector3.up)):viewRotation*local;}
        void Update()
        {
            if(!IsActive||Data==null)return;
            Vector2 view=(guidedPose||IsDemo)?Vector2.zero:input["View"].ReadValue<Vector2>();
            if(Mathf.Abs(view.x)>.2f)orbit+=view.x*65*Time.unscaledDeltaTime;
            if(Mathf.Abs(view.y)>.2f)viewDistance=Mathf.Clamp(viewDistance-view.y*.7f*Time.unscaledDeltaTime,.65f,3.5f);
            UpdateView();
            bool trigger=!IsDemo&&input["Pause"].ReadValue<float>()>.5f;
            if(trigger&&!triggerPrevious)Paused=!Paused;triggerPrevious=trigger;
            float seek=IsDemo?0:input["Seek"].ReadValue<Vector2>().x;
            if(Mathf.Abs(seek)>.25f)PlaybackTime+=seek*4*Time.unscaledDeltaTime;
            else if(!Paused)PlaybackTime+=Time.unscaledDeltaTime;
            float end=Data.frames[Data.frames.Count-1].time;
            if(PlaybackTime>=end){if(IsDemo){Close();return;}PlaybackTime=end;Paused=true;}
            PlaybackTime=Mathf.Clamp(PlaybackTime,0,end);
            int lo=0,hi=Data.frames.Count-1;
            while(lo<hi){int m=(lo+hi+1)/2;if(Data.frames[m].time<=PlaybackTime)lo=m;else hi=m-1;}
            var a=Data.frames[lo];var b=Data.frames[Mathf.Min(lo+1,Data.frames.Count-1)];
            float t=b.time-a.time<.2f&&b.time>a.time?Mathf.InverseLerp(a.time,b.time,PlaybackTime):0;
            var h=Vector3.Lerp(a.head,b.head,t);var hq=Quaternion.Slerp(a.headRotation,b.headRotation,t);
            bool headOK=a.headValid&&b.headValid;
            headModel.gameObject.SetActive(headOK);chest.gameObject.SetActive(headOK);pelvis.gameObject.SetActive(headOK);
            if(headOK){headModel.SetPositionAndRotation(Map(h),Map(hq));}
            Vector3 up=Data.rotation*Vector3.up,right=Data.rotation*Vector3.right,forward=Data.rotation*Vector3.forward;
            // These are a schematic reconstruction, not recorded anatomical joints.
            Vector3 c=h-up*.35f,hip=Data.origin-up*.85f+Vector3.ProjectOnPlane(h-Data.origin,up)*.35f;
            if(neck){neck.gameObject.SetActive(headOK);Segment(neck,Map(h-up*.10f),Map(c+up*.12f),.09f);}
            if(waist){waist.gameObject.SetActive(headOK);Segment(waist,Map(c-up*.12f),Map(hip+up*.05f),.18f);}
            chest.SetPositionAndRotation(Map(c),Map(Data.rotation));pelvis.SetPositionAndRotation(Map(hip),Map(Data.rotation));
            for(int s=0;s<2;s++)
            {
                bool ok=headOK&&(s==0?a.leftValid&&b.leftValid:a.rightValid&&b.rightValid);
                Vector3 hand=Vector3.Lerp(s==0?a.left:a.right,s==0?b.left:b.right,t);
                Quaternion q=Quaternion.Slerp(s==0?a.leftRotation:a.rightRotation,s==0?b.leftRotation:b.rightRotation,t);
                Vector3 target=Vector3.Lerp(s==0?a.targetLeft:a.targetRight,s==0?b.targetLeft:b.targetRight,t);
                Quaternion tq=Quaternion.Slerp(s==0?a.targetLeftRotation:a.targetRightRotation,s==0?b.targetLeftRotation:b.targetRightRotation,t);
                hands[s].gameObject.SetActive(ok);wands[s].gameObject.SetActive(ok&&Data.equipment!="shield");
                if(replayShields[s]){replayShields[s].gameObject.SetActive(ok&&Data.equipment=="shield"&&s==a.activeSide);replayShields[s].SetPositionAndRotation(Map(hand),IsDemo?Quaternion.Euler(0,viewer.eulerAngles.y,0):Map(q));}
                upperArms[s].gameObject.SetActive(ok);forearms[s].gameObject.SetActive(ok);
                float sign=s==0?-1:1;
                Vector3 shoulder=c+up*.10f+right*sign*.18f;
                if(IsDemo&&s==a.activeSide)shoulder+=forward*Mathf.Lerp(a.illustrativeShoulderOffset,b.illustrativeShoulderOffset,t);
                Vector3 pole=right*sign*.3f-forward*.2f-up*.4f;
                Vector3 elbow=EstimateElbow(shoulder,hand,pole);
                if(ok){hands[s].SetPositionAndRotation(Map(hand),Map(q));wands[s].SetPositionAndRotation(Map(hand),Map(q));Segment(upperArms[s],Map(shoulder),Map(elbow),.035f);Segment(forearms[s],Map(elbow),Map(hand),.028f);}
                targetHands[s].position=Map(guidedPose&&s==guidanceSide?guidanceHand:target);targetTips[s].position=Map(target+tq*Vector3.forward*Data.wandLength);
                Vector3 targetShoulder=Data.origin-up*.25f+right*sign*.18f;
                targetElbows[s].position=Map(EstimateElbow(targetShoulder,target,pole));
                Vector3 legTop=hip+right*sign*.11f;
                Vector3 foot=Data.origin-up*1.55f+right*sign*.12f+forward*.04f;
                Vector3 knee=(legTop+foot)*.5f+forward*.04f;
                thighs[s].gameObject.SetActive(headOK);shins[s].gameObject.SetActive(headOK);feet[s].gameObject.SetActive(headOK);
                Segment(thighs[s],Map(legTop),Map(knee),.045f);Segment(shins[s],Map(knee),Map(foot),.035f);feet[s].SetPositionAndRotation(Map(foot),Map(Data.rotation));
            }
            targetHead.position=Map(Data.origin);
            for(int s=0;s<2;s++){targetHands[s].gameObject.SetActive(Data.equipment!="shield"||s==a.activeSide);targetTips[s].gameObject.SetActive(Data.equipment!="shield");targetElbows[s].gameObject.SetActive(Data.equipment!="shield");}
            label.transform.SetPositionAndRotation(anchor+Vector3.up*.40f,Quaternion.Euler(0,viewer.eulerAngles.y,0));
            label.text=(Data.inputSource=="SIMULATED TEST"?"SIMULATED REPLAY  ":"REPLAY  ")+PlaybackTime.ToString("F1")+" / "+end.ToString("F1")+"s  "+(Paused?"PAUSED":"PLAYING")+"\nHead + hands recorded. BODY / ELBOWS / LEGS ESTIMATED.\nTransparent spheres = targets; yellow elbows = schematic guides.\nRight trigger: pause/play  |  Right stick: seek  |  B: return\nLeft stick: rotate / closer-further. You can also walk around.";
            if(Data.equipment=="shield")label.text+="\n"+a.exerciseId+"  "+a.phase+"  "+(a.activeSide==0?"LEFT":"RIGHT")+" | gaps show last recorded pose";
            if(IsDemo)label.text="WATCH FIRST - "+new[]{"TOGETHER","ALTERNATING","SMALL MOVEMENTS"}[Mathf.Min((int)(PlaybackTime/6),2)]+"\nMove hands up/down in front, elbows comfortably bent.\nWhole-arm movement; this is NOT an elbow-isolation test.\nKeep wands pointing forward. Demo body is schematic.\nGame starts after the demonstration.";
            if(IsDemo&&!string.IsNullOrEmpty(demoCaption))label.text=demoCaption;
        }
        public static Vector3 EstimateElbow(Vector3 shoulder,Vector3 hand,Vector3 pole)
        {
            const float upper=.30f,lower=.27f;
            Vector3 direction=hand-shoulder;float distance=Mathf.Clamp(direction.magnitude,.04f,upper+lower-.001f);direction=direction.sqrMagnitude>.0001f?direction.normalized:Vector3.down;
            Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(direction,Vector3.right).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));return shoulder+direction*along+bend*height;
        }
        static void Segment(Transform bone,Vector3 from,Vector3 to,float radius)
        {bone.position=(from+to)*.5f;bone.rotation=Quaternion.FromToRotation(Vector3.up,to-from);bone.localScale=new Vector3(radius*Scale,Vector3.Distance(from,to)*.5f,radius*Scale);}
    }
}
