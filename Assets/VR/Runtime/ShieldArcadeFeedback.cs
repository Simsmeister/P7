using UnityEngine;
namespace EmilVR
{
    // Pure timing model: no wall-clock catch-up and no rewards during gaps, rest or tracking loss.
    public class PerfectTrackingBonus
    {
        public const float WarmupSeconds=2, TickSeconds=.5f, MinimumQuality=.9f;
        public float Seconds {get;private set;}
        public int Ticks {get;private set;}
        public int StreakPoints {get;private set;}
        public int Multiplier=>Mathf.Clamp(1+(Ticks-1)/4,1,5);
        public void Reset(){Seconds=0;Ticks=StreakPoints=0;}
        public int Advance(bool eligible,float quality,float dt)
        {
            if(!eligible||float.IsNaN(quality)||quality<MinimumQuality){Reset();return 0;}
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return 0;
            Seconds+=Mathf.Min(dt,.1f);
            if(Seconds+0.00001f<WarmupSeconds+Ticks*TickSeconds)return 0;
            Ticks++;int points=100*Multiplier;StreakPoints+=points;return points;
        }
    }
    public class ShieldArcadeFeedback : MonoBehaviour
    {
        public Transform core,drone,burst;
        public Renderer[] cells=new Renderer[6];
        public TextMesh counter;
        public int Points {get;private set;}
        public int TrackingBonusPoints {get;private set;}
        public readonly PerfectTrackingBonus TrackingBonus=new PerfectTrackingBonus();
        TextMesh trackingPopup,totalScore,streakLabel,completionLabel,trackingWarning;
        Transform scoreHud;
        LineRenderer energyTrack,energyFill,flowTrack,flowFill,bonusHalo;
        float displayedPoints,scorePulse,completionTime;
        static readonly Color Ice=new Color(.35f,.85f,1),Gold=new Color(1,.84f,.35f);
        LineRenderer HudLine(string name,Transform parent,float width,Color tint)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=game.pathStream.paths[0].sharedMaterial;line.useWorldSpace=false;line.widthMultiplier=width;line.numCapVertices=4;
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",tint);line.SetPropertyBlock(block);return line;
        }
        static void Bar(LineRenderer line,float x,float y,float width){line.positionCount=2;line.SetPosition(0,new Vector3(x,y,0));line.SetPosition(1,new Vector3(x+Mathf.Max(.001f,width),y,0));}
        void CreateHud()
        {
            scoreHud=new GameObject("Floating score HUD").transform;scoreHud.SetParent(transform,false);
            totalScore.transform.SetParent(scoreHud,false);totalScore.transform.localPosition=Vector3.zero;totalScore.gameObject.SetActive(true);
            streakLabel=ScoreText("Rep streak",.007f,Ice);streakLabel.transform.SetParent(scoreHud,false);streakLabel.transform.localPosition=new Vector3(0,-.12f,0);
            completionLabel=ScoreText("Completion accent",.009f,Gold);completionLabel.transform.SetParent(scoreHud,false);completionLabel.transform.localPosition=new Vector3(0,.19f,0);
            trackingWarning=ScoreText("Tracking warning",.007f,Gold);trackingWarning.transform.SetParent(scoreHud,false);trackingWarning.transform.localPosition=new Vector3(0,-.33f,0);
            energyTrack=HudLine("Energy rail",scoreHud,.006f,new Color(.06f,.15f,.2f));energyFill=HudLine("Energy progress",scoreHud,.009f,Ice);
            flowTrack=HudLine("Flow rail",scoreHud,.004f,new Color(.06f,.15f,.2f));flowFill=HudLine("Flow remaining",scoreHud,.005f,new Color(.5f,.65f,.75f));
            Bar(energyTrack,-.28f,-.18f,.56f);Bar(flowTrack,-.28f,-.24f,.56f);energyFill.transform.localPosition=flowFill.transform.localPosition=Vector3.back*.002f;
            bonusHalo=HudLine("Bonus pulse ring",transform,.003f,Gold);bonusHalo.loop=true;bonusHalo.positionCount=48;bonusHalo.gameObject.SetActive(false);
            var energyLabel=ScoreText("Energy label",.0055f,Ice);energyLabel.transform.SetParent(scoreHud,false);energyLabel.transform.localPosition=new Vector3(-.39f,-.18f,0);energyLabel.text="ENERGY";
            var flowLabel=ScoreText("Flow label",.0055f,new Color(.5f,.65f,.75f));flowLabel.transform.SetParent(scoreHud,false);flowLabel.transform.localPosition=new Vector3(-.39f,-.24f,0);flowLabel.text="FLOW";
        }
        ShieldExerciseGame game;
        AudioClip[] trackingDings;
        float trackingPulse;
        public void TrackPerfect(bool eligible,float quality,float dt)
        {
            int points=TrackingBonus.Advance(eligible,quality,dt);
            if(points==0)return;
            Points+=points;TrackingBonusPoints+=points;trackingPulse=1;scorePulse=1;
            sound.PlayOneShot(trackingDings[TrackingBonus.Multiplier-1],.8f);
        }
        TextMesh ScoreText(string name,float size,Color tint)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var text=go.AddComponent<TextMesh>();text.font=counter.font;text.GetComponent<Renderer>().sharedMaterial=counter.GetComponent<Renderer>().sharedMaterial;
            text.fontSize=64;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=tint;return text;
        }
        void LateUpdate()
        {
            if(!game)return;
            bool visible=game.PathsVisible&&!(game.replay&&game.replay.IsActive);
            if(!scoreHud)CreateHud();
            scoreHud.gameObject.SetActive(visible);
            scoreHud.SetPositionAndRotation(game.CoachOrigin+game.CoachRotation*new Vector3(0,.10f,2.8f),game.CoachRotation);
            float dt=Time.unscaledDeltaTime;
            displayedPoints=Mathf.MoveTowards(displayedPoints,Points,Mathf.Max(3000,Mathf.Abs(Points-displayedPoints)*9)*dt);
            scorePulse=Mathf.Max(0,scorePulse-dt*3);completionTime=Mathf.Max(0,completionTime-dt);
            totalScore.text=Mathf.RoundToInt(displayedPoints).ToString("N0");
            totalScore.transform.localScale=Vector3.one*(1+.06f*scorePulse);totalScore.color=Color.Lerp(new Color(.8f,.9f,.96f),Color.white,scorePulse);
            streakLabel.text=Combo>1?"<b>"+Multiplier+"x</b>  /  "+Combo+" STREAK":"";
            completionLabel.gameObject.SetActive(completionTime>0);completionLabel.color=Color.Lerp(new Color(.25f,.25f,.22f),Gold,Mathf.Clamp01(completionTime));
            completionLabel.transform.localPosition=new Vector3(0,.19f+.035f*(1-Mathf.Clamp01(completionTime)),0);
            trackingWarning.gameObject.SetActive(visible&&!game.RepairTracked);trackingWarning.text="TRACKING PAUSED";
            if(game.conduit){Bar(energyFill,-.28f,-.18f,.56f*game.conduit.Run.power);Bar(flowFill,-.28f,-.24f,.56f*game.conduit.Run.battery/100);}
            bool bonusVisible=visible&&game.CurrentStep=="Playing"&&TrackingBonus.Ticks>0;
            trackingPopup.gameObject.SetActive(bonusVisible);
            trackingPulse=Mathf.Max(0,trackingPulse-Time.unscaledDeltaTime*4);
            bonusHalo.gameObject.SetActive(bonusVisible&&trackingPulse>0);
            if(bonusVisible){
                bonusHalo.transform.SetPositionAndRotation(game.ghost.position,game.CoachRotation);
                float radius=.09f+(1-trackingPulse)*.045f;for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;bonusHalo.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,-.008f));}
                bonusHalo.widthMultiplier=.003f*trackingPulse;
                trackingPopup.transform.SetPositionAndRotation(game.ghost.position+game.CoachRotation*new Vector3(0,.17f+.015f*trackingPulse,-.015f),game.CoachRotation);
                trackingPopup.transform.localScale=Vector3.one*(1+.13f*trackingPulse);
                trackingPopup.text="+"+TrackingBonus.StreakPoints+"   x"+TrackingBonus.Multiplier+"\n<size=24>PERFECT FLOW</size>";
            }
        }
        public int Combo {get;private set;}
        public int BestCombo {get;private set;}
        public int Multiplier=>Mathf.Clamp(Combo,1,5);
        public string LastLabel {get;private set;}="";
        // Team decision 2026-11-04: perfect = complete, score >= 90 and coverage >= 90 %, the same band the adaptive coach treats as strong.
        public static bool Perfect(bool complete,float score,float coverage)=>complete&&score>=.9f&&coverage>=.9f;
        public static string Label(bool complete,bool perfect)=>perfect?"PRÆCIS":complete?"GOD":"";
        // Audio tunables. A major pentatonic from the 880 Hz completion tone, so a perfect rep never sounds lower than a complete one; a mute would scale these levels.
        static readonly float[] StreakHz={880,987.77f,1108.73f,1318.51f,1479.98f,1760};
        const int LineHz=220;
        const float LineLevelOff=.012f,LineLevelOn=.035f,LineCutoffOff=300,LineCutoffOn=1600,LineFadeSeconds=.2f,ChimeDelay=.2f;
        public static int StreakNote(int combo)=>Mathf.Clamp(combo-1,0,StreakHz.Length-1);
        public static float LineLevel(float quality)=>Mathf.Lerp(LineLevelOff,LineLevelOn,quality);
        public static float LineCutoff(float quality)=>LineCutoffOff*Mathf.Pow(LineCutoffOn/LineCutoffOff,Mathf.Clamp01(quality));
        readonly int[] charges=new int[6],goals=new int[6];
        AudioSource sound,lineSource;AudioClip captureTone,blockTone,rewardTone,multiplierChime,lineClip;AudioClip[] streakNotes;
        AudioLowPassFilter lineFilter;float lineFade,lineQuality;
        float burstTime;
        MaterialPropertyBlock color;
        void Awake(){game=GetComponentInParent<ShieldExerciseGame>();trackingPopup=ScoreText("Perfect flow bonus",.008f,new Color(1,.86f,.4f));totalScore=ScoreText("Session points",.022f,new Color(.65f,.78f,.85f));trackingPopup.gameObject.SetActive(false);totalScore.gameObject.SetActive(false);
            trackingDings=new AudioClip[5];for(int i=0;i<5;i++){int n=6615;var clip=AudioClip.Create("Perfect flow ding",n,1,22050,false);var samples=new float[n];for(int j=0;j<n;j++)samples[j]=Bell(j/22050f,StreakHz[i])*.55f;clip.SetData(samples,0);trackingDings[i]=clip;}
            color=new MaterialPropertyBlock();sound=gameObject.AddComponent<AudioSource>();sound.spatialBlend=0;sound.volume=.18f;captureTone=Tone(660,.16f);blockTone=Tone(330,.12f);rewardTone=Tone(880,.28f);multiplierChime=Chime();streakNotes=new AudioClip[StreakHz.Length];for(int i=0;i<StreakHz.Length;i++)streakNotes[i]=Tone(StreakHz[i],.28f);
            // A filter acts on every source on its GameObject, so the line tone gets a child of its own to keep the one-shots unfiltered.
            var line=new GameObject("Line Tone");line.transform.SetParent(transform,false);lineClip=LineClip();lineSource=line.AddComponent<AudioSource>();lineSource.playOnAwake=false;lineSource.loop=true;lineSource.spatialBlend=0;lineSource.volume=0;lineSource.clip=lineClip;lineFilter=line.AddComponent<AudioLowPassFilter>();}
        static AudioClip Tone(float frequency,float seconds){int n=(int)(22050*seconds);var clip=AudioClip.Create("Shield cue",n,1,22050,false);var data=new float[n];for(int i=0;i<n;i++){float u=i/(float)n;data[i]=Mathf.Sin(2*Mathf.PI*frequency*i/22050)*Mathf.Sin(Mathf.PI*u)*.35f;}clip.SetData(data,0);return clip;}
        // Lead-in silence bakes the delay into the clip: the streak note sounds first and the chime follows without a timer.
        static AudioClip Chime(){int n=(int)(22050*(ChimeDelay+.5f));var clip=AudioClip.Create("Multiplier chime",n,1,22050,false);var data=new float[n];for(int i=0;i<n;i++){float t=i/22050f-ChimeDelay;data[i]=(Bell(t,1318.51f)+Bell(t-.09f,1760))*.28f;}clip.SetData(data,0);return clip;}
        static float Bell(float t,float frequency)=>t<0?0:Mathf.Sin(2*Mathf.PI*frequency*t)*Mathf.Min(1,t/.004f)*Mathf.Exp(-t*12);
        // Whole number of cycles in the one second clip, so the loop point does not click. Overtones give the low-pass something to take away.
        static AudioClip LineClip(){var clip=AudioClip.Create("Line tone",22050,1,22050,false);var data=new float[22050];for(int i=0;i<22050;i++){float w=2*Mathf.PI*LineHz*i/22050;data[i]=(Mathf.Sin(w)+.4f*Mathf.Sin(2*w)+.2f*Mathf.Sin(3*w)+.1f*Mathf.Sin(4*w))/1.7f;}clip.SetData(data,0);return clip;}
        // Called every frame by the game; the source only runs while audible, and starts and stops at zero volume so neither clicks.
        public void LineTone(bool on,float quality)
        {
            if(!on&&lineFade<=0)return;
            float step=Time.unscaledDeltaTime/LineFadeSeconds;lineFade=Mathf.MoveTowards(lineFade,on?1:0,step);lineQuality=Mathf.MoveTowards(lineQuality,quality,step);
            lineSource.volume=lineFade*LineLevel(lineQuality);lineFilter.cutoffFrequency=LineCutoff(lineQuality);
            if(lineFade<=0)lineSource.Stop();else if(!lineSource.isPlaying)lineSource.Play();
        }
        public void StopLineTone(){lineFade=0;if(lineSource){lineSource.volume=0;lineSource.Stop();}}
        void OnDisable(){StopLineTone();TrackingBonus.Reset();}
        void OnApplicationPause(bool pause){if(pause)StopLineTone();}
        void OnDestroy(){if(trackingDings!=null)foreach(var clip in trackingDings)Destroy(clip);Destroy(captureTone);Destroy(blockTone);Destroy(rewardTone);Destroy(multiplierChime);Destroy(lineClip);if(streakNotes!=null)foreach(var note in streakNotes)Destroy(note);}
        public void ResetMission(){TrackingBonus.Reset();TrackingBonusPoints=0;trackingPulse=scorePulse=completionTime=displayedPoints=0;Points=Combo=BestCombo=0;LastLabel="";System.Array.Clear(charges,0,charges.Length);burstTime=0;UpdateCells();}
        public void Captured(){sound.PlayOneShot(captureTone);}
        public void Block(Vector3 point){sound.PlayOneShot(blockTone);burst.position=point;burstTime=.5f;}
        public void Reward(int sector,bool complete,bool perfect,float quality)
        {
            scorePulse=1;completionTime=complete?1.4f:0;
            if(completionLabel)completionLabel.text=perfect?"PERFECT":"NICE";
            int multiplierBefore=Multiplier;Points+=Mathf.RoundToInt(1000*quality);LastLabel=Label(complete,perfect);
            if(complete){charges[sector]++;if(!perfect)sound.PlayOneShot(rewardTone);}
            if(perfect){Combo++;BestCombo=Mathf.Max(BestCombo,Combo);Points+=Mathf.RoundToInt(1000*quality)*Mathf.Min(Combo-1,4);sound.PlayOneShot(streakNotes[StreakNote(Combo)]);if(Multiplier>multiplierBefore)sound.PlayOneShot(multiplierChime);}else Combo=0;
            UpdateCells();
        }
        void UpdateCells(){for(int i=0;i<cells.Length;i++){color.SetColor("_BaseColor",charges[i]>0?Color.Lerp(new Color(.15f,.5f,.7f),new Color(.1f,1,.6f),Mathf.Clamp01(charges[i]/(float)Mathf.Max(1,goals[i]))):new Color(.08f,.12f,.2f));cells[i].SetPropertyBlock(color);}}
        public void Show(bool active,int sector,int repetition,int totalRepetitions)
        {goals[sector]=Mathf.Max(1,totalRepetitions);core.gameObject.SetActive(active);counter.gameObject.SetActive(active);foreach(var c in cells)c.gameObject.SetActive(active);if(active)counter.text="Energy "+(charges[0]+charges[1]+charges[2]+charges[3]+charges[4]+charges[5])+"   "+Points+" points   Combo x"+Multiplier;else{drone.gameObject.SetActive(false);burst.gameObject.SetActive(false);}}
        void Update(){if(core.gameObject.activeSelf)core.Rotate(0,Time.unscaledDeltaTime*18,0,Space.Self);if(burstTime>0){burstTime-=Time.unscaledDeltaTime;burst.gameObject.SetActive(true);burst.localScale=Vector3.one*(.2f+(1-burstTime/.5f)*.5f);}else burst.gameObject.SetActive(false);}
    }
}
