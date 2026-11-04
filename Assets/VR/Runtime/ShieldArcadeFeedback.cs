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
        readonly int[] charges=new int[6],goals=new int[6];
        AudioSource sound;AudioClip captureTone,blockTone,rewardTone;
        float burstTime;
        MaterialPropertyBlock color;
        void Awake(){color=new MaterialPropertyBlock();sound=gameObject.AddComponent<AudioSource>();sound.spatialBlend=0;sound.volume=.18f;captureTone=Tone(660,.16f);blockTone=Tone(330,.12f);rewardTone=Tone(880,.28f);}
        static AudioClip Tone(float frequency,float seconds){int n=(int)(22050*seconds);var clip=AudioClip.Create("Shield cue",n,1,22050,false);var data=new float[n];for(int i=0;i<n;i++){float u=i/(float)n;data[i]=Mathf.Sin(2*Mathf.PI*frequency*i/22050)*Mathf.Sin(Mathf.PI*u)*.35f;}clip.SetData(data,0);return clip;}
        void OnDestroy(){Destroy(captureTone);Destroy(blockTone);Destroy(rewardTone);}
        public void ResetMission(){Points=Combo=BestCombo=0;LastLabel="";System.Array.Clear(charges,0,charges.Length);burstTime=0;UpdateCells();}
        public void Captured(){sound.PlayOneShot(captureTone);}
        public void Block(Vector3 point){sound.PlayOneShot(blockTone);burst.position=point;burstTime=.5f;}
        public void Reward(int sector,bool complete,bool perfect,float quality)
        {
            Points+=Mathf.RoundToInt(100*quality);LastLabel=Label(complete,perfect);
            if(complete){charges[sector]++;sound.PlayOneShot(rewardTone);}
            if(perfect){Combo++;BestCombo=Mathf.Max(BestCombo,Combo);Points+=Mathf.RoundToInt(100*quality)*Mathf.Min(Combo-1,4);}else Combo=0;
            UpdateCells();
        }
        void UpdateCells(){for(int i=0;i<cells.Length;i++){color.SetColor("_BaseColor",charges[i]>0?Color.Lerp(new Color(.15f,.5f,.7f),new Color(.1f,1,.6f),Mathf.Clamp01(charges[i]/(float)Mathf.Max(1,goals[i]))):new Color(.08f,.12f,.2f));cells[i].SetPropertyBlock(color);}}
        public void Show(bool active,int sector,int repetition,int totalRepetitions)
        {goals[sector]=Mathf.Max(1,totalRepetitions);core.gameObject.SetActive(active);counter.gameObject.SetActive(active);foreach(var c in cells)c.gameObject.SetActive(active);if(active)counter.text="Energy "+(charges[0]+charges[1]+charges[2]+charges[3]+charges[4]+charges[5])+"   "+Points+" points   Combo x"+Multiplier;else{drone.gameObject.SetActive(false);burst.gameObject.SetActive(false);}}
        void Update(){if(core.gameObject.activeSelf)core.Rotate(0,Time.unscaledDeltaTime*18,0,Space.Self);if(burstTime>0){burstTime-=Time.unscaledDeltaTime;burst.gameObject.SetActive(true);burst.localScale=Vector3.one*(.2f+(1-burstTime/.5f)*.5f);}else burst.gameObject.SetActive(false);}
    }
}
