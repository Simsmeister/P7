using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace EmilVR
{
    [Serializable] public class RepairRun
    {
        public string version="conduit-1",outcome="active";
        public float battery=100,power;public int movements,target=18;
        public bool Finished=>outcome!="active";
        public void Resolve(float accuracy)
        {
            if(Finished)return;
            float q=Mathf.Clamp01(accuracy);movements++;
            power=Mathf.Min(1,power+Mathf.InverseLerp(.25f,1,q)/Mathf.Max(1,target));
            battery=Mathf.Clamp(battery-Mathf.Max(0,.6f-q)*16+(q>=.85f?2:0),0,100);
            if(battery<=0)outcome="defeat";else if(power>=.99999f)outcome="victory";
        }
    }
    // All game rewards use the existing personal-range score. Tracking loss never drains battery.
    public class ConduitRepairGame : MonoBehaviour
    {
        public ShieldExerciseGame game;public Transform hud,chargeRing;public TextMesh status;
        public Renderer[] nodes,shipLights;public LineRenderer[] repaired;
        public RepairRun Run {get;private set;}=new RepairRun();
        public string Result=>Run.outcome=="victory"?"FULL ENERGY - GREAT SESSION":Run.outcome=="defeat"?"OUT OF FLOW - TRY AGAIN":"SESSION STOPPED";
        readonly List<string> rows=new List<string>();
        readonly List<string> nodeRows=new List<string>();
        readonly float[] quality=new float[120];readonly bool[] sampled=new bool[120];
        int repetition=-1,lastSample=-1;float lastClock,charge;
        MaterialPropertyBlock tint;
        static string F(float v)=>v.ToString("F4",CultureInfo.InvariantCulture);
        public void Begin(int target){Run=new RepairRun{target=Mathf.Max(1,target)};rows.Clear();nodeRows.Clear();repetition=-1;}
        public void Resolve(float accuracy){Run.Resolve(accuracy);rows.Add(string.Join(",",Run.movements,F(accuracy),F(Run.power),F(Run.battery),Run.outcome));}
        public string Export()=>"movement,accuracy,ship_power,battery,outcome\n"+string.Join("\n",rows);
        public string ExportNodes()=>"movement,exercise_id,side,exercise_time_s,path_quality,node_repaired\n"+string.Join("\n",nodeRows);
        void Awake(){tint=new MaterialPropertyBlock();}
        void Colorize(Renderer renderer,Color color){tint.SetColor("_BaseColor",color);renderer.SetPropertyBlock(tint);}
        public static bool NodeRepaired(float q)=>q>=.65f;
        // Graded feedback: dim amber when far off the line, full white when on it. NodeRepaired stays the export/charge threshold.
        public static Color Glow(float q)=>Color.Lerp(new Color(1,.48f,.12f)*.35f,Color.white,Mathf.InverseLerp(.4f,.9f,q));
        TrailRenderer[] trails;
        // Short hand trail, added at runtime so the generated marker prefabs stay untouched.
        void UpdateTrails(bool visible,float match)
        {
            if(trails==null){trails=new TrailRenderer[game.shields.Length];for(int s=0;s<trails.Length;s++){var t=game.shields[s].gameObject.AddComponent<TrailRenderer>();t.sharedMaterial=game.pathStream.paths[0].sharedMaterial;t.time=.25f;t.minVertexDistance=.005f;t.widthMultiplier=.03f;t.widthCurve=AnimationCurve.Linear(0,1,1,0);t.emitting=false;trails[s]=t;}}
            for(int s=0;s<trails.Length;s++){
                var t=trails[s];bool on=visible&&game.CurrentStep=="Playing"&&game.RepairTracked&&t.gameObject.activeInHierarchy;
                if(on&&!t.emitting)t.Clear();t.emitting=on;
                if(on){tint.SetColor("_BaseColor",Glow(match));t.SetPropertyBlock(tint);}
            }
        }
        void LateUpdate()
        {
            if(!game||!game.head)return;
            transform.SetPositionAndRotation(game.CoachOrigin,game.CoachRotation);
            var camera=game.head.GetComponent<Camera>();if(camera){camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.03f,.055f);}
            bool visible=game.PathsVisible&&!(game.replay&&game.replay.IsActive);
            hud.gameObject.SetActive(visible);chargeRing.gameObject.SetActive(false);
            if(!visible){foreach(var n in nodes)n.gameObject.SetActive(false);foreach(var line in repaired)line.gameObject.SetActive(false);}
            if(shipLights!=null)for(int i=0;i<shipLights.Length;i++)Colorize(shipLights[i],i<Mathf.FloorToInt(Run.power*shipLights.Length)?new Color(.15f,.9f,1):new Color(.06f,.12f,.19f));
            if(!visible||!game.PathDefinition(0,out var p,out var c,out int side))return;
            float clock=game.PathClock,duration=p.outwardSeconds+p.holdSeconds+p.returnSeconds;
            if(repetition!=game.CompletedRepetitions||clock<lastClock){Array.Clear(sampled,0,sampled.Length);Array.Clear(quality,0,quality.Length);repetition=game.CompletedRepetitions;lastSample=-1;charge=0;}
            float dt=Mathf.Max(0,clock-lastClock);lastClock=clock;
            float match=game.RepairQuality;UpdateTrails(visible,match);
            Color hand=side==0?new Color(1,.2f,.3f):new Color(.1f,1,.45f),missed=new Color(1,.48f,.12f);
            if(game.CurrentStep=="Playing"&&game.RepairTracked){
                int sample=Mathf.Clamp(Mathf.FloorToInt(clock/duration*119),0,119);
                // Record one path sample per time bin; never fill tracking gaps with invented data.
                if(sample!=lastSample){quality[sample]=match;sampled[sample]=true;lastSample=sample;nodeRows.Add(string.Join(",",repetition+1,p.exerciseId,side==0?"left":"right",F(clock),F(match),NodeRepaired(match)?1:0));}
                if(p.Phase(clock)=="hold"&&NodeRepaired(match))charge=Mathf.Min(1,charge+dt/Mathf.Max(.1f,p.holdSeconds));
            }
            for(int i=0;i<nodes.Length;i++){
                float t=duration*i/(nodes.Length-1f);bool show=t>=clock-1.3f;nodes[i].gameObject.SetActive(show);if(!show)continue;
                int bin=Mathf.Clamp(Mathf.RoundToInt(t/duration*119),0,119);bool passed=t<=clock;
                nodes[i].gameObject.SetActive(true);nodes[i].transform.position=Point(p,c,side,t,clock);
                Colorize(nodes[i],passed?(sampled[bin]?Glow(quality[bin])*Mathf.Clamp01(1-(clock-t)/1.3f):missed*.35f):hand*.35f);
                nodes[i].transform.localScale=Vector3.one*(passed?.025f:.035f);
            }
            for(int i=0;i<repaired.Length;i++){
                float t=duration*i/repaired.Length,next=duration*(i+1)/repaired.Length;
                int bin=Mathf.Clamp(Mathf.RoundToInt(t/duration*119),0,119);var line=repaired[i];bool show=next<=clock&&t>=clock-1.2f&&sampled[bin];line.gameObject.SetActive(show);if(!show)continue;line.SetPosition(0,Point(p,c,side,t,clock));line.SetPosition(1,Point(p,c,side,next,clock));float fade=Mathf.Clamp01(1-(clock-t)/1.2f);line.widthMultiplier=.018f*fade;Colorize(line,Glow(quality[bin])*fade);
            }
            if(clock<=p.outwardSeconds+p.holdSeconds){chargeRing.gameObject.SetActive(true);float t=Mathf.Max(p.outwardSeconds,clock);chargeRing.position=Point(p,c,side,t,clock);chargeRing.rotation=game.CoachRotation;chargeRing.localScale=Vector3.one*(.14f+.045f*charge);Colorize(chargeRing.GetComponent<Renderer>(),Color.Lerp(hand*.5f,hand,charge));}
            string cue=!game.RepairTracked?"TRACKING PAUSED":clock<0?(game.arcade&&game.arcade.LastLabel!=""?game.arcade.LastLabel+"!   ":"")+"NEXT LINE INCOMING":p.Phase(clock)=="hold"?"HOLD "+Mathf.RoundToInt(charge*100)+"%":p.Phase(clock)=="rest"?"LINE COMPLETE":NodeRepaired(match)?"IN FLOW":"BACK ON THE LINE";
            status.text="ENERGY "+Mathf.RoundToInt(Run.power*100)+"%   |   FLOW "+Mathf.CeilToInt(Run.battery)+"%\n"+(game.arcade?game.arcade.Points+" POINTS   x"+game.arcade.Multiplier+"   |   ":"")+cue;
        }
        Vector3 Point(ShieldExerciseProfile p,ShieldExerciseGame.Calibration c,int side,float t,float clock)=>ExercisePathStream.Approach(game.PathVisualPoint(p,c,side,t),game.CoachRotation*Vector3.forward,t-clock,game.pathStream.approachSpeed);
    }
}
