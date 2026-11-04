using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EmilVR
{
    // World-space menu. Both controller rays and desktop mouse share the same hit testing.
    public class ExerciseGameMenu : MonoBehaviour
    {
        [Serializable] public class Widget
        {
            public string key; public RectTransform rect; public Text label;
            public Button button;public Toggle toggle;public Slider slider;
        }
        public ShieldExerciseGame game;
        public RectTransform canvasRoot;
        public GameObject home,settings,details,resetConfirmation;
        public Text homeStatus,detailTitle,detailStatus,footer;
        public LineRenderer pointer;
        public List<Widget> widgets=new List<Widget>();
        readonly List<UnityEngine.XR.XRInputSubsystem> xrSystems=new List<UnityEngine.XR.XRInputSubsystem>();
        void OnEnable(){UnityEngine.SubsystemManager.GetSubsystems(xrSystems);foreach(var xr in xrSystems)xr.trackingOriginUpdated+=OnRecenter;}
        void OnDisable(){foreach(var xr in xrSystems)xr.trackingOriginUpdated-=OnRecenter;}
        int recenterFrames;
        void OnRecenter(UnityEngine.XR.XRInputSubsystem xr){recenterFrames=3;}
        void OnApplicationFocus(bool focus){if(focus)recenterFrames=3;}
        public void Recenter(){if(game&&game.head&&game.MenuVisible)transform.SetPositionAndRotation(game.head.position+Quaternion.Euler(0,game.head.eulerAngles.y,0)*new Vector3(0,-.2f,2.3f),Quaternion.Euler(0,game.head.eulerAngles.y,0));}
        int page,exercise;bool wasVisible,wasDown;Widget dragging;float refresh;
        public void Initialize()
        {
            foreach(var w in widgets){var item=w;
                if(w.button)w.button.onClick.AddListener(()=>Navigate(item.key));
                if(w.toggle)w.toggle.onValueChanged.AddListener(v=>Change(item.key,v?1:0));
                if(w.slider)w.slider.onValueChanged.AddListener(v=>Change(item.key,v));
            }
        }
        void Awake(){Initialize();}
        public void Navigate(string key)
        {
            if(key=="recenter"){Recenter();return;}
            if(key=="start"){game.Begin();return;}
            if(key=="redo"){game.Begin(true);return;}
            if(key=="replay"){game.ShowLastReplay();return;}
            if(key=="confirmReset"){game.ResetSavedSettings();page=0;Refresh();return;}
            if(key=="reset"){page=3;Refresh();return;}
            page=key=="settings"||key=="back"?1:0;
            if(key.StartsWith("exercise:")){exercise=int.Parse(key.Substring(9));page=2;}
            Refresh();
        }
        public void Change(string key,float value)
        {
            var data=game.PlayerSettings;
            if(key=="total")data.totalRepetitions=Mathf.RoundToInt(value);
            else if(key=="dev")data.devMode=value>.5f;
            else if(key.StartsWith("left:")||key.StartsWith("right:")){int e=int.Parse(key.Split(':')[1]);data.Arm(game.exercises[e].exerciseId,key.StartsWith("left:")?0:1).enabled=value>.5f;}
            else{
                string id=game.exercises[exercise].exerciseId;
                if(key=="limit")data.Limit(id).fasterPercent=value;
                else if(data.devMode){var arm=data.Arm(id,key.EndsWith("Left")?0:1);if(key.StartsWith("speed"))arm.devSpeed=value/100;else if(key.StartsWith("range"))arm.devRange=value/100;}
            }
            data.Save();Refresh();
        }
        void Refresh()
        {
            game.EnsurePlayerSettings();var data=game.PlayerSettings;
            home.SetActive(page==0);settings.SetActive(page==1);details.SetActive(page==2);resetConfirmation.SetActive(page==3);
            homeStatus.text=(game.Finished?game.SessionResult+"  |  Accuracy "+game.Score.ToString("F0")+" / 100"+(game.arcade?"  |  "+game.arcade.Points+" points  |  Best streak "+game.arcade.BestCombo:"")+"\n":"")+(data.devMode?"DEV MODE - fixed overrides, normal progress protected":"Your personal pace and range are saved automatically")+"\nPower target: "+data.totalRepetitions+" perfect repairs";
            footer.text=!string.IsNullOrEmpty(data.error)?"Could not save settings. Progress may not persist.":!game.AnyEnabled?"Enable at least one exercise and arm in Settings.":"Point with either controller and press trigger. Drag sliders to adjust.";
            string id=game.exercises[Mathf.Clamp(exercise,0,game.exercises.Length-1)].exerciseId;
            detailTitle.text=game.exercises[exercise].displayName.ToUpper();
            var left=data.Arm(id,0);var right=data.Arm(id,1);
            detailStatus.text="Saved: Left "+(left.speed*100).ToString("F0")+"% speed / "+(left.range*100).ToString("F0")+"% range   |   Right "+(right.speed*100).ToString("F0")+"% / "+(right.range*100).ToString("F0")+"%\n"+(data.devMode?"Dev sliders override normal presets. Learning is off.":"Dev sliders are disabled. Turn Dev mode on in Settings to use them.");
            foreach(var w in widgets){
                if(w.key=="start"||w.key=="redo")w.button.interactable=game.AnyEnabled;
                if(w.key=="dev")w.toggle.SetIsOnWithoutNotify(data.devMode);
                if(w.key.StartsWith("left:")||w.key.StartsWith("right:")){int e=int.Parse(w.key.Split(':')[1]);w.toggle.SetIsOnWithoutNotify(data.Arm(game.exercises[e].exerciseId,w.key.StartsWith("left:")?0:1).enabled);}
                if(w.toggle)w.label.text=(w.key=="dev"?"DEV MODE":w.key.StartsWith("left:")?"LEFT":"RIGHT")+"   "+(w.toggle.isOn?"ON":"OFF");
                if(!w.slider)continue;
                float value=w.key=="total"?data.totalRepetitions:w.key=="limit"?data.Limit(id).fasterPercent:w.key=="speedLeft"?left.devSpeed*100:w.key=="speedRight"?right.devSpeed*100:w.key=="rangeLeft"?left.devRange*100:right.devRange*100;
                w.slider.SetValueWithoutNotify(value);w.slider.interactable=w.key=="total"||w.key=="limit"||data.devMode;
                string label=w.key=="total"?"Repair target (perfect movements)":w.key=="limit"?"Maximum speed increase (normal mode)":w.key=="speedLeft"?"Dev left speed":w.key=="speedRight"?"Dev right speed":w.key=="rangeLeft"?"Dev left range":"Dev right range";
                w.label.text=label+": "+value.ToString("F0")+(w.key=="total"?"":"%");w.label.color=w.slider.interactable?Color.white:new Color(.5f,.55f,.6f);
            }
        }
        public static bool Hit(RectTransform rect,Ray ray,out Vector2 local)
        {
            local=Vector2.zero;var plane=new Plane(rect.forward,rect.position);
            if(!plane.Raycast(ray,out float distance)||distance<0)return false;
            var point=rect.InverseTransformPoint(ray.GetPoint(distance));local=new Vector2(point.x,point.y);return rect.rect.Contains(local);
        }
        void LateUpdate()
        {
            if(!game||!canvasRoot)return;if(recenterFrames>0&&--recenterFrames==0)Recenter();bool visible=game.MenuVisible;
            canvasRoot.gameObject.SetActive(visible);pointer.gameObject.SetActive(false);
            if(!visible){wasVisible=false;wasDown=false;dragging=null;return;}
            if(!wasVisible){page=0;transform.SetPositionAndRotation(game.head.position+Quaternion.Euler(0,game.head.eulerAngles.y,0)*new Vector3(0,-.2f,2.3f),Quaternion.Euler(0,game.head.eulerAngles.y,0));Refresh();wasVisible=true;}
            refresh+=Time.unscaledDeltaTime;if(refresh>.3f){refresh=0;Refresh();}
            bool right=game.MenuRay(1,out Ray ray,out bool down);bool left=game.MenuRay(0,out var leftRay,out bool leftDown);
            if(left&&(!right||leftDown)){ray=leftRay;down=leftDown;right=true;}
            if(!UnityEngine.XR.XRSettings.isDeviceActive&&Mouse.current!=null){var camera=game.head.GetComponent<Camera>();ray=camera.ScreenPointToRay(Mouse.current.position.ReadValue());down=Mouse.current.leftButton.isPressed;right=true;}
            if(!right){wasDown=false;dragging=null;return;}
            var plane=new Plane(canvasRoot.forward,canvasRoot.position);
            if(plane.Raycast(ray,out float d)&&d>0){pointer.gameObject.SetActive(true);pointer.SetPosition(0,ray.origin);pointer.SetPosition(1,ray.GetPoint(d));}
            bool press=down&&!wasDown;wasDown=down;if(!down)dragging=null;
            foreach(var w in widgets){if(!w.rect.gameObject.activeInHierarchy)continue;
                bool hit=Hit(w.rect,ray,out var local);
                if(w.button){w.button.image.color=!w.button.interactable?new Color(.15f,.18f,.22f):hit?new Color(.15f,.55f,.65f):new Color(.1f,.24f,.33f);if(hit&&press&&w.button.interactable){w.button.onClick.Invoke();break;}}
                if(w.toggle&&hit&&press){w.toggle.isOn=!w.toggle.isOn;break;}
                if(w.slider&&w.slider.interactable){if(hit&&press)dragging=w;if(dragging==w&&down){var p=w.rect.InverseTransformPoint(ray.GetPoint(Mathf.Max(0,d)));w.slider.normalizedValue=Mathf.InverseLerp(w.rect.rect.xMin,w.rect.rect.xMax,p.x);break;}}
            }
        }
    }
}
