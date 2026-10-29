using UnityEngine;
namespace EmilVR
{
    public enum ShieldMovement { Scaption, SerratusReach, ExternalRotation }
    [CreateAssetMenu(menuName="Emil VR/Shield Exercise")]
    public class ShieldExerciseProfile : ScriptableObject
    {
        public string exerciseId,displayName;
        [TextArea] public string instruction;
        public ShieldMovement movement;
        public int repetitions=3;
        public float outwardSeconds=3,holdSeconds=1.5f,returnSeconds=3,restSeconds=1;
        public float fullCreditMetres=.04f,zeroCreditMetres=.14f,fullCreditDegrees=12,zeroCreditDegrees=40;
        public float minimumTravel=.025f;
        public float Cycle=>outwardSeconds+holdSeconds+returnSeconds+restSeconds;
        public float Progress(float time)
        {
            if(time<outwardSeconds)return Mathf.SmoothStep(0,1,time/outwardSeconds);
            if(time<outwardSeconds+holdSeconds)return 1;
            return 1-Mathf.SmoothStep(0,1,(time-outwardSeconds-holdSeconds)/returnSeconds);
        }
        public string Phase(float time)=>time<outwardSeconds?"outward":time<outwardSeconds+holdSeconds?"hold":time<outwardSeconds+holdSeconds+returnSeconds?"return":"rest";
        public static Vector3 Path(ShieldMovement movement,Vector3 start,Vector3 end,Vector3 pivot,float u)
        {
            if(movement==ShieldMovement.SerratusReach)return Vector3.Lerp(start,end,u);
            Vector3 a=start-pivot,b=end-pivot;
            if(a.magnitude<.05f||b.magnitude<.05f)return Vector3.Lerp(start,end,u);
            return pivot+Vector3.Slerp(a.normalized,b.normalized,u)*Mathf.Lerp(a.magnitude,b.magnitude,u);
        }
        // Rotation remains available in recordings but is not scored.
        public float Quality(float metres,float degrees)=>1-Mathf.InverseLerp(fullCreditMetres,zeroCreditMetres,metres);
    }
}
