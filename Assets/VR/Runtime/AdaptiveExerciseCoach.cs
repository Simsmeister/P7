using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EmilVR
{
    // One local coach: train a base network explicitly, load it read-only, adapt each player separately.
    // This starter model learns synthetic examples, not clinically validated treatment decisions.
    public class AdaptiveExerciseCoach : MonoBehaviour
    {
        // Read top to bottom: session integration, network, personal files, editor training.
        // Full explanation and retraining steps: ADAPTIVE_COACH.md at the project root.
        public bool adaptationEnabled = true;
        ExercisePlayerSettings settings;
        bool developerMode;
        public void Configure(ExercisePlayerSettings value)
        {
            ResetSession();settings=value;developerMode=value!=null&&value.devMode;
            LoadBaseModel();LoadPersonalData();
        }
        [Range(.25f, 1f)] public float minimumRangeScale = .4f;
        [Range(.75f, 1f)] public float fastestDurationScale = .85f;
        [Range(1f, 2f)] public float slowestDurationScale = 2f;
        [Serializable] public class Decision
        {
            public string policy = "neural-range-tempo-v1", exerciseId, side, reason;
            public string modelId;
            public float predictedRangeChange,predictedSpeedChange;
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
            var decision = new Decision { modelId=baseModel!=null?baseModel.modelId:"unavailable", exerciseId = source.exerciseId, side = side == 0 ? "left" : "right", repetition = repetition,
                rangeScaleUsed = state.range, calibratedTravelMetres = calibratedTravelMetres, targetTravelMetres = targetTravelMetres,
                enabled = adaptationEnabled&&!developerMode, developerMode=developerMode, maximumSpeedIncreasePercent=(state.maxSpeed-1)*100, score = score, coverage = coverage, trackingLostSeconds = trackingLostSeconds,
                headPositionSeverity=headPositionSeverity, headDisplacementMetres = headMetres, headRotationDegrees = headDegrees, durationScaleUsed = state.scale,
                outwardSecondsUsed = state.profile.outwardSeconds, returnSecondsUsed = state.profile.returnSeconds };
            bool usable = validSeconds > 0 && trackingLostSeconds / Mathf.Max(.001f, validSeconds + trackingLostSeconds) <= .1f
                && Finite(score) && Finite(coverage) && Finite(headPositionSeverity)
                && score>=0 && score<=100 && coverage>=0 && coverage<=1 && Finite(validSeconds) && Finite(trackingLostSeconds) && trackingLostSeconds>=0;
            if (!adaptationEnabled || developerMode || !usable)
                decision.reason = developerMode?"Developer overrides; no learning or preset writes":!adaptationEnabled?"Adaptation disabled":"Tracking unreliable; keep difficulty";
            else if (baseModel==null)
                decision.reason="Base model unavailable; keep difficulty";
            else
            {
                Vector2 change=Predict(baseModel,score/100f,coverage);
                // A small dead zone prevents tiny network approximation errors from causing drift.
                float rangeChange=Mathf.Abs(change.x)<.015f?0:change.x;
                float speedChange=Mathf.Abs(change.y)<.0225f?0:change.y;
                decision.predictedRangeChange=change.x;decision.predictedSpeedChange=change.y;
                // Head displacement may block an increase, but never prevents easing.
                if(headPositionSeverity>.2f){rangeChange=Mathf.Min(0,rangeChange);speedChange=Mathf.Min(0,speedChange);}
                state.range=Mathf.Clamp(state.range+rangeChange,state.rangeFloor,1);
                float speed=Mathf.Clamp((1/state.scale)*(1+speedChange),1/slowestDurationScale,state.maxSpeed);
                state.scale=1/speed;
                decision.reason=rangeChange<0||speedChange<0?"Network eases next attempt":rangeChange>0||speedChange>0?"Network increases next attempt within limits":"Maintain difficulty";
            }
            decision.nextRangeScale = state.range;
            decision.nextDurationScale = state.scale;
            Apply(state);
            LastMessage = source.displayName+" / "+(side==0?"LEFT":"RIGHT")+": next speed "+(100/state.scale).ToString("F0")+"%, range "+(100*state.range).ToString("F0")+"%";
            if(settings!=null&&!developerMode&&adaptationEnabled&&usable&&baseModel!=null)
            {
                var arm=settings.Arm(source.exerciseId,side);arm.speed=1/state.scale;arm.range=state.range;
                var personal=PersonalArm(source.exerciseId,side);personal.speed=arm.speed;personal.range=arm.range;
                personal.meanScore=personal.observations==0?score:Mathf.Lerp(personal.meanScore,score,.25f);
                personal.meanCoverage=personal.observations==0?coverage:Mathf.Lerp(personal.meanCoverage,coverage,.25f);
                personal.observations++;personal.lastModelId=baseModel.modelId;
                settings.Save();SavePersonalData(); // Mirror speed/range for the existing settings UI.
            }
            decisions.Add(decision);
            return decision;
        }
        static void Apply(State state){state.profile.outwardSeconds=state.outward*state.scale;state.profile.returnSeconds=state.returnTime*state.scale;}
        public string ExportJson() => JsonUtility.ToJson(new Report { decisions = decisions.ToArray() }, true);

        // 1. BASE MODEL: 2 inputs -> 12 tanh neurons -> 2 tanh outputs.
        // Inputs are positional score and travel coverage, both 0..1. No hand/head rotation input.
        // Outputs are range-fraction change and relative speed change for the NEXT attempt.
        const int Hidden=12;
        public const float RangeStep=.1f, SpeedStep=.15f;
        const string ResourceName="AdaptiveCoach/base-model";
        const string TrainingPath="Assets/VR/Training/coach-training.json";
        const string ModelPath="Assets/Resources/AdaptiveCoach/base-model.json";
        [Serializable] public class Model
        {
            public int version=1,hidden=Hidden,seed=42,epochs,trainingExamples,validationExamples;
            public string modelId,inputSchema="score01,coverage01",provenance;
            public float trainingMse,validationMse;
            public float[] inputWeights=new float[Hidden*2],hiddenBias=new float[Hidden];
            public float[] outputWeights=new float[Hidden*2],outputBias=new float[2];
        }
        [Serializable] public class Example
        {
            public float score,coverage,rangeChange,speedChange;
            public bool validation;
        }
        [Serializable] public class TrainingSet {public int version=1;public string provenance;public Example[] examples;}
        Model baseModel;
        public string ModelError {get;private set;}
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static bool ValidArray(float[] values,int size)=>values!=null&&values.Length==size&&Array.TrueForAll(values,x=>Finite(x)&&Mathf.Abs(x)<=100);
        public static bool ValidModel(Model m)=>m!=null&&m.version==1&&m.hidden==Hidden&&m.inputSchema=="score01,coverage01"&&!string.IsNullOrEmpty(m.modelId)
            &&ValidArray(m.inputWeights,Hidden*2)&&ValidArray(m.hiddenBias,Hidden)&&ValidArray(m.outputWeights,Hidden*2)&&ValidArray(m.outputBias,2);
        void LoadBaseModel()
        {
            baseModel=null;ModelError=null;
            try
            {
                var asset=Resources.Load<TextAsset>(ResourceName);
                if(!asset)throw new InvalidDataException("Bundled neural model is missing. Train the base model in Unity first.");
                var model=JsonUtility.FromJson<Model>(asset.text);
                if(!ValidModel(model))throw new InvalidDataException("Neural model schema or weights are invalid.");
                baseModel=model;
            }
            catch(Exception e){ModelError=e.Message;LastMessage=ModelError;Debug.LogWarning(ModelError);}
        }
        static float Tanh(float x)=>(float)Math.Tanh(x);
        static Vector2 Forward(Model model,float score,float coverage,float[] hidden)
        {
            float x=score*2-1,y=coverage*2-1;
            for(int h=0;h<Hidden;h++)hidden[h]=Tanh(model.hiddenBias[h]+model.inputWeights[h*2]*x+model.inputWeights[h*2+1]*y);
            float a=model.outputBias[0],b=model.outputBias[1];
            for(int h=0;h<Hidden;h++){a+=model.outputWeights[h]*hidden[h];b+=model.outputWeights[Hidden+h]*hidden[h];}
            return new Vector2(Tanh(a),Tanh(b));
        }
        public static Vector2 Predict(Model model,float score,float coverage)
        {
            if(!ValidModel(model)||!Finite(score)||!Finite(coverage))return Vector2.zero;
            Vector2 output=Forward(model,Mathf.Clamp01(score),Mathf.Clamp01(coverage),new float[Hidden]);
            return new Vector2(output.x*RangeStep,output.y*SpeedStep);
        }

        // 2. PERSONAL DATA: separate from weights, one record per exercise AND arm.
        // The settings file retains calibration/toggles and a compatibility mirror of speed/range.
        [Serializable] public class PersonalLevel
        {
            public string exerciseId,lastModelId;public int side,observations;
            public float speed=1,range=1,meanScore,meanCoverage;
        }
        [Serializable] public class PersonalData {public int version=1;public List<PersonalLevel> arms=new List<PersonalLevel>();}
        PersonalData personalData=new PersonalData();
        public static string PersonalPath(string settingsPath)=>string.IsNullOrEmpty(settingsPath)?null:Path.Combine(Path.GetDirectoryName(settingsPath),Path.GetFileNameWithoutExtension(settingsPath)+"-adaptive.json");
        PersonalLevel PersonalArm(string id,int side)
        {
            var entry=personalData.arms.Find(a=>a.exerciseId==id&&a.side==side);
            if(entry==null){var arm=settings.Arm(id,side);entry=new PersonalLevel{exerciseId=id,side=side,speed=arm.speed,range=arm.range};personalData.arms.Add(entry);}
            return entry;
        }
        static void WriteJson(string path,object data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(data,true));
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        void LoadPersonalData()
        {
            personalData=new PersonalData();if(settings==null||developerMode)return;
            string path=PersonalPath(settings.filePath);
            try
            {
                if(path!=null&&File.Exists(path))
                {
                    var loaded=JsonUtility.FromJson<PersonalData>(File.ReadAllText(path));
                    if(loaded==null||loaded.version!=1||loaded.arms==null||loaded.arms.Exists(a=>a==null||string.IsNullOrEmpty(a.exerciseId)||(a.side!=0&&a.side!=1)||!Finite(a.speed)||!Finite(a.range)||!Finite(a.meanScore)||!Finite(a.meanCoverage)))
                        throw new InvalidDataException("Invalid personal coach data");
                    personalData=loaded;
                    foreach(var a in personalData.arms){var arm=settings.Arm(a.exerciseId,a.side);arm.speed=Mathf.Clamp(a.speed,.5f,2);arm.range=Mathf.Clamp(a.range,minimumRangeScale,1);}
                }
                else foreach(var arm in settings.arms)PersonalArm(arm.id,arm.side); // Migrate existing progress, never discard it.
            }
            catch(Exception e){Debug.LogWarning("Personal coach file could not be loaded; retaining settings mirror. "+e.Message);}
        }
        void SavePersonalData()
        {
            string path=PersonalPath(settings.filePath);if(path==null)return;
            try{WriteJson(path,personalData);}catch(Exception e){settings.error="Could not save personal coach progress: "+e.Message;Debug.LogWarning(settings.error);}
        }
        public static void ResetPersonalData(ExercisePlayerSettings settings)
        {
            string path=PersonalPath(settings.filePath);if(path==null)return;
            try{WriteJson(path,new PersonalData());}catch(Exception e){Debug.LogWarning("Could not reset personal coach data: "+e.Message);}
        }
        public void Recalibrated(string id,int side)
        {
            if(settings==null||developerMode)return;
            var arm=PersonalArm(id,side);arm.speed=arm.range=1;arm.observations=0;arm.meanScore=arm.meanCoverage=0;
            states.Remove(Key(id,side),out var previous);if(previous!=null&&previous.profile){if(Application.isPlaying)Destroy(previous.profile);else DestroyImmediate(previous.profile);}
            SavePersonalData();
        }

        // 3. TRAINING: supervised backpropagation, only run explicitly in the editor.
        // Training labels must specify desired changes; raw movement recordings alone are not labels.
        public static Model Train(TrainingSet data,int epochs=1200)
        {
            if(data==null||data.version!=1||data.examples==null||data.examples.Length<10)throw new InvalidDataException("Need a version-1 labelled training set");
            foreach(var e in data.examples)if(e==null||!Finite(e.score)||!Finite(e.coverage)||!Finite(e.rangeChange)||!Finite(e.speedChange)||e.score<0||e.score>1||e.coverage<0||e.coverage>1||Mathf.Abs(e.rangeChange)>RangeStep||Mathf.Abs(e.speedChange)>SpeedStep)
                throw new InvalidDataException("Invalid example: inputs must be 0..1 and changes within per-rep limits");
            var random=new System.Random(42);var model=new Model{epochs=epochs,provenance=data.provenance};
            model.trainingExamples=Array.FindAll(data.examples,e=>!e.validation).Length;model.validationExamples=data.examples.Length-model.trainingExamples;
            if(model.trainingExamples<5||model.validationExamples<5)throw new InvalidDataException("Need separate training and validation examples");
            for(int i=0;i<model.inputWeights.Length;i++)model.inputWeights[i]=(float)(random.NextDouble()-.5);
            for(int i=0;i<model.outputWeights.Length;i++)model.outputWeights[i]=(float)(random.NextDouble()-.5);
            var hidden=new float[Hidden];var order=Array.FindAll(data.examples,e=>!e.validation);const float rate=.025f;
            for(int epoch=0;epoch<epochs;epoch++)
            {
                for(int i=order.Length-1;i>0;i--){int j=random.Next(i+1);var swap=order[i];order[i]=order[j];order[j]=swap;}
                foreach(var e in order)
                {
                    Vector2 output=Forward(model,e.score,e.coverage,hidden);
                    float da=(output.x-e.rangeChange/RangeStep)*(1-output.x*output.x);
                    float db=(output.y-e.speedChange/SpeedStep)*(1-output.y*output.y);
                    for(int h=0;h<Hidden;h++)
                    {
                        float dh=(da*model.outputWeights[h]+db*model.outputWeights[Hidden+h])*(1-hidden[h]*hidden[h]);
                        model.inputWeights[h*2]-=rate*dh*(e.score*2-1);model.inputWeights[h*2+1]-=rate*dh*(e.coverage*2-1);model.hiddenBias[h]-=rate*dh;
                        model.outputWeights[h]-=rate*da*hidden[h];model.outputWeights[Hidden+h]-=rate*db*hidden[h];
                    }
                    model.outputBias[0]-=rate*da;model.outputBias[1]-=rate*db;
                }
            }
            foreach(var e in data.examples){Vector2 o=Forward(model,e.score,e.coverage,hidden);float error=((o.x-e.rangeChange/RangeStep)*(o.x-e.rangeChange/RangeStep)+(o.y-e.speedChange/SpeedStep)*(o.y-e.speedChange/SpeedStep))/2;if(e.validation)model.validationMse+=error;else model.trainingMse+=error;}
            model.trainingMse/=model.trainingExamples;model.validationMse/=model.validationExamples;
            using(var hash=System.Security.Cryptography.SHA256.Create())model.modelId="nn1-"+BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)+epochs))).Replace("-","").Substring(0,16);
            if(!ValidModel(model)||!Finite(model.validationMse)||model.validationMse>.02f)throw new InvalidDataException("Training failed validation; existing base model was not replaced");
            return model;
        }
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Emil VR/AI/Train Base Neural Model")]
        public static void TrainBaseModel()
        {
            var data=JsonUtility.FromJson<TrainingSet>(File.ReadAllText(TrainingPath));var model=Train(data);
            WriteJson(ModelPath,model);UnityEditor.AssetDatabase.Refresh();
            Debug.Log("NEURAL_MODEL_TRAINED "+model.modelId+" training MSE="+model.trainingMse+" held-out MSE="+model.validationMse);
        }
#endif
    }
}
