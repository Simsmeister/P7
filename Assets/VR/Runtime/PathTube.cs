using UnityEngine;
namespace EmilVR
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PathTube : MonoBehaviour
    {
        const int Rings = 49, Sides = 12;
        Mesh mesh;
        readonly Vector3[] vertices = new Vector3[Rings * Sides];
        void Awake()
        {
            mesh = new Mesh { name = "Approaching path tube" }; mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var triangles = new int[(Rings - 1) * Sides * 6]; int k = 0;
            for (int r=0;r<Rings-1;r++) for(int s=0;s<Sides;s++)
            { int a=r*Sides+s,b=r*Sides+(s+1)%Sides,c=a+Sides,d=b+Sides;
                triangles[k++]=a; triangles[k++]=c; triangles[k++]=b; triangles[k++]=b; triangles[k++]=c; triangles[k++]=d; }
            mesh.vertices = vertices; mesh.triangles = triangles;
        }
        public void Draw(WandPathGame game, int side, float time)
        {
            for(int r=0;r<Rings;r++)
            {
                float u=r/(float)(Rings-1);
                game.Target(side,time+u*3f,out Vector3 p,out Quaternion q);
                Vector3 centre=p+q*Vector3.forward*game.wandLength+game.FrameRotation*Vector3.forward*(u*1.25f);
                for(int s=0;s<Sides;s++)
                { float a=s*Mathf.PI*2/Sides; vertices[r*Sides+s]=transform.InverseTransformPoint(centre+game.FrameRotation*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*Mathf.Min(.065f,game.tipTolerance)); }
            }
            mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        void OnDestroy() { if(mesh) Destroy(mesh); }
    }
}
