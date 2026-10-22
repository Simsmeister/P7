using UnityEditor;
using UnityEngine;
using EmilVR;

public static class EmilVRTrackingTrailsSetup
{
    [MenuItem("Tools/Emil VR/Set Up Tracking Trails")]
    public static void Install()
    {
        const string root = "Assets/VR/";
        var material = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Tracking Trail.mat");
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, root + "Materials/Tracking Trail.mat");
        }
        var go = new GameObject("Tracking Trail Stroke");
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 0;
        line.widthMultiplier = .006f; line.numCapVertices = 4; line.numCornerVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        var stroke = PrefabUtility.SaveAsPrefabAsset(go, root + "Prefabs/Tracking Trail Stroke.prefab");
        Object.DestroyImmediate(go);
        var trailObject = new GameObject("Controller Tracking Trails");
        var trails = trailObject.AddComponent<ControllerTrackingTrails>();
        trails.strokePrefab = stroke.GetComponent<LineRenderer>();
        var trailPrefab = PrefabUtility.SaveAsPrefabAsset(trailObject, root + "Prefabs/Controller Tracking Trails.prefab");
        Object.DestroyImmediate(trailObject);
        string rigPath = root + "Prefabs/Emil VR Rig.prefab";
        var rig = PrefabUtility.LoadPrefabContents(rigPath);
        try
        {
            var component = rig.GetComponentInChildren<ControllerTrackingTrails>(true);
            if (!component) component = ((GameObject)PrefabUtility.InstantiatePrefab(trailPrefab, rig.transform)).GetComponent<ControllerTrackingTrails>();
            component.trackingSpace = rig.transform.Find("Camera Offset");
            component.strokePrefab = stroke.GetComponent<LineRenderer>();
            if (!component.trackingSpace) throw new System.Exception("Missing tracking space");
            PrefabUtility.SaveAsPrefabAsset(rig, rigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(rig); }
        AssetDatabase.SaveAssets();
        Verify(stroke.GetComponent<LineRenderer>());
        Debug.Log("EMIL_TRACKING_TRAILS_READY_AND_TESTED");
    }
    static void Verify(LineRenderer prefab)
    {
        var go = new GameObject("Trail Verification");
        try
        {
            var trails = go.AddComponent<ControllerTrackingTrails>(); trails.strokePrefab = prefab;
            trails.RecordFrame(true, false, true, Vector3.zero, true, Vector3.right);
            trails.RecordFrame(true, false, true, Vector3.up * .1f, true, Vector3.right + Vector3.up * .1f);
            var lines = go.GetComponentsInChildren<LineRenderer>();
            Require(lines.Length == 2 && lines[0].positionCount == 2 && lines[1].positionCount == 2, "Both paths recorded");
            trails.RecordFrame(false, false, true, Vector3.up * .2f, true, Vector3.right);
            Require(lines[0].positionCount == 2, "Release stops drawing");
            trails.RecordFrame(true, false, true, Vector3.up * .3f, false, Vector3.zero);
            Require(go.GetComponentsInChildren<LineRenderer>().Length == 3, "New press creates separate stroke");
            trails.RecordFrame(true, false, false, Vector3.zero, false, Vector3.zero);
            trails.RecordFrame(true, false, true, Vector3.up * .4f, false, Vector3.zero);
            Require(go.GetComponentsInChildren<LineRenderer>().Length == 4, "Tracking loss leaves a gap");
            trails.RecordFrame(true, true, true, Vector3.zero, true, Vector3.right);
            Require(go.GetComponentsInChildren<LineRenderer>().Length == 0, "Left trigger clears and overrides drawing");
        }
        finally { Object.DestroyImmediate(go); }
    }
    static void Require(bool condition, string message) { if (!condition) throw new System.Exception("Trail verification failed: " + message); }
}
