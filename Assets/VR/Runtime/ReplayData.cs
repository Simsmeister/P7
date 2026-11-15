using System;
using System.Collections.Generic;
using UnityEngine;

namespace P7
{
    // One recorded frame. Saved in replay.json, so the field names must not change.
    [Serializable]
    public class ReplayFrame
    {
        public string exerciseId, phase;
        public int activeSide;
        // Only used by the exercise demo, to push the shoulder forward during a reach.
        public float illustrativeShoulderOffset;
        public float time;
        public bool headValid, leftValid, rightValid;
        public Vector3 head, left, right, targetLeft, targetRight;
        public Quaternion headRotation, leftRotation, rightRotation, targetLeftRotation, targetRightRotation;
    }

    [Serializable]
    public class ReplayData
    {
        public string equipment = "shield";
        public string inputSource = "XR headset and controllers";
        public string schemaVersion = "1";
        public string bodySource = "estimated mannequin - elbows, torso and legs not measured";
        public Vector3 origin;
        public Quaternion rotation;
        public List<ReplayFrame> frames = new List<ReplayFrame>();
    }
}
