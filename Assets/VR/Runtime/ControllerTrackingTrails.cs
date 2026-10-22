using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace EmilVR
{
    public class ControllerTrackingTrails : MonoBehaviour
    {
        [Tooltip("The XR rig's Camera Offset transform, which converts tracking poses to world space.")]
        public Transform trackingSpace;
        public LineRenderer strokePrefab;
        public Color leftColor = new Color(.15f, .75f, 1f);
        public Color rightColor = new Color(1f, .45f, .12f);
        [Min(.001f)] public float minimumPointDistance = .005f;
        [Min(2)] public int pointsPerStroke = 2048;
        [Min(2)] public int maximumStrokes = 128;
        InputActionMap actions;
        readonly Queue<LineRenderer> strokes = new Queue<LineRenderer>();
        LineRenderer leftStroke, rightStroke;
        bool clearHeld;

        void Awake()
        {
            actions = new InputActionMap("Controller Tracking Trails");
            actions.AddAction("Draw", InputActionType.Value, "<XRController>{RightHand}/trigger");
            actions.AddAction("Clear", InputActionType.Value, "<XRController>{LeftHand}/trigger");
            foreach (string side in new[] { "Left", "Right" })
            {
                string path = "<XRController>{" + side + "Hand}/";
                actions.AddAction(side + "Position", InputActionType.Value, path + "devicePosition", expectedControlLayout: "Vector3");
                actions.AddAction(side + "Tracked", InputActionType.Value, path + "isTracked", expectedControlLayout: "Button");
                actions.AddAction(side + "State", InputActionType.Value, path + "trackingState", expectedControlLayout: "Integer");
            }
        }
        void OnEnable() { actions?.Enable(); }
        void OnDisable() { actions?.Disable(); EndStrokes(); clearHeld = false; }
        void OnDestroy() { actions?.Dispose(); }
        void LateUpdate()
        {
            if (!trackingSpace || !strokePrefab) return;
            bool leftValid = PositionTracked("Left"), rightValid = PositionTracked("Right");
            RecordFrame(actions["Draw"].ReadValue<float>() > .5f,
                actions["Clear"].ReadValue<float>() > .5f,
                leftValid, trackingSpace.TransformPoint(actions["LeftPosition"].ReadValue<Vector3>()),
                rightValid, trackingSpace.TransformPoint(actions["RightPosition"].ReadValue<Vector3>()));
        }
        bool PositionTracked(string side)
        {
            var state = (InputTrackingState)actions[side + "State"].ReadValue<int>();
            return actions[side + "Tracked"].IsPressed() && (state & InputTrackingState.Position) != 0;
        }
        // Accepts world-space samples; also permits deterministic testing without a headset.
        public void RecordFrame(bool draw, bool clear, bool leftValid, Vector3 left, bool rightValid, Vector3 right)
        {
            if (clear)
            {
                if (!clearHeld) ClearTrails();
                clearHeld = true;
                return; // Erase takes priority when both triggers are held.
            }
            clearHeld = false;
            if (!draw) { EndStrokes(); return; }
            RecordSide(ref leftStroke, leftValid, left, leftColor, "Left Tracking Trail");
            RecordSide(ref rightStroke, rightValid, right, rightColor, "Right Tracking Trail");
        }
        void RecordSide(ref LineRenderer stroke, bool valid, Vector3 point, Color color, string label)
        {
            if (!valid || !float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
            { stroke = null; return; }
            if (stroke && stroke.positionCount > 0)
            {
                var previous = stroke.GetPosition(stroke.positionCount - 1);
                float distance = Vector3.Distance(previous, point);
                if (distance < minimumPointDistance) return;
                // Do not connect implausible reacquisition jumps through the room.
                if (distance > .35f) stroke = null;
                else if (stroke.positionCount >= Mathf.Max(2, pointsPerStroke))
                {
                    stroke = NewStroke(color, label);
                    stroke.positionCount = 1; stroke.SetPosition(0, previous);
                }
            }
            if (!stroke) stroke = NewStroke(color, label);
            int index = stroke.positionCount;
            stroke.positionCount = index + 1;
            stroke.SetPosition(index, point);
        }
        LineRenderer NewStroke(Color color, string label)
        {
            while (strokes.Count >= Mathf.Max(2, maximumStrokes))
            {
                var old = strokes.Dequeue();
                if (old == leftStroke) leftStroke = null;
                if (old == rightStroke) rightStroke = null;
                Remove(old);
            }
            var line = Instantiate(strokePrefab, transform);
            line.name = label; line.useWorldSpace = true; line.positionCount = 0;
            line.startColor = line.endColor = color;
            strokes.Enqueue(line);
            return line;
        }
        public void ClearTrails()
        {
            EndStrokes();
            while (strokes.Count > 0) Remove(strokes.Dequeue());
        }
        void EndStrokes() { leftStroke = rightStroke = null; }
        static void Remove(LineRenderer line)
        {
            if (!line) return;
            line.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(line.gameObject);
            else DestroyImmediate(line.gameObject);
        }
    }
}
