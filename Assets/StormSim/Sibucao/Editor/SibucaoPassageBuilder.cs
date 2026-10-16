using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

public static class SibucaoPassageBuilder
{
    const string Folder="Assets/StormSim/Sibucao/StormPassages";
    const string RootName="SIBUCAO - interactive storm passages";
    static SibucaoSession s;
    static SibucaoFloodTerrain terrain;
    static Material wood,metal,rope;
    static Transform root;
    static readonly List<string> audit=new List<string>();
    static readonly List<SibucaoStormPassage> gates=new List<SibucaoStormPassage>();
    static bool Ignore(Collider c)=>c.isTrigger||c.attachedRigidbody||c.GetComponentInParent<PhysicalDecisionStation>()||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<SibucaoResident>()||root&&c.transform.IsChildOf(root);
    static bool Ground(float x,float z,out Vector3 p)
    {
        p=new Vector3(x,0,z);
        int ix=Mathf.RoundToInt((x-terrain.origin.x)/terrain.cellSize),iz=Mathf.RoundToInt((z-terrain.origin.y)/terrain.cellSize);
        if(ix<0||iz<0||ix>=terrain.width||iz>=terrain.height)return false;
        int n=iz*terrain.width+ix;if(terrain.kind[n]!=1||terrain.covered[n])return false;
        float expected=terrain.ground[n],height=float.NegativeInfinity;
        foreach(var h in Physics.RaycastAll(new Vector3(x,expected+1.4f,z),Vector3.down,3,~0,QueryTriggerInteraction.Ignore))
            if(!Ignore(h.collider)&&h.normal.y>.72f&&h.point.y<expected+.7f)height=Mathf.Max(height,h.point.y);
        if(!float.IsFinite(height))return false;p.y=height+.025f;
        return !Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.27f,~0,QueryTriggerInteraction.Ignore).Any(c=>!Ignore(c));
    }
    static Material Mat(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Smoothness",.22f);return m;
    }
    static Transform Node(Transform parent,string name,Vector3 position)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
    static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool collider=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Combine(Transform parent,string assetName)
    {
        var filters=parent.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform!=parent&&!f.GetComponent<SibucaoPassageHandle>()&&!f.GetComponentInParent<SibucaoPassageHandle>()).ToArray();
        if(filters.Length==0)return;
        var groups=filters.GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial).ToArray();var meshes=new List<Mesh>();
        foreach(var group in groups)
        {
            var mesh=new Mesh();mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);meshes.Add(mesh);
        }
        var final=new Mesh{name=assetName};final.CombineMeshes(meshes.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);
        var path=Folder+"/"+assetName+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(final,old);Object.DestroyImmediate(final);final=old;}else AssetDatabase.CreateAsset(final,path);
        parent.gameObject.AddComponent<MeshFilter>().sharedMesh=final;parent.gameObject.AddComponent<MeshRenderer>().sharedMaterials=groups.Select(g=>g.Key).ToArray();
        foreach(var f in filters)Object.DestroyImmediate(f.gameObject);foreach(var m in meshes)Object.DestroyImmediate(m);
    }
    static SibucaoPassageHandle Handle(Transform parent,SibucaoStormPassage p,int part,Vector3 pos)
    {
        var go=Box(parent,part<2?"Tied fastener "+(part+1):"Two-hand grip",pos,new Vector3(.21f,.24f,.15f),part<2?rope:metal,true);
        var h=go.AddComponent<SibucaoPassageHandle>();h.passage=p;h.part=part;return h;
    }
    static void Fence(Transform parent,float left,float right,float z,string id)
    {
        if(right-left<.2f)return;
        var f=Node(parent,"Storm-thrown fencing - "+id,new Vector3((left+right)*.5f,0,z));
        int count=Mathf.CeilToInt((right-left)/1.4f);
        for(int i=0;i<count;i++)
        {
            float x=Mathf.Lerp(left,right,(i+.5f)/count);Ground(x,z,out var g);float w=(right-left)/count+.08f;
            var panel=Node(f,"Porous timber section",new Vector3(x-f.position.x,g.y,0));
            for(int slat=0;slat<5;slat++)Box(panel,"Splintered upright",new Vector3((slat/4f-.5f)*w,1.0f,0),new Vector3(.11f,2,.1f),wood);
            for(int r=0;r<3;r++)Box(panel,"Cross brace",new Vector3(0,.25f+r*.68f,0),new Vector3(w,.13f,.12f),wood);
            var col=panel.gameObject.AddComponent<BoxCollider>();col.center=Vector3.up*.99f;col.size=new Vector3(w,1.98f,.17f);
        }
        // Keep colliders on their own nodes while combining only render geometry.
        var filterNodes=f.GetComponentsInChildren<MeshFilter>().ToArray();
        if(filterNodes.Length>0)Combine(f,id+"_fence");
    }
    static SibucaoStormPassage Gate(Transform parent,float x,float z,float width,int home,int zone,int serial,bool core)
    {
        Ground(x,z,out var ground);string id="H"+home+"_Z"+zone+"_P"+serial;
        var t=Node(parent,id+" - movable storm obstruction",ground);t.rotation=Quaternion.LookRotation(Vector3.back);
        var p=t.gameObject.AddComponent<SibucaoStormPassage>();p.persistentId=id;p.home=home;p.zone=zone;p.core=core;p.width=width;
        // A cart needs a parking pocket. Use overhead/pivot motion in narrow alleys.
        p.kind=(StormPassageKind)(serial%3);
        if(p.kind==StormPassageKind.PushCart&&(!Ground(x-width-1.2f,z,out _)||!Ground(x-width-1.2f,z-2.2f,out _)))p.kind=StormPassageKind.TiltRack;
        p.description=p.kind==StormPassageKind.SwingFence?"Wind-torn bamboo fence":p.kind==StormPassageKind.PushCart?"Jammed delivery cart":"Fallen market rack";
        p.moving=Node(t,p.description,Vector3.zero);
        var center=p.kind==StormPassageKind.SwingFence?new Vector3(width*.5f,1,0):p.kind==StormPassageKind.TiltRack?new Vector3(0,-.9f,0):new Vector3(0,1,0);
        for(int i=0;i<7;i++)Box(p.moving,"Weathered slat",center+new Vector3((i/6f-.5f)*width,0,0),new Vector3(.13f,1.8f,.11f),wood);
        for(int i=0;i<3;i++)Box(p.moving,"Braced rail",center+new Vector3(0,(i-1)*.7f,0),new Vector3(width,.14f,.17f),wood);
        for(int i=0;i<4;i++)Box(p.moving,"Rusted fixing",center+new Vector3((i%2==0?-.4f:.4f)*width,i<2?-.65f:.65f,-.12f),new Vector3(.055f,.055f,.055f),metal);
        if(p.kind==StormPassageKind.PushCart)
        {
            Box(p.moving,"Cart shelf",new Vector3(0,.27f,0),new Vector3(width,.14f,.7f),wood);
            for(int i=0;i<2;i++)Box(p.moving,"Wheel housing",new Vector3((i==0?-.4f:.4f)*width,.18f,0),new Vector3(.24f,.32f,.3f),metal);
        }
        Combine(p.moving,id+"_moving");
        var solid=p.moving.gameObject.AddComponent<BoxCollider>();solid.center=center;solid.size=new Vector3(width,1.8f,.2f);
        p.fasteners=new Transform[2];
        for(int i=0;i<2;i++)p.fasteners[i]=Handle(t,p,i,new Vector3((i==0?-.42f:.42f)*width,1.05f,-.24f)).transform;
        p.grip=Handle(p.moving,p,2,center+new Vector3(p.kind==StormPassageKind.SwingFence?width*.3f:0,0,-.24f));
        if(p.kind==StormPassageKind.SwingFence)p.moving.localPosition=new Vector3(-width*.5f,0,0);
        if(p.kind==StormPassageKind.TiltRack)p.moving.localPosition=new Vector3(0,2.25f,0);
        gates.Add(p);return p;
    }
    static IEnumerable<float> BranchCrossings(PhysicalDecisionStation station,int home,float z)
    {
        foreach(var branch in station.branches)
        {
            var variant=branch.layouts.FirstOrDefault(l=>l.home==home);if(variant==null)continue;
            var points=variant.points.Select(branch.transform.TransformPoint).ToArray();
            if(points.All(p=>p.z<z))yield return points[0].x;
            for(int i=1;i<points.Length;i++)if((points[i-1].z-z)*(points[i].z-z)<=0&&Mathf.Abs(points[i].z-points[i-1].z)>.01f)
                yield return Mathf.Lerp(points[i-1].x,points[i].x,(z-points[i-1].z)/(points[i].z-points[i-1].z));
        }
    }
    static void Band(Transform parent,int home,int zone,float z,bool extra)
    {
        var st=s.physicalDecisions.stations[zone];var desired=BranchCrossings(st,home,z).ToList();
        float min=terrain.origin.x,max=min+(terrain.width-1)*terrain.cellSize,begin=float.NaN;
        int spans=0,placed=0;float blockedWidth=0;
        var scan=new List<(float a,float b)>();
        for(float x=min;x<=max+.5f;x+=.5f)
        {
            bool walk=x<=max&&Ground(x,z,out var point)&&!s.physicalDecisions.evacuationCenter.Contains(point,1);
            if(walk&&float.IsNaN(begin))begin=x-.25f;
            if(!walk&&!float.IsNaN(begin)){if(x-.25f-begin>=.2f)scan.Add((begin,x-.25f));begin=float.NaN;}
        }
        foreach(var span in scan)
        {
            spans++;float a=span.a-.18f,b=span.b+.18f;
            var openings=desired.Where(x=>x>a+.55f&&x<b-.55f).OrderBy(x=>x).ToList();
            for(int i=openings.Count-1;i>0;i--)if(openings[i]-openings[i-1]<1.6f)openings.RemoveAt(i);
            bool carGap=!extra&&zone==1&&st.transform.position.x>a&&st.transform.position.x<b;
            float reservedA=carGap?st.transform.position.x-2.38f:extra?st.transform.position.x-6:float.NaN;
            float reservedB=carGap?st.transform.position.x+2.38f:extra?st.transform.position.x+6:float.NaN;
            if(!float.IsNaN(reservedA))openings.RemoveAll(x=>x>reservedA-1.4f&&x<reservedB+1.4f);
            if(openings.Count==0&&b-a>1.35f)
            {
                float c=(a+b)*.5f;
                if(float.IsNaN(reservedA)||c<reservedA-1.4f||c>reservedB+1.4f)openings.Add(c);
            }
            // Wide yards have repeated usable passages; narrow gaps become visibly blocked debris.
            for(float x=a+3;x<b-2;x+=10)if(openings.All(o=>Mathf.Abs(o-x)>4)&&(float.IsNaN(reservedA)||x<reservedA-2||x>reservedB+2))openings.Add(x);
            var gaps=new List<(float a,float b)>();
            if(!float.IsNaN(reservedA)&&reservedB>a&&reservedA<b)gaps.Add((Mathf.Max(a,reservedA),Mathf.Min(b,reservedB)));
            openings.Sort();
            foreach(float x in openings)
            {
                float w=Mathf.Min(2.35f,2*Mathf.Min(x-a,b-x)-.1f);
                foreach(float other in openings)if(other!=x)w=Mathf.Min(w,Mathf.Abs(other-x)-.18f);
                if(w<.85f)continue;
                bool isCore=!extra&&desired.Any(d=>Mathf.Abs(d-x)<1.4f);
                Gate(parent,x,z,w,home,zone,gates.Count,isCore);gaps.Add((x-w*.5f,x+w*.5f));placed++;
            }
            float cursor=a;
            foreach(var gap in gaps.OrderBy(g=>g.a)){Fence(parent,cursor,gap.a,z,"H"+home+"_B"+zone+"_"+(extra?"extra":"core")+"_S"+spans+"_"+cursor.ToString("F2").Replace('.','_'));blockedWidth+=Mathf.Max(0,gap.a-cursor);cursor=Mathf.Max(cursor,gap.b);}
            Fence(parent,cursor,b,z,"H"+home+"_B"+zone+"_"+(extra?"extra":"core")+"_S"+spans+"_end");blockedWidth+=Mathf.Max(0,b-cursor);
        }
        audit.Add($"H{home} zone {zone+1} {(extra?"side-street band":"encounter band")} z={z:F1}: {spans} open-ground spans, {placed} movable passages, {blockedWidth:F1}m visible fencing");
    }
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Build outside Play Mode.");
        s=Object.FindFirstObjectByType<SibucaoSession>();if(s.gameObject.scene.name!="SIBUCAO_RAIN")throw new Exception("SIBUCAO_RAIN only");
        terrain=s.GetComponent<FloodSimulationManager>().terrain;
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("tmp/passages");
        File.Copy(s.gameObject.scene.path,"tmp/passages/SIBUCAO_RAIN.before.unity",false);
        AssetDatabase.Refresh();wood=Mat("Storm weathered bamboo",new Color(.26f,.22f,.15f));metal=Mat("Storm rusted iron",new Color(.22f,.25f,.24f));rope=Mat("Storm rope bindings",new Color(.48f,.42f,.3f));
        var old=GameObject.Find(RootName);if(old)throw new Exception("Passage layout already exists; inspect before rebuilding.");
        root=new GameObject(RootName).transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Add interactive storm passages");
        var manager=s.passages?s.passages:s.gameObject.AddComponent<SibucaoPassageDirector>();s.passages=manager;
        manager.layouts=new GameObject[4];gates.Clear();audit.Clear();
        var positions=s.physicalDecisions.stations.Select(x=>x.transform.position).ToArray();var rotations=s.physicalDecisions.stations.Select(x=>x.transform.rotation).ToArray();
        try
        {
            for(int home=0;home<4;home++)
            {
                var layout=Node(root,"Home "+home+" - surveyed storm passages",Vector3.zero);manager.layouts[home]=layout.gameObject;
                float start=s.route.Progress(s.homes[home].standingPoint.position);
                for(int i=0;i<9;i++)
                {
                    float d=start+(s.route.length-start-12)*(i+1)/10;var forward=s.route.Sample(d+2)-s.route.Sample(d-2);forward.y=0;
                    s.physicalDecisions.stations[i].transform.SetPositionAndRotation(s.route.Sample(d),Quaternion.LookRotation(forward));
                }
                Physics.SyncTransforms();
                for(int i=0;i<9;i++)
                {
                    var st=s.physicalDecisions.stations[i];float z=i==1?st.transform.TransformPoint(0,0,1.4f).z:st.transform.TransformPoint(0,0,-5).z;
                    // Batch mesh imports; importing every slat group separately is expensive
                    // in this large project and needlessly repeats asset callbacks.
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        Band(layout,home,i,z,false);
                        if(i<8)Band(layout,home,i,(z+s.physicalDecisions.stations[i+1].transform.position.z)*.5f,true);
                    }
                    finally{AssetDatabase.StopAssetEditing();}
                }
                layout.gameObject.SetActive(home==0);
            }
        }
        finally {for(int i=0;i<9;i++)s.physicalDecisions.stations[i].transform.SetPositionAndRotation(positions[i],rotations[i]);Physics.SyncTransforms();}
        manager.passages=gates.ToArray();EditorUtility.SetDirty(s);EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(s.gameObject.scene);
        audit.Add("Total across four mutually exclusive home layouts: "+gates.Count+" passages. Survey extent: "+terrain.origin+" + "+terrain.width*terrain.cellSize+"m x "+terrain.height*terrain.cellSize+"m. Water/refuge excluded; B2 car channel preserved.");
        File.WriteAllLines("tmp/passages/coverage.txt",audit);return audit.Last();
    }
}
