using UnityEditor;
using UnityEngine;
using EmilVR;
public static class ShieldCoachSetup
{
    public static void Build()
    {
        const string markerPath="Assets/VR/Shield/Coach Hand Marker.prefab";
        var marker=AssetDatabase.LoadAssetAtPath<GameObject>(markerPath);
        if(!marker){var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="Coach Hand Marker";Object.DestroyImmediate(sphere.GetComponent<Collider>());sphere.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/VR/Prototype/Recorded Head.mat");marker=PrefabUtility.SaveAsPrefabAsset(sphere,markerPath);Object.DestroyImmediate(sphere);}
        const string path="Assets/VR/Shield/Shield Exercise Game.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try{
            var game=root.GetComponent<ShieldExerciseGame>();
            if(!game.coach){
                var assetPath="Assets/VR/Shield/Movement Coach.prefab";
                var go=new GameObject("Movement Coach",typeof(ShieldCoachDisplay));
                go.GetComponent<ShieldCoachDisplay>().handMarkerPrefab=marker;var asset=PrefabUtility.SaveAsPrefabAsset(go,assetPath);Object.DestroyImmediate(go);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);game.coach=instance.GetComponent<ShieldCoachDisplay>();game.coach.game=game;PrefabUtility.RecordPrefabInstancePropertyModifications(game.coach);
            }
            game.coach.handMarkerPrefab=marker;PrefabUtility.RecordPrefabInstancePropertyModifications(game.coach);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        var coachRoot=PrefabUtility.LoadPrefabContents("Assets/VR/Shield/Movement Coach.prefab");
        try{coachRoot.GetComponent<ShieldCoachDisplay>().handMarkerPrefab=marker;PrefabUtility.SaveAsPrefabAsset(coachRoot,"Assets/VR/Shield/Movement Coach.prefab");}finally{PrefabUtility.UnloadPrefabContents(coachRoot);}
        AssetDatabase.SaveAssets();Debug.Log("SHIELD_COACH_READY");
    }
}
