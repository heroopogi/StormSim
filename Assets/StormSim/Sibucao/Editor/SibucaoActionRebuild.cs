using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoActionRebuild
    {
        const string Folder="Assets/StormSim/Sibucao/Art/Cast/";
        const string Nolant="Assets/Stylized NPC - Peasant Nolant/Models/Peasant Nolant(Free Version).fbx";
        static AnimationClip Clip(string name)=>AssetDatabase.LoadAllAssetsAtPath(Nolant).OfType<AnimationClip>().First(c=>c.name==name);
        [Serializable] public class RigCheck {public string prefab;public bool valid,human,bones,idleRetargetValidated;public float stride,footMin,legLengthDrift;}
        [Serializable] public class RigReport {public RigCheck[] variants;public string[] clips;public bool nolantAvatarValid,nolantAvatarHuman;}
        public static RigCheck[] Audit()
        {
            var results=new List<RigCheck>();var walk=Clip("metarig|Walk");
            foreach(var path in System.IO.Directory.GetFiles("Assets/npc_casual_set_00/Prefabs","npc_csl_00_character_*.prefab").OrderBy(x=>x))
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));var go=Object.Instantiate(source);go.hideFlags=HideFlags.HideAndDontSave;go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);var a=go.GetComponentInChildren<Animator>();
                var result=new RigCheck{prefab=path.Replace('\\','/'),valid=a&&a.avatar&&a.avatar.isValid,human=a&&a.avatar&&a.avatar.isHuman};
                if(!result.valid||!result.human){Object.DestroyImmediate(go);throw new Exception("Invalid avatar: "+path);}
                a.runtimeAnimatorController=null;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();
                var graph=PlayableGraph.Create("Retarget audit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var playable=AnimationClipPlayable.Create(graph,walk);playable.SetApplyFootIK(true);AnimationPlayableOutput.Create(graph,"Audit",a).SetSourcePlayable(playable);graph.Play();
                var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);var knee=a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var hip=a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);result.bones=foot&&knee&&hip&&a.GetBoneTransform(HumanBodyBones.RightFoot)&&a.GetBoneTransform(HumanBodyBones.Head);
                float minY=100,baseLength=0,drift=0;var samples=new List<Vector3>();
                for(int i=0;i<=80;i++){playable.SetTime(walk.length*i/80.0);graph.Evaluate(0);var p=go.transform.InverseTransformPoint(foot.position);samples.Add(p);minY=Mathf.Min(minY,p.y);float length=Vector3.Distance(foot.position,knee.position)+Vector3.Distance(knee.position,hip.position);if(i==0)baseLength=length;drift=Mathf.Max(drift,Mathf.Abs(length-baseLength));}
                var speeds=new List<float>();float dt=walk.length/80;
                for(int i=1;i<samples.Count;i++){float v=(samples[i-1].z-samples[i].z)/dt;if(samples[i].y<minY+.07f&&v>.15f&&v<3)speeds.Add(v);}
                speeds.Sort();result.stride=speeds.Count>0?speeds[speeds.Count/2]:0;result.footMin=minY;result.legLengthDrift=drift;
                var idle=Clip("metarig|Idle");graph.Destroy();
                var idleGraph=PlayableGraph.Create("Idle retarget audit");idleGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var idlePlayable=AnimationClipPlayable.Create(idleGraph,idle);AnimationPlayableOutput.Create(idleGraph,"Idle audit",a).SetSourcePlayable(idlePlayable);idleGraph.Play();
                result.idleRetargetValidated=idle.humanMotion;
                for(int frame=0;frame<12;frame++)
                {
                    idlePlayable.SetTime(idle.length*frame/12.0);idleGraph.Evaluate(0);
                    var head=a.GetBoneTransform(HumanBodyBones.Head);var hips=a.GetBoneTransform(HumanBodyBones.Hips);
                    float length=Vector3.Distance(foot.position,knee.position)+Vector3.Distance(knee.position,hip.position);
                    result.idleRetargetValidated&=float.IsFinite(head.position.y)&&head.position.y>hips.position.y+.3f&&Mathf.Abs(length-baseLength)<.015f;
                }
                idleGraph.Destroy();Object.DestroyImmediate(go);if(result.stride<.15f||!result.bones||drift>.015f||!result.idleRetargetValidated)throw new Exception("Retarget validation failed: "+path);results.Add(result);
            }
            var nolant=AssetDatabase.LoadAllAssetsAtPath(Nolant).OfType<Avatar>().First();
            var report=new RigReport{variants=results.ToArray(),clips=new[]{"metarig|Idle (3.0s)","metarig|Walk (1.333s, in place)"},nolantAvatarValid=nolant.isValid,nolantAvatarHuman=nolant.isHuman};
            System.IO.Directory.CreateDirectory("Documentation");System.IO.File.WriteAllText("Documentation/SIBUCAO_RIG_AUDIT.json",JsonUtility.ToJson(report,true));return results.ToArray();
        }
        static Material ConvertMaterial(Material original)
        {
            if(!original)return null;if(original.shader.name.StartsWith("Universal Render Pipeline"))return original;
            string path=Folder+original.name+"_URP.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=original.name+"_URP"};
            if(original.HasProperty("_MainTex"))mat.SetTexture("_BaseMap",original.GetTexture("_MainTex"));if(original.HasProperty("_Color"))mat.SetColor("_BaseColor",original.GetColor("_Color"));
            if(original.HasProperty("_BumpMap")&&original.GetTexture("_BumpMap")){mat.SetTexture("_BumpMap",original.GetTexture("_BumpMap"));mat.EnableKeyword("_NORMALMAP");}
            mat.SetFloat("_Smoothness",.2f);AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static SibucaoActor Cast(SibucaoResident old,string role,RigCheck rig,float height)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(old.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(old.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(Transform child in old.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            old.name=role;var marker=old.GetComponent<PlaceholderCharacter>();if(marker)Object.DestroyImmediate(marker);
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(rig.prefab));visual.name="Casual character - real Humanoid rig";visual.transform.SetParent(old.transform,false);
            old.transform.localScale=Vector3.one;old.height=height;old.pelvis=old.chest=old.head=null;old.thighs=old.shins=old.feet=old.upperArms=old.forearms=old.hands=Array.Empty<Transform>();
            var animator=visual.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;
            foreach(var r in visual.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(ConvertMaterial).ToArray();
            var bounds=new Bounds();bool first=true;foreach(var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            float scale=height/Mathf.Max(.1f,bounds.size.y);visual.transform.localScale=Vector3.one*scale;visual.transform.localPosition=Vector3.zero;
            var actor=old.gameObject.AddComponent<SibucaoActor>();actor.role=role;actor.animator=animator;actor.idle=Clip("metarig|Idle");actor.walk=Clip("metarig|Walk");actor.strideSpeed=rig.stride*scale;old.casting=actor;
            var feet=animator.gameObject.AddComponent<SibucaoFootPlant>();feet.actor=actor;feet.soleHeight=Mathf.Max(.03f,rig.footMin*scale);
            return actor;
        }
        static PhysicalDecisionSensor Zone(PhysicalDecisionStation s,string name,Vector3 p,Vector3 size,PhysicalSensorKind kind,int route=0,int step=0)
        {
            var go=new GameObject(name);go.transform.SetParent(s.transform,false);go.transform.localPosition=p;var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.size=size;
            var sensor=go.AddComponent<PhysicalDecisionSensor>();sensor.station=s;sensor.kind=kind;sensor.outcome=route;sensor.sequenceStep=step;var marker=go.AddComponent<DecisionTriggerMarker>();marker.decision=s.index+1;marker.purpose=kind+" ordered step "+step;return sensor;
        }
        static void Path(PhysicalDecisionStation s,int outcome,Vector3 middle,Vector3 end)
        {
            Zone(s,"Route "+outcome+" approach",new Vector3(middle.x*.5f,1,-3.3f),new Vector3(3,3,1),PhysicalSensorKind.Path,outcome,0);
            Zone(s,"Route "+outcome+" passage",middle,new Vector3(1.4f,3,1),PhysicalSensorKind.Path,outcome,1);
            Zone(s,"Route "+outcome+" exit",end,new Vector3(2.3f,3,1.3f),PhysicalSensorKind.Path,outcome,2);
        }
        public static string Install()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode first.");var s=Object.FindAnyObjectByType<SibucaoSession>();
            if(GameObject.Find("SIBUCAO action rebuild installed"))return "Already installed; use audit/validation.";
            var rigs=Audit();System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            int ambient=0,castCount=0;
            foreach(var old in Object.FindObjectsByType<SibucaoResident>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(r=>r.routeDistance).ToArray())
            {
                bool bg=old.enabled;bool neighbour=old.GetComponentInParent<SibucaoNeighborWarning>();
                if(bg&&ambient++%3!=0){Object.DestroyImmediate(old.gameObject);continue;}
                int selected=castCount%12;string role=bg?"Evacuating resident "+(ambient/3+1):neighbour?"Doorstep warning resident":old.name.Contains("Wheelchair")?"Wheelchair resident":old.name.Contains("Collapsed")?"Collapsed evacuee":old.name.Contains("Responder")?"Checkpoint volunteer":"Doorway resident";
                if(role=="Wheelchair resident")selected=0;else if(role=="Collapsed evacuee")selected=4;else if(role=="Checkpoint volunteer")selected=10;else if(role=="Doorway resident")selected=7;
                var actor=Cast(old,role,rigs[selected],bg?1.55f+(castCount%5)*.055f:1.65f);castCount++;
                if(role=="Wheelchair resident"){actor.seatedPose=true;actor.animator.transform.localPosition+=Vector3.down*.35f;}
                if(role=="Collapsed evacuee"){actor.injuredPose=true;actor.animator.transform.localRotation=Quaternion.Euler(0,0,80);actor.animator.transform.localPosition=new Vector3(.7f,.2f,0);var rb=old.GetComponentInParent<Rigidbody>();rb.constraints=RigidbodyConstraints.FreezeRotation;}
                if(bg){old.behavior=ResidentBehavior.Evacuating;old.speed=actor.strideSpeed;}
            }
            var elderly=new GameObject("Elderly resident - age-specific asset pending");elderly.transform.SetParent(s.physicalDecisions.stations[0].transform,false);elderly.transform.localPosition=new Vector3(-2.6f,0,-1);var elder=elderly.AddComponent<SibucaoResident>();elder.enabled=false;Cast(elder,"Elderly resident - age-specific asset pending",rigs[3],1.58f);
            foreach(var marker in Object.FindObjectsByType<PlaceholderCharacter>(FindObjectsInactive.Include,FindObjectsSortMode.None).ToArray())if(marker.gameObject.scene.IsValid())Object.DestroyImmediate(marker.gameObject);
            foreach(var station in s.physicalDecisions.stations)
            {
                station.timeout=120;if(station.routeMark)station.routeMark.SetActive(false);
                foreach(var sensor in station.GetComponentsInChildren<PhysicalDecisionSensor>(true).ToArray())
                {
                    if(sensor.kind==PhysicalSensorKind.Path||sensor.kind==PhysicalSensorKind.Boundary)Object.DestroyImmediate(sensor.gameObject);
                    else if(sensor.kind==PhysicalSensorKind.StrapSocket){sensor.GetComponent<BoxCollider>().size=new Vector3(.07f,.10f,.07f);}
                    else if(sensor.kind==PhysicalSensorKind.Water){sensor.GetComponent<BoxCollider>().size=new Vector3(3.2f,5,5);sensor.transform.localPosition=new Vector3(0,1.2f,1);}
                }
                if(station.index==0||station.index==8){Path(station,0,new Vector3(0,1,1),new Vector3(0,1,6));Path(station,1,new Vector3(3,1,1),new Vector3(3,1,6));}
                if(station.index==1){Path(station,0,new Vector3(3.8f,1,1.5f),new Vector3(3.8f,1,6));Path(station,1,new Vector3(0,1,1.5f),new Vector3(0,1,6));}
                if(station.index==2){Path(station,1,new Vector3(0,1,3),new Vector3(0,1,6));Path(station,2,new Vector3(3.5f,1,0),new Vector3(3.5f,1,6));}
                if(station.index==3){Path(station,0,new Vector3(-3.4f,1,0),new Vector3(-3.4f,1,5));Path(station,1,new Vector3(2.1f,1,0),new Vector3(2.1f,1,5));}
                if(station.index==6){Path(station,2,new Vector3(-2,1,1),new Vector3(-2,1,6));Path(station,1,new Vector3(2.8f,1,-2.5f),new Vector3(2.8f,1,1));}
                if(station.assist)
                {
                    var dest=station.GetComponentsInChildren<PhysicalDecisionSensor>().First(c=>c.kind==PhysicalSensorKind.AssistDestination);dest.GetComponent<BoxCollider>().size=new Vector3(3,4,3);
                    if(station.index==7)station.assist.Body.isKinematic=true;
                }
                foreach(var item in station.GetComponentsInChildren<PhysicalGrabbable>(true))if(item.kind==PhysicalItemKind.Strap&&!item.tip){var tip=new GameObject("Strap connector");tip.transform.SetParent(item.transform,false);tip.transform.localPosition=Vector3.up*.42f;item.tip=tip.transform;}
            }
            var last=s.physicalDecisions.stations[8];if(!last.GetComponentInChildren<PhysicalGrabbable>())
            {
                var source=s.physicalDecisions.stations[0].GetComponentInChildren<PhysicalGrabbable>();var stick=Object.Instantiate(source,last.transform);stick.name="Creek depth stick";stick.station=last;stick.persistentId="B8_creek_stick";stick.transform.localPosition=new Vector3(1.4f,1,-4);
                Zone(last,"Creek physical water",new Vector3(0,1,1),new Vector3(4,5,5),PhysicalSensorKind.Water);
            }
            var flood=s.GetComponent<FloodSimulationManager>();flood.temporaryWaterMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/Art/Home/Floodwater.mat");
            var ground=new List<Vector3>();for(float d=0;d<s.route.length;d+=8)ground.Add(s.route.Sample(d));ground.Add(s.route.Sample(s.route.length));flood.groundSamples=ground.ToArray();
            InstallDebris(s,flood);var audio=s.gameObject.AddComponent<SibucaoRouteAudio>();
            audio.wheelchair=Source(s.physicalDecisions.stations[5],"SFX_Wheelchair_Scream_PLACEHOLDER");audio.collapsed=Source(s.physicalDecisions.stations[7],"SFX_Collapse_Scream_PLACEHOLDER");audio.checkpoint=Source(s.physicalDecisions.stations[6],"SFX_Checkpoint_Crowd_PLACEHOLDER");audio.boat=Source(s.physicalDecisions.stations[6],"SFX_Boat_Crowd_PLACEHOLDER");audio.boat.transform.localPosition=Vector3.right*3;
            foreach(var item in Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None))item.routinePickup=item.kind==PhysicalItemKind.Supply||item.kind==PhysicalItemKind.FirstAid;
            new GameObject("SIBUCAO action rebuild installed");EditorSceneManager.MarkSceneDirty(s.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(s.gameObject.scene);return "Installed real cast, ordered decisions, temporary flood and audio slots. Cast count "+(castCount+1);
        }
        static AudioSource Source(PhysicalDecisionStation station,string name){var go=new GameObject(name);go.transform.SetParent(station.transform,false);var a=go.AddComponent<AudioSource>();a.playOnAwake=false;a.spatialBlend=1;a.minDistance=3;a.maxDistance=20;a.volume=.5f;return a;}
        static void InstallDebris(SibucaoSession s,FloodSimulationManager flood)
        {
            var root=new GameObject("TEMPORARY flood drifting debris");
            for(int i=0;i<36;i++)
            {
                string[] names={"Branch","Loose GI sheet","Plastic container","Floating footwear"};var go=GameObject.CreatePrimitive(i%4==0?PrimitiveType.Cylinder:i%4==2?PrimitiveType.Capsule:PrimitiveType.Cube);go.name="Floating "+names[i%4];go.transform.SetParent(root.transform);
                go.transform.position=s.route.Sample(18+i*(s.route.length-36)/36)+new Vector3(i%2==0?4:-4,.4f,0);go.transform.localScale=i%4==0?new Vector3(.07f,.65f,.07f):i%4==1?new Vector3(.7f,.025f,1):i%4==2?new Vector3(.22f,.18f,.22f):new Vector3(.11f,.035f,.26f);
                if(i%4==0)go.transform.rotation=Quaternion.Euler(80,15*i,0);
                string path=Folder+"Debris_"+(i%4)+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=i%4==0?new Color(.28f,.18f,.1f):i%4==1?new Color(.4f,.43f,.43f):i%4==2?new Color(.12f,.31f,.5f):new Color(.3f,.16f,.16f);AssetDatabase.CreateAsset(mat,path);}go.GetComponent<Renderer>().sharedMaterial=mat;
                var rb=go.AddComponent<Rigidbody>();rb.mass=i%4==1?2:.35f;rb.linearDamping=.5f;rb.angularDamping=1;rb.interpolation=RigidbodyInterpolation.Interpolate;var debris=go.AddComponent<SibucaoFloatingDebris>();debris.flood=flood;
            }
        }
    }
}

