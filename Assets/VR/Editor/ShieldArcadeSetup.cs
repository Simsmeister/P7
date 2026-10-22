using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EmilVR;
using System.IO;
public static class ShieldArcadeSetup
{
    const string Root="Assets/VR/Shield/";
    static Material Mat(string name,Color color){var m=AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,Root+name+".mat");}m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);return m;}
    static Transform Part(string name,Transform parent,Vector3 position,Vector3 scale,Material mat,PrimitiveType type=PrimitiveType.Cube){var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=position;o.transform.localScale=scale;Object.DestroyImmediate(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=mat;return o.transform;}
    static Transform Ring(string name,Transform parent,Material mat,float radius){var o=new GameObject(name,typeof(LineRenderer));o.transform.SetParent(parent,false);var l=o.GetComponent<LineRenderer>();l.useWorldSpace=false;l.loop=true;l.positionCount=64;l.widthMultiplier=.012f;l.sharedMaterial=mat;for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;l.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius);}return o.transform;}
    public static void Build()
    {
        var dark=Mat("Reactor Metal",new Color(.04f,.12f,.20f));var blue=Mat("Reactor Light",new Color(.1f,.7f,1));var yellow=Mat("Drone Light",new Color(1,.7f,.1f));var green=Mat("Reward Light",new Color(.1f,1,.5f));
        var drone=new GameObject("Pulse Drone");Part("Body",drone.transform,Vector3.zero,new Vector3(.16f,.09f,.10f),dark);Part("Left wing",drone.transform,new Vector3(-.13f,0,0),new Vector3(.12f,.025f,.06f),blue);Part("Right wing",drone.transform,new Vector3(.13f,0,0),new Vector3(.12f,.025f,.06f),blue);Part("Emitter",drone.transform,new Vector3(0,0,-.07f),Vector3.one*.045f,yellow,PrimitiveType.Sphere);
        var dronePrefab=PrefabUtility.SaveAsPrefabAsset(drone,Root+"Pulse Drone.prefab");Object.DestroyImmediate(drone);
        var root=new GameObject("Reactor Rescue");var arcade=root.AddComponent<ShieldArcadeFeedback>();arcade.core=Part("Reactor Core",root.transform,new Vector3(0,-.15f,3),Vector3.one*.32f,blue,PrimitiveType.Sphere);var ring=Ring("Core Ring",arcade.core,green,.75f);ring.localRotation=Quaternion.Euler(65,0,0);
        arcade.drone=((GameObject)PrefabUtility.InstantiatePrefab(dronePrefab,root.transform)).transform;
        arcade.burst=Ring("Block Burst",root.transform,green,.4f);arcade.burst.gameObject.SetActive(false);
        for(int i=0;i<6;i++)arcade.cells[i]=Part("Sector "+(i+1),root.transform,new Vector3((i-2.5f)*.16f,.25f,2.28f),new Vector3(.12f,.045f,.025f),dark).GetComponent<Renderer>();
        var text=new GameObject("Mission Progress");text.transform.SetParent(root.transform,false);text.transform.localPosition=new Vector3(0,.35f,2.28f);arcade.counter=text.AddComponent<TextMesh>();arcade.counter.anchor=TextAnchor.MiddleCenter;arcade.counter.alignment=TextAlignment.Center;arcade.counter.characterSize=.009f;arcade.counter.fontSize=64;arcade.counter.color=Color.white;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Reactor Rescue.prefab");Object.DestroyImmediate(root);
        string gamePath=Root+"Shield Exercise Game.prefab";var contents=PrefabUtility.LoadPrefabContents(gamePath);try{var game=contents.GetComponent<ShieldExerciseGame>();if(!game.arcade)game.arcade=((GameObject)PrefabUtility.InstantiatePrefab(prefab,contents.transform)).GetComponent<ShieldArcadeFeedback>();PrefabUtility.SaveAsPrefabAsset(contents,gamePath);}finally{PrefabUtility.UnloadPrefabContents(contents);}
        AssetDatabase.SaveAssets();Debug.Log("SHIELD_ARCADE_READY");
    }
}
