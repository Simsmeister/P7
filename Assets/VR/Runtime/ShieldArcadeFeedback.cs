using UnityEngine;
namespace EmilVR
{
    public class ShieldArcadeFeedback : MonoBehaviour
    {
        public Transform core,drone,burst;
        public Renderer[] cells=new Renderer[6];
        public TextMesh counter;
        public int Points {get;private set;}
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
        void Awake(){color=new MaterialPropertyBlock();sound=gameObject.AddComponent<AudioSource>();sound.spatialBlend=0;sound.volume=.18f;captureTone=Tone(660,.16f);blockTone=Tone(330,.12f);rewardTone=Tone(880,.28f);multiplierChime=Chime();streakNotes=new AudioClip[StreakHz.Length];for(int i=0;i<StreakHz.Length;i++)streakNotes[i]=Tone(StreakHz[i],.28f);
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
        void OnDisable(){StopLineTone();}
        void OnApplicationPause(bool pause){if(pause)StopLineTone();}
        void OnDestroy(){Destroy(captureTone);Destroy(blockTone);Destroy(rewardTone);Destroy(multiplierChime);Destroy(lineClip);if(streakNotes!=null)foreach(var note in streakNotes)Destroy(note);}
        public void ResetMission(){Points=Combo=BestCombo=0;LastLabel="";System.Array.Clear(charges,0,charges.Length);burstTime=0;UpdateCells();}
        public void Captured(){sound.PlayOneShot(captureTone);}
        public void Block(Vector3 point){sound.PlayOneShot(blockTone);burst.position=point;burstTime=.5f;}
        public void Reward(int sector,bool complete,bool perfect,float quality)
        {
            int multiplierBefore=Multiplier;Points+=Mathf.RoundToInt(100*quality);LastLabel=Label(complete,perfect);
            if(complete){charges[sector]++;if(!perfect)sound.PlayOneShot(rewardTone);}
            if(perfect){Combo++;BestCombo=Mathf.Max(BestCombo,Combo);Points+=Mathf.RoundToInt(100*quality)*Mathf.Min(Combo-1,4);sound.PlayOneShot(streakNotes[StreakNote(Combo)]);if(Multiplier>multiplierBefore)sound.PlayOneShot(multiplierChime);}else Combo=0;
            UpdateCells();
        }
        void UpdateCells(){for(int i=0;i<cells.Length;i++){color.SetColor("_BaseColor",charges[i]>0?Color.Lerp(new Color(.15f,.5f,.7f),new Color(.1f,1,.6f),Mathf.Clamp01(charges[i]/(float)Mathf.Max(1,goals[i]))):new Color(.08f,.12f,.2f));cells[i].SetPropertyBlock(color);}}
        public void Show(bool active,int sector,int repetition,int totalRepetitions)
        {goals[sector]=Mathf.Max(1,totalRepetitions);core.gameObject.SetActive(active);counter.gameObject.SetActive(active);foreach(var c in cells)c.gameObject.SetActive(active);if(active)counter.text="Energy "+(charges[0]+charges[1]+charges[2]+charges[3]+charges[4]+charges[5])+"   "+Points+" points   Combo x"+Multiplier;else{drone.gameObject.SetActive(false);burst.gameObject.SetActive(false);}}
        void Update(){if(core.gameObject.activeSelf)core.Rotate(0,Time.unscaledDeltaTime*18,0,Space.Self);if(burstTime>0){burstTime-=Time.unscaledDeltaTime;burst.gameObject.SetActive(true);burst.localScale=Vector3.one*(.2f+(1-burstTime/.5f)*.5f);}else burst.gameObject.SetActive(false);}
    }
}
