using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using EmilVR;

public static class EmilVRSetup
{
    const string Root = "Assets/VR/";
    [MenuItem("Tools/Emil VR/Configure Quest 3")]
    public static void Setup()
    {
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/Emil.unity")
            throw new System.InvalidOperationException("Open Emil.unity before configuring its VR rig.");
        ConfigureXR();
        if (GameObject.Find("Emil VR Rig") == null) CreateRig();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != "Assets/Scenes/Emil.unity").ToList();
        scenes.Insert(0, new EditorBuildSettingsScene("Assets/Scenes/Emil.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        File.WriteAllText(Root + "SetupComplete.txt", "Quest 3 setup created in Emil. See README.md.\n");
        Debug.Log("EMIL_VR_SETUP_COMPLETE");
    }
    static void ConfigureXR()
    {
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget all);
        if (all == null)
        {
            all = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(all, Root + "XRGeneralSettings.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, all, true);
        }
        var group = BuildTargetGroup.Android;
        if (!all.HasManagerSettingsForBuildTarget(group)) all.CreateDefaultManagerSettingsForBuildTarget(group);
        var general = all.SettingsForBuildTarget(group);
        general.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (settings == null) throw new System.Exception("OpenXR settings were not created.");
        foreach (var feature in settings.GetFeatures<OpenXRFeature>())
        {
            string name = feature.GetType().Name;
            if (name == "MetaQuestFeature" || name == "OculusTouchControllerProfile" || name == "MetaQuestTouchPlusControllerProfile" || name == "HandTracking" || name == "MetaHandTrackingAim")
            { feature.enabled = true; EditorUtility.SetDirty(feature); }
        }
        settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(general);
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
        PlayerSettings.colorSpace = ColorSpace.Linear;
    }
    static GameObject Child(string name, Transform parent) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
    static GameObject Save(GameObject go, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/" + name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }
    static void CreateRig()
    {
        var rig = new GameObject("Emil VR Rig");
        var origin = rig.AddComponent<XROrigin>();
        var offset = Child("Camera Offset", rig.transform);
        origin.CameraFloorOffsetObject = offset;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        origin.CameraYOffset = 1.65f;
        var cameraObject = Child("VR Camera", offset.transform);
        cameraObject.tag = "MainCamera";
        var cam = cameraObject.AddComponent<Camera>(); cam.nearClipPlane = .05f;
        cameraObject.AddComponent<AudioListener>(); origin.Camera = cam;
        var pose = cameraObject.AddComponent<TrackedPoseDriver>();
        pose.positionInput = new InputActionProperty(new InputAction("Head Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        pose.rotationInput = new InputActionProperty(new InputAction("Head Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        pose.trackingStateInput = new InputActionProperty(new InputAction("Head Tracking", binding: "<XRHMD>/trackingState", expectedControlType: "Integer"));
        var input = rig.AddComponent<EmilVRInput>();
        input.leftTrackedHand = Hand("Left", offset.transform);
        input.rightTrackedHand = Hand("Right", offset.transform);
        input.leftController = Controller("Left", offset.transform);
        input.rightController = Controller("Right", offset.transform);
        input.leftControllerVisual = input.leftController.GetChild(0).gameObject;
        input.rightControllerVisual = input.rightController.GetChild(0).gameObject;
        var oldCamera = GameObject.Find("Main Camera");
        if (oldCamera != null) { Undo.RecordObject(oldCamera, "Disable old camera"); oldCamera.SetActive(false); }
        var prefab = Save(rig, "Emil VR Rig");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Selection.activeGameObject = instance;
        if (GameObject.Find("VR Test Floor") == null)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "VR Test Floor";
            floor.transform.position = new Vector3(0, -.05f, 0); floor.transform.localScale = new Vector3(8, .1f, 8);
            var mat = Material("Floor", new Color(.18f, .23f, .28f)); floor.GetComponent<Renderer>().sharedMaterial = mat;
            PrefabUtility.InstantiatePrefab(Save(floor, "VR Test Floor"));
        }
    }
    static GameObject Hand(string side, Transform parent)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "HandVisualizer/Prefabs/" + side + " Hand Tracking.prefab");
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(source); temp.name = side + " Tracked Hand";
        var prefab = Save(temp, side + " Tracked Hand");
        var hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        hand.transform.localPosition = Vector3.zero; hand.transform.localRotation = Quaternion.identity;
        return hand;
    }
    static Material Material(string name, Color color)
    {
        string path = Root + "Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color;
        AssetDatabase.CreateAsset(mat, path); return mat;
    }
    static Transform Controller(string side, Transform parent)
    {
        var go = new GameObject(side + " Controller Hand");
        var visual = Child("Visual", go.transform);
        EmilVRControllerModels.Populate(side, visual.transform);
        var prefab = Save(go, side + " Controller Hand");
        return ((GameObject)PrefabUtility.InstantiatePrefab(prefab,parent)).transform;
    }
}
