using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EmilVR;

public static class WandReplaySetup
{
    const string Root="Assets/VR/Prototype/";
    static Material Mat(string name,Color color,bool transparent=false)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,Root+name+".mat");}
        m.SetColor("_BaseColor",color);m.SetFloat("_Cull",0);
        if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;}
        EditorUtility.SetDirty(m);return m;
    }
    static Transform Part(string name,Transform parent,PrimitiveType shape,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);go.transform.localScale=size*.65f;Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
    }
    [MenuItem("Tools/Emil VR/Set Up Session Replay")]
    public static void Build()
    {
        var game=Object.FindFirstObjectByType<WandPathGame>();if(!game)throw new System.Exception("Open Emil with the prototype.");if(game.replay)return;
        var root=new GameObject("Session Replay");var replay=root.AddComponent<WandSessionReplay>();var figure=new GameObject("Estimated Body and Recorded Devices");figure.transform.SetParent(root.transform,false);replay.figure=figure.transform;
        var body=Mat("Estimated Body",new Color(.24f,.34f,.43f));var joint=Mat("Recorded Head",new Color(.65f,.8f,.9f));var cyan=Mat("Head Target",new Color(.2f,.8f,1,.25f),true);var yellow=Mat("Estimated Elbow Guide",new Color(1,.8f,.1f,.2f),true);
        replay.headModel=Part("Recorded Head",figure.transform,PrimitiveType.Sphere,new Vector3(.18f,.23f,.2f),joint);
        var visor=Part("Facing Direction",replay.headModel,PrimitiveType.Cube,new Vector3(.9f,.4f,.2f),Mat("Visor",new Color(.025f,.045f,.07f)));visor.localPosition=new Vector3(0,0,.48f);
        replay.chest=Part("ESTIMATED Torso",figure.transform,PrimitiveType.Capsule,new Vector3(.36f,.27f,.22f),body);
        replay.pelvis=Part("ESTIMATED Pelvis",figure.transform,PrimitiveType.Sphere,new Vector3(.30f,.20f,.20f),body);
        replay.targetHead=Part("Neutral Head Target",figure.transform,PrimitiveType.Sphere,Vector3.one*.23f,cyan);
        for(int side=0;side<2;side++)
        {
            string label=side==0?"Left Red":"Right Green";var solid=AssetDatabase.LoadAssetAtPath<Material>(Root+label+".mat");var ghost=AssetDatabase.LoadAssetAtPath<Material>(Root+(side==0?"Left Ghost":"Right Ghost")+".mat");
            replay.hands[side]=Part(label+" Recorded Hand",figure.transform,PrimitiveType.Sphere,Vector3.one*.065f,solid);
            replay.wands[side]=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+label+" Wand.prefab"),figure.transform)).transform;replay.wands[side].localScale=Vector3.one*.65f;
            replay.upperArms[side]=Part(label+" ESTIMATED Upper Arm",figure.transform,PrimitiveType.Cylinder,Vector3.one,body);
            replay.forearms[side]=Part(label+" ESTIMATED Forearm",figure.transform,PrimitiveType.Cylinder,Vector3.one,body);
            replay.thighs[side]=Part(label+" ESTIMATED Thigh",figure.transform,PrimitiveType.Cylinder,Vector3.one,body);
            replay.shins[side]=Part(label+" ESTIMATED Shin",figure.transform,PrimitiveType.Cylinder,Vector3.one,body);
            replay.feet[side]=Part(label+" ESTIMATED Foot",figure.transform,PrimitiveType.Cube,new Vector3(.11f,.05f,.20f),body);
            replay.targetHands[side]=Part(label+" Target Hand",figure.transform,PrimitiveType.Sphere,Vector3.one*.12f,ghost);
            replay.targetTips[side]=Part(label+" Target Tip",figure.transform,PrimitiveType.Sphere,Vector3.one*.09f,ghost);
            replay.targetElbows[side]=Part(label+" ESTIMATED Target Elbow",figure.transform,PrimitiveType.Sphere,Vector3.one*.10f,yellow);
        }
        var labelObject=new GameObject("Replay Explanation");labelObject.transform.SetParent(figure.transform,false);replay.label=labelObject.AddComponent<TextMesh>();replay.label.anchor=TextAnchor.MiddleCenter;replay.label.alignment=TextAlignment.Center;replay.label.characterSize=.011f;replay.label.fontSize=64;replay.label.color=Color.white;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Session Replay.prefab");Object.DestroyImmediate(root);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);game.replay=instance.GetComponent<WandSessionReplay>();game.replay.viewer=game.head;
        PrefabUtility.RecordPrefabInstancePropertyModifications(game);PrefabUtility.RecordPrefabInstancePropertyModifications(game.replay);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(game.gameObject.scene);Debug.Log("WAND_REPLAY_READY");
    }
    [MenuItem("Tools/Emil VR/Load Replay File in Play Mode")]
    static void ImportReplay()
    {
        if(!EditorApplication.isPlaying){Debug.LogWarning("Enter Play Mode first.");return;}
        string file=EditorUtility.OpenFilePanel("Select exported replay.json","","json");if(string.IsNullOrEmpty(file))return;
        var replay=Object.FindFirstObjectByType<WandSessionReplay>();if(!replay.LoadRecording(file)||!replay.Open())throw new System.Exception("Invalid replay file.");
    }
    [MenuItem("Tools/Emil VR/Test Prototype Replay")]
    static void TestReplay()
    {
        var replay=Object.FindFirstObjectByType<WandSessionReplay>();if(!EditorApplication.isPlaying)throw new System.Exception("Run the desktop test session first.");
        var files=System.IO.Directory.GetFiles("/tmp/emil-wand-prototype-test","replay.json",System.IO.SearchOption.AllDirectories);System.Array.Sort(files);
        if(files.Length==0||!replay.LoadRecording(files[files.Length-1])||!replay.Open())throw new System.Exception("Saved replay round-trip failed.");
        Debug.Log("WAND_REPLAY_DISK_ROUNDTRIP_PASSED frames="+replay.Data.frames.Count);
    }
}
