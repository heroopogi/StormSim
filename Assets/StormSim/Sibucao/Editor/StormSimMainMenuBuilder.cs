using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace StormSim.Sibucao.Editor
{
    public static class StormSimMainMenuBuilder
    {
        const string Folder="Assets/StormSim/Sibucao/UI/MainMenu/";
        static Color ColorOf(string c){ColorUtility.TryParseHtmlString(c,out var result);return result;}
        static Sprite SpriteOf(string name)
        {
            string p=Folder+name+".png";var i=(TextureImporter)AssetImporter.GetAtPath(p);i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.alphaIsTransparency=true;i.mipmapEnabled=false;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=2048;i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {var g=new GameObject(name,typeof(RectTransform));var r=(RectTransform)g.transform;r.SetParent(parent,false);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        static Text Label(string name,Transform parent,Vector2 pos,Vector2 size,string content,Font font,int pixels,Color color)
        {var t=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();t.font=font;t.fontSize=pixels;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.text=content;t.raycastTarget=false;return t;}
        public static string Install()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play mode first.");
            if(UnityEngine.Object.FindAnyObjectByType<StormSimMainMenu>())return "Main Menu already installed.";
            var inter=AssetDatabase.LoadAssetAtPath<Font>(Folder+"Inter.ttf");var sora=AssetDatabase.LoadAssetAtPath<Font>(Folder+"Sora.ttf");if(!inter||!sora)throw new System.Exception("Sora and Inter fonts must be imported first.");
            var r=Rect("STORMSIM - Main Menu",null,Vector2.zero,new Vector2(920,575));r.localScale=Vector3.one*.0025f;
            var canvas=r.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=20;
            r.gameObject.AddComponent<GraphicRaycaster>();r.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var controller=r.gameObject.AddComponent<StormSimMainMenu>();controller.canvas=canvas;controller.group=r.gameObject.AddComponent<CanvasGroup>();
            var bg=Rect("Void gradient - reference lens",r,Vector2.zero,new Vector2(920,575)).gameObject.AddComponent<Image>();bg.sprite=SpriteOf("Backdrop");bg.raycastTarget=false;
            var logo=Rect("Droplet - exact reference SVG",r,new Vector2(0,94),new Vector2(34,34)).gameObject.AddComponent<Image>();logo.sprite=SpriteOf("Logo");logo.color=ColorOf("#7FC2DC");logo.raycastTarget=false;
            Label("Sora wordmark",r,new Vector2(0,49),new Vector2(350,48),"STORMSIM",sora,30,ColorOf("#E7F0F2"));
            // Branding is the only permanent text; action labels appear solely on hover/gaze.
            var names=new[]{"Locations","Play","Settings"};controller.orbs=new StormSimMenuOrb[3];
            for(int n=0;n<3;n++)
            {
                bool primary=n==1;int d=primary?78:64;
                var b=Rect(names[n],r,new Vector2((n-1)*93,-51),new Vector2(d,d));var img=b.gameObject.AddComponent<Image>();img.sprite=SpriteOf(primary?"OrbPlay":"Orb");
                var button=b.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.targetGraphic=img;
                var icon=Rect("Exact SVG icon",b,Vector2.zero,Vector2.one*(primary?26:24)).gameObject.AddComponent<Image>();icon.sprite=SpriteOf(names[n]);icon.color=ColorOf(primary?"#0A1519":"#E7F0F2");icon.raycastTarget=false;
                var label=Label("Hover and gaze label",b,new Vector2(0,-d/2f-18),new Vector2(280,26),names[n],inter,11,ColorOf("#8FA9B0"));label.gameObject.SetActive(false);
                var orb=b.gameObject.AddComponent<StormSimMenuOrb>();orb.tooltip=label;orb.disc=img;orb.normal=img.sprite;orb.hover=SpriteOf("OrbHover");orb.primary=primary;controller.orbs[n]=orb;
                if(n==0)controller.locations=button;else if(n==1)controller.play=button;else controller.settings=button;
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(r.gameObject,Folder+"STORMSIM_MainMenu.prefab");
            UnityEngine.Object.DestroyImmediate(r.gameObject);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if(!UnityEngine.Object.FindAnyObjectByType<EventSystem>()){var es=new GameObject("STORMSIM XR UI EventSystem");es.AddComponent<EventSystem>();es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();}
            var s=UnityEngine.Object.FindAnyObjectByType<SibucaoSession>();if(s)s.GetComponent<SibucaoUI>().worldMenu=instance.GetComponent<StormSimMainMenu>();
            var cam=s?s.player.view:Camera.main;if(cam)instance.transform.SetPositionAndRotation(cam.transform.position+cam.transform.forward*1.8f,cam.transform.rotation);
            var scene=EditorSceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);return "Main Menu prefab installed: flat world-space uGUI at 1.8m, exact SVG icons, Sora/Inter.";
        }
    }
}

