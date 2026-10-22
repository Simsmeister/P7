using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EmilVR;

public static class WandPathPrototypeSetup
{
    const string Root="Assets/VR/Prototype/";
    [MenuItem("Tools/Emil VR/Test Desktop Session")]
    static void TestDesktopSession()
    {
        if(!EditorApplication.isPlaying)throw new System.Exception("Enter Play Mode first.");
        var shield=Object.FindFirstObjectByType<ShieldExerciseGame>();if(shield){shield.TestMode=true;shield.Begin();return;}
        var game=Object.FindFirstObjectByType<WandPathGame>();game.RecordSimulationForTesting=true;game.roundSeconds=2f;game.SendMessage("Calibrate");
    }
    static Material Mat(string name,Color color,bool transparent=false)
    {
        string path=Root+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Cull",0);
        if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;}
        EditorUtility.SetDirty(m);return m;
    }
    static GameObject Part(string name,Transform parent,PrimitiveType type,Vector3 p,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
    }
    static GameObject Save(GameObject go,string name){var p=PrefabUtility.SaveAsPrefabAsset(go,Root+name+".prefab");Object.DestroyImmediate(go);return p;}
    static GameObject Wand(string name,Material shaft,Material tip)
    {
        var go=new GameObject(name);
        var stick=Part("Sword Wand",go.transform,PrimitiveType.Cylinder,new Vector3(0,0,.30f),new Vector3(.018f,.30f,.018f),shaft);stick.transform.localRotation=Quaternion.Euler(90,0,0);
        Part("Tip",go.transform,PrimitiveType.Sphere,new Vector3(0,0,.60f),Vector3.one*.035f,tip);
        Part("Handle Guide",go.transform,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.035f,shaft);
        return Save(go,name);
    }
    static GameObject Gate(string name,Material mat)
    {
        var go=new GameObject(name);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=mat;line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.004f;line.positionCount=64;
        for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.065f);}return Save(go,name);
    }
    [MenuItem("Tools/Emil VR/Build Wand Path Prototype")]
    public static void Build()
    {
        if(SceneManager.GetActiveScene().path!="Assets/Scenes/Emil.unity")throw new System.Exception("Open Emil first.");
        if(GameObject.Find("Wand Path Prototype"))return;
        var red=Mat("Left Red",new Color(1,.12f,.18f));var green=Mat("Right Green",new Color(.12f,1,.5f));var grip=Mat("Wand Grip",new Color(.6f,.7f,.8f));
        var faintRed=Mat("Left Ghost",new Color(1,.15f,.2f,.32f),true);var faintGreen=Mat("Right Ghost",new Color(.1f,1,.45f,.32f),true);
        var tubeRed=Mat("Left Tube",new Color(1,.1f,.15f,.12f),true);var tubeGreen=Mat("Right Tube",new Color(.05f,1,.4f,.12f),true);
        var root=new GameObject("Wand Path Prototype");var game=root.AddComponent<WandPathGame>();
        for(int side=0;side<2;side++)
        {
            string label=side==0?"Left Red":"Right Green";var color=side==0?red:green;var faint=side==0?faintRed:faintGreen;
            game.wands[side]=((GameObject)PrefabUtility.InstantiatePrefab(Wand(label+" Wand",grip,color),root.transform)).transform;
            game.ghosts[side]=((GameObject)PrefabUtility.InstantiatePrefab(Wand(label+" Ghost Wand",faint,faint),root.transform)).transform;
            game.gates[side]=((GameObject)PrefabUtility.InstantiatePrefab(Gate(label+" Target Ring",color),root.transform)).transform;
            var tube=new GameObject(label+" Path Tube",typeof(MeshFilter),typeof(MeshRenderer),typeof(PathTube));tube.GetComponent<MeshRenderer>().sharedMaterial=side==0?tubeRed:tubeGreen;
            game.tubes[side]=((GameObject)PrefabUtility.InstantiatePrefab(Save(tube,label+" Path Tube"),root.transform)).GetComponent<PathTube>();
        }
        var board=new GameObject("Instructions and Score");board.transform.SetParent(root.transform,false);game.board=board.transform;
        Part("Panel",board.transform,PrimitiveType.Cube,new Vector3(0,0,.02f),new Vector3(2.25f,.62f,.015f),Mat("Panel",new Color(.018f,.032f,.06f)));
        var text=new GameObject("Text");text.transform.SetParent(board.transform,false);game.display=text.AddComponent<TextMesh>();game.display.anchor=TextAnchor.MiddleCenter;game.display.alignment=TextAlignment.Center;game.display.characterSize=.012f;game.display.fontSize=64;game.display.color=new Color(.85f,.95f,1);game.display.text="WAND PATH LAB\nLeft RED / Right GREEN\nHold both triggers to calibrate.";
        board.transform.position=new Vector3(0,1.9f,1.6f);
        var prefab=Save(root,"Wand Path Prototype");var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var component=instance.GetComponent<WandPathGame>();
        var rig=Object.FindFirstObjectByType<EmilVRInput>();component.trackingSpace=rig.transform.Find("Camera Offset");component.head=component.trackingSpace.GetComponentInChildren<Camera>().transform;
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        string rigPath="Assets/VR/Prefabs/Emil VR Rig.prefab";var contents=PrefabUtility.LoadPrefabContents(rigPath);
        try {var trails=contents.GetComponentInChildren<ControllerTrackingTrails>(true);if(trails)trails.gameObject.SetActive(false);contents.transform.Find("Camera Offset").localPosition=Vector3.up*1.65f;PrefabUtility.SaveAsPrefabAsset(contents,rigPath);}finally{PrefabUtility.UnloadPrefabContents(contents);}
        var existingTrails=rig.GetComponentInChildren<ControllerTrackingTrails>(true);if(existingTrails)existingTrails.gameObject.SetActive(false);
        Selection.activeGameObject=instance;
        Verify();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Debug.Log("WAND_PATH_PROTOTYPE_READY");
    }
    [MenuItem("Tools/Emil VR/Verify Wand Scoring")]
    public static void Verify()
    {
        if(WandPathGame.Quality(0,0,0,.09f,.045f,25)!=1)throw new System.Exception("Perfect pose score");
        if(WandPathGame.Quality(.2f,0,0,.09f,.045f,25)!=0)throw new System.Exception("Tip-only shortcut must not score");
        if(WandPathGame.Quality(0,0,50,.09f,.045f,25)!=0)throw new System.Exception("Wrong pointing angle must not score");
        if(WandPathGame.Quality(.01f,.01f,2,.09f,.045f,25)<=WandPathGame.Quality(.05f,.03f,15,.09f,.045f,25))throw new System.Exception("Error score ordering");
        if(WandPathGame.Quality(.035f,.06f,10,.12f,.16f,35)!=1)throw new System.Exception("Full-credit acceptance zone");
        if(WandPathGame.Quality(.12f,0,0,.12f,.16f,35)!=0)throw new System.Exception("Hand-position gate");
        if(WandPathGame.Quality(0,.16f,0,.12f,.16f,35)!=0)throw new System.Exception("Tip-position gate");
        if(WandPathGame.Quality(0,0,35,.12f,.16f,35)!=0)throw new System.Exception("Pointing gate");
        Debug.Log("WAND_SCORING_TESTS_PASSED");
    }
}
