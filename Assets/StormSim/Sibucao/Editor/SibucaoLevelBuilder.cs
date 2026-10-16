using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoLevelBuilder
    {
        public const string Base="Assets/StormSim/Sibucao";
        public const string ScenePath="Assets/Scenes/SIBUCAO_RAIN.unity";
        static Material orange,clothes,skin,metal,rainMaterial;
        static GameObject root;
        static T Asset<T>(T asset,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old){EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);return old;}
            AssetDatabase.CreateAsset(asset,path);return asset;
        }
        static GameObject Child(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
        static Material Mat(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.2f);return Asset(m,Base+"/Art/"+name+".mat");
        }
        static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material,bool collider=true)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.layer=2;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        [MenuItem("Tools/StormSim/Build Sibucao Rain Prototype")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before building.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("SIBUCAO_RAIN already exists. Inspect it instead of overwriting.");
            var source=EditorSceneManager.GetActiveScene();
            if(source.path!="Assets/Scenes/MAIN_GAME.unity")throw new InvalidOperationException("The confirmed MAIN_GAME must be active.");
            if(!EditorSceneManager.SaveScene(source,ScenePath,true))throw new IOException("Could not copy the source scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.gameObject.SetActive(false);
            foreach(var a in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include,FindObjectsSortMode.None))a.enabled=false;
            foreach(var a in UnityEngine.Object.FindObjectsByType<BanagoReviewAtmosphere>(FindObjectsInactive.Include,FindObjectsSortMode.None))a.enabled=false;
            foreach(var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {if(b && (b.GetType().Name=="BanagoSeaWaves"||b.GetType().Name=="RisingWater"||b.GetType().Name=="TsunamiScenario"||b.GetType().Name=="FloodDetector"))b.enabled=false;}
            foreach(var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional)l.enabled=false;
            Directory.CreateDirectory(Base+"/Prefabs");AssetDatabase.Refresh();
            orange=Mat("Prototype safety vest",new Color(1,.541f,.239f));clothes=Mat("Prototype clothing",new Color(.18f,.25f,.3f));skin=Mat("Prototype hands and face",new Color(.6f,.42f,.29f));metal=Mat("Prototype storm debris",new Color(.32f,.35f,.38f));
            root=new GameObject("SIBUCAO RAIN - standalone gameplay");
            var session=root.AddComponent<SibucaoSession>();root.AddComponent<FloodSimulationManager>();
            var ui=root.AddComponent<SibucaoUI>();ui.roboto=AssetDatabase.LoadAssetAtPath<Font>(Base+"/Art/Roboto-Regular.ttf");ui.robotoBold=AssetDatabase.LoadAssetAtPath<Font>(Base+"/Art/Roboto-Bold.ttf");
            if(!ui.roboto||!ui.robotoBold)throw new Exception("Roboto fonts missing.");
            var route=ScriptableObject.CreateInstance<SibucaoRoute>();var p=JsonConvert.DeserializeObject<float[][]>(File.ReadAllText(Base+"/Data/route-source.json"));route.points=p.Select(v=>new Vector3(v[0],v[1],v[2])).ToArray();
            for(int i=1;i<route.points.Length;i++)route.length+=Vector3.Distance(route.points[i-1],route.points[i]);
            route.spawnDistances=new float[]{0,8,16,24,32};route.spawnPoints=route.spawnDistances.Select(d=>route.Sample(d)+Vector3.up*.15f).ToArray();session.route=Asset(route,Base+"/Data/SibucaoRoute.asset");
            var spawns=Child("Northern spawn candidates",root.transform);
            for(int i=0;i<route.spawnPoints.Length;i++){var s=Child("Spawn "+(i+1)+" - "+Mathf.RoundToInt(route.length-route.spawnDistances[i])+"m to school",spawns.transform);s.transform.position=route.spawnPoints[i];}
            var player=Child("First Person Resident",root.transform);var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.27f;cc.center=new Vector3(0,.9f,0);cc.stepOffset=.35f;cc.slopeLimit=55;cc.skinWidth=.04f;
            var fp=player.AddComponent<SibucaoPlayer>();session.player=fp;player.transform.position=route.spawnPoints[0];
            var camera=Child("Resident Camera",player.transform);camera.tag="MainCamera";camera.transform.localPosition=new Vector3(0,1.63f,0);fp.view=camera.AddComponent<Camera>();fp.view.nearClipPlane=.025f;fp.view.farClipPlane=700;fp.view.fieldOfView=72;camera.AddComponent<AudioListener>();camera.AddComponent<UniversalAdditionalCameraData>();
            BuildHands(fp);
            session.weather=BuildWeather(fp);
            var nodes=BuildDecisions();session.opening=nodes[0];session.arrival=nodes[8];
            var encounters=Child("Route encounters",root.transform);var list=new List<SibucaoEncounter>();
            float[] distances={75,163,252,357,454,556,659};
            for(int i=1;i<=7;i++)
            {
                var g=Child(i.ToString("00")+" - "+nodes[i].title,encounters.transform);g.layer=2;g.transform.position=route.Sample(distances[i-1]);
                Vector3 direction=route.Sample(distances[i-1]+3)-route.Sample(distances[i-1]);direction.y=0;g.transform.rotation=Quaternion.LookRotation(direction);
                var e=g.AddComponent<SibucaoEncounter>();e.node=nodes[i];e.index=i;e.routeDistance=distances[i-1];
                var trigger=g.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,1.5f,0);trigger.size=new Vector3(15,5,16);
                var prefab=BuildPlaceholder(i);var actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab);actor.transform.SetParent(g.transform,false);e.actor=actor.transform;
                list.Add(e);
            }
            var arrival=Child("09 - Arrival - school campus trigger",encounters.transform);arrival.layer=2;arrival.transform.position=route.points[route.points.Length-1];var win=arrival.AddComponent<SibucaoEncounter>();win.index=8;win.arrival=true;win.node=nodes[8];win.routeDistance=route.length;
            var volume=arrival.AddComponent<BoxCollider>();volume.isTrigger=true;volume.center=new Vector3(0,2,0);volume.size=new Vector3(15,7,14);list.Add(win);session.encounters=list.ToArray();
            // Correctly grounded helper volumes only; no environment mesh/material edits.
            Physics.SyncTransforms();
            foreach(var e in list)if(!e.arrival)
            {
                if(Physics.Raycast(e.transform.position+Vector3.up*2,Vector3.down,out var hit,5,1,QueryTriggerInteraction.Ignore))e.transform.position=new Vector3(e.transform.position.x,hit.point.y+.025f,e.transform.position.z);
            }
            var oldScenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList();oldScenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=oldScenes.ToArray();
            var xr=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if(xr){xr.InitManagerOnStart=false;EditorUtility.SetDirty(xr);}
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return "Built "+ScenePath+"; "+route.length.ToString("F1")+"m route, five spawns, seven decisions, school arrival. MAIN_GAME unchanged.";
        }
        static SibucaoWeather BuildWeather(SibucaoPlayer fp)
        {
            var g=Child("Rain wind and distant thunder",root.transform);var w=g.AddComponent<SibucaoWeather>();w.player=fp.transform;
            var sun=Child("Storm directional light",g.transform);sun.transform.rotation=Quaternion.Euler(55,135,0);w.sun=sun.AddComponent<Light>();w.sun.type=LightType.Directional;w.sun.intensity=.7f;w.sun.shadows=LightShadows.Soft;
            var sky=new Material(Shader.Find("StormSim/Sibucao Overcast Sky")){name="Sibucao overcast sky"};w.stormSky=Asset(sky,Base+"/Art/Overcast.mat");RenderSettings.skybox=w.stormSky;
            var rg=Child("World rain particles",g.transform);rg.transform.position=fp.transform.position+Vector3.up*10;
            w.rain=rg.AddComponent<ParticleSystem>();w.rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=w.rain.main;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=1.4f;main.startSpeed=0;main.startSize=.018f;main.startColor=new Color(.63f,.75f,.88f,.42f);main.maxParticles=5000;main.prewarm=true;
            var shape=w.rain.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(28,.2f,28);
            var velocity=w.rain.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=2.8f;velocity.y=-17;velocity.z=1;
            var emission=w.rain.emission;emission.rateOverTime=1500;
            var collision=w.rain.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;collision.quality=ParticleSystemCollisionQuality.Low;collision.collidesWith=1;collision.lifetimeLoss=1;collision.maxCollisionShapes=64;
            rainMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name="URP rain streak"};rainMaterial.SetColor("_BaseColor",new Color(.65f,.75f,.85f,.4f));rainMaterial.SetFloat("_Surface",1);rainMaterial.SetFloat("_Blend",0);rainMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);rainMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);rainMaterial.SetFloat("_ZWrite",0);rainMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");rainMaterial.renderQueue=3000;rainMaterial=Asset(rainMaterial,Base+"/Art/URP Rain.mat");
            var render=rg.GetComponent<ParticleSystemRenderer>();render.sharedMaterial=rainMaterial;render.renderMode=ParticleSystemRenderMode.Stretch;render.lengthScale=4;render.velocityScale=.025f;render.shadowCastingMode=ShadowCastingMode.Off;w.rain.Play();
            w.rainAudio=Audio(g.transform,"Rain loop","Assets/Organized Assets/Audio/Rain/rain_heavy.ogg",true);
            w.windAudio=Audio(g.transform,"Wind loop","Assets/Organized Assets/Audio/Wind/storm-wind-loop.wav",true);
            w.thunderAudio=Audio(g.transform,"Thunder",null,false);
            w.thunder=new[]{"tenkoku_thunder_distant01.wav","tenkoku_thunder_distant02.wav","tenkoku_thunder_medium01.wav"}.Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Organized Assets/Audio/Thunder/"+n)).Where(c=>c).ToArray();return w;
        }
        static AudioSource Audio(Transform parent,string name,string path,bool loop)
        {
            var a=Child(name,parent).AddComponent<AudioSource>();a.clip=path==null?null:AssetDatabase.LoadAssetAtPath<AudioClip>(path);a.loop=loop;a.playOnAwake=loop;a.spatialBlend=0;a.volume=.4f;return a;
        }
        static GameObject BuildPlaceholder(int index)
        {
            var g=new GameObject("Encounter placeholder "+index);g.layer=2;
            if(index<=2)
            {
                if(index==1)
                {
                    Primitive("Wind-blown branch",PrimitiveType.Cylinder,g.transform,new Vector3(1,.25f,0),new Vector3(.18f,1.7f,.18f),metal).transform.localRotation=Quaternion.Euler(0,25,90);
                    Primitive("Loose GI sheet",PrimitiveType.Cube,g.transform,new Vector3(2,.12f,.8f),new Vector3(1.3f,.07f,1.6f),metal).transform.localRotation=Quaternion.Euler(4,25,3);
                }
                else
                {
                    for(int j=0;j<5;j++)Primitive("Downed cable section "+j,PrimitiveType.Cylinder,g.transform,new Vector3(-1+j*.7f,.05f,Mathf.Sin(j)*.12f),new Vector3(.035f,.42f,.035f),clothes).transform.localRotation=Quaternion.Euler(0,0,90);
                    Primitive("Fallen insulator",PrimitiveType.Cylinder,g.transform,new Vector3(2,.15f,0),new Vector3(.2f,.16f,.2f),metal);
                }
            }
            else if(index==3)
            {
                Primitive("Go-bag",PrimitiveType.Cube,g.transform,new Vector3(2,.35f,0),new Vector3(.45f,.6f,.25f),clothes);
                Primitive("Broken strap",PrimitiveType.Cube,g.transform,new Vector3(2,.7f,.06f),new Vector3(.035f,.25f,.025f),orange).transform.localRotation=Quaternion.Euler(0,0,30);
            }
            else
            {
                Primitive("Resident torso",PrimitiveType.Capsule,g.transform,new Vector3(2,1.02f,0),new Vector3(.48f,.53f,.3f),index==6?orange:clothes);
                Primitive("Resident head",PrimitiveType.Sphere,g.transform,new Vector3(2,1.72f,0),new Vector3(.3f,.34f,.3f),skin);
                for(int j=-1;j<=1;j+=2){Primitive("Leg",PrimitiveType.Capsule,g.transform,new Vector3(2+j*.13f,.37f,0),new Vector3(.16f,.39f,.16f),clothes);Primitive("Arm",PrimitiveType.Capsule,g.transform,new Vector3(2+j*.33f,1.05f,0),new Vector3(.12f,.33f,.12f),skin);}
                if(index==4)Primitive("Loose shutter",PrimitiveType.Cube,g.transform,new Vector3(2,1,.5f),new Vector3(.8f,.8f,.08f),metal);
                if(index==6){Primitive("Checkpoint barrier",PrimitiveType.Cube,g.transform,new Vector3(-1,.8f,.9f),new Vector3(1.8f,.12f,.15f),orange);Primitive("Barrier support",PrimitiveType.Cube,g.transform,new Vector3(-1,.4f,.9f),new Vector3(.1f,.8f,.1f),metal);}
                if(index==7)Primitive("Arm dressing",PrimitiveType.Cube,g.transform,new Vector3(2.33f,1.04f,-.02f),new Vector3(.14f,.17f,.16f),orange,false);
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(g,Base+"/Prefabs/Encounter "+index+".prefab");UnityEngine.Object.DestroyImmediate(g);return prefab;
        }
        static void BuildHands(SibucaoPlayer fp)
        {
            var hands=Child("Rigged first-person hands",fp.view.transform);hands.transform.localPosition=new Vector3(0,-.25f,.44f);fp.hands=hands.transform;
            var fingers=new List<Transform>();
            for(int side=-1;side<=1;side+=2)
            {
                var hand=Child(side<0?"Left hand":"Right hand",hands.transform);hand.transform.localPosition=new Vector3(side*.22f,0,0);hand.transform.localRotation=Quaternion.Euler(-12,-side*12,side*8);
                var palm=Child("Wrist",hand.transform);var bones=new List<Transform>{palm.transform};var positions=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();var weights=new List<BoneWeight>();
                AddBox(Vector3.zero,new Vector3(.085f,.038f,.115f),0,positions,normals,triangles,weights);
                AddBox(new Vector3(0,0,-.11f),new Vector3(.06f,.045f,.12f),0,positions,normals,triangles,weights);
                for(int f=0;f<5;f++)
                {
                    var finger=Child("Finger "+f,palm.transform);finger.transform.localPosition=f==0?new Vector3(side*.05f,0,-.015f):new Vector3((f-2.5f)*.023f,0,.055f);
                    if(f==0)finger.transform.localRotation=Quaternion.Euler(0,side*45,0);
                    bones.Add(finger.transform);fingers.Add(finger.transform);
                    float len=f==0?.055f:.072f-Mathf.Abs(f-2)*.009f;
                    AddBox(finger.transform.localPosition+new Vector3(0,0,len*.5f),new Vector3(.017f,.025f,len),bones.Count-1,positions,normals,triangles,weights);
                }
                var mesh=new Mesh{name="Rigged low-poly "+hand.name};mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*hand.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();mesh=Asset(mesh,Base+"/Art/"+hand.name+".asset");
                var skinRenderer=hand.AddComponent<SkinnedMeshRenderer>();skinRenderer.sharedMesh=mesh;skinRenderer.bones=bones.ToArray();skinRenderer.rootBone=palm.transform;skinRenderer.sharedMaterial=skin;skinRenderer.shadowCastingMode=ShadowCastingMode.Off;skinRenderer.updateWhenOffscreen=true;
            }
            fp.fingerBones=fingers.ToArray();PrefabUtility.SaveAsPrefabAsset(hands,Base+"/Prefabs/First Person Hands.prefab");
        }
        static void AddBox(Vector3 center,Vector3 size,int bone,List<Vector3> positions,List<Vector3> normals,List<int> triangles,List<BoneWeight> weights)
        {
            Vector3[] ns={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var n in ns)
            {
                Vector3 u=Vector3.Cross(n,Mathf.Abs(n.y)>.5f?Vector3.forward:Vector3.up),v=Vector3.Cross(n,u);int start=positions.Count;
                foreach(var uv in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){positions.Add(center+Vector3.Scale(n+u*uv.x+v*uv.y,size)*.5f);normals.Add(n);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
        }
        static DecisionChoice Choice(string label,int points,float chance,float time,string outcome,string failure="",int resource=0,int teamwork=0,int evacuation=0,bool delayed=false,bool kit=false,bool companion=false,float speed=1)
        {
            var c=ScriptableObject.CreateInstance<DecisionChoice>();c.label=label;c.basePoints=points;c.baseSuccessChance=chance;c.timeCost=time;c.outcome=outcome;c.failureOutcome=failure;c.resourcePoints=resource;c.teamworkPoints=teamwork;c.evacuationPoints=evacuation;c.delayedConsequenceFlag=delayed;c.consumesFirstAid=kit;c.recruitCompanion=companion;c.movementMultiplier=speed;return c;
        }
        static DecisionNode Node(int index,string title,string speaker,string message,DecisionCategory category,bool major,params DecisionChoice[] choices)
        {
            var n=ScriptableObject.CreateInstance<DecisionNode>();n.id="sibucao-"+index;n.title=title;n.speaker=speaker;n.message=message;n.category=category;n.major=major;n.automatic=index==0||index==8;
            for(int i=0;i<choices.Length;i++){choices[i].category=category;choices[i]=Asset(choices[i],Base+"/Data/"+index+" Choice "+i+".asset");}
            n.choices=choices;return Asset(n,Base+"/Data/"+index+" "+title+".asset");
        }
        static DecisionNode[] BuildDecisions()
        {
            return new[]{
                Node(0,"Storm Intensifying","Resident","The wind is picking up. Rain is coming down hard. I need to reach the school.",DecisionCategory.Environmental,false),
                Node(1,"Debris in the Road","Resident","Branches and loose roofing cover part of the street. There is space around the obstruction.",DecisionCategory.RouteNavigation,false,
                    Choice("Go around the debris",20,1,20,"I took the clear edge of the road.",evacuation:10),
                    Choice("Push through the debris",-10,.45f,3,"I made it through the debris.","A sharp edge caught my leg as I passed.",delayed:true),
                    Choice("Take another street",15,1,50,"The longer detour brings me back to the route.",evacuation:8)),
                Node(2,"Downed Power Line","Resident","A fallen cable lies across the wet road. I cannot tell whether it is energized.",DecisionCategory.ElectricalFire,false,
                    Choice("Detour around this section of road",20,1,35,"I kept clear of the cable and rejoined the route.",evacuation:10),
                    Choice("Pass at a wide distance",8,.85f,18,"I kept my distance from the line.","I had to retreat when I got closer to the cable."),
                    Choice("Step over the cable",-15,.25f,3,"I crossed the cable without an immediate incident.","I stumbled near the line and had to back away.",delayed:true)),
                Node(3,"Go-Bag Strap Breaks","Resident","My go-bag strap has snapped. The rain is soaking the exposed supplies.",DecisionCategory.ResourceEquipment,true,
                    Choice("Stop and repair the strap",20,1,40,"The strap is secure and my supplies are packed.",resource:20),
                    Choice("Carry the items loose",0,.65f,8,"I gathered the loose items and moved on.","Some supplies slipped from my hands.",resource:-5,speed:.85f),
                    Choice("Leave non-essentials and lighten the bag",12,1,18,"I kept water, medicine and first aid, and left non-essentials.",resource:10)),
                Node(4,"Neighbor Securing Their Roof","Neighbor","The wind keeps pulling this shutter loose. I am trying to secure it before I leave.",DecisionCategory.PeopleSocial,false,
                    Choice("Help secure the loose shutter",12,.95f,45,"We secured the shutter from ground level, then headed for shelter.","We could not secure it and moved away from the house.",teamwork:20),
                    Choice("Suggest taking shelter and continue",18,1,10,"I urged my neighbor to leave the shutter and seek shelter.",teamwork:10),
                    Choice("Continue along the route",0,1,0,"I continued toward the school.")),
                Node(5,"Person Struggling Against the Wind","Resident","I am having trouble keeping my balance in these gusts. Is the school this way?",DecisionCategory.PeopleSocial,false,
                    Choice("Walk with them to the school",20,1,20,"We will continue together at a steadier pace.",teamwork:25,companion:true,speed:.78f),
                    Choice("Point out the route and continue",8,1,8,"I explained the route along Felix Amante Drive.",teamwork:8),
                    Choice("Continue without stopping",0,1,0,"I continued along the road.")),
                Node(6,"Checkpoint Redirect","Responder","There is debris ahead. We are guiding people around the obstruction and back toward the school entrance.",DecisionCategory.RescueAuthority,true,
                    Choice("Follow the responder's redirect",20,1,25,"I followed the responder around the blocked section.",evacuation:15),
                    Choice("Continue on the original route",-8,.55f,5,"I found a way past the obstruction.","The obstruction forced me to turn back.",evacuation:-5),
                    Choice("Ask for the current route before proceeding",20,1,35,"The responder explained the clear approach to the school.",evacuation:15)),
                Node(7,"Evacuee Injured by Blown Debris","Evacuee","A piece of roofing cut my arm. I can walk, but I need something to cover it.",DecisionCategory.PeopleSocial,false,
                    Choice("Use the first aid kit",20,1,35,"I helped dress the cut. The evacuee can continue to shelter.",resource:10,teamwork:20,kit:true),
                    Choice("Give directions to the school",8,1,8,"I pointed out the school entrance and its responders.",teamwork:8),
                    Choice("Continue toward shelter",0,1,0,"I continued toward the school entrance.")),
                Node(8,"Arrival","Resident","We have reached the school. Shelter is close at hand.",DecisionCategory.Environmental,false)
            };
        }
    }
}
