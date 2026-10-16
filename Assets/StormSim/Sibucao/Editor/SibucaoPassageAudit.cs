using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

// Runs one surveyed cross-section per Editor update so coverage checks do not
// monopolize the Editor. Generated geometry belongs only to the passage root.
public static class SibucaoPassageAudit
{
    static readonly List<string> report=new List<string>();
    static string[] rows;static int row;
    static SibucaoSession s;static SibucaoPassageDirector director;static SibucaoFloodTerrain terrain;
    static Transform root;static Material wood,metal,rope;static Mesh ring;
    public static string Status{get;private set;}="Idle";
    static Mesh Ring()
    {
        const string path="Assets/StormSim/Sibucao/StormPassages/Shared iron grip.asset";
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old)return old;
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int a=0;a<16;a++)for(int b=0;b<6;b++)
        {float u=a*Mathf.PI/8,v=b*Mathf.PI/3,r=.105f+Mathf.Cos(v)*.019f;vertices.Add(new Vector3(Mathf.Cos(u)*r,Mathf.Sin(u)*r,Mathf.Sin(v)*.019f));}
        for(int a=0;a<16;a++)for(int b=0;b<6;b++)
        {int i=a*6+b,j=((a+1)%16)*6+b,k=a*6+(b+1)%6,l=((a+1)%16)*6+(b+1)%6;triangles.AddRange(new[]{i,j,k,k,j,l});}
        var m=new Mesh{name="Forged circular hand grip"};m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,path);return m;
    }
    public static string Start()
    {
        if(EditorApplication.isPlaying)throw new Exception("Coverage audit requires Edit Mode");
        if(Status.StartsWith("Running"))return Status;
        s=Object.FindFirstObjectByType<SibucaoSession>();director=s.passages;
        if(!director||!director.Installed)throw new Exception("Passage build not complete");
        terrain=s.GetComponent<FloodSimulationManager>().terrain;root=director.layouts[0].transform.parent;
        wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/StormPassages/Storm weathered bamboo.mat");
        metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/StormPassages/Storm rusted iron.mat");
        rope=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/StormPassages/Storm rope bindings.mat");
        ring=Ring();report.Clear();row=0;
        var reusable=new Dictionary<string,Mesh>();
        foreach(var p in director.passages)
        {
            if(p.kind==StormPassageKind.TiltRack)p.moving.localPosition=new Vector3(0,2.25f,0);
            var f=p.moving.GetComponent<MeshFilter>();string key=p.kind+"_"+p.width.ToString("F4");
            if(reusable.TryGetValue(key,out var mesh))f.sharedMesh=mesh;else reusable[key]=f.sharedMesh;
            var handle=p.grip;handle.transform.localScale=Vector3.one;handle.GetComponent<MeshFilter>().sharedMesh=ring;
            var col=handle.GetComponent<BoxCollider>();col.size=new Vector3(.31f,.31f,.2f);
            if(!p.transform.Find("Fixed hinge post"))
            {
                for(int i=0;i<2;i++)
                {
                    var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.name=i==0?"Fixed hinge post":"Fixed latch post";
                    post.transform.SetParent(p.transform,false);post.transform.localPosition=new Vector3((i==0?-1:1)*(p.width*.5f+.05f),.7f,0);
                    post.transform.localScale=new Vector3(.1f,1.4f,.15f);post.GetComponent<Renderer>().sharedMaterial=wood;
                }
            }
            foreach(var fastener in p.fasteners)
            {
                fastener.localScale=Vector3.one;fastener.GetComponent<MeshFilter>().sharedMesh=ring;
                fastener.GetComponent<BoxCollider>().size=new Vector3(.3f,.3f,.2f);
            }
        }
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            if(!renderer.GetComponentInParent<SibucaoStormPassage>())GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
        foreach(var m in new[]{wood,metal,rope}){m.enableInstancing=true;EditorUtility.SetDirty(m);}
        rows=File.ReadAllLines("tmp/passages/coverage.txt").Where(l=>l.StartsWith("H")).ToArray();
        Status="Running coverage audit";EditorApplication.update+=Step;return Status;
    }
    static bool Ground(float x,float z,out Vector3 point)
    {
        point=new Vector3(x,0,z);int ix=Mathf.RoundToInt((x-terrain.origin.x)/terrain.cellSize),iz=Mathf.RoundToInt((z-terrain.origin.y)/terrain.cellSize);
        if(ix<0||iz<0||ix>=terrain.width||iz>=terrain.height)return false;int n=iz*terrain.width+ix;
        if(terrain.kind[n]!=1||terrain.covered[n])return false;float y=float.NegativeInfinity;
        foreach(var h in Physics.RaycastAll(new Vector3(x,terrain.ground[n]+1.4f,z),Vector3.down,3,~0,QueryTriggerInteraction.Ignore))
            if(!Ignore(h.collider)&&h.normal.y>.72f&&h.point.y<terrain.ground[n]+.7f)y=Mathf.Max(y,h.point.y);
        if(!float.IsFinite(y))return false;point.y=y+.025f;
        return !Physics.OverlapCapsule(point+Vector3.up*.4f,point+Vector3.up*1.5f,.27f,~0,QueryTriggerInteraction.Ignore).Any(c=>!Ignore(c));
    }
    static bool Ignore(Collider c)=>c.attachedRigidbody||c.isTrigger||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<SibucaoResident>()||c.GetComponentInParent<PhysicalDecisionStation>()||c.transform.IsChildOf(root);
    static bool Blocked(Vector3 p)=>Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.27f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.transform.IsChildOf(root));
    static void Step()
    {
        try
        {
            if(row>=rows.Length){Finish();return;}
            var match=Regex.Match(rows[row],@"H(\d) zone (\d) (.*?) z=([-\d.]+):");
            int home=int.Parse(match.Groups[1].Value),zone=int.Parse(match.Groups[2].Value)-1;
            bool extra=match.Groups[3].Value.Contains("side-street");float z=float.Parse(match.Groups[4].Value,System.Globalization.CultureInfo.InvariantCulture);
            for(int i=0;i<4;i++)director.layouts[i].SetActive(i==home);
            Physics.SyncTransforms();
            float start=s.route.Progress(s.homes[home].standingPoint.position),d=start+(s.route.length-start-12)*(zone+1)/10;
            float centerX=s.route.Sample(d).x,reserved=extra?6:zone==1?2.38f:0;
            int checkedPoints=0,closed=0,added=0;
            for(float x=terrain.origin.x;x<=terrain.origin.x+(terrain.width-1)*terrain.cellSize;x+=.25f)
            {
                if(reserved>0&&Mathf.Abs(x-centerX)<reserved+.1f)continue;
                if(!Ground(x,z,out var p)||s.physicalDecisions.evacuationCenter.Contains(p,1))continue;
                checkedPoints++;
                if(Blocked(p)){closed++;continue;}
                var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.name="Wind-thrown panel closing pavement seam";
                panel.transform.SetParent(director.layouts[home].transform,false);panel.transform.position=p+Vector3.up;
                panel.transform.localScale=new Vector3(.8f,2,.18f);panel.GetComponent<Renderer>().sharedMaterial=wood;
                GameObjectUtility.SetStaticEditorFlags(panel,StaticEditorFlags.BatchingStatic);Physics.SyncTransforms();added++;
            }
            int remaining=0;
            for(float x=terrain.origin.x;x<=terrain.origin.x+(terrain.width-1)*terrain.cellSize;x+=.25f)
                if(!(reserved>0&&Mathf.Abs(x-centerX)<reserved+.1f)&&Ground(x,z,out var p)&&!s.physicalDecisions.evacuationCenter.Contains(p,1)&&!Blocked(p))remaining++;
            report.Add($"H{home} B{zone+1} {(extra?"side-street":"encounter")}: {checkedPoints} walkable capsule samples; {closed} initially blocked; {added} visible seam panels added; {remaining} uncovered samples. Reserved channel width {reserved*2:F2}m.");
            Status="Running coverage "+(++row)+"/"+rows.Length;File.WriteAllLines("tmp/passages/collision-audit.txt",new[]{Status}.Concat(report));
        }
        catch(Exception e){Status="FAIL "+e;EditorApplication.update-=Step;File.WriteAllLines("tmp/passages/collision-audit.txt",new[]{Status}.Concat(report));}
    }
    static void Finish()
    {
        EditorApplication.update-=Step;
        for(int i=0;i<4;i++)director.layouts[i].SetActive(i==0);
        for(int h=0;h<4;h++)for(int z=0;z<9;z++)
        {var gates=director.passages.Where(p=>p.home==h&&p.zone==z).ToArray();report.Add($"H{h} B{z+1}: {gates.Length} total passages; {gates.Count(p=>p.core)} core passages.");}
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        Status="Complete";File.WriteAllLines("tmp/passages/collision-audit.txt",new[]{Status}.Concat(report));
    }
}
