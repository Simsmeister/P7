using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EmilVR;
using System.IO;
public static class ShieldExerciseSetup
{
    const string Root="Assets/VR/Shield/";
    static Material Mat(string name,Color color)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,Root+name+".mat");}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Cull",0);
        if(color.a<1){mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;}
        EditorUtility.SetDirty(mat);return mat;
    }
    static GameObject Shield(string name,Material material)
    {
        var root=new GameObject(name);var disc=GameObject.CreatePrimitive(PrimitiveType.Cylinder);disc.name="Circular Shield";disc.transform.SetParent(root.transform,false);disc.transform.localScale=new Vector3(.28f,.008f,.28f);disc.transform.localRotation=Quaternion.Euler(90,0,0);Object.DestroyImmediate(disc.GetComponent<Collider>());disc.GetComponent<Renderer>().sharedMaterial=material;
        var ring=new GameObject("Shield Rim",typeof(LineRenderer));ring.transform.SetParent(root.transform,false);var line=ring.GetComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.008f;line.positionCount=64;for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.14f,Mathf.Sin(a)*.14f,.02f));}
        var asset=PrefabUtility.SaveAsPrefabAsset(root,Root+name+".prefab");Object.DestroyImmediate(root);return asset;
    }
    [MenuItem("Tools/Emil VR/Set Up Shield Exercises")]
    public static void Build()
    {
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        var red=Shield("Left Shield",Mat("Red Shield",new Color(1,.15f,.2f,.65f)));var green=Shield("Right Shield",Mat("Green Shield",new Color(.1f,1,.4f,.65f)));var ghost=Shield("Target Shield",Mat("Target Shield",new Color(.2f,.75f,1,.25f)));
        string[] ids={"scaption","serratus-reach","external-rotation"},names={"SCAPTION","SERRATUS REACH","EXTERNAL ROTATION"},instructions={"Lift diagonally outward, slightly forward of your side.\nKeep elbow comfortably straight; lower slowly.","Arm forward at shoulder height, elbow straight.\nReach a little farther through shoulder blade; hold and return.","Elbow bent beside ribs. Keep upper arm beside body.\nTurn forearm outward, hold, then return slowly."};
        var profiles=new ShieldExerciseProfile[3];for(int i=0;i<3;i++){string path=Root+ids[i]+".asset";var p=AssetDatabase.LoadAssetAtPath<ShieldExerciseProfile>(path);if(!p){p=ScriptableObject.CreateInstance<ShieldExerciseProfile>();AssetDatabase.CreateAsset(p,path);}p.exerciseId=ids[i];p.displayName=names[i];p.instruction=instructions[i];p.movement=(ShieldMovement)i;p.minimumTravel=i==1?.015f:.06f;profiles[i]=p;EditorUtility.SetDirty(p);}
        string replayPath="Assets/VR/Prototype/Session Replay.prefab";var contents=PrefabUtility.LoadPrefabContents(replayPath);
        try{var r=contents.GetComponent<WandSessionReplay>();for(int s=0;s<2;s++)if(!r.replayShields[s]){r.replayShields[s]=((GameObject)PrefabUtility.InstantiatePrefab(s==0?red:green,r.figure)).transform;r.replayShields[s].localScale=Vector3.one*.65f;}PrefabUtility.SaveAsPrefabAsset(contents,replayPath);}finally{PrefabUtility.UnloadPrefabContents(contents);}
        EditorSceneManager.OpenScene("Assets/Scenes/Emil.unity");
        var existing=Object.FindFirstObjectByType<ShieldExerciseGame>();if(existing)return;
        var old=Object.FindFirstObjectByType<WandPathGame>();var replay=Object.FindFirstObjectByType<WandSessionReplay>();var root=new GameObject("Shield Exercise Game");var game=root.AddComponent<ShieldExerciseGame>();game.exercises=profiles;
        game.shields[0]=((GameObject)PrefabUtility.InstantiatePrefab(red,root.transform)).transform;game.shields[1]=((GameObject)PrefabUtility.InstantiatePrefab(green,root.transform)).transform;game.ghost=((GameObject)PrefabUtility.InstantiatePrefab(ghost,root.transform)).transform;
        var bolt=GameObject.CreatePrimitive(PrimitiveType.Sphere);bolt.name="Energy Pulse";bolt.transform.localScale=new Vector3(.045f,.045f,.18f);Object.DestroyImmediate(bolt.GetComponent<Collider>());bolt.GetComponent<Renderer>().sharedMaterial=Mat("Energy Pulse",new Color(1,.8f,.1f));var boltPrefab=PrefabUtility.SaveAsPrefabAsset(bolt,Root+"Energy Pulse.prefab");Object.DestroyImmediate(bolt);game.projectile=((GameObject)PrefabUtility.InstantiatePrefab(boltPrefab,root.transform)).transform;
        game.board=Object.Instantiate(old.board,root.transform);game.board.name="Shield Instructions";game.display=game.board.GetComponentInChildren<TextMesh>();game.display.characterSize=.011f;
        var asset=PrefabUtility.SaveAsPrefabAsset(root,Root+"Shield Exercise Game.prefab");Object.DestroyImmediate(root);game=((GameObject)PrefabUtility.InstantiatePrefab(asset)).GetComponent<ShieldExerciseGame>();game.head=old.head;game.trackingSpace=old.trackingSpace;game.replay=replay;PrefabUtility.RecordPrefabInstancePropertyModifications(game);
        old.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(game.gameObject.scene);Debug.Log("SHIELD_SETUP_READY");
    }
    public static void Verify()
    {
        foreach(ShieldMovement m in System.Enum.GetValues(typeof(ShieldMovement))){var a=new Vector3(.2f,0,.3f);var b=new Vector3(.4f,.3f,.1f);if(Vector3.Distance(ShieldExerciseProfile.Path(m,a,b,Vector3.zero,0),a)>.0001f||Vector3.Distance(ShieldExerciseProfile.Path(m,a,b,Vector3.zero,1),b)>.0001f)throw new System.Exception("Path endpoints");}
        var p=ScriptableObject.CreateInstance<ShieldExerciseProfile>();if(p.Quality(.02f,5)!=1||p.Quality(.2f,0)!=0||p.Quality(0,50)!=0)throw new System.Exception("Quality bands");if(p.Progress(0)!=0||p.Progress(p.outwardSeconds)!=1||p.Progress(p.Cycle)!=0)throw new System.Exception("Phase motion");Object.DestroyImmediate(p);foreach(ShieldMovement m in System.Enum.GetValues(typeof(ShieldMovement)))for(int side=0;side<2;side++)for(int i=-10;i<=10;i++){
            var mapped=ShieldExerciseGame.ProjectShield(m,Vector3.zero,Vector3.up,Vector3.one*i,side);
            var anchor=ShieldExerciseGame.ProjectShield(m,Vector3.zero,Vector3.up,Vector3.zero,side);
            if(Vector3.Distance(mapped-anchor,Vector3.one*i)>.0001f)throw new System.Exception("All three axes must preserve displacement and overshoot");
        }
        if(ShieldExerciseGame.MovementScore(1,1,1)!=1||ShieldExerciseGame.MovementScore(.8f,1,.75f)<.7f||ShieldExerciseGame.MovementScore(1,0,1)>.25f)throw new System.Exception("Partial credit / stationary coverage scoring");
        if(ShieldExerciseGame.TravelCoverage(2,2)!=0||ShieldExerciseGame.TravelCoverage(-2,-2)!=0||ShieldExerciseGame.TravelCoverage(float.PositiveInfinity,float.NegativeInfinity)!=0||ShieldExerciseGame.TravelCoverage(0,1)!=1)throw new System.Exception("Observed travel range must not invent motion outside calibrated limits");
        Debug.Log("SHIELD_UNIT_TESTS_PASSED");
    }
}
