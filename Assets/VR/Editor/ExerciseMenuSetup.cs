using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using EmilVR;

public static class ExerciseMenuSetup
{
    static Font font; static Sprite panelSprite;
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);return r;}
    static Text Label(Transform parent,string text,float x,float y,float w,float h,int size=26)
    {var r=Rect(text,parent,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;return t;}
    static Image Panel(RectTransform r,Color color){var image=r.gameObject.AddComponent<Image>();image.color=color;if(panelSprite&&r.sizeDelta.y>20){image.sprite=panelSprite;image.type=Image.Type.Sliced;}return image;}
    static void Button(ExerciseGameMenu menu,Transform page,string key,string title,float x,float y,float width=750)
    {var r=Rect(title,page,x,y,width,70);var image=Panel(r,new Color(.1f,.24f,.33f));var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var label=Label(r,title,0,0,width,70);menu.widgets.Add(new ExerciseGameMenu.Widget{key=key,rect=r,button=b,label=label});}
    static void Toggle(ExerciseGameMenu menu,Transform page,string key,float x,float y,float width=180)
    {var r=Rect(key,page,x,y,width,60);var image=Panel(r,new Color(.12f,.25f,.3f));var t=r.gameObject.AddComponent<Toggle>();t.targetGraphic=image;var check=Rect("ON indicator",r,-width/2+12,0,8,45);t.graphic=Panel(check,new Color(.1f,1,.65f));var label=Label(r,key,5,0,width-20,60,23);menu.widgets.Add(new ExerciseGameMenu.Widget{key=key,rect=r,toggle=t,label=label});}
    static void Slider(ExerciseGameMenu menu,Transform page,string key,float y,float min,float max,bool whole)
    {var r=Rect(key,page,0,y,760,35);Panel(r,new Color(.15f,.2f,.25f));var s=r.gameObject.AddComponent<Slider>();s.minValue=min;s.maxValue=max;s.wholeNumbers=whole;
        var handle=Rect("Handle",r,0,0,28,48);s.targetGraphic=Panel(handle,new Color(.15f,.85f,.85f));s.handleRect=handle;s.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
        var label=Label(page,key,0,y+40,850,40,24);menu.widgets.Add(new ExerciseGameMenu.Widget{key=key,rect=r,slider=s,label=label});}
    public static void Build()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        const string panelPath="Assets/VR/Shield/Rounded UI.asset";
        panelSprite=AssetDatabase.LoadAssetAtPath<Sprite>(panelPath);
        if(!panelSprite){var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);texture.name="Rounded panel pixels";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=Mathf.Max(12-x,x-51),dy=Mathf.Max(12-y,y-51);float distance=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(12-distance)));}texture.Apply();
            panelSprite=Sprite.Create(texture,new Rect(0,0,64,64),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));panelSprite.name="Rounded UI";AssetDatabase.CreateAsset(panelSprite,panelPath);AssetDatabase.AddObjectToAsset(texture,panelSprite);}

        var root=new GameObject("Exercise Main Menu");var menu=root.AddComponent<ExerciseGameMenu>();
        var canvas=Rect("Menu canvas",root.transform,0,0,1000,900);canvas.localScale=Vector3.one*.0022f;canvas.gameObject.AddComponent<Canvas>().renderMode=RenderMode.WorldSpace;Panel(canvas,new Color(.018f,.035f,.065f,.98f));menu.canvasRoot=canvas;
        Panel(Rect("Top accent",canvas,0,446,1000,8),new Color(.1f,.85f,.8f));
        Panel(Rect("Footer divider",canvas,0,-380,900,2),new Color(.12f,.3f,.37f));
        menu.home=Rect("Home",canvas,0,0,1000,900).gameObject;menu.settings=Rect("Settings",canvas,0,0,1000,900).gameObject;menu.details=Rect("Exercise settings",canvas,0,0,1000,900).gameObject;menu.resetConfirmation=Rect("Reset confirmation",canvas,0,0,1000,900).gameObject;
        Label(menu.home.transform,"POWER / MOVEMENT LAB",0,345,900,70,42);menu.homeStatus=Label(menu.home.transform,"",0,255,900,100,23);
        Button(menu,menu.home.transform,"start","START REPAIR",0,130);Button(menu,menu.home.transform,"redo","TUTORIAL / RECALIBRATE",0,35);Button(menu,menu.home.transform,"settings","SETTINGS",0,-60);Button(menu,menu.home.transform,"replay","LAST REPLAY",0,-155);
        Button(menu,menu.home.transform,"recenter","CENTER MENU",0,-245);
        Label(menu.home.transform,"Repair the paths. Charge the nodes. Restore ship power.\nFirst time? Start guides you through your personal calibration.",0,-340,900,80,21);
        Label(menu.settings.transform,"MISSION SETTINGS",0,365,900,60,38);Slider(menu,menu.settings.transform,"total",260,1,120,true);Toggle(menu,menu.settings.transform,"dev",0,170,350);
        string[] names={"SCAPTION","FORWARD REACH","OUTWARD ROTATION"};
        for(int i=0;i<3;i++){float y=65-i*110;Label(menu.settings.transform,names[i],-310,y,300,60,23);Toggle(menu,menu.settings.transform,"left:"+i,-50,y,155);Toggle(menu,menu.settings.transform,"right:"+i,125,y,155);Button(menu,menu.settings.transform,"exercise:"+i,"ADJUST",330,y,190);}
        Button(menu,menu.settings.transform,"reset","RESET SAVED SETTINGS",0,-245);Button(menu,menu.settings.transform,"home","BACK TO MENU",0,-335);
        menu.detailTitle=Label(menu.details.transform,"EXERCISE",0,370,950,60,34);menu.detailStatus=Label(menu.details.transform,"",0,300,950,85,19);
        Slider(menu,menu.details.transform,"limit",190,0,100,true);
        Slider(menu,menu.details.transform,"speedLeft",75,50,200,true);Slider(menu,menu.details.transform,"rangeLeft",-15,30,100,true);
        Slider(menu,menu.details.transform,"speedRight",-110,50,200,true);Slider(menu,menu.details.transform,"rangeRight",-200,30,100,true);Button(menu,menu.details.transform,"back","BACK TO SETTINGS",0,-315);
        Label(menu.resetConfirmation.transform,"RESET SAVED SETTINGS?",0,250,950,80,36);
        Label(menu.resetConfirmation.transform,"This resets calibration, learned speed/range,\nexercise choices, level length and dev overrides.\n\nYour recorded sessions and replays are kept.\nStart will guide you through calibration again.",0,50,900,240,26);
        Button(menu,menu.resetConfirmation.transform,"confirmReset","RESET SETTINGS",0,-150);Button(menu,menu.resetConfirmation.transform,"back","CANCEL",0,-250);
        menu.footer=Label(canvas,"",0,-415,950,55,20);
        var line=new GameObject("Menu pointer",typeof(LineRenderer));line.transform.SetParent(root.transform,false);menu.pointer=line.GetComponent<LineRenderer>();menu.pointer.positionCount=2;menu.pointer.useWorldSpace=true;menu.pointer.widthMultiplier=.003f;menu.pointer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/VR/Shield/Energy Pulse.mat");
        menu.settings.SetActive(false);menu.details.SetActive(false);menu.resetConfirmation.SetActive(false);menu.pointer.gameObject.SetActive(false);
        const string path="Assets/VR/Shield/Exercise Main Menu.prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
        const string gamePath="Assets/VR/Shield/Shield Exercise Game.prefab";var contents=PrefabUtility.LoadPrefabContents(gamePath);
        try{var game=contents.GetComponent<ShieldExerciseGame>();if(!game.menu)game.menu=((GameObject)PrefabUtility.InstantiatePrefab(prefab,contents.transform)).GetComponent<ExerciseGameMenu>();game.menu.game=game;PrefabUtility.RecordPrefabInstancePropertyModifications(game.menu);PrefabUtility.SaveAsPrefabAsset(contents,gamePath);}
        finally{PrefabUtility.UnloadPrefabContents(contents);}AssetDatabase.SaveAssets();
    }
}
