using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoHomeBuilder
    {
        const string Folder="Assets/StormSim/Sibucao/Art/Home";
        static Transform root;
        static Material plaster,wood,tile,cream,teal,metal,red,mat,black;
        static Font font;
        static Material lettering;
        static int meshIndex;
        static Material M(string name,Color color,string shader="Universal Render Pipeline/Lit")
        {
            var m=new Material(Shader.Find(shader)){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.24f);
            AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
        }
        static Transform Child(string name,Transform parent,Vector3 p)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=p;return g.transform;}
        static GameObject Box(string name,Vector3 p,Vector3 size,Material material,Transform parent=null,bool solid=true,bool architecture=false)
        {
            GameObject g;
            if(architecture)
            {
                var pb=ShapeGenerator.GenerateCube(PivotLocation.Center,size);g=pb.gameObject;
                var mesh=g.GetComponent<MeshFilter>().sharedMesh;AssetDatabase.CreateAsset(mesh,Folder+"/House mesh "+meshIndex+++".asset");
                if(solid){var c=g.GetComponent<BoxCollider>();if(!c)c=g.AddComponent<BoxCollider>();c.size=size;}
            }
            else {g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.transform.localScale=size;if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
            g.name=name;g.transform.SetParent(parent?parent:root,false);g.transform.localPosition=p;g.GetComponent<Renderer>().sharedMaterial=material;return g;
        }
        static GameObject Round(string name,Vector3 p,Vector3 size,Material material,Transform parent=null)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent?parent:root,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;return g;
        }
        static TextMesh Text(string name,string text,Vector3 p,float size,Color color,Transform parent=null)
        {
            var g=Child(name,parent?parent:root,p).gameObject;var t=g.AddComponent<TextMesh>();t.font=font;t.fontSize=64;t.characterSize=size*.33f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.text=text;g.GetComponent<MeshRenderer>().sharedMaterial=lettering;return t;
        }
        static void Pattern(Material m,string name,bool woven)
        {
            var tex=new Texture2D(128,128);tex.name=name;tex.wrapMode=TextureWrapMode.Repeat;
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float v=woven ? ((x/8+y/8)%2==0?.85f:.55f)+(x%4==0||y%4==0?-.15f:0) : .65f+.18f*Mathf.PerlinNoise(x*.16f,y*.012f);
                tex.SetPixel(x,y,new Color(v,v,v));
            }
            tex.Apply();File.WriteAllBytes(Folder+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(Folder+"/"+name+".png");
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+name+".png"));m.SetTextureScale("_BaseMap",new Vector2(woven?4:2,woven?6:1));
        }
        public static string Install()
        {
            var scene=EditorSceneManager.GetActiveScene();if(Application.isPlaying||scene.path!="Assets/Scenes/SIBUCAO_RAIN.unity")throw new InvalidOperationException("Open SIBUCAO_RAIN in Edit mode.");
            var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();if(s.home)throw new InvalidOperationException("Home already installed; edit the existing scene objects.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();meshIndex=0;
            plaster=M("Warm worn plaster",new Color(.65f,.69f,.60f));wood=M("Dark narra wood",new Color(.42f,.23f,.11f));tile=M("Cream floor tiles",new Color(.62f,.63f,.55f));cream=M("Cotton curtains",new Color(.85f,.79f,.62f));teal=M("Painted teal trim",new Color(.09f,.31f,.30f));metal=M("Galvanized roof",new Color(.28f,.31f,.33f));red=M("Red household plastic",new Color(.6f,.13f,.08f));mat=M("Woven banig",new Color(.77f,.61f,.34f));black=M("Television cabinet",new Color(.018f,.022f,.025f));Pattern(wood,"Wood grain",false);Pattern(mat,"Banig weave",true);
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/StormSim/Sibucao/Art/Roboto-Regular.ttf");
            lettering=new Material(Shader.Find("StormSim/World Sign Text")){name="Home depth-tested text"};lettering.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(lettering,Folder+"/Home text.mat");
            root=new GameObject("SIBUCAO - enterable spawn home").transform;root.position=new Vector3(-97,-1.25f,1260);Undo.RegisterCreatedObjectUndo(root.gameObject,"Build Sibucao spawn home");
            var home=root.gameObject.AddComponent<SibucaoHomeOpening>();s.home=home;
            Box("Raised concrete foundation",new Vector3(0,-.3f,0),new Vector3(8.25f,.6f,7.25f),plaster,architecture:true);
            Box("Tiled floor",new Vector3(0,.02f,0),new Vector3(8,.04f,7),tile,architecture:true);
            for(int x=-4;x<=4;x++)Box("Tile grout",new Vector3(x,.044f,0),new Vector3(.012f,.003f,7),cream,solid:false);
            for(int z=-3;z<=3;z++)Box("Tile grout",new Vector3(0,.044f,z),new Vector3(8,.003f,.012f),cream,solid:false);
            Box("Back wall",new Vector3(0,1.5f,3.5f),new Vector3(8,.0f+3,.18f),plaster,architecture:true);
            // The doorway is actual empty geometry, not a painted opening.
            Box("Front wall left of doorway",new Vector3(-1.1f,1.5f,-3.5f),new Vector3(5.8f,3,.18f),plaster,architecture:true);
            Box("Front wall right of doorway",new Vector3(3.6f,1.5f,-3.5f),new Vector3(.8f,3,.18f),plaster,architecture:true);
            Box("Door lintel",new Vector3(2.5f,2.65f,-3.5f),new Vector3(1.4f,.7f,.18f),wood,architecture:true);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Window sill wall",new Vector3(side*4,.55f,0),new Vector3(.18f,1.1f,7),plaster,architecture:true);
                Box("Window header wall",new Vector3(side*4,2.7f,0),new Vector3(.18f,.6f,7),plaster,architecture:true);
                foreach(float z in new[]{-2.75f,2.75f})Box("Window side pier",new Vector3(side*4,1.75f,z),new Vector3(.18f,1.3f,1.5f),plaster,architecture:true);
                for(int z=-2;z<=2;z+=2)Box("Window upright",new Vector3(side*4,1.75f,z),new Vector3(.22f,1.3f,.075f),wood);
                for(int j=0;j<5;j++)Box("Window jalousie slat",new Vector3(side*4,1.22f+j*.24f,0),new Vector3(.07f,.07f,4),teal).transform.localRotation=Quaternion.Euler(0,0,20);
            }
            Box("Ceiling",new Vector3(0,3.06f,0),new Vector3(8.4f,.12f,7.4f),cream,architecture:true);
            for(int x=-3;x<=3;x+=2)Box("Exposed ceiling joist",new Vector3(x,2.96f,0),new Vector3(.12f,.16f,7),wood);
            for(int side=-1;side<=1;side+=2)
            {
                var roof=Box("Sloped corrugated metal roof",new Vector3(side*2.18f,3.45f,0),new Vector3(4.6f,.12f,8),metal,architecture:true);roof.transform.localRotation=Quaternion.Euler(0,0,-side*10);
                for(int j=0;j<22;j++)Box("Roof corrugation",new Vector3(-4.3f+j*.41f,3.84f-Mathf.Abs(-4.3f+j*.41f)*.176f,0),new Vector3(.045f,.04f,8),metal,solid:false);
            }
            home.doorHinge=Child("Front door hinge",root,new Vector3(1.8f,0,-3.5f));
            home.doorPanel=Box("Front door - E to open",new Vector3(.68f,1.13f,0),new Vector3(1.36f,2.26f,.09f),wood,home.doorHinge).transform;
            for(int i=0;i<4;i++)Box("Raised door panel",new Vector3(0,-.7f+i*.47f,-.06f),new Vector3(.99f,.35f,.04f),teal,home.doorPanel,false);
            Box("Door handle",new Vector3(.49f,0,.09f),new Vector3(.05f,.22f,.05f),metal,home.doorPanel,false);
            for(int i=0;i<3;i++)Box("Front step "+i,new Vector3(2.5f,-.10f-i*.16f,-3.85f-i*.34f),new Vector3(1.7f,.2f,.4f),plaster,architecture:true);
            Text("House number","24\nSIBUCAO",new Vector3(3.6f,1.9f,-3.61f),.06f,Color.white);
            // Wooden daybed with a woven mat, pillow and visible covered legs.
            Box("Daybed frame",new Vector3(-1.4f,.48f,-1.3f),new Vector3(1.65f,.17f,2.75f),wood);
            Box("Banig on daybed",new Vector3(-1.4f,.59f,-1.3f),new Vector3(1.53f,.07f,2.64f),mat);
            Box("Cotton pillow",new Vector3(-1.4f,.71f,-2.28f),new Vector3(1.05f,.19f,.48f),cream);
            for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)Box("Daybed leg",new Vector3(-1.4f+side*.68f,.24f,-1.3f+end*1.2f),new Vector3(.1f,.48f,.1f),wood);
            Box("Daybed headboard",new Vector3(-1.4f,.89f,-2.68f),new Vector3(1.72f,.68f,.12f),wood);
            Box("Folded woven blanket",new Vector3(-1.4f,.68f,-.48f),new Vector3(1.3f,.12f,.7f),teal);
            home.lyingView=Child("Lying eye position - watch TV",root,new Vector3(-1.4f,1.04f,-2.25f));home.lyingView.LookAt(root.TransformPoint(new Vector3(-.85f,1.65f,2.96f)));home.lyingView.Rotate(0,0,-5,Space.Self);
            home.standingPoint=Child("Stand up beside daybed",root,new Vector3(.25f,.07f,-1.6f));
            Box("TV wooden cabinet",new Vector3(-.85f,.46f,2.91f),new Vector3(2.55f,.9f,.64f),wood);
            for(int side=-1;side<=1;side+=2)Box("Cabinet panel",new Vector3(-.85f+side*.62f,.5f,2.57f),new Vector3(1.05f,.57f,.035f),teal);
            Box("Television body",new Vector3(-.85f,1.69f,2.91f),new Vector3(2.24f,1.35f,.2f),black);
            var screen=GameObject.CreatePrimitive(PrimitiveType.Quad);screen.name="TV screen - replaceable news video";screen.transform.SetParent(root,false);screen.transform.localPosition=new Vector3(-.85f,1.71f,2.795f);screen.transform.localScale=new Vector3(2.1f,1.18f,1);UnityEngine.Object.DestroyImmediate(screen.GetComponent<Collider>());
            var tvmat=M("TV signal",Color.white,"StormSim/Sibucao Home Television");home.screen=screen.GetComponent<Renderer>();home.screen.sharedMaterial=tvmat;
            home.television=screen.AddComponent<VideoPlayer>();home.television.playOnAwake=false;
            home.television.audioOutputMode=VideoAudioOutputMode.AudioSource;home.television.controlledAudioTrackCount=1;
            var audio=screen.AddComponent<AudioSource>();audio.spatialBlend=1;audio.minDistance=2;audio.maxDistance=15;audio.volume=.85f;audio.playOnAwake=false;home.television.SetTargetAudioSource(0,audio);
            home.fallbackNews=Child("Fallback local news bulletin",root,new Vector3(-.85f,1.71f,2.78f)).gameObject;
            Text("Station","STORMSIM  /  WEATHER WATCH",new Vector3(0,.43f,0),.044f,new Color(.4f,.8f,.92f),home.fallbackNews.transform);
            home.newsText=Text("Bulletin","TYPHOON APPROACHING\nHeavy rain and strong winds expected",Vector3.zero,.052f,Color.white,home.fallbackNews.transform);
            Text("Ticker","SIBUCAO  •  PREPARE EARLY  •  STAY ALERT",new Vector3(0,-.44f,0),.035f,new Color(1,.7f,.3f),home.fallbackNews.transform);
            home.televisionGlow=Child("TV blue glow",root,new Vector3(-.85f,1.8f,2.4f)).gameObject.AddComponent<Light>();home.televisionGlow.type=LightType.Point;home.televisionGlow.range=5;home.televisionGlow.color=new Color(.3f,.65f,1);home.televisionGlow.intensity=.7f;
            // Familiar local household details, all individually editable.
            Box("Wall calendar backing",new Vector3(2.25f,2.02f,3.38f),new Vector3(.66f,.87f,.03f),cream,root,false);
            // Printed page mesh is installed by SibucaoWaterUpgrade; never substitute a TextMesh calendar.
            Box("Family picture frame",new Vector3(-2.82f,2.11f,3.36f),new Vector3(.7f,.58f,.05f),wood,solid:false);
            Box("Family picture",new Vector3(-2.82f,2.11f,3.325f),new Vector3(.59f,.47f,.01f),teal,solid:false);
            Text("Family portrait caption","BAHAY\nPAMILYA",new Vector3(-2.82f,2.1f,3.31f),.065f,Color.white);
            var cross=Child("Wooden wall cross",root,new Vector3(-.85f,2.69f,3.32f));Box("Cross upright",Vector3.zero,new Vector3(.06f,.36f,.04f),wood,cross,false);Box("Cross arms",new Vector3(0,.05f,0),new Vector3(.24f,.06f,.04f),wood,cross,false);
            var curtains=Child("Window curtain pivot",root,new Vector3(-3.85f,2.43f,.9f));home.curtain=curtains;
            for(int i=0;i<9;i++)Box("Curtain fold",new Vector3(i%2*.035f,-.6f,(i-4)*.1f),new Vector3(.05f,1.2f,.11f),cream,curtains,false);
            Box("Dining table top",new Vector3(2.9f,.88f,1.05f),new Vector3(1.45f,.1f,1.5f),wood);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Table leg",new Vector3(2.9f+x*.57f,.43f,1.05f+z*.59f),new Vector3(.09f,.86f,.09f),wood);
            var water=Child("Drinking water - pack with E",root,new Vector3(2.65f,1.14f,.77f));Round("Water container",Vector3.zero,new Vector3(.24f,.2f,.24f),teal,water);Round("Bottle cap",new Vector3(0,.23f,0),new Vector3(.11f,.035f,.11f),cream,water);
            var food=Child("Food and flashlight - pack with E",root,new Vector3(3.18f,1.08f,1.28f));Box("Emergency bag",Vector3.zero,new Vector3(.4f,.3f,.4f),red,food);Round("Flashlight",new Vector3(-.27f,-.08f,0),new Vector3(.07f,.17f,.07f),black,food).transform.localRotation=Quaternion.Euler(90,0,0);
            var docs=Child("Document pouch - pack with E",root,new Vector3(.02f,.97f,2.85f));Box("Waterproof document pouch",Vector3.zero,new Vector3(.43f,.07f,.28f),cream,docs);
            home.supplies=new[]{water,food,docs};
            // Monobloc chair and a rice pail beside the dining corner.
            Box("Monobloc seat",new Vector3(2.9f,.48f,-.28f),new Vector3(.58f,.07f,.55f),red);
            Box("Monobloc back",new Vector3(2.9f,.78f,-.52f),new Vector3(.58f,.6f,.055f),red);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Chair leg",new Vector3(2.9f+x*.23f,.24f,-.28f+z*.22f),new Vector3(.065f,.48f,.065f),red);
            Round("Rice storage pail",new Vector3(3.3f,.28f,2.7f),new Vector3(.5f,.28f,.5f),cream);Round("Rice pail lid",new Vector3(3.3f,.58f,2.7f),new Vector3(.54f,.025f,.54f),teal);
            var bulb=Child("Warm household ceiling bulb",root,new Vector3(.7f,2.75f,0)).gameObject.AddComponent<Light>();bulb.type=LightType.Point;bulb.color=new Color(1,.78f,.48f);bulb.range=9;bulb.intensity=1.5f;bulb.shadows=LightShadows.Soft;
            Box("Door mat",new Vector3(2.5f,.06f,-2.93f),new Vector3(1.25f,.025f,.62f),mat,solid:false);
            home.wind=Child("Typhoon wind",root,Vector3.zero).gameObject.AddComponent<WindZone>();home.wind.mode=WindZoneMode.Directional;home.wind.windMain=1.8f;home.wind.windTurbulence=1;home.wind.windPulseMagnitude=.6f;home.wind.windPulseFrequency=.5f;
            var flood=GameObject.CreatePrimitive(PrimitiveType.Plane);flood.name="Delayed neighborhood floodwater";flood.transform.SetParent(root,false);flood.transform.position=new Vector3(-97,home.initialWaterHeight,1235);flood.transform.localScale=new Vector3(home.floodSize.x/10,1,home.floodSize.y/10);UnityEngine.Object.DestroyImmediate(flood.GetComponent<Collider>());
            var fm=new Material(Shader.Find("StormSim/FloodTest/Depth Water")){name="Sibucao sediment floodwater"};AssetDatabase.CreateAsset(fm,Folder+"/Floodwater.mat");flood.GetComponent<Renderer>().sharedMaterial=fm;home.floodSurface=flood.transform;flood.SetActive(false);
            var cameraData=s.player.view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();cameraData.requiresDepthTexture=true;cameraData.requiresColorTexture=true;
            var collision=s.weather.rain.collision;collision.quality=ParticleSystemCollisionQuality.High;collision.collidesWith=1;collision.enableDynamicColliders=true;
            s.player.Teleport(home.standingPoint.position,home.standingPoint.forward);s.player.view.transform.localPosition=new Vector3(0,1.63f,0);
            EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return "Installed furnished enterable spawn home, TV, 60-second intro, 180-second preparation, and delayed flood in SIBUCAO_RAIN.";
        }
    }
}
