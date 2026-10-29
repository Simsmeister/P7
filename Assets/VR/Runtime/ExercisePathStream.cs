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
        void Awake(){tint=new MaterialPropertyBlock();}
        public static Vector3 Approach(Vector3 point,Vector3 forward,float timeUntilArrival,float speed)=>point+forward*(timeUntilArrival*speed);
        void LateUpdate()
        {
            bool visible=game&&game.PathsVisible&&!game.replay.IsActive;
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
                starts[n].GetComponent<Renderer>().SetPropertyBlock(tint);ends[n].GetComponent<Renderer>().SetPropertyBlock(tint);
            }
        }
    }
}
