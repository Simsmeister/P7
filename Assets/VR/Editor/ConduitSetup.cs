using UnityEngine;
using UnityEditor;
using EmilVR;
public static class ConduitSetup
{
    const string Dir="Assets/VR/Shield/";
    static Material Mat(string name,Color color,bool transparent=false)
    {
        string path=Dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}m.name=name;m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);
        if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");}return m;
    }
    static Transform Shape(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {var g=GameObject.CreatePrimitive(type);g.name=name;Object.DestroyImmediate(g.GetComponent<Collider>());g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;return g.transform;}
    static TextMesh Text(string name,Transform parent,Vector3 position,float size)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=position;var t=g.AddComponent<TextMesh>();t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.78f,.93f,1);return t;}
    static void Marker(string name,Material fill,Material rim,bool hollow)
    {
        var root=new GameObject(name);
        if(!hollow){var disc=Shape("Solid player centre",root.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.11f,.003f,.11f),fill);disc.localRotation=Quaternion.Euler(90,0,0);}
        var edge=new GameObject(hollow?"Hollow target":"White player outline",typeof(LineRenderer));edge.transform.SetParent(root.transform,false);var line=edge.GetComponent<LineRenderer>();line.sharedMaterial=rim;line.useWorldSpace=false;line.loop=true;line.widthMultiplier=hollow?.005f:.004f;line.positionCount=64;
        for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(hollow?.085f:.057f)+Vector3.back*.004f);}
        PrefabUtility.SaveAsPrefabAsset(root,Dir+name+".prefab");Object.DestroyImmediate(root);
    }
    [MenuItem("Tools/Emil VR/Build Conduit Repair")]
    public static void Build()
    {
        foreach(var pair in new[]{new[]{"Convoy Navy","Arena Navy"},new[]{"Convoy Blue","Arena Blue"},new[]{"Convoy Cyan","Arena Cyan"},new[]{"Convoy Road","Arena Floor"},new[]{"Convoy Orange","Conduit Amber"},new[]{"Convoy Shield Glass","Conduit Glass"}})
            if(AssetDatabase.LoadAssetAtPath<Material>(Dir+pair[0]+".mat")&&!AssetDatabase.LoadAssetAtPath<Material>(Dir+pair[1]+".mat"))AssetDatabase.MoveAsset(Dir+pair[0]+".mat",Dir+pair[1]+".mat");
        var navy=Mat("Arena Navy",new Color(.035f,.07f,.12f));var blue=Mat("Arena Blue",new Color(.06f,.5f,.8f));var cyan=Mat("Arena Cyan",new Color(.15f,.95f,.95f));var dark=Mat("Arena Floor",new Color(.08f,.13f,.19f));
        Mat("Reward Light",Color.white);var white=Mat("Player White",Color.white);var red=Mat("Player Red",new Color(1,.2f,.3f));var green=Mat("Player Green",new Color(.1f,1,.45f));
        Marker("Left Shield",red,white,false);Marker("Right Shield",green,white,false);Marker("Target Shield",white,white,true);
        var root=new GameObject("Conduit Repair");var repair=root.AddComponent<ConduitRepairGame>();
        var room=new GameObject("Training arena").transform;room.SetParent(root.transform,false);
        Shape("Platform",room,PrimitiveType.Cylinder,new Vector3(0,-1.62f,1.5f),new Vector3(9,.04f,9),navy);
        for(int i=0;i<7;i++)Shape("Floor route",room,PrimitiveType.Cube,new Vector3(0,-1.57f,.5f+i),new Vector3(3.4f,.008f,.012f),dark);
        for(int s=-1;s<=1;s+=2){Shape("Floor edge",room,PrimitiveType.Cube,new Vector3(s*1.8f,-1.56f,3),new Vector3(.015f,.012f,7),blue);Shape("Side pillar",room,PrimitiveType.Cube,new Vector3(s*3.1f,-.3f,5.8f),new Vector3(.3f,2.6f,.4f),dark);Shape("Pillar light",room,PrimitiveType.Cube,new Vector3(s*3.1f,-.3f,5.58f),new Vector3(.025f,2.3f,.02f),cyan);}
        repair.shipLights=new Renderer[12];for(int i=0;i<12;i++)repair.shipLights[i]=Shape("Restored ship circuit "+i,room,PrimitiveType.Cube,new Vector3(-1.65f+i*.3f,-1.52f,4.8f),new Vector3(.18f,.025f,.4f),cyan).GetComponent<Renderer>();
        var hud=new GameObject("Repair status").transform;hud.SetParent(root.transform,false);hud.localPosition=new Vector3(0,.24f,2.3f);repair.hud=hud;
        Shape("Status panel",hud,PrimitiveType.Cube,new Vector3(0,0,.02f),new Vector3(1.65f,.22f,.015f),navy);repair.status=Text("Power and battery",hud,Vector3.zero,.009f);
        var ring=new GameObject("Hold charge node",typeof(LineRenderer));ring.transform.SetParent(root.transform,false);var r=ring.GetComponent<LineRenderer>();r.useWorldSpace=false;r.loop=true;r.sharedMaterial=cyan;r.widthMultiplier=.045f;r.positionCount=48;
        for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;r.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0));}repair.chargeRing=ring.transform;
        repair.nodes=new Renderer[24];for(int i=0;i<24;i++)repair.nodes[i]=Shape("Energy node "+i,root.transform,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.035f,cyan).GetComponent<Renderer>();
        repair.repaired=new LineRenderer[119];for(int i=0;i<119;i++){var go=new GameObject("Repaired section "+i,typeof(LineRenderer));go.transform.SetParent(root.transform,false);var line=go.GetComponent<LineRenderer>();line.useWorldSpace=true;line.sharedMaterial=white;line.widthMultiplier=.018f;line.numCapVertices=3;line.positionCount=2;repair.repaired[i]=line;}
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Dir+"Conduit Repair.prefab");Object.DestroyImmediate(root);
        var contents=PrefabUtility.LoadPrefabContents(Dir+"Shield Exercise Game.prefab");
        try{var game=contents.GetComponent<ShieldExerciseGame>();foreach(var old in contents.GetComponentsInChildren<ConduitRepairGame>(true))Object.DestroyImmediate(old.gameObject);game.conduit=((GameObject)PrefabUtility.InstantiatePrefab(prefab,contents.transform)).GetComponent<ConduitRepairGame>();game.conduit.game=game;
            var panel=game.board.Find("Panel");if(panel){panel.localScale=new Vector3(2.25f,.48f,.015f);panel.GetComponent<Renderer>().sharedMaterial=navy;}
            PrefabUtility.RecordPrefabInstancePropertyModifications(game.conduit);PrefabUtility.SaveAsPrefabAsset(contents,Dir+"Shield Exercise Game.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(contents);}
        ExerciseMenuSetup.Build();Verify();AssetDatabase.SaveAssets();
    }
    public static void Verify()
    {
        var good=new RepairRun{target=18};for(int i=0;i<18;i++)good.Resolve(1);if(good.outcome!="victory"||good.battery!=100)throw new System.Exception("Accurate repairs restore power");
        var bad=new RepairRun{target=1};for(int i=0;i<100&&!bad.Finished;i++)bad.Resolve(.25f);if(bad.outcome!="defeat"||bad.power!=0)throw new System.Exception("Stationary input must fail even a short session");
        var medium=new RepairRun{target=18};for(int i=0;i<18;i++)medium.Resolve(.7f);if(medium.Finished)throw new System.Exception("Continue beyond initial list");for(int i=0;i<100&&!medium.Finished;i++)medium.Resolve(.7f);if(medium.outcome!="victory")throw new System.Exception("Moderate repairs must succeed");
        int count=good.movements;good.Resolve(0);if(good.movements!=count)throw new System.Exception("Terminal outcome immutable");
        if(ConduitRepairGame.NodeRepaired(.2f)||!ConduitRepairGame.NodeRepaired(.9f))throw new System.Exception("Node quality feedback");
        Debug.Log("CONDUIT_CHECKS_PASSED victory defeat extension stationary terminal nodes");
    }
}
