using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmilVR
{
    // Local, deterministic observe -> decide -> adapt policy. No cloud model or clinical inference.
    public class AdaptiveExerciseCoach : MonoBehaviour
    {
        public bool adaptationEnabled = true;
        ExercisePlayerSettings settings;
        bool developerMode;
        public void Configure(ExercisePlayerSettings value){ResetSession();settings=value;developerMode=value!=null&&value.devMode;}
        [Range(.25f, 1f)] public float minimumRangeScale = .4f;
        [Range(.75f, 1f)] public float fastestDurationScale = .85f;
        [Range(1f, 2f)] public float slowestDurationScale = 2f;
        [Serializable] public class Decision
        {
            public string policy = "position-aware-range-tempo-v5", exerciseId, side, reason;
            public int repetition;
            public bool enabled, developerMode;
            public float maximumSpeedIncreasePercent, headPositionSeverity;
            public float score, coverage, trackingLostSeconds, headDisplacementMetres, headRotationDegrees;
            public float rangeScaleUsed, nextRangeScale, calibratedTravelMetres, targetTravelMetres;
            public float durationScaleUsed, nextDurationScale, outwardSecondsUsed, returnSecondsUsed;
        }
        [Serializable] public class Report { public Decision[] decisions; }
        class State
        {
            public ShieldExerciseProfile profile;
            public float outward, returnTime, scale = 1, range = 1, rangeFloor = .4f;
            public float maxSpeed = 1.4f;
        }
        readonly Dictionary<string, State> states = new Dictionary<string, State>();
        readonly List<Decision> decisions = new List<Decision>();
        public string LastMessage { get; private set; } = "Coach: learning your pace";
        static string Key(string id, int side) => id + ":" + side;
        public void ResetSession()
        {
            foreach (var state in states.Values) if (state.profile) { if(Application.isPlaying) Destroy(state.profile); else DestroyImmediate(state.profile); }
            states.Clear(); decisions.Clear(); LastMessage = "Coach: learning your pace";
        }
        void OnDestroy() { ResetSession(); }
        public ShieldExerciseProfile ProfileFor(ShieldExerciseProfile source, int side)
        {
            string key = Key(source.exerciseId, side);
            if (!states.TryGetValue(key, out var state))
            {
                state = new State { profile = Instantiate(source), outward = source.outwardSeconds, returnTime = source.returnSeconds };
                if(settings!=null){var arm=settings.Arm(source.exerciseId,side);state.maxSpeed=1+settings.Limit(source.exerciseId).fasterPercent/100;
                    state.scale=1/Mathf.Clamp(developerMode?arm.devSpeed:arm.speed,.5f,developerMode?2:state.maxSpeed);state.range=Mathf.Clamp(developerMode?arm.devRange:arm.range,developerMode?.3f:minimumRangeScale,1);}
                else state.maxSpeed=1/fastestDurationScale;
                Apply(state);states.Add(key, state);
            }
            return state.profile;
        }
        public float RangeFor(ShieldExerciseProfile source, int side, float calibratedTravelMetres)
        {
            ProfileFor(source, side);
            var state = states[Key(source.exerciseId, side)];
            // Do not shrink already-small movements below the calibration noise threshold.
            state.rangeFloor = Mathf.Clamp(Mathf.Max(developerMode?.3f:minimumRangeScale, source.minimumTravel / Mathf.Max(.00001f, calibratedTravelMetres)), 0, 1);
            state.range = Mathf.Clamp(state.range, state.rangeFloor, 1);
            return state.range;
        }
        public Decision Observe(ShieldExerciseProfile source, int side, int repetition, float score, float coverage,
            float validSeconds, float trackingLostSeconds, float headMetres, float headDegrees, float calibratedTravelMetres = 0, float targetTravelMetres = 0, float headPositionSeverity = 0)
        {
            ProfileFor(source, side);
            var state = states[Key(source.exerciseId, side)];
            var decision = new Decision { exerciseId = source.exerciseId, side = side == 0 ? "left" : "right", repetition = repetition,
                rangeScaleUsed = state.range, calibratedTravelMetres = calibratedTravelMetres, targetTravelMetres = targetTravelMetres,
                enabled = adaptationEnabled&&!developerMode, developerMode=developerMode, maximumSpeedIncreasePercent=(state.maxSpeed-1)*100, score = score, coverage = coverage, trackingLostSeconds = trackingLostSeconds,
                headPositionSeverity=headPositionSeverity, headDisplacementMetres = headMetres, headRotationDegrees = headDegrees, durationScaleUsed = state.scale,
                outwardSecondsUsed = state.profile.outwardSeconds, returnSecondsUsed = state.profile.returnSeconds };
            bool usable = validSeconds > 0 && trackingLostSeconds / Mathf.Max(.001f, validSeconds + trackingLostSeconds) <= .1f
                && !float.IsNaN(score) && !float.IsNaN(coverage);
            if (!adaptationEnabled || developerMode || !usable)
                decision.reason = developerMode?"Developer overrides; no learning or preset writes":!adaptationEnabled?"Adaptation disabled":"Tracking unreliable; keep difficulty";
            else if (score < 65 || coverage < .65f)
            {
                bool severe=score<45||coverage<.45f;
                state.range=Mathf.Max(state.rangeFloor,state.range-(severe?.15f:.1f));
                state.scale=Mathf.Min(slowestDurationScale,state.scale/(severe?.8f:.85f));
                decision.reason="Below target band; reduce range and speed for next attempt";
            }
            else if (headPositionSeverity > .2f)
                decision.reason = "Sustained downward/opposite-side displacement; no increase (poor attempts can still ease)";
            else if (score >= 90 && coverage >= .9f)
            {
                state.range=Mathf.Min(1,state.range+.05f);
                state.scale=Mathf.Max(1/state.maxSpeed,state.scale/1.1f);
                decision.reason="Above target band; increase range and speed within limits";
            }
            else decision.reason="In target band (65–90); maintain difficulty";
            decision.nextRangeScale = state.range;
            decision.nextDurationScale = state.scale;
            Apply(state);
            LastMessage = source.displayName+" / "+(side==0?"LEFT":"RIGHT")+": next speed "+(100/state.scale).ToString("F0")+"%, range "+(100*state.range).ToString("F0")+"%";
            if(settings!=null&&!developerMode&&adaptationEnabled&&usable){var arm=settings.Arm(source.exerciseId,side);arm.speed=1/state.scale;arm.range=state.range;settings.Save();}
            decisions.Add(decision);
            return decision;
        }
        static void Apply(State state){state.profile.outwardSeconds=state.outward*state.scale;state.profile.returnSeconds=state.returnTime*state.scale;}
        public string ExportJson() => JsonUtility.ToJson(new Report { decisions = decisions.ToArray() }, true);
    }
}
