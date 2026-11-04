using UnityEngine;
namespace EmilVR
{
    // Each finite exercise is a separate ribbon. Time-to-arrival adds depth, while the
    // exercise's own depth remains intact. At arrival its point equals the scored target.
    public class ExercisePathStream : MonoBehaviour
    {
        public ShieldExerciseGame game;
        public LineRenderer[] paths=new LineRenderer[2];
        public Transform[] starts=new Transform[2],ends=new Transform[2];
        public float approachSpeed=.55f;
        const int Samples=180;
        readonly Vector3[] points=new Vector3[Samples];
        MaterialPropertyBlock tint;
        // Dim bridge from the current path's end to the next path's start, so the stream reads as one line. Added at runtime; the prefab is generated.
        LineRenderer bridge;
        void Awake(){tint=new MaterialPropertyBlock();bridge=new GameObject("Path Bridge").AddComponent<LineRenderer>();bridge.transform.SetParent(transform,false);bridge.sharedMaterial=paths[0].sharedMaterial;bridge.widthMultiplier=paths[0].widthMultiplier*.5f;bridge.positionCount=2;bridge.gameObject.SetActive(false);}
        public static Vector3 Approach(Vector3 point,Vector3 forward,float timeUntilArrival,float speed)=>point+forward*(timeUntilArrival*speed);
        void LateUpdate()
        {
            bool visible=game&&game.PathsVisible&&!game.replay.IsActive;
            int shown=0;
            for(int n=0;n<paths.Length;n++){
                bool show=visible&&game.PathDefinition(n,out _,out _,out _);
                paths[n].gameObject.SetActive(show);starts[n].gameObject.SetActive(false);ends[n].gameObject.SetActive(show);
                if(!show)continue;
                game.PathDefinition(n,out var profile,out var calibration,out int side);
                float offset=n==0?0:game.ActiveProfile.Cycle+game.exerciseGapSeconds;
                float first=n==0?Mathf.Clamp(game.PathClock,0,profile.Cycle):0;
                for(int i=0;i<Samples;i++){
                    float t=Mathf.Lerp(first,profile.Cycle,i/(float)(Samples-1));
                    points[i]=Approach(game.PathVisualPoint(profile,calibration,side,t),game.CoachRotation*Vector3.forward,offset+t-game.PathClock,approachSpeed);
                }
                paths[n].positionCount=Samples;paths[n].SetPositions(points);
                var color=side==0?new Color(1,.2f,.3f):new Color(.1f,1,.45f);
                color*=n>0?.30f:.55f;color.a=1;tint.SetColor("_BaseColor",color);paths[n].SetPropertyBlock(tint);
                starts[n].position=points[0];ends[n].position=points[Samples-1];
                if(n==0){bridge.SetPosition(0,points[Samples-1]);shown++;}else if(shown==1){bridge.SetPosition(1,points[0]);tint.SetColor("_BaseColor",color*.5f);bridge.SetPropertyBlock(tint);tint.SetColor("_BaseColor",color);shown++;}
                starts[n].GetComponent<Renderer>().SetPropertyBlock(tint);ends[n].GetComponent<Renderer>().SetPropertyBlock(tint);
            }
            bridge.gameObject.SetActive(shown==2);
        }
    }
}
