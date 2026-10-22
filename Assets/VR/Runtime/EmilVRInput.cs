using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.InputSystem;

namespace EmilVR
{
    public class EmilVRInput : MonoBehaviour
    {
        public enum InputMode { Automatic, Hands, Controllers }
        [Tooltip("Automatic switches to tracked hands when controllers are put down. Left controller menu cycles modes.")]
        public InputMode mode;
        public bool allowModeSwitch = true;
        public GameObject leftTrackedHand, rightTrackedHand;
        public Transform leftController, rightController;
        public GameObject leftControllerVisual, rightControllerVisual;
        readonly List<XRHandSubsystem> hands = new List<XRHandSubsystem>();
        XRHandSubsystem subsystem;
        bool menuWasDown;
        InputActionMap actions;
        void Awake()
        {
            actions = new InputActionMap("Emil VR Controllers");
            foreach (string side in new[] { "Left", "Right" })
            {
                string path = "<XRController>{" + side + "Hand}/";
                actions.AddAction(side + "Position", InputActionType.Value, path + "devicePosition", expectedControlLayout: "Vector3");
                actions.AddAction(side + "Rotation", InputActionType.Value, path + "deviceRotation", expectedControlLayout: "Quaternion");
                actions.AddAction(side + "Tracked", InputActionType.Value, path + "isTracked", expectedControlLayout: "Button");
            }
            actions.AddAction("Menu", InputActionType.Button, "<XRController>{LeftHand}/menuButton");
        }
        void OnEnable() { actions.Enable(); Application.onBeforeRender += Refresh; }
        void OnDisable() { actions.Disable(); Application.onBeforeRender -= Refresh; }
        void Update()
        {
            bool down = actions["Menu"].IsPressed();
            if (allowModeSwitch && down && !menuWasDown) CycleMode();
            menuWasDown = down;
            Refresh();
        }
        void OnDestroy() { actions?.Dispose(); }
        public void CycleMode() { mode = (InputMode)(((int)mode + 1) % 3); Debug.Log("VR input: " + mode); }
        public void SetMode(int value) { mode = (InputMode)Mathf.Clamp(value, 0, 2); }
        void Refresh()
        {
            if (subsystem == null || !subsystem.running)
            {
                SubsystemManager.GetSubsystems(hands);
                subsystem = hands.Find(s => s.running);
            }
            UpdateSide(XRNode.LeftHand, subsystem != null && subsystem.leftHand.isTracked, leftTrackedHand, leftController, leftControllerVisual);
            UpdateSide(XRNode.RightHand, subsystem != null && subsystem.rightHand.isTracked, rightTrackedHand, rightController, rightControllerVisual);
        }
        void UpdateSide(XRNode node, bool handTracked, GameObject hand, Transform controller, GameObject visual)
        {
            string side = node == XRNode.LeftHand ? "Left" : "Right";
            bool tracked = actions[side + "Tracked"].IsPressed();
            Vector3 p = actions[side + "Position"].ReadValue<Vector3>();
            Quaternion q = actions[side + "Rotation"].ReadValue<Quaternion>();
            bool showHand = mode != InputMode.Controllers && handTracked;
            // Prefer optical hands when supplied by the runtime; never display an untracked controller.
            bool showController = mode != InputMode.Hands && !showHand && tracked;
            if (hand.activeSelf != showHand) hand.SetActive(showHand);
            if (visual.activeSelf != showController) visual.SetActive(showController);
            if (showController) controller.SetLocalPositionAndRotation(p, q);
        }
    }
}
