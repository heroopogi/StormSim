using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>Corrected church and pond layout traced from the user's marked reference.</summary>
public static class BanagoChurchBoundaryBuilder
{
    [MenuItem("Tools/StormSim/Create Corrected Church and Pond Layout")]
    public static void BuildFromMenu() => Build();

    // The existing correction must be reverted before recreating it. No duplicate groups are added.
    // commit=false returns the reference plan without changing the scene or creating assets.
    public static object Build(bool commit = true)
    {
        if (!commit) return System.IO.File.ReadAllText("Assets/Editor/ReferenceData/ChurchPondLayout.json");
        var result = BuildLayout();
        RefineAppearance();
        return result;
    }

    private static object BuildLayout()
    {
        const string title="Church and pond neighbourhood - corrected reference layout";
        var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene, Play mode, or correction already exists.");
        var plan=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Editor/ReferenceData/ChurchPondLayout.json"));
        Vector2 P(Newtonsoft.Json.Linq.JToken p)=>new Vector2((float)p[0],(float)p[1]);
        Vector3 V(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
        Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        bool In(Vector2 p,Vector2[] poly){bool b=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)b=!b;return b;}
        float Segment(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.00001f,d.sqrMagnitude)));}
        float Edge(Vector2 p,Vector2[] poly){float d=9999;for(int i=0;i<poly.Length;i++)d=Mathf.Min(d,Segment(p,poly[i],poly[(i+1)%poly.Length]));return d;}
        var wall=plan["wall"].Select(P).ToArray();var clear=plan["clearPolygon"].Select(P).ToArray();var open=plan["openLot"].Select(P).ToArray();var track=plan["track"].Select(P).ToArray();
        var removed=plan["removedHouses"].Select(h=>new{name=(string)h["name"],poly=h["footprint"].Select(P).ToArray()}).ToArray();
        var housePlans=plan["newHouses"].ToArray();var newPolys=housePlans.Select(h=>h["footprint"].Select(P).ToArray()).ToArray();
        float AlongDistance(Vector2 p,Vector2[] line){float d=9999;for(int i=0;i<line.Length-1;i++)d=Mathf.Min(d,Segment(p,line[i],line[i+1]));return d;}
        bool RemoveAt(Vector2 p,float margin=.65f)=>removed.Any(h=>In(p,h.poly)||Edge(p,h.poly)<margin);
        var before=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var ground=before.Select(t=>t.GetComponent<MeshCollider>()).Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&c.name.StartsWith("Measured terrain")).ToArray();
        var roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")&&c.name!="residential 791977357").ToArray();
        bool Hit(MeshCollider c,Vector2 p,out float y){y=0;var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)return false;if(c.Raycast(new Ray(V(p,60),Vector3.down),out var h,120)){y=h.point.y;return true;}return false;}
        float Ground(Vector2 p){foreach(var c in ground)if(Hit(c,p,out var h))return h;throw new Exception("No terrain at "+p);}
        float Surface(Vector2 p){float h=Ground(p);foreach(var c in roads)if(Hit(c,p,out var r))h=Mathf.Max(h,r);return h;}
        bool Road(Vector2 p)=>roads.Any(c=>Hit(c,p,out _));
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");string backup="RoadRepairBackup/ChurchLayoutCorrection_"+stamp;System.IO.Directory.CreateDirectory(backup);
        if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-correction.unity",true))throw new Exception("Backup failed.");
        string folder="Assets/StormSim/Generated/ChurchLayoutCorrection_"+stamp;AssetDatabase.CreateFolder("Assets/StormSim/Generated","ChurchLayoutCorrection_"+stamp);
        var root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Correct church and pond layout from marked reference");
        var visibility=root.AddComponent<BanagoLocalDetailReview>();var review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;review.notes="User-marked wall route and denser pond-side housing; retained original meshes support undo.";
        var hidden=new List<GameObject>();var hiddenActive=new List<bool>();
        void Hide(GameObject g){if(!g||hidden.Contains(g))return;hidden.Add(g);hiddenActive.Add(g.activeSelf);Undo.RecordObject(g,"Clear misplaced neighbourhood object");g.SetActive(false);}
        var wrong=GameObject.Find("Church-side boundary wall and narrow access lane");int oldPanels=wrong?wrong.GetComponentsInChildren<MeshCollider>().Length:0;if(wrong)Undo.DestroyObjectImmediate(wrong);
        foreach(var name in plan["hiddenInfill"].Values<string>())Hide(GameObject.Find(name));
        var spur=GameObject.Find("Study Area Roads").transform.Find("residential 791977357");if(spur)Hide(spur.gameObject);
        int asset=0,cutTriangles=0,changedMeshes=0;var swaps=new List<BanagoReferenceAreasReview.Swap>();
        void Swap(MeshFilter f,Mesh m){var r=f.GetComponent<Renderer>();var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,after=m,materialsBefore=r.sharedMaterials,materialsAfter=r.sharedMaterials,colliderBefore=c?c.sharedMesh:null});f.sharedMesh=m;if(c)c.sharedMesh=m;}
        // Original houses share neighbourhood meshes. Remove only the selected complete footprints.
        foreach(var t in before)
        {
            if(!t||!t.gameObject.activeInHierarchy)continue;var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();if(!f||!f.sharedMesh||!r)continue;
            bool combined=(t.parent&&t.parent.name.StartsWith("Neighbourhood "))||t.name=="House upper-floor slabs"||t.name.Contains("raised timber floors")||t.name.Contains("short timber shoreline supports")||(t.parent&&t.parent.name=="Street reference test - patchwork homes");
            if(!combined)continue;var b=r.bounds;if(b.max.x< -145||b.min.x>6||b.max.z<753||b.min.z>858)continue;
            var old=f.sharedMesh;var v=old.vertices;var lists=new List<int[]>();int removedHere=0;
            for(int sub=0;sub<old.subMeshCount;sub++){var ix=old.GetTriangles(sub);var keep=new List<int>();for(int i=0;i<ix.Length;i+=3){var p=XZ(t.TransformPoint((v[ix[i]]+v[ix[i+1]]+v[ix[i+2]])/3));if(RemoveAt(p,.85f)){removedHere++;continue;}keep.Add(ix[i]);keep.Add(ix[i+1]);keep.Add(ix[i+2]);}lists.Add(keep.ToArray());}
            if(removedHere==0)continue;var replacement=UnityEngine.Object.Instantiate(old);replacement.name=old.name+" - corrected church layout";for(int sub=0;sub<lists.Count;sub++)replacement.SetTriangles(lists[sub],sub);replacement.RecalculateBounds();AssetDatabase.CreateAsset(replacement,folder+"/Retained neighbourhood "+(++asset)+".asset");Swap(f,replacement);cutTriangles+=removedHere;changedMeshes++;
        }
        // Remove the corresponding small porches, rooftop objects, plants, and frontage additions.
        foreach(var t in before)
        {
            if(!t||!t.gameObject.activeInHierarchy||t.IsChildOf(root.transform))continue;var r=t.GetComponent<Renderer>();if(!r)continue;var b=r.bounds;
            if(b.size.x>26||b.size.z>26||b.max.x< -145||b.min.x>5||b.max.z<753||b.min.z>860)continue;
            string path=t.name+" "+(t.parent?t.parent.name:"");bool named=removed.Any(h=>path.Contains(h.name));
            bool prop=path.Contains("Rooftop")||path.Contains("rooftop")||path.Contains("flowers")||path.Contains("clutter")||path.Contains("frontage")||path.Contains("Baybayin lean-to")||path.Contains("Baybayin raised");
            bool plant=path.Contains("canopy")||path.Contains("Near leaves")||path.Contains("Distant silhouette")||path.Contains("Banana")||path.Contains("palm");
            var p=XZ(b.center);if(named||(prop&&RemoveAt(p,1.1f))||(plant&&(AlongDistance(p,wall)<3.6f||newPolys.Any(h=>In(p,h)||Edge(p,h)<1.3f))))Hide(t.gameObject);
        }
        review.swaps=swaps.ToArray();visibility.originals=hidden.ToArray();visibility.originalActive=hiddenActive.ToArray();visibility.notes="Misplaced infill and obsolete asphalt spur are retained inactive for undo. The earlier incorrect wall group was removed after a scene backup.";EditorUtility.SetDirty(review);EditorUtility.SetDirty(visibility);
        System.IO.File.WriteAllText(backup+"/progress.txt","Cleared original layout; creating corrected geometry.");
        Material Mat(string name,string shaderName){var s=Shader.Find(shaderName);if(!s||ShaderUtil.ShaderHasError(s))throw new Exception("Shader missing: "+shaderName);var m=new Material(s){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
        var concrete=Mat("Stained concrete boundary","StormSim/Banago Reference Areas");concrete.SetFloat("_Mode",7);
        var soil=Mat("Worn lanes and open-lot grass","StormSim/Banago Reference Areas");soil.SetFloat("_Mode",5);
        var wallsMat=Mat("Weathered plaster timber and block houses","StormSim/Weathered Building Surface");wallsMat.SetFloat("_VertexColor",1);wallsMat.SetFloat("_Weathering",.33f);wallsMat.SetFloat("_Cull",0);
        var roofMat=Mat("Patched silver rust and painted GI roofs","StormSim/Weathered Building Surface");roofMat.SetFloat("_VertexColor",1);roofMat.SetFloat("_Weathering",.49f);roofMat.SetFloat("_Corrugation",1);roofMat.SetFloat("_Cull",0);
        GameObject Group(string name){var g=new GameObject(name);g.transform.SetParent(root.transform,false);return g;}
        var wallGroup=Group("Correct wall - follows the red marked boundary");var laneGroup=Group("Small road on house side of wall");var houseGroup=Group("Dense pond-side houses - individually adjustable");var lotGroup=Group("Open grassy construction lot and rough access track");
        void Quad(BanagoEnvironmentModels.Parts p,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color col,Vector3 normal){p.Face(a,b,c,col,normal);p.Face(a,c,d,col,normal);}
        void Box(BanagoEnvironmentModels.Parts p,Vector3 center,Vector3 size,Color col,Quaternion rot=default)
        {
            if(rot==default)rot=Quaternion.identity;var v=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)}.Select(s=>center+rot*Vector3.Scale(s,size*.5f)).ToArray();
            foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}})Quad(p,v[f[0]],v[f[1]],v[f[2]],v[f[3]],col,Vector3.Cross(v[f[1]]-v[f[0]],v[f[2]]-v[f[0]]));
        }
        GameObject Save(string name,BanagoEnvironmentModels.Parts p,Material mat,Transform parent,bool collider=false,bool wallUV=false)
        {
            if(p.vertices.Count==0)return null;var m=p.Mesh(name);m.uv=m.vertices.Select(v=>wallUV?new Vector2(v.x+v.z,v.y):new Vector2(v.x,v.z)).ToArray();AssetDatabase.CreateAsset(m,folder+"/Mesh "+(++asset).ToString("000")+".asset");var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=m;return g;
        }
        var centers=new List<Vector2>();var normals=new List<Vector2>();var distance=new List<float>();float total=0;
        for(int s=0;s<wall.Length-1;s++){var a=wall[s];var b=wall[s+1];var dir=(b-a).normalized;float len=Vector2.Distance(a,b);int n=Mathf.CeilToInt(len/.42f);for(int j=0;j<n;j++){centers.Add(Vector2.Lerp(a,b,j/(float)n));distance.Add(total+len*j/n);var right=new Vector2(dir.y,-dir.x);if(j==0&&s>0){var prev=(wall[s]-wall[s-1]).normalized;var avg=(right+new Vector2(prev.y,-prev.x)).normalized;right=avg/Mathf.Max(.6f,Vector2.Dot(avg,right));}normals.Add(right);}total+=len;}
        centers.Add(wall.Last());var fd=(wall.Last()-wall[wall.Length-2]).normalized;normals.Add(new Vector2(fd.y,-fd.x));distance.Add(total);
        var laneMesh=new BanagoEnvironmentModels.Parts();var laneRepairs=new BanagoEnvironmentModels.Parts();var gutter=new BanagoEnvironmentModels.Parts();
        Vector3 LanePoint(int i,float offset,float lift=.045f){var p=centers[i]+normals[i]*offset;return V(p,Surface(p)+lift);}
        for(int i=0;i<centers.Count-1;i++)
        {
            for(int k=0;k<5;k++){float a=.34f+2.25f*k/5,b=.34f+2.25f*(k+1)/5;var p=centers[i];var col=Color.Lerp(new Color(.56f,.53f,.45f),new Color(.67f,.62f,.51f),Mathf.PerlinNoise(p.x*.26f,p.y*.26f));Quad(laneMesh,LanePoint(i,a),LanePoint(i+1,a),LanePoint(i+1,b),LanePoint(i,b),col,Vector3.up);}
            Quad(gutter,LanePoint(i,.12f,.028f),LanePoint(i+1,.12f,.028f),LanePoint(i+1,.30f,.028f),LanePoint(i,.30f,.028f),new Color(.38f,.405f,.31f),Vector3.up);
            if(i%19<3){float o=.85f+(.7f*Mathf.PerlinNoise(i*2,5));Quad(laneRepairs,LanePoint(i,o-.26f,.051f),LanePoint(i+1,o-.22f,.051f),LanePoint(i+1,o+.25f,.051f),LanePoint(i,o+.34f,.051f),new Color(.63f,.61f,.53f),Vector3.up);}
        }
        Save("Continuous 2.25m worn access road",laneMesh,soil,laneGroup.transform,true);Save("Irregular concrete repairs",laneRepairs,soil,laneGroup.transform);Save("Narrow damp drainage edge",gutter,soil,laneGroup.transform);
        int panels=0;float wallLength=0;
        for(int seg=0;seg<wall.Length-1;seg++)
        {
            var a=wall[seg];var b=wall[seg+1];float len=Vector2.Distance(a,b);int count=Mathf.CeilToInt(len/2.5f);
            for(int j=0;j<count;j++)
            {
                var from=Vector2.Lerp(a,b,j/(float)count);var to=Vector2.Lerp(a,b,(j+1)/(float)count);if((seg==0&&j==0)||Road(from)||Road(to)||Road((from+to)*.5f))continue;
                float low=999,high=-999;for(int q=0;q<=8;q++){float y=Ground(Vector2.Lerp(from,to,q/8f));low=Mathf.Min(low,y);high=Mathf.Max(high,y);}float span=Vector2.Distance(from,to),h=1.85f+high-low;var p=new BanagoEnvironmentModels.Parts();var col=new Color(.62f,.605f,.55f);
                Box(p,new Vector3(0,h*.5f,0),new Vector3(span+.016f,h,.18f),col);Box(p,new Vector3(0,h+.025f,0),new Vector3(span+.025f,.07f,.24f),col);Box(p,new Vector3(-span*.5f,h*.5f+.025f,0),new Vector3(.25f,h+.08f,.27f),col*.97f);
                var g=Save("Wall section "+(++panels).ToString("000"),p,concrete,wallGroup.transform,true,true);g.transform.position=V((from+to)*.5f,low-.07f);var delta=to-from;g.transform.rotation=Quaternion.LookRotation(new Vector3(-delta.y,0,delta.x));wallLength+=span;
            }
        }
        // Compact rows and patched low roofs replace the sparse scattered infill.
        var wallColors=new[]{new Color(.63f,.62f,.55f),new Color(.63f,.56f,.43f),new Color(.54f,.60f,.57f),new Color(.67f,.66f,.60f),new Color(.57f,.53f,.44f)};
        var roofColors=new[]{new Color(.69f,.70f,.67f),new Color(.76f,.75f,.70f),new Color(.48f,.28f,.21f),new Color(.58f,.35f,.29f),new Color(.38f,.49f,.50f),new Color(.36f,.46f,.34f)};
        int added=0;
        foreach(var hp in housePlans)
        {
            var center=P(hp["center"]);float w=(float)hp["width"],d=(float)hp["depth"],angle=(float)hp["angle"];var poly=hp["footprint"].Select(P).ToArray();var rot=Quaternion.Euler(0,angle,0);float lo=999,hi=-999;
            for(float x=-w*.5f;x<=w*.5f+.1f;x+=.4f)for(float z=-d*.5f;z<=d*.5f+.1f;z+=.4f){var p=center+XZ(rot*new Vector3(x,0,z));if(Road(p))throw new Exception("New house intersects main road: "+hp["name"]);float y=Ground(p);lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);}
            var g=new GameObject((string)hp["name"]);g.transform.SetParent(houseGroup.transform,false);g.transform.SetPositionAndRotation(V(center,hi+.055f),rot);int index=added++;float height=2.40f+(index%5)*.13f,rise=.30f+(index%4)*.13f;var body=new BanagoEnvironmentModels.Parts();var roof=new BanagoEnvironmentModels.Parts();var detail=new BanagoEnvironmentModels.Parts();var color=wallColors[index%wallColors.Length];
            Box(body,new Vector3(0,-(hi-lo+.15f)*.5f,0),new Vector3(w,hi-lo+.15f,d),new Color(.46f,.45f,.39f));Box(body,new Vector3(0,height*.5f,0),new Vector3(w,height,d),color);
            body.Face(new Vector3(-w*.5f,height,-d*.5f),new Vector3(w*.5f,height,-d*.5f),new Vector3(0,height+rise,-d*.5f),color,Vector3.back);body.Face(new Vector3(w*.5f,height,d*.5f),new Vector3(-w*.5f,height,d*.5f),new Vector3(0,height+rise,d*.5f),color,Vector3.forward);
            var dark=new Color(.15f,.19f,.18f);Box(detail,new Vector3(-w*.25f,1.0f,-d*.5f-.028f),new Vector3(.85f,2.0f,.07f),new Color(.36f,.32f,.26f));Box(detail,new Vector3(w*.20f,1.52f,-d*.5f-.04f),new Vector3(1.1f,.85f,.08f),dark);Box(detail,new Vector3(w*.5f+.035f,1.5f,0),new Vector3(.075f,.82f,1.15f),dark);
            for(int bar=0;bar<5;bar++)Box(detail,new Vector3(w*.20f-.48f+bar*.24f,1.52f,-d*.5f-.10f),new Vector3(.025f,.85f,.028f),color*.7f);
            if(index%3==0)for(float z=-d*.46f;z<d*.47f;z+=.23f)Box(detail,new Vector3(-w*.5f-.015f,height*.47f,z),new Vector3(.022f,height*.92f,.025f),color*.77f);
            for(float z=-d*.5f-.16f;z<d*.5f+.16f;z+=.78f){float end=Mathf.Min(z+.79f,d*.5f+.16f);int strip=Mathf.FloorToInt((z+d*.5f+.16f)/.78f);var rc=roofColors[(index*5+strip/2)%roofColors.Length];Quad(roof,new Vector3(-w*.5f-.17f,height,z),new Vector3(0,height+rise,z),new Vector3(0,height+rise,end),new Vector3(-w*.5f-.17f,height,end),rc,Vector3.up);Quad(roof,new Vector3(0,height+rise,z),new Vector3(w*.5f+.17f,height,z),new Vector3(w*.5f+.17f,height,end),new Vector3(0,height+rise,end),roofColors[(index*5+strip/3+1)%roofColors.Length],Vector3.up);}
            Save("Walls and grounded foundation",body,wallsMat,g.transform,true,true);Save("Doors window grilles and timber repairs",detail,wallsMat,g.transform,false,true);Save("Low patched corrugated roof",roof,roofMat,g.transform);
        }
        // Restore the open lot's grass and worn soil, sampled on the existing terrain.
        var lot=new BanagoEnvironmentModels.Parts();var grass=new BanagoEnvironmentModels.Parts();var siteTrack=new BanagoEnvironmentModels.Parts();var waste=new BanagoEnvironmentModels.Parts();int lotCells=0;
        var church=GameObject.Find("Banago Adventist church and light bamboo coastal homes");Bounds churchBounds=default;if(church){var rs=church.GetComponentsInChildren<Renderer>();if(rs.Length>0){churchBounds=rs[0].bounds;foreach(var r in rs)churchBounds.Encapsulate(r.bounds);churchBounds.Expand(1.4f);}}
        bool LotSafe(Vector2 p)=>In(p,open)&&!Road(p)&&AlongDistance(p,wall)>.18f&&!(church&&p.x>churchBounds.min.x&&p.x<churchBounds.max.x&&p.y>churchBounds.min.z&&p.y<churchBounds.max.z);
        for(float x=-142;x< -48;x+=.9f)for(float z=762;z<848;z+=.9f)
        {
            var cs=new[]{new Vector2(x,z),new Vector2(x+.9f,z),new Vector2(x+.9f,z+.9f),new Vector2(x,z+.9f)};if(cs.Any(p=>!LotSafe(p)))continue;var center=new Vector2(x+.45f,z+.45f);float roadDist=AlongDistance(center,track);float vegetation=Mathf.SmoothStep(0,1,Mathf.Clamp01((roadDist-2.0f)/3.5f));float noise=Mathf.PerlinNoise(x*.065f,z*.065f);vegetation*=Mathf.SmoothStep(0,1,noise*1.5f);
            var dirt=new Color(.59f,.545f,.44f);var green=Color.Lerp(new Color(.31f,.40f,.23f),new Color(.44f,.50f,.29f),Mathf.PerlinNoise(x*.24f,z*.24f));var col=Color.Lerp(dirt,green,vegetation);var vs=cs.Select(p=>V(p,Ground(p)+.028f)).ToArray();Quad(lot,vs[0],vs[3],vs[2],vs[1],col,Vector3.up);lotCells++;
            if(vegetation>.62f&&Mathf.PerlinNoise(x*2.3f,z*2.1f)>.68f&&lotCells%3==0){var pos=V(center,Ground(center)+.035f);for(int blade=0;blade<3;blade++){float a=blade*1.4f;var side=new Vector3(Mathf.Cos(a)*.16f,0,Mathf.Sin(a)*.16f);Quad(grass,pos-side,pos+side,pos+side*.15f+Vector3.up*.28f,pos-side*.15f+Vector3.up*.28f,green*.90f,Vector3.up);}}
        }
        for(int s=0;s<track.Length-1;s++){var a=track[s];var b=track[s+1];var dir=(b-a).normalized;var normal=new Vector2(-dir.y,dir.x);float len=Vector2.Distance(a,b);int n=Mathf.CeilToInt(len/.8f);for(int j=0;j<n;j++){var p=Vector2.Lerp(a,b,j/(float)n);var q=Vector2.Lerp(a,b,(j+1)/(float)n);float width=2.0f+.23f*Mathf.PerlinNoise(j*.17f,s);var cs=new[]{p-normal*width,q-normal*width,q+normal*width,p+normal*width};if(cs.Any(v=>AlongDistance(v,wall)<1.0f))continue;var vs=cs.Select(v=>V(v,Surface(v)+.035f)).ToArray();Quad(siteTrack,vs[0],vs[1],vs[2],vs[3],new Color(.65f,.59f,.48f),Vector3.up);}}
        foreach(var p in new[]{new Vector2(-106,821),new Vector2(-94,824),new Vector2(-88,817),new Vector2(-111,830),new Vector2(-83,828)}){float y=Ground(p);for(int j=0;j<6;j++)Box(waste,V(p+new Vector2((j%3)*.54f,(j/3)*.8f),y+.12f),new Vector3(.42f,.24f,.72f),new Color(.57f,.555f,.50f),Quaternion.Euler(0,j*17,3));}
        Save("Irregular grassy open lot with worn soil",lot,soil,lotGroup.transform);Save("Sparse low grass in vacant ground",grass,soil,lotGroup.transform);Save("Rough unmarked access track through open lot",siteTrack,soil,lotGroup.transform,true);Save("Small stacks of concrete and construction rubble",waste,wallsMat,lotGroup.transform);
        int invalid=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertices.Count(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)));
        if(invalid>0)throw new Exception("Invalid generated geometry");
        var result=new{root=title,folder,backup,oldIncorrectWallRemoved=wrong==null,oldWallColliderCount=oldPanels,originalHousesCleared=removed.Length,infillHousesCleared=plan["hiddenInfill"].Count(),denseHousesAdded=added,removedHouseTriangles=cutTriangles,retainedNeighbourhoodMeshes=changedMeshes,wallPanels=panels,wallLengthMetres=wallLength,laneWidthMetres=2.25f,laneRouteLengthMetres=total,openLotCells=lotCells,hiddenObjects=hidden.Count,invalidVertices=invalid};
        System.IO.File.WriteAllText("RoadRepairBackup/church-layout-correction-latest.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));System.IO.File.WriteAllText(backup+"/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");Selection.activeGameObject=root;SceneView.RepaintAll();return result;
    }

    private static object RefineAppearance()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        var root=GameObject.Find("Church and pond neighbourhood - corrected reference layout");
        if(!root||EditorApplication.isPlaying)throw new Exception("Open the corrected scene in Edit mode.");
        var report=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("RoadRepairBackup/church-layout-correction-latest.json"));
        string folder=(string)report["folder"];
        var plan=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Editor/ReferenceData/ChurchPondLayout.json"));
        Vector2 P(Newtonsoft.Json.Linq.JToken t)=>new Vector2((float)t[0],(float)t[1]);
        float Segment(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
        float Edge(Vector2 p,Vector2[] poly){float d=999;for(int i=0;i<poly.Length;i++)d=Mathf.Min(d,Segment(p,poly[i],poly[(i+1)%poly.Length]));return d;}
        float Route(Vector2 p,Vector2[] line){float d=999;for(int i=0;i<line.Length-1;i++)d=Mathf.Min(d,Segment(p,line[i],line[i+1]));return d;}
        var rng=new System.Random(9714);
        float Rand(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
        var palette=new[]{new Color(.60f,.62f,.60f),new Color(.70f,.70f,.65f),new Color(.55f,.56f,.54f),new Color(.45f,.28f,.21f),new Color(.52f,.32f,.26f),new Color(.31f,.41f,.40f),new Color(.29f,.38f,.27f)};
        var homes=root.transform.Find("Dense pond-side houses - individually adjustable");
        int index=0;
        foreach(Transform home in homes)
        {
            var hp=plan["newHouses"][index];
            // Keep all modifications inside the already-cleared home envelope.
            home.localScale=new Vector3(Rand(.90f,.985f),Rand(.94f,1.16f),Rand(.89f,.98f));
            home.rotation=Quaternion.Euler(0,(float)hp["angle"]+Rand(-1.4f,1.4f),0);
            home.position+=new Vector3(Rand(-.10f,.10f),0,Rand(-.14f,.14f));
            var f=home.Find("Low patched corrugated roof").GetComponent<MeshFilter>();
            var m=f.sharedMesh;var v=m.vertices;var colors=m.colors;int dominant=rng.Next(10);dominant=dominant<5?rng.Next(3):dominant<8?rng.Next(3,5):rng.Next(5,7);
            var baseColor=palette[dominant];int patch1=rng.Next(v.Length/6),patch2=rng.Next(v.Length/6);
            for(int i=0;i<v.Length;i+=6)
            {
                int panel=i/6;var col=baseColor*Rand(.89f,1.10f);
                if(panel==patch1||panel==patch2)col=palette[rng.Next(palette.Length)];
                col.a=1;for(int j=i;j<Mathf.Min(i+6,v.Length);j++)colors[j]=col;
            }
            m.colors=colors;EditorUtility.SetDirty(m);
            var body=home.Find("Walls and grounded foundation").GetComponent<MeshFilter>().sharedMesh;
            var bcol=body.colors;var wc=new[]{new Color(.62f,.62f,.56f),new Color(.57f,.58f,.54f),new Color(.58f,.50f,.39f),new Color(.43f,.52f,.49f),new Color(.65f,.64f,.58f)}[rng.Next(5)];
            for(int i=36;i<bcol.Length;i++)bcol[i]=wc;body.colors=bcol;EditorUtility.SetDirty(body);
            index++;
        }
        var open=plan["openLot"].Select(P).ToArray();var track=plan["track"].Select(P).ToArray();
        AssetDatabase.ImportAsset("Assets/StormSim/Generated/BanagoReferenceLotGround.shader",ImportAssetOptions.ForceSynchronousImport);
        var shader=Shader.Find("StormSim/Reference Lot Ground");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Ground shader did not compile.");
        var material=new Material(shader){name="Feathered grass and worn earth"};AssetDatabase.CreateAsset(material,folder+"/Feathered grass and worn earth.mat");
        var lot=root.transform.Find("Open grassy construction lot and rough access track");
        var lotMesh=lot.Find("Irregular grassy open lot with worn soil").GetComponent<MeshFilter>().sharedMesh;
        var lv=lotMesh.vertices;var lc=lotMesh.colors;
        for(int i=0;i<lv.Length;i++)
        {
            var p=new Vector2(lv[i].x,lv[i].z);float ed=Edge(p,open);float t=Route(p,track);float veg=Mathf.SmoothStep(0,1,(t-1.8f)/4f)*Mathf.SmoothStep(0,1,Mathf.PerlinNoise(p.x*.14f+41,p.y*.14f)*1.3f);
            var green=Color.Lerp(new Color(.40f,.50f,.28f),new Color(.52f,.56f,.34f),Mathf.PerlinNoise(p.x*.67f,p.y*.67f));
            var dirt=new Color(.65f,.595f,.47f);lc[i]=Color.Lerp(dirt,green,veg);lc[i].a=Mathf.SmoothStep(0,1,ed/3.1f);lv[i].y+=.018f;
        }
        lotMesh.vertices=lv;lotMesh.colors=lc;lotMesh.RecalculateBounds();EditorUtility.SetDirty(lotMesh);
        lot.Find("Irregular grassy open lot with worn soil").GetComponent<Renderer>().sharedMaterial=material;
        var tm=lot.Find("Rough unmarked access track through open lot").GetComponent<MeshFilter>().sharedMesh;var tv=tm.vertices;var tc=tm.colors;
        for(int i=0;i<tv.Length;i++){var p=new Vector2(tv[i].x,tv[i].z);tc[i]=new Color(.68f,.61f,.49f,.30f);tv[i].y+=.025f;}tm.vertices=tv;tm.colors=tc;tm.RecalculateBounds();EditorUtility.SetDirty(tm);
        lot.Find("Rough unmarked access track through open lot").GetComponent<Renderer>().sharedMaterial=material;
        // The water's original height and shoreline are retained; brighten the local pond's near-black surface.
        var pond=GameObject.Find("Pond 24 - still water");var pr=pond.GetComponent<Renderer>();var old=pr.sharedMaterials;
        var water=new Material(pr.sharedMaterial){name="Pond beside corrected homes - grey green water"};water.SetColor("_BaseColor",new Color(.39f,.455f,.45f,1));AssetDatabase.CreateAsset(water,folder+"/Pond grey green water.mat");
        pr.sharedMaterial=water;var review=root.GetComponent<BanagoReferenceAreasReview>();var pf=pond.GetComponent<MeshFilter>();
        review.swaps=review.swaps.Concat(new[]{new BanagoReferenceAreasReview.Swap{target=pf,before=pf.sharedMesh,after=pf.sharedMesh,materialsBefore=old,materialsAfter=new[]{water},colliderBefore=pond.GetComponent<MeshCollider>()?pond.GetComponent<MeshCollider>().sharedMesh:null}}).ToArray();EditorUtility.SetDirty(review);
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);bool saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return new{refinedHomes=index,pondHeightUnchanged=true,saved};
    }
}
