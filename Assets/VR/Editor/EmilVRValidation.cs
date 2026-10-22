using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

public static class EmilVRValidation
{
    [MenuItem("Tools/Emil VR/Validate Quest Setup")]
    static void Validate()
    {
        if (!File.Exists("Assets/VR/SetupComplete.txt")) return;
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) { Debug.LogWarning("Switch to Quest Android before validation."); return; }
        EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
        var issues = new List<OpenXRFeature.ValidationRule>();
        OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
        foreach (var issue in issues)
            Debug.LogWarning("EMIL_VR_VALIDATION " + (issue.error ? "ERROR: " : "WARNING: ") + issue.message);
        File.WriteAllText("Assets/VR/Validation.txt", string.Join("\n", issues.ConvertAll(i => (i.error ? "ERROR: " : "WARNING: ") + i.message)));
        Debug.Log("EMIL_VR_VALIDATION_COUNT " + issues.Count);
    }
}
