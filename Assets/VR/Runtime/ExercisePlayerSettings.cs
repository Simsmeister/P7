using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EmilVR
{
    [Serializable] public class ExerciseArmSettings
    {
        public string id;
        public int side;
        public bool enabled = true;
        public float speed = 1, range = 1, devSpeed = 1, devRange = 1;
        public ShieldExerciseGame.Calibration calibration; // Head-relative position and yaw-relative rotation.
    }
    [Serializable] public class ExerciseSpeedLimit { public string id; public float fasterPercent = 40; }
    [Serializable] public class ExercisePlayerSettings
    {
        public int version = 1, totalRepetitions = 18;
        public bool tutorialComplete, devMode;
        public List<ExerciseArmSettings> arms = new List<ExerciseArmSettings>();
        public List<ExerciseSpeedLimit> limits = new List<ExerciseSpeedLimit>();
        [NonSerialized] public string filePath, error;
        public ExerciseArmSettings Arm(string id,int side)
        {
            var arm=arms.Find(a=>a.id==id&&a.side==side);
            if(arm==null){arm=new ExerciseArmSettings{id=id,side=side};arms.Add(arm);}return arm;
        }
        public ExerciseSpeedLimit Limit(string id)
        {var limit=limits.Find(l=>l.id==id);if(limit==null){limit=new ExerciseSpeedLimit{id=id};limits.Add(limit);}return limit;}
        public static ExercisePlayerSettings Load(string path)
        {
            ExercisePlayerSettings data=null;string failure=null;
            try{if(File.Exists(path))data=JsonUtility.FromJson<ExercisePlayerSettings>(File.ReadAllText(path));}
            catch(Exception e){failure="Saved settings could not be read: "+e.Message;}
            if(data==null||data.version!=1){data=new ExercisePlayerSettings();if(File.Exists(path))failure="Saved settings unavailable. Calibration is required.";}
            data.filePath=path;data.error=failure;data.arms=data.arms??new List<ExerciseArmSettings>();data.limits=data.limits??new List<ExerciseSpeedLimit>();
            data.totalRepetitions=Mathf.Clamp(data.totalRepetitions,1,120);
            foreach(var a in data.arms){a.speed=FiniteClamp(a.speed,.5f,2,1);a.range=FiniteClamp(a.range,.4f,1,1);a.devSpeed=FiniteClamp(a.devSpeed,.5f,2,1);a.devRange=FiniteClamp(a.devRange,.3f,1,1);}
            foreach(var l in data.limits)l.fasterPercent=FiniteClamp(l.fasterPercent,0,100,40);
            return data;
        }
        static float FiniteClamp(float value,float min,float max,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max);
        public void Save()
        {
            if(string.IsNullOrEmpty(filePath))return;
            try{Directory.CreateDirectory(Path.GetDirectoryName(filePath));string temp=filePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(this,true));if(File.Exists(filePath))File.Replace(temp,filePath,null);else File.Move(temp,filePath);error=null;}
            catch(Exception e){error="Settings could not be saved: "+e.Message;Debug.LogWarning(error);}
        }
        public static ShieldExerciseGame.Calibration TransformCalibration(ShieldExerciseGame.Calibration c,Vector3 origin,Quaternion yaw,bool toLocal)
        {
            Vector3 P(Vector3 p)=>toLocal?Quaternion.Inverse(yaw)*(p-origin):origin+yaw*p;
            Quaternion Q(Quaternion q)=>toLocal?Quaternion.Inverse(yaw)*q:yaw*q;
            return new ShieldExerciseGame.Calibration{exerciseId=c.exerciseId,side=c.side,start=P(c.start),end=P(c.end),pivot=P(c.pivot),startRotation=Q(c.startRotation),endRotation=Q(c.endRotation),calibratedTravelMetres=c.calibratedTravelMetres};
        }
        public static bool UsableCalibration(ShieldExerciseGame.Calibration c,ShieldExerciseProfile profile)
        {
            if(c==null||c.exerciseId!=profile.exerciseId)return false;
            float d=Vector3.Distance(c.start,c.end);
            return !float.IsNaN(d)&&!float.IsInfinity(d)&&d>=profile.minimumTravel&&d<2&&Quaternion.Dot(c.startRotation,c.startRotation)>.5f&&Quaternion.Dot(c.endRotation,c.endRotation)>.5f;
        }
        static void Shuffle(List<int> values,System.Random random){for(int i=values.Count-1;i>0;i--){int j=random.Next(i+1);int value=values[i];values[i]=values[j];values[j]=value;}}
        public List<int> Playlist(ShieldExerciseProfile[] profiles,int? seed=null)
        {
            var enabled=new List<int>();for(int side=0;side<2;side++)for(int e=0;e<profiles.Length;e++)if(Arm(profiles[e].exerciseId,side).enabled)enabled.Add(e*2+side);
            var result=new List<int>();if(enabled.Count==0)return result;
            var random=new System.Random(seed??Guid.NewGuid().GetHashCode());
            Shuffle(enabled,random); // Short levels do not always favour the same exercise/arm.
            for(int i=0;i<Mathf.Clamp(totalRepetitions,1,120);i++)result.Add(enabled[i%enabled.Count]);
            Shuffle(result,random); // Balanced counts, random order; adjacent repeats are allowed.
            return result;
        }
    }
}
