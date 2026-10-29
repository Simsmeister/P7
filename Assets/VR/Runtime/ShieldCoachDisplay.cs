using UnityEngine;

namespace EmilVR
{
    // Reusable world-space instruction cards. The pictured body is illustrative, never measured anatomy.
    public class ShieldCoachDisplay : MonoBehaviour
    {
        public ShieldExerciseGame game;
        public GameObject handMarkerPrefab;
        class Card { public Transform root; public TextMesh title; public LineRenderer body,arm,trail,head; public Transform live; }
        Material cardMaterial;
        Card[] cards=new Card[1];TextMesh caption;Material lineMaterial;
        void Awake()
        {
            lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterial.color=Color.white;
            caption=Text("Coach instructions",transform,.011f);
            for(int i=0;i<cards.Length;i++){
                var c=new Card();c.root=new GameObject("Movement card "+i).transform;c.root.SetParent(transform,false);
                if(!cardMaterial){cardMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));cardMaterial.color=new Color(.025f,.065f,.10f);}
                var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.name="Current movement panel";Destroy(panel.GetComponent<Collider>());panel.transform.SetParent(c.root,false);panel.transform.localPosition=new Vector3(0,-.15f,.06f);panel.transform.localScale=new Vector3(.85f,.7f,.015f);panel.GetComponent<Renderer>().sharedMaterial=cardMaterial;
                c.title=Text("View label",c.root,.0075f);c.title.transform.localPosition=new Vector3(0,.12f,0);
                c.body=Line(c.root,"Illustrative body",new Color(.5f,.7f,.85f),.012f);
                c.arm=Line(c.root,"Active arm",Color.white,.021f);c.trail=Line(c.root,"Movement route",new Color(.2f,.8f,1),.008f);c.head=Line(c.root,"Head",new Color(.7f,.8f,.9f),.012f);
                var orb=Instantiate(handMarkerPrefab,c.root,false);orb.name="Your measured hand";orb.transform.localScale=Vector3.one*.035f;orb.GetComponent<Renderer>().sharedMaterial=lineMaterial;c.live=orb.transform;
                cards[i]=c;
            }
        }
        TextMesh Text(string name,Transform parent,float size){var t=new GameObject(name,typeof(TextMesh)).GetComponent<TextMesh>();t.transform.SetParent(parent,false);t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.fontSize=64;t.characterSize=size;return t;}
        LineRenderer Line(Transform parent,string name,Color color,float width){var l=new GameObject(name,typeof(LineRenderer)).GetComponent<LineRenderer>();l.transform.SetParent(parent,false);l.sharedMaterial=lineMaterial;l.useWorldSpace=false;l.widthMultiplier=width;Tint(l,color);return l;}
        static void Tint(LineRenderer l,Color color){l.startColor=l.endColor=color;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);l.SetPropertyBlock(block);}
        static void Set(LineRenderer l,params Vector3[] p){l.positionCount=p.Length;l.SetPositions(p);}
        static Vector3 Project(Vector3 p,float yaw,float scale)=>Quaternion.Euler(0,yaw,0)*p*scale;
        void Draw(Card c,ShieldExerciseProfile profile,int side,Vector3 hand,Vector3 start,Vector3 end,Vector3 pivot,float yaw,float scale,bool live)
        {
            float sign=side==0?-1:1;Vector3 shoulder=new Vector3(sign*.18f,-.25f,0),hip=new Vector3(0,-.85f,0);
            Vector3 elbow=WandSessionReplay.EstimateElbow(shoulder,hand,new Vector3(sign*.3f,-.4f,-.2f));
            Vector3 P(Vector3 v)=>Project(v,yaw,scale);
            Set(c.body,P(new Vector3(-.18f,-1.55f,0)),P(hip),P(new Vector3(.18f,-1.55f,0)),P(hip),P(new Vector3(0,-.25f,0)),P(new Vector3(-.18f,-.25f,0)),P(new Vector3(.18f,-.25f,0)));
            Set(c.arm,P(shoulder),P(elbow),P(hand));Tint(c.arm,side==0?new Color(1,.2f,.3f):new Color(.1f,1,.4f));
            var circle=new Vector3[33];for(int i=0;i<circle.Length;i++){float a=i*Mathf.PI/16;circle[i]=new Vector3(Mathf.Cos(a)*.09f,Mathf.Sin(a)*.1f,0)*scale;}Set(c.head,circle);
            var path=new Vector3[31];for(int i=0;i<path.Length;i++)path[i]=P(ShieldExerciseProfile.Path(profile.movement,start,end,pivot,i/30f));Set(c.trail,path);
            c.live.gameObject.SetActive(live);if(live)c.live.localPosition=P(Quaternion.Inverse(game.CoachRotation)*(game.LiveHand-game.CoachOrigin));
        }
        public static void Example(ShieldExerciseProfile profile,int side,float u,out Vector3 hand,out Vector3 start,out Vector3 end,out Vector3 pivot)
        {
            float sign=side==0?-1:1;pivot=new Vector3(sign*.18f,-.25f,0);
            if(profile.movement==ShieldMovement.Scaption){start=pivot+Vector3.down*.565f;end=pivot+new Vector3(sign*.457f,0,.332f);}
            else if(profile.movement==ShieldMovement.SerratusReach){start=pivot+Vector3.forward*.565f;end=start+Vector3.forward*.06f;}
            else{pivot+=Vector3.down*.25f;start=pivot+Vector3.forward*.25f;end=pivot+Quaternion.Euler(0,sign*40,0)*Vector3.forward*.25f;}
            hand=ShieldExerciseProfile.Path(profile.movement,start,end,pivot,u);
        }
        void LateUpdate()
        {
            if(!game)return;
            bool visible=game.IsMission&&!game.replay.IsActive;
            caption.gameObject.SetActive(false);
            for(int i=0;i<cards.Length;i++)cards[i].root.gameObject.SetActive(visible&&i==0);
            if(!visible)return;
            transform.SetPositionAndRotation(game.CoachOrigin+game.CoachRotation*new Vector3(0,0,2.25f),game.CoachRotation);
            var profile=game.ActiveProfile;int side=game.ActiveSide;
            Example(profile,side,game.CurrentMovementProgress,out var hand,out var start,out var end,out var pivot);
            if(game.PathDefinition(0,out _,out var calibrated,out _)){
                var inverse=Quaternion.Inverse(game.CoachRotation);
                start=inverse*(calibrated.start-game.CoachOrigin);end=inverse*(calibrated.end-game.CoachOrigin);pivot=inverse*(calibrated.pivot-game.CoachOrigin);
                hand=ShieldExerciseProfile.Path(profile.movement,start,end,pivot,game.CurrentMovementProgress);
            }
            cards[0].root.localPosition=new Vector3(0,-.82f,0);
            cards[0].title.text="CURRENT / "+(side==0?"LEFT":"RIGHT")+"\n"+profile.displayName;
            Draw(cards[0],profile,side,hand,start,end,pivot,profile.movement==ShieldMovement.SerratusReach?90:0,.22f,false);
        }
        void OnDestroy(){if(lineMaterial)Destroy(lineMaterial);if(cardMaterial)Destroy(cardMaterial);}
    }
}
