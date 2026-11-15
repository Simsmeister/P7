using UnityEngine;
using UnityEngine.InputSystem;

namespace P7
{
    // Moves the controller models to the tracked controllers and hides them when tracking is lost.
    public class ControllerVisuals : MonoBehaviour
    {
        public Transform leftController, rightController;
        public GameObject leftControllerVisual, rightControllerVisual;

        InputActionMap actions;

        void Awake()
        {
            actions = new InputActionMap("P7 Controllers");
            foreach (string side in new[] { "Left", "Right" })
            {
                string path = "<XRController>{" + side + "Hand}/";
                actions.AddAction(side + "Position", InputActionType.Value, path + "devicePosition", expectedControlLayout: "Vector3");
                actions.AddAction(side + "Rotation", InputActionType.Value, path + "deviceRotation", expectedControlLayout: "Quaternion");
                actions.AddAction(side + "Tracked", InputActionType.Value, path + "isTracked", expectedControlLayout: "Button");
            }
        }

        void OnEnable()
        {
            actions.Enable();
            // Updating again just before rendering gives the newest pose and less lag.
            Application.onBeforeRender += Refresh;
        }

        void OnDisable()
        {
            actions.Disable();
            Application.onBeforeRender -= Refresh;
        }

        void OnDestroy() => actions.Dispose();

        void Update() => Refresh();

        void Refresh()
        {
            UpdateSide("Left", leftController, leftControllerVisual);
            UpdateSide("Right", rightController, rightControllerVisual);
        }

        void UpdateSide(string side, Transform controller, GameObject visual)
        {
            bool tracked = actions[side + "Tracked"].IsPressed();
            if (visual.activeSelf != tracked)
                visual.SetActive(tracked);
            if (!tracked)
                return;

            Vector3 position = actions[side + "Position"].ReadValue<Vector3>();
            Quaternion rotation = actions[side + "Rotation"].ReadValue<Quaternion>();
            controller.SetLocalPositionAndRotation(position, rotation);
        }
    }
}
