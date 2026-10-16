using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoExpansionBuilder
    {
        const string Folder="Assets/StormSim/Sibucao/Art/Expansion";
        static Material wood,steel,white,black,skin,cloth,green,clay;
        static Dictionary<Material,Material> imported=new Dictionary<Material,Material>();
        static int modelIndex;
        static Material Mat(string name,Color c)
        {string p=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.2f);AssetDatabase.CreateAsset(m,p);return m;}
        static Transform Root(string name,Transform parent,Vector3 p)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=p;return g.transform;}
        static GameObject Shape(string name,Transform parent,Vector3 p,Vector3 size,Material m,PrimitiveType type=PrimitiveType.Cube,bool collider=false)
        {var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static void Rod(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material m)
        {var g=Shape(name,parent,(a+b)*.5f,new Vector3(radius*2,(b-a).magnitude*.5f,radius*2),m,PrimitiveType.Cylinder);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Ring(string name,Transform parent,Vector3 center,float radius,Material material)
        {var g=Root(name,parent,center).gameObject;var l=g.AddComponent<LineRenderer>();l.sharedMaterial=material;l.useWorldSpace=false;l.loop=true;l.widthMultiplier=.008f;l.positionCount=48;for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;l.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));}}
        static void Init()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            wood=Mat("Weathered wood",new Color(.25f,.15f,.085f));steel=Mat("Aged galvanized steel",new Color(.3f,.34f,.36f));white=Mat("Worn enamel",new Color(.76f,.75f,.64f));black=Mat("Rubber and hair",new Color(.025f,.023f,.021f));green=Mat("Leaf and green plastic",new Color(.15f,.34f,.2f));clay=Mat("Terracotta",new Color(.48f,.24f,.13f));
        }
        static void Imported(string path,Transform parent,Vector3 position,float width,float yaw=0)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Organized Assets/"+path);if(!prefab)throw new Exception("Missing household model "+path);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(parent,false);g.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var rs=g.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);float factor=width/Mathf.Max(bounds.size.x,bounds.size.z);g.transform.localScale*=factor;
            bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);g.transform.position+=parent.TransformPoint(position)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var r in rs){var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];if(!source||source.shader.name.StartsWith("Universal Render Pipeline"))continue;
                if(!imported.TryGetValue(source,out var copy))
                {copy=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=source.name+" - Home"};if(source.HasProperty("_MainTex"))copy.SetTexture("_BaseMap",source.GetTexture("_MainTex"));if(source.HasProperty("_Color"))copy.SetColor("_BaseColor",source.GetColor("_Color"));copy.SetFloat("_Smoothness",.2f);copy.SetFloat("_Cull",0);AssetDatabase.CreateAsset(copy,Folder+"/Imported material "+modelIndex+++".mat");imported[source]=copy;}materials[i]=copy;
            }r.sharedMaterials=materials;}
        }
        static void Detail(SibucaoHomeOpening home,int variant)
        {
            var h=home.transform;var detail=Root("Crafted household and facade details",h,Vector3.zero);
            foreach(var t in h.GetComponentsInChildren<TextMesh>(true))if(t.name=="Family portrait caption")t.gameObject.SetActive(false);
            var picture=h.Find("Family picture");if(picture)
            {
                var p=Root("Framed family keepsake - modeled portrait",detail,new Vector3(-2.82f,2.1f,3.31f));
                for(int i=0;i<3;i++){float x=(i-1)*.15f;var tone=Mat("Portrait skin "+i,new Color(.5f+i*.05f,.3f+i*.035f,.18f+i*.03f));Shape("Portrait shoulder",p,new Vector3(x,-.09f,0),new Vector3(.17f,.2f,.012f),i==1?green:clay,PrimitiveType.Sphere);Shape("Portrait face",p,new Vector3(x,.04f,-.012f),new Vector3(.095f,.13f,.015f),tone,PrimitiveType.Sphere);Shape("Portrait hair",p,new Vector3(x,.09f,-.013f),new Vector3(.1f,.07f,.015f),black,PrimitiveType.Sphere);}
            }
            // Plaster edges, masonry courses and timber joints, not labels painted on cubes.
            var mortar=Mat("Exposed concrete block",new Color(.4f,.41f,.37f));
            for(int row=0;row<3;row++)for(int col=0;col<9-row*2;col++)Shape("Exposed hollow-block course",detail,new Vector3(-3.7f+col*.43f+(row%2)*.2f,.15f+row*.2f,-3.604f),new Vector3(.41f,.18f,.025f),mortar);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Timber corner post",detail,new Vector3(side*3.91f,1.5f,-3.61f),new Vector3(.12f,3,.12f),wood);
                Rod("Rain gutter",detail,new Vector3(side*4.24f,2.98f,-3.9f),new Vector3(side*4.24f,2.98f,3.9f),.055f,steel);
                Rod("Downpipe",detail,new Vector3(side*4.18f,2.98f,-3.65f),new Vector3(side*4.18f,.05f,-3.65f),.043f,steel);
                for(int j=0;j<4;j++)Shape("Pipe wall clamp",detail,new Vector3(side*4.18f,.35f+j*.7f,-3.66f),new Vector3(.13f,.035f,.12f),steel);
                for(int z=-2;z<=2;z+=2){Shape("Window trim top",detail,new Vector3(side*4.12f,2.42f,z),new Vector3(.14f,.1f,1.92f),wood);Shape("Window drip sill",detail,new Vector3(side*4.15f,1.09f,z),new Vector3(.32f,.12f,1.95f),wood);}
            }
            Shape("Electric meter backboard",detail,new Vector3(-3.45f,1.77f,-3.67f),new Vector3(.3f,.46f,.07f),wood);
            var meter=Shape("Electric meter glass housing",detail,new Vector3(-3.45f,1.84f,-3.74f),new Vector3(.2f,.1f,.2f),white,PrimitiveType.Cylinder);meter.transform.localRotation=Quaternion.Euler(90,0,0);
            Rod("Meter conduit",detail,new Vector3(-3.45f,1.98f,-3.7f),new Vector3(-3.45f,2.92f,-3.7f),.013f,black);
            for(int i=0;i<18;i++)Shape("Roof fastening",detail,new Vector3(-4+i*.46f,3.9f-Mathf.Abs(-4+i*.46f)*.176f,-3.82f),new Vector3(.036f,.027f,.036f),steel,PrimitiveType.Sphere);
            // Kitchen counter with real imported pots, dishes, and practical objects.
            Shape("Kitchen worktop",detail,new Vector3(-2.98f,.86f,1.24f),new Vector3(1.45f,.1f,1.65f),wood,collider:true);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Shape("Counter leg",detail,new Vector3(-2.98f+x*.6f,.43f,1.24f+z*.7f),new Vector3(.1f,.86f,.1f),wood);
            Imported("Kitchenware/Pots/Prefabs/Pot_1_1_Prefab.prefab",detail,new Vector3(-3.18f,.92f,1.62f),.4f);
            Imported("Kitchenware/Plates and Pans/Prefabs/Plate_Prefab.prefab",detail,new Vector3(-2.73f,.92f,.91f),.29f);
            Imported("Containers/Buckets/Prefabs/Bucket_Prefab.prefab",detail,new Vector3(-3.35f,.05f,2.65f),.47f);
            Imported("Nature/Potted Plants/Prefabs/pot plant.prefab",detail,new Vector3(-3.15f,-.3f,-4.28f),.56f);
            var cooker=Root("Rice cooker with lid handle and switch",detail,new Vector3(-2.63f,1.1f,1.65f));
            Shape("Cooker body",cooker,Vector3.zero,new Vector3(.28f,.18f,.28f),white,PrimitiveType.Cylinder);Shape("Rice cooker lid",cooker,new Vector3(0,.19f,0),new Vector3(.31f,.018f,.31f),steel,PrimitiveType.Cylinder);Shape("Lid handle",cooker,new Vector3(0,.24f,0),new Vector3(.11f,.05f,.04f),black);Shape("Warm cook switch",cooker,new Vector3(0,-.05f,-.145f),new Vector3(.07f,.08f,.015f),black);
            var broom=Root("Walis tambo beside front door",detail,new Vector3(1.35f,.08f,-3.07f));broom.localRotation=Quaternion.Euler(-8,0,9);Rod("Broom handle",broom,new Vector3(0,.32f,0),new Vector3(0,1.32f,0),.014f,wood);
            for(int j=0;j<24;j++)Rod("Broom palm fiber",broom,new Vector3(0,.42f,0),new Vector3((j-11.5f)*.012f,.025f,Mathf.Sin(j)*.025f),.004f,clay);
            // Oscillating fan: cage rings, radial spokes, hub and separate blade rotor.
            var fan=Root("Pedestal fan",detail,new Vector3(-3.3f,0,-1.7f));Shape("Fan foot",fan,new Vector3(0,.065f,0),new Vector3(.5f,.09f,.42f),green,PrimitiveType.Sphere);Rod("Fan pole",fan,new Vector3(0,.08f,0),new Vector3(0,1.1f,0),.023f,steel);
            for(int i=0;i<4;i++)Ring("Fan cage ring",fan,new Vector3(0,1.24f,-.05f),.09f+i*.055f,steel);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Rod("Fan cage spoke",fan,new Vector3(0,1.24f,-.05f),new Vector3(Mathf.Cos(a)*.26f,1.24f+Mathf.Sin(a)*.26f,-.05f),.0035f,steel);}
            var rotor=Root("Fan blade rotor",fan,new Vector3(0,1.24f,.01f));rotor.gameObject.AddComponent<SibucaoPropMotion>().rotationPerSecond=new Vector3(0,0,600);
            for(int i=0;i<3;i++){float a=i*120;var blade=Shape("Fan blade",rotor,new Vector3(Mathf.Cos(a*Mathf.Deg2Rad)*.1f,Mathf.Sin(a*Mathf.Deg2Rad)*.1f,0),new Vector3(.24f,.08f,.018f),green,PrimitiveType.Sphere);blade.transform.localRotation=Quaternion.Euler(0,0,a+22);}
            Shape("Fan hub",fan,new Vector3(0,1.24f,-.075f),new Vector3(.085f,.085f,.06f),green,PrimitiveType.Sphere);
            // Seams, straps and lids make the interactable supplies recognizable.
            var bottle=home.supplies[0];for(int i=0;i<4;i++)Ring("Bottle molded rib",bottle,new Vector3(0,-.1f+i*.065f,0),.121f,white);
            var bag=home.supplies[1];for(int i=-1;i<=1;i+=2)Rod("Bag stitched strap",bag,new Vector3(i*.13f,-.1f,-.215f),new Vector3(i*.13f,.26f,-.215f),.014f,black);
            Shape("Bag front zip pocket",bag,new Vector3(0,-.025f,-.212f),new Vector3(.29f,.16f,.025f),green);Shape("Zipper pull",bag,new Vector3(.1f,.08f,-.232f),new Vector3(.025f,.04f,.014f),steel);
            var docs=home.supplies[2];for(int i=0;i<5;i++)Shape("Paper edges in waterproof pouch",docs,new Vector3(0,.04f+i*.003f,0),new Vector3(.37f,.002f,.22f),white);
            // Each home gets a different facade palette without changing shared materials.
            var tint=Mat("Home facade "+variant,new[]{new Color(.55f,.62f,.49f),new Color(.57f,.5f,.38f),new Color(.39f,.53f,.55f),new Color(.67f,.61f,.48f)}[variant]);
            foreach(var r in h.GetComponentsInChildren<Renderer>())if(r.sharedMaterial&&r.sharedMaterial.name=="Warm worn plaster")r.sharedMaterial=tint;
            var eyes=GameObject.CreatePrimitive(PrimitiveType.Quad);eyes.name="Cinematic eyelids - stereo compatible";eyes.transform.SetParent(h,false);eyes.transform.localScale=new Vector3(.3f,.24f,1);UnityEngine.Object.DestroyImmediate(eyes.GetComponent<Collider>());
            var em=new Material(Shader.Find("StormSim/Sibucao Eyelids"));AssetDatabase.CreateAsset(em,Folder+"/Eyelids "+variant+".mat");home.eyelids=eyes.GetComponent<Renderer>();home.eyelids.sharedMaterial=em;home.eyelids.enabled=false;
        }
        public static string InstallHomes()
        {
            if(Application.isPlaying)throw new Exception("Stop Play first.");var s=UnityEngine.Object.FindAnyObjectByType<SibucaoSession>();if(s.gameObject.scene.name!="SIBUCAO_RAIN")throw new Exception("Sibucao only.");if(s.homes!=null&&s.homes.Length>1)return "Already expanded.";
            Init();var original=s.home;
            Vector3[] positions={original.transform.position,new Vector3(-50.387787f,-1.715255f,1140.41028f),new Vector3(-121.095993f,-.8985209f,993.8785f),new Vector3(-129.698425f,-4.133802f,819.1804f)};
            float[] angles={0,125.64518f,105.147438f,44.293148f};string[] names={"Northern courtyard","Road-bend household","Low-road family home","School-approach household"};
            var homes=new List<SibucaoHomeOpening>{original};for(int i=1;i<4;i++){var copy=UnityEngine.Object.Instantiate(original.gameObject);homes.Add(copy.GetComponent<SibucaoHomeOpening>());}
            for(int i=0;i<4;i++)
            {
                var h=homes[i];h.name="SIBUCAO Home "+(i+1)+" - "+names[i];h.locationName=names[i];h.transform.SetPositionAndRotation(positions[i],Quaternion.Euler(0,angles[i],0));h.preparationDuration=new[]{180f,150f,210f,120f}[i];h.floodRiseDuration=new[]{180f,150f,120f,200f}[i];h.initialWaterHeight=positions[i].y-.9f;h.finalWaterHeight=positions[i].y+.85f;
                h.floodSurface.SetPositionAndRotation(positions[i]+new Vector3(0,-.9f,0),Quaternion.identity);h.floodSurface.gameObject.SetActive(false);Detail(h,i);
                // A broad, solid sloping path joins the front steps to the existing road.
                Vector3 road=s.route.Sample(new[]{0f,131f,302f,501f}[i]);Vector3 door=h.transform.TransformPoint(new Vector3(2.5f,-.43f,-4.5f));Vector3 delta=road-door;
                var path=Shape("Walkable yard approach",h.transform,Vector3.zero,new Vector3(1.85f,.13f,delta.magnitude),Mat("Yard concrete",new Color(.36f,.35f,.3f)),collider:true);path.transform.position=(door+road)*.5f-Vector3.up*.07f;path.transform.rotation=Quaternion.LookRotation(delta);
            }
            s.homes=homes.ToArray();s.home=homes[0];EditorUtility.SetDirty(s);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "Four detailed homes with non-repeating starts, separate scenario timings, and walked yard approaches.";
        }
        static GameObject Profile(string name,Transform parent,Vector3 position,float[] ys,float[] xs,float[] zs,Material material)
        {
            var g=Root(name,parent,position).gameObject;var vertices=new List<Vector3>();var triangles=new List<int>();const int sides=16;
            for(int j=0;j<ys.Length;j++)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(a)*xs[j],ys[j],Mathf.Sin(a)*zs[j]));}
            for(int j=0;j<ys.Length-1;j++)for(int i=0;i<sides;i++){int a=j*sides+i,b=j*sides+(i+1)%sides,c=b+sides,d=a+sides;triangles.AddRange(new[]{a,c,b,a,d,c});}
            int bottom=vertices.Count;vertices.Add(new Vector3(0,ys[0],0));int top=vertices.Count;vertices.Add(new Vector3(0,ys[ys.Length-1],0));
            for(int i=0;i<sides;i++){triangles.AddRange(new[]{bottom,(i+1)%sides,i,top,(ys.Length-1)*sides+i,(ys.Length-1)*sides+(i+1)%sides});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Resident mesh "+modelIndex+++".asset");g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;
        }
        static Transform Bone(string name,Transform parent,float length,float thickness,Material material)
        {
            var b=Root(name,parent,Vector3.zero);
            Profile(name+" tapered form",b,Vector3.zero,new[]{-length,-length*.6f,-length*.18f,0},new[]{thickness*.39f,thickness*.45f,thickness*.51f,thickness*.46f},new[]{thickness*.38f,thickness*.42f,thickness*.46f,thickness*.43f},material);
            Shape(name+" joint",b,Vector3.zero,Vector3.one*thickness*.85f,material,PrimitiveType.Sphere);return b;
        }
        static SibucaoResident Person(int index,Transform parent,SibucaoRoute route)
        {
            var root=Root("Resident "+(index+1)+" - "+((ResidentBehavior)(index%4)),parent,Vector3.zero);var npc=root.gameObject.AddComponent<SibucaoResident>();npc.route=route;npc.variation=index;npc.behavior=(ResidentBehavior)(index%4);npc.initialDistance=18+index*24;npc.routeDistance=npc.initialDistance;npc.lane=(index%2==0?-1:1)*(.7f+index%3*.3f);npc.height=new[]{1.5f,1.62f,1.72f,1.79f,1.57f}[index%5];npc.build=new[]{.9f,1.05f,1.2f,.97f}[index%4];float sc=npc.height/1.7f;
            skin=Mat("Resident skin "+index%5,new[]{new Color(.55f,.35f,.22f),new Color(.64f,.43f,.29f),new Color(.43f,.27f,.17f),new Color(.59f,.4f,.28f),new Color(.69f,.49f,.34f)}[index%5]);
            cloth=Mat("Everyday clothing "+index%6,new[]{new Color(.24f,.38f,.49f),new Color(.58f,.23f,.16f),new Color(.48f,.49f,.35f),new Color(.63f,.55f,.4f),new Color(.26f,.38f,.32f),new Color(.5f,.36f,.45f)}[index%6]);
            var trousers=Mat("Trousers "+index%3,new[]{new Color(.12f,.17f,.2f),new Color(.27f,.25f,.21f),new Color(.19f,.22f,.28f)}[index%3]);
            npc.pelvis=Root("Pelvis",root,new Vector3(0,.9f*sc,0));Shape("Waist and hips",npc.pelvis,new Vector3(0,.01f,0),new Vector3(.31f*npc.build,.24f*sc,.21f),trousers,PrimitiveType.Sphere);
            npc.chest=Root("Breathing torso",npc.pelvis,new Vector3(0,.29f*sc,0));
            Profile("Tailored shirt torso",npc.chest,Vector3.zero,new[]{-.24f*sc,-.2f*sc,0,.19f*sc,.25f*sc},new[]{.17f*npc.build,.175f*npc.build,.19f*npc.build,.215f*npc.build,.12f*npc.build},new[]{.105f*npc.build,.115f*npc.build,.13f*npc.build,.115f*npc.build,.075f*npc.build},cloth);
            Shape("Chest pocket",npc.chest,new Vector3(.1f,.055f,.12f*npc.build),new Vector3(.075f,.085f,.012f),cloth);
            Rod("Shirt pocket seam",npc.chest,new Vector3(.06f,.098f,.135f*npc.build),new Vector3(.14f,.098f,.135f*npc.build),.003f,white);
            Shape("Neck",npc.chest,new Vector3(0,.31f*sc,0),new Vector3(.105f,.12f,.105f),skin,PrimitiveType.Cylinder);
            npc.head=Root("Head and gaze",npc.chest,new Vector3(0,.4f*sc,0));
            Shape("Face",npc.head,Vector3.zero,new Vector3(.19f,.25f,.2f),skin,PrimitiveType.Sphere);
            Shape("Hair cap",npc.head,new Vector3(0,.074f,-.012f),new Vector3(.203f,.14f,.207f),black,PrimitiveType.Sphere);
            if(index%3==1)Shape("Tied-back hair",npc.head,new Vector3(0,.01f,-.123f),new Vector3(.12f,.14f,.11f),black,PrimitiveType.Sphere);
            Shape("Nose",npc.head,new Vector3(0,-.012f,.104f),new Vector3(.037f,.05f,.04f),skin,PrimitiveType.Sphere);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Ear",npc.head,new Vector3(side*.098f,-.006f,0),new Vector3(.025f,.052f,.031f),skin,PrimitiveType.Sphere);
                Shape("Eye",npc.head,new Vector3(side*.042f,.027f,.09f),new Vector3(.035f,.014f,.015f),white,PrimitiveType.Sphere);Shape("Iris",npc.head,new Vector3(side*.042f,.027f,.099f),new Vector3(.012f,.012f,.007f),black,PrimitiveType.Sphere);
                Rod("Eyebrow",npc.head,new Vector3(side*.022f,.047f,.092f),new Vector3(side*.062f,.05f,.085f),.003f,black);
            }
            Rod("Mouth",npc.head,new Vector3(-.027f,-.057f,.091f),new Vector3(.027f,-.057f,.091f),.0027f,wood);
            npc.thighs=new Transform[2];npc.shins=new Transform[2];npc.feet=new Transform[2];npc.upperArms=new Transform[2];npc.forearms=new Transform[2];npc.hands=new Transform[2];
            for(int i=0;i<2;i++)
            {
                npc.thighs[i]=Bone("Thigh "+i,root,.43f*sc,.16f*npc.build,trousers);npc.shins[i]=Bone("Shin "+i,root,.43f*sc,.115f*npc.build,index%2==0?skin:trousers);
                npc.feet[i]=Root("Foot "+i,root,Vector3.zero);Shape("Sandal sole",npc.feet[i],new Vector3(0,-.035f,.055f),new Vector3(.11f,.034f,.25f),black,PrimitiveType.Sphere);Shape("Foot",npc.feet[i],new Vector3(0,-.015f,.047f),new Vector3(.097f,.055f,.2f),skin,PrimitiveType.Sphere);Rod("Sandal strap",npc.feet[i],new Vector3(-.052f,.01f,.04f),new Vector3(.052f,.01f,.1f),.012f,green);
                npc.upperArms[i]=Bone("Upper arm "+i,root,.29f*sc,.105f*npc.build,skin);Profile("Shirt sleeve",npc.upperArms[i],Vector3.zero,new[]{-.15f*sc,0},new[]{.061f*npc.build,.075f*npc.build},new[]{.06f,.072f},cloth);npc.forearms[i]=Bone("Forearm "+i,root,.26f*sc,.079f*npc.build,skin);
                npc.hands[i]=Root("Hand "+i,root,Vector3.zero);Shape("Palm",npc.hands[i],new Vector3(0,-.035f,0),new Vector3(.075f,.095f,.036f),skin,PrimitiveType.Sphere);for(int f=0;f<4;f++)Shape("Finger",npc.hands[i],new Vector3((f-1.5f)*.017f,-.091f,0),new Vector3(.014f,.065f,.014f),skin,PrimitiveType.Capsule);
            }
            if(index%3==0){Shape("Evacuation backpack",npc.chest,new Vector3(0,-.02f,-.19f),new Vector3(.28f,.35f,.13f),green,PrimitiveType.Sphere);for(int side=-1;side<=1;side+=2)Rod("Backpack strap",npc.chest,new Vector3(side*.14f,.18f,.11f),new Vector3(side*.13f,-.22f,.09f),.018f,black);}
            npc.ResetResident();return npc;
        }
        public static string InstallResidents()
        {
            if(GameObject.Find("Sibucao neighborhood residents"))return "Residents already installed.";Init();var s=UnityEngine.Object.FindAnyObjectByType<SibucaoSession>();var root=Root("Sibucao neighborhood residents",null,Vector3.zero);
            for(int i=0;i<28;i++){var npc=Person(i,root,s.route);npc.name="PLACEHOLDER_"+npc.name;npc.gameObject.AddComponent<PlaceholderCharacter>();if(i<4)PrefabUtility.SaveAsPrefabAsset(npc.gameObject,Folder+"/PLACEHOLDER_Resident_"+npc.behavior+".prefab");}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "28 articulated residents: walking, running, panicking then evacuating, and shelter-bound; five heights and four builds.";
        }
    }
}



