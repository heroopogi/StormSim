using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

public static partial class SibucaoDiegeticInstaller
{
    static SibucaoHazardRecord[] pools;
    static float Lowering(Vector3 p)
    {
        var refuge=s.GetComponent<FloodSimulationManager>().dryRefuge;if(refuge&&refuge.Contains(p,3))return 0;
        float amount=0;
        foreach(var site in pools)
        {
            float x=(p.x-site.position.x)/13f,z=(p.z-site.position.z)/11f,r=Mathf.Sqrt(x*x+z*z);
            if(r>=1||Mathf.Abs(p.y-site.position.y)>3)continue;
            float v=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((r-.22f)/.78f));amount=Mathf.Max(amount,1.75f*v);
        }
        if(amount<=0)return 0;
        // Retain the principal road crown and home foundations.
        foreach(var h in s.homes)if(Flat(p,h.transform.position)<9)return 0;
        return amount*Mathf.SmoothStep(0,1,Mathf.Clamp01((RouteLateral(p)-5)/3));
    }
    struct Vertex
    {
        public Vector3 p;public Vector2 uv;
        public static Vertex Mid(Vertex a,Vertex b)=>new Vertex{p=(a.p+b.p)*.5f,uv=(a.uv+b.uv)*.5f};
    }
    static bool NearPool(Vector3 a,Vector3 b,Vector3 c)
    {
        float x0=Mathf.Min(a.x,b.x,c.x),x1=Mathf.Max(a.x,b.x,c.x),z0=Mathf.Min(a.z,b.z,c.z),z1=Mathf.Max(a.z,b.z,c.z);
        return pools.Any(p=>x0<p.position.x+14&&x1>p.position.x-14&&z0<p.position.z+12&&z1>p.position.z-12);
    }
    static void Triangle(Vertex a,Vertex b,Vertex c,int level,List<Vertex> output,List<int> indices)
    {
        float length=Mathf.Max((a.p-b.p).sqrMagnitude,(a.p-c.p).sqrMagnitude,(b.p-c.p).sqrMagnitude);
        if(level<7&&length>6.25f&&NearPool(a.p,b.p,c.p))
        {
            var ab=Vertex.Mid(a,b);var bc=Vertex.Mid(b,c);var ca=Vertex.Mid(c,a);
            Triangle(a,ab,ca,level+1,output,indices);Triangle(ab,b,bc,level+1,output,indices);Triangle(ca,bc,c,level+1,output,indices);Triangle(ab,bc,ca,level+1,output,indices);return;
        }
        indices.Add(output.Count);output.Add(a);indices.Add(output.Count);output.Add(b);indices.Add(output.Count);output.Add(c);
    }
    public static string BuildHydrology()
    {
        throw new InvalidOperationException("Ground reshaping was reverted at the user's request. Keep original terrain and tune floodwater only.");
    }
    // Historical implementation retained for reference; never used by installation.
    static string ArchivedGroundReshaping()
    {
        if(EditorApplication.isPlaying)throw new Exception("Terrain installation requires Edit Mode");Context();
        if(AssetDatabase.GetAssetPath(terrain).StartsWith(Folder))throw new Exception("Hydrology is already installed; do not lower the terrain twice.");
        survey=JsonUtility.FromJson<SibucaoHazardSurvey>(File.ReadAllText("tmp/diegetic/survey.json"));pools=survey.sites.Where(x=>x.kind==SibucaoHazardKind.LowStreet).ToArray();
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var modified=new List<string>();
        var filters=Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(m=>m.sharedMesh&&m.GetComponent<Renderer>()&&(m.name.StartsWith("Measured terrain")||m.transform.parent&&m.transform.parent.name=="Study Area Roads"||m.sharedMesh.name.StartsWith("Road surface"))).ToArray();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var filter in filters)
            {
                var bounds=filter.GetComponent<Renderer>().bounds;if(!pools.Any(p=>bounds.min.x<p.position.x+14&&bounds.max.x>p.position.x-14&&bounds.min.z<p.position.z+12&&bounds.max.z>p.position.z-12))continue;
                var original=filter.sharedMesh;if(!original.isReadable){report.Add("Unreadable floor mesh: "+filter.name);continue;}
                var positions=original.vertices;var uv=original.uv;var data=new Vertex[positions.Length];for(int i=0;i<data.Length;i++)data[i]=new Vertex{p=filter.transform.TransformPoint(positions[i]),uv=i<uv.Length?uv[i]:Vector2.zero};
                var output=new List<Vertex>();var submeshes=new List<int[]>();
                for(int part=0;part<original.subMeshCount;part++)
                {
                    var triangles=original.GetTriangles(part);var indices=new List<int>();
                    for(int i=0;i<triangles.Length;i+=3)Triangle(data[triangles[i]],data[triangles[i+1]],data[triangles[i+2]],0,output,indices);
                    submeshes.Add(indices.ToArray());
                }
                if(!output.Any(v=>Lowering(v.p)>.005f))continue;
                var mesh=new Mesh{name="Runoff depression - "+filter.name,indexFormat=IndexFormat.UInt32};
                mesh.vertices=output.Select(v=>filter.transform.InverseTransformPoint(v.p-Vector3.up*Lowering(v.p))).ToArray();mesh.uv=output.Select(v=>v.uv).ToArray();mesh.subMeshCount=submeshes.Count;
                for(int part=0;part<submeshes.Count;part++)mesh.SetTriangles(submeshes[part],part);mesh.RecalculateNormals();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Folder+"/Depressed_ground_"+modified.Count+".asset");filter.sharedMesh=mesh;
                var collider=filter.GetComponent<MeshCollider>();if(collider){collider.sharedMesh=null;collider.sharedMesh=mesh;}
                EditorUtility.SetDirty(filter);modified.Add(filter.name);
            }
            var clone=Object.Instantiate(terrain);clone.name="Sibucao diegetic street runoff";AssetDatabase.CreateAsset(clone,Folder+"/Sibucao_diegetic_runoff.asset");
            var before=(float[])clone.ground.Clone();
            for(int i=0;i<clone.ground.Length;i++)if(clone.kind[i]==1&&!clone.covered[i])clone.ground[i]-=Lowering(terrain.Position(i));
            for(int z=0;z<clone.height;z++)for(int x=0;x<clone.width;x++)
            {
                int i=z*clone.width+x;
                if(x+1<clone.width&&clone.crestX[i]-Mathf.Max(before[i],before[i+1])<.28f)clone.crestX[i]=Mathf.Max(clone.ground[i],clone.ground[i+1])+Mathf.Max(0,clone.crestX[i]-Mathf.Max(before[i],before[i+1]));
                if(z+1<clone.height&&clone.crestZ[i]-Mathf.Max(before[i],before[i+clone.width])<.28f)clone.crestZ[i]=Mathf.Max(clone.ground[i],clone.ground[i+clone.width])+Mathf.Max(0,clone.crestZ[i]-Mathf.Max(before[i],before[i+clone.width]));
            }
            terrain=clone;s.GetComponent<FloodSimulationManager>().terrain=clone;
        }
        finally{AssetDatabase.StopAssetEditing();}
        Physics.SyncTransforms();
        // Reconcile the simulation bed with the real, modified topmost street collider.
        for(int i=0;i<terrain.ground.Length;i++)if(terrain.kind[i]==1&&!terrain.covered[i])
        {
            var p=terrain.Position(i);if(!pools.Any(a=>Flat(a.position,p)<18))continue;
            float best=float.NegativeInfinity;
            foreach(var hit in Physics.RaycastAll(p+Vector3.up*3,Vector3.down,5,~0,QueryTriggerInteraction.Ignore))if(!Ignore(hit.collider)&&hit.normal.y>.6f&&hit.point.y<p.y+1.9f)best=Mathf.Max(best,hit.point.y);
            if(float.IsFinite(best))terrain.ground[i]=best;
        }
        // Keep existing walls/curbs, lower only open-ground spill edges.
        for(int z=0;z<terrain.height;z++)for(int x=0;x<terrain.width;x++)
        {int i=z*terrain.width+x;if(x+1<terrain.width)terrain.crestX[i]=Mathf.Max(terrain.crestX[i],Mathf.Max(terrain.ground[i],terrain.ground[i+1]));if(z+1<terrain.height)terrain.crestZ[i]=Mathf.Max(terrain.crestZ[i],Mathf.Max(terrain.ground[i],terrain.ground[i+terrain.width]));}
        EditorUtility.SetDirty(terrain);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        File.WriteAllLines("tmp/diegetic/ground-meshes.txt",modified);return modified.Count+" visible floor meshes and matching mesh colliders lowered; independent runoff grid saved. Initial rainfall bake pending.";
    }
    public static string BakeAntecedentRain(float rainfall=.32f)
    {
        Context();int count=terrain.ground.Length,w=terrain.width,h=terrain.height;var depth=new float[count];var change=new float[count];var outgoing=new float[count];var xq=new float[count];var zq=new float[count];var kind=(byte[])terrain.kind.Clone();
        var refuge=s.GetComponent<FloodSimulationManager>().dryRefuge;
        if(refuge)for(int i=0;i<count;i++){if(refuge.Contains(terrain.Position(i),terrain.cellSize*.25f))kind[i]=0;else if(kind[i]==1&&refuge.Contains(terrain.Position(i),refuge.drainageWidth))kind[i]=2;}
        const int steps=1600;const float dt=.2f;double drained=0,added=0;
        float Flux(int a,int b,float crest)
        {
            if(kind[a]==0||kind[b]==0)return 0;float ha=terrain.ground[a]+depth[a],hb=terrain.ground[b]+depth[b],head=Mathf.Max(0,Mathf.Max(ha,hb)-crest),difference=ha-hb;
            float speed=Mathf.Sign(difference)*Mathf.Min(1.4f,2*Mathf.Sqrt(9.81f*head*Mathf.Abs(difference)/terrain.cellSize));
            return Mathf.Sign(speed)*Mathf.Min(Mathf.Abs(speed*head*dt/terrain.cellSize),Mathf.Abs(difference)*.24f);
        }
        void Transfer(int a,int b,float q)
        {if(q==0)return;int donor=q>0?a:b;q*=Mathf.Min(1,(kind[donor]==2?0:depth[donor])/Mathf.Max(.000001f,outgoing[donor]));change[a]-=q;change[b]+=q;}
        for(int step=0;step<steps;step++)
        {
            Array.Clear(change,0,count);Array.Clear(outgoing,0,count);
            for(int i=0;i<count;i++)if(kind[i]==1&&!terrain.covered[i]){depth[i]+=rainfall/steps;added+=rainfall/steps;}
            for(int z=0;z<h;z++)for(int x=0;x<w;x++)
            {int i=z*w+x;xq[i]=x+1<w?Flux(i,i+1,terrain.crestX[i]):0;zq[i]=z+1<h?Flux(i,i+w,terrain.crestZ[i]):0;if(xq[i]>0)outgoing[i]+=xq[i];else if(x+1<w)outgoing[i+1]-=xq[i];if(zq[i]>0)outgoing[i]+=zq[i];else if(z+1<h)outgoing[i+w]-=zq[i];}
            for(int z=0;z<h;z++)for(int x=0;x<w;x++){int i=z*w+x;if(x+1<w)Transfer(i,i+1,xq[i]);if(z+1<h)Transfer(i,i+w,zq[i]);}
            for(int i=0;i<count;i++)if(kind[i]==1)depth[i]=Mathf.Max(0,depth[i]+change[i]);else if(kind[i]==2)drained+=change[i];
        }
        terrain.initialDepth=depth;terrain.antecedentRainMetres=rainfall;EditorUtility.SetDirty(terrain);AssetDatabase.SaveAssets();
        survey=JsonUtility.FromJson<SibucaoHazardSurvey>(File.ReadAllText("tmp/diegetic/survey.json"));var lines=new List<string>();
        foreach(var site in survey.sites.Where(p=>p.kind==SibucaoHazardKind.LowStreet))
        {int x=Mathf.Clamp(Mathf.RoundToInt((site.position.x-terrain.origin.x)/terrain.cellSize),0,w-1),z=Mathf.Clamp(Mathf.RoundToInt((site.position.z-terrain.origin.y)/terrain.cellSize),0,h-1);lines.Add(site.id+" initial bed depth="+depth[z*w+x].ToString("F3")+"m");}
        double balance=added-drained-depth.Sum(v=>(double)v);lines.Add("Antecedent rainfall="+rainfall+"m. Volume balance error="+(balance*terrain.cellSize*terrain.cellSize).ToString("F3")+"m3. Dry refuge cells excluded. This is a scenario initial condition, not surveyed rainfall.");
        File.WriteAllLines("tmp/diegetic/initial-water.txt",lines);return string.Join("\n",lines);
    }
}
