using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;
using EmilVR;

public static class EmilVRControllerModels
{
    [MenuItem("Tools/Emil VR/Update Controller Hand Models")]
    public static void UpdateModels()
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            string path = "Assets/VR/Prefabs/" + side + " Controller Hand.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Keep the root and Visual objects so existing rig references remain valid.
                var visual = root.transform.Find("Visual");
                Populate(side, visual);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VR/Prefabs/Emil VR Rig.prefab");
        var input = rig.GetComponent<EmilVRInput>();
        if (!input.leftControllerVisual || !input.rightControllerVisual)
            throw new System.Exception("Controller visual references were lost.");
        foreach (string side in new[] { "Left", "Right" })
        {
            var tracked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VR/Prefabs/" + side + " Tracked Hand.prefab");
            var controller = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VR/Prefabs/" + side + " Controller Hand.prefab");
            var a = tracked.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var b = controller.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!a || !b || a.sharedMesh != b.sharedMesh || !a.sharedMaterials.SequenceEqual(b.sharedMaterials))
                throw new System.Exception(side + " hand mesh/material mismatch");
            if (controller.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new System.Exception("Optical tracking components remain on controller hand.");
        }
        Debug.Log("EMIL_CONTROLLER_MODELS_VERIFIED: both controllers use the tracked hand meshes and materials; rig references preserved.");
    }
    public static void Populate(string side, Transform visual)
    {
        for (int i = visual.childCount - 1; i >= 0; --i)
            Object.DestroyImmediate(visual.GetChild(i).gameObject);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VR/Prefabs/" + side + " Tracked Hand.prefab");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, visual);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = side + " Hand Model";
        model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        var skeleton = model.GetComponentInChildren<XRHandSkeletonDriver>(true);
        // Optical tracking treats this transform as the wrist pose. Anchor it just
        // behind the controller grip while keeping the mesh's authored finger pose.
        skeleton.rootTransform.SetLocalPositionAndRotation(new Vector3(0, 0, -.04f), Quaternion.identity);
        foreach (var c in model.GetComponentsInChildren<XRHandMeshController>(true)) Object.DestroyImmediate(c);
        foreach (var c in model.GetComponentsInChildren<XRHandSkeletonDriver>(true)) Object.DestroyImmediate(c);
        foreach (var c in model.GetComponentsInChildren<XRHandTrackingEvents>(true)) Object.DestroyImmediate(c);
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.enabled = true;
        model.SetActive(true);
    }
}
