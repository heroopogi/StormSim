using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>Builds the saved street-reference road materials and scene details.</summary>
public static class BanagoStreetReferenceBuilder
{
    [MenuItem("Tools/StormSim/Create Felix Amante Street Reference")]
    public static void Build()
    {
        BuildSurfaces();
        BuildDressing();
        FitCanopies();
    }

    private static object BuildSurfaces()
    {
        const string title="Felix Amante street reference - dirt mud school and utilities";
        var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Open MAIN_GAME in Edit mode; avoid duplicate street refinements.");
        var shaderPath="Assets/StormSim/StreetReference/BanagoStreetSurface.shader";AssetDatabase.ImportAsset(shaderPath,ImportAssetOptions.ForceSynchronousImport);
        var shader=Shader.Find("StormSim/Reference Dirt Mud and Paving");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Street shader failed compilation.");
        Texture2D Tex(string name,bool normal=false){string p="Assets/StormSim/StreetReference/Textures/"+name;AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(p);}
        var dirt=Tex("PackedDirt_Albedo.png");var mud=Tex("WetMud_Albedo.png");var normal=Tex("DirtRuts_Normal.png",true);
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss"),folder="Assets/StormSim/StreetReference/Generated_"+stamp,backup="RoadRepairBackup/StreetReference_"+stamp;System.IO.Directory.CreateDirectory(backup);
        if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-street-reference.unity",true))throw new Exception("Backup failed.");
        AssetDatabase.CreateFolder("Assets/StormSim/StreetReference","Generated_"+stamp);
        var root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Refine road surfaces from street photographs");
        var review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;review.notes="Road centerlines and the Cesium-derived terrain are retained. Vertex red paints wet mud; green paints pavement; alpha softens shoulders.";
        var visibility=root.AddComponent<BanagoLocalDetailReview>();var hidden=new List<GameObject>();var states=new List<bool>();
        var roadRoot=GameObject.Find("Study Area Roads");var grounds=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&c.sharedMesh&&c.name.StartsWith("Measured terrain")).ToArray();
        float Ground(Vector3 p,float fallback){foreach(var c in grounds){var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.z<b.min.z||p.z>b.max.z)continue;if(c.Raycast(new Ray(new Vector3(p.x,50,p.z),Vector3.down),out var hit,100))return hit.point.y;}return fallback;}
        var material=new Material(shader){name="Felix Amante - dirt mud and school pavement"};material.SetTexture("_MainTex",dirt);material.SetTexture("_MudTex",mud);material.SetTexture("_NormalMap",normal);material.SetVector("_TileSize",new Vector4(4,8,0,0));material.SetFloat("_MudAmount",.43f);material.SetFloat("_PuddleAmount",.80f);material.SetFloat("_NormalStrength",1.05f);AssetDatabase.CreateAsset(material,folder+"/Street dirt mud pavement.mat");
        var swaps=new List<BanagoReferenceAreasReview.Swap>();int number=0;float length=0;var routes=new List<object>();
        void Swap(MeshFilter f,Mesh m){var r=f.GetComponent<Renderer>();var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,after=m,materialsBefore=r.sharedMaterials,materialsAfter=new[]{material},colliderBefore=c?c.sharedMesh:null});AssetDatabase.CreateAsset(m,folder+"/Road surface "+(++number)+".asset");f.sharedMesh=m;r.sharedMaterial=material;if(c)c.sharedMesh=m;}
        Color Paint(Vector3 p,float cross,float alpha,bool lane=false){float paved=lane?0:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(678,704,p.z));float low=Mathf.Exp(-Mathf.Pow((p.z-757)/14,2))*.40f+Mathf.Exp(-Mathf.Pow((p.z-794)/9,2))*.31f;float wet=Mathf.Clamp01(.23f+low+.24f*Mathf.PerlinNoise(p.x*.17f,p.z*.17f)+Mathf.Abs(cross)*.13f);return new Color(wet,paved,0,alpha);}
        var selected=roadRoot.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject.activeInHierarchy&&f.name=="Felix Amante Drive"&&f.GetComponent<Renderer>().bounds.min.z>=600&&f.GetComponent<Renderer>().bounds.max.z<823).ToArray();
        AssetDatabase.StartAssetEditing();
        try
        {
         foreach(var f in selected)
         {
          var src=f.sharedMesh;var old=src.vertices.Select(f.transform.TransformPoint).ToArray();if(old.Length%2!=0)throw new Exception("Road does not use paired edge vertices.");
          var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();var centers=new List<Vector3>();float along=0;int crossSteps=18,rows=0;
          for(int pair=0;pair<old.Length/2-1;pair++)
          {
           var ca=(old[pair*2]+old[pair*2+1])*.5f;var cb=(old[pair*2+2]+old[pair*2+3])*.5f;float len=Vector3.Distance(ca,cb);int steps=Mathf.Max(1,Mathf.CeilToInt(len/.60f));
           for(int j=0;j<steps+(pair==old.Length/2-2?1:0);j++)
           {
            float t=j/(float)steps;var left=Vector3.Lerp(old[pair*2],old[pair*2+2],t);var right=Vector3.Lerp(old[pair*2+1],old[pair*2+3],t);var center=(left+right)*.5f;var across=(right-left);float width=new Vector2(across.x,across.z).magnitude;var flat=Vector3.ProjectOnPlane(across,Vector3.up).normalized;
            float shoulder=.70f+.22f*Mathf.PerlinNoise(center.x*.47f,center.z*.47f);centers.Add(center);float full=width+2*shoulder;
            for(int k=0;k<=crossSteps;k++)
            {
             float u=-shoulder+full*k/crossSteps;float tside=u/width;var p=Vector3.LerpUnclamped(left,right,tside);float outside=Mathf.Max(-u,u-width,0);if(outside>0)p.y=Mathf.Lerp(p.y,Ground(p,p.y-.14f),Mathf.Clamp01(outside/shoulder));p.y+=.026f;
             float edge=Mathf.Min(u+shoulder,width+shoulder-u);float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/(shoulder+.25f)));vertices.Add(f.transform.InverseTransformPoint(p));uv.Add(new Vector2(u,along+len*t));colors.Add(Paint(p,(u-width*.5f)/(width*.5f),alpha));
             if(rows>0&&k>0){int a=vertices.Count-1,b=a-1,c=a-crossSteps-1,d=c-1;triangles.AddRange(new[]{d,b,c,b,a,c});}
            }
            rows++;
           }
           along+=len;
          }
          var m=new Mesh{name=f.name+" - reference dirt mud transition",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};m.SetVertices(vertices);m.SetUVs(0,uv);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();Swap(f,m);length+=along;
          routes.Add(new{name=f.name,centers=centers.Select(v=>new[]{v.x,v.y,v.z}).ToArray()});
          foreach(Transform child in f.transform)if(child.name.Contains("markings")&&child.gameObject.activeSelf){hidden.Add(child.gameObject);states.Add(true);child.gameObject.SetActive(false);}
         }
         foreach(var name in new[]{"Junction 7403901869","Junction 7403939054"})
         {
          var g=roadRoot.transform.Find(name);if(!g)continue;var f=g.GetComponent<MeshFilter>();var m=UnityEngine.Object.Instantiate(f.sharedMesh);m.name=name+" - matching surface";var v=m.vertices;var uv=new Vector2[v.Length];var col=new Color[v.Length];for(int i=0;i<v.Length;i++){var p=g.TransformPoint(v[i]);v[i]+=Vector3.up*.026f;uv[i]=new Vector2(p.x,p.z);col[i]=Paint(p,0,1);}m.vertices=v;m.uv=uv;m.colors=col;Swap(f,m);
         }
         var wallRoot=GameObject.Find("Church and pond neighbourhood - corrected reference layout");var lane=wallRoot.transform.Find("Small road on house side of wall/Continuous 2.25m worn access road").GetComponent<MeshFilter>();var laneMesh=UnityEngine.Object.Instantiate(lane.sharedMesh);
         var plan=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Editor/ReferenceData/ChurchPondLayout.json"));var wall=plan["wall"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();
         var laneV=laneMesh.vertices;var laneUv=new Vector2[laneV.Length];var laneC=new Color[laneV.Length];
         for(int i=0;i<laneV.Length;i++){var p=lane.transform.TransformPoint(laneV[i]);var xz=new Vector2(p.x,p.z);float best=999,total=0,u=0,v=0;for(int s=0;s<wall.Length-1;s++){var d=wall[s+1]-wall[s];float len=d.magnitude;float t=Mathf.Clamp01(Vector2.Dot(xz-wall[s],d)/d.sqrMagnitude);var q=wall[s]+d*t;float dist=Vector2.Distance(xz,q);if(dist<best){best=dist;u=Vector2.Dot(xz-q,new Vector2(d.y,-d.x).normalized)-.34f;v=total+len*t;}total+=len;}laneUv[i]=new Vector2(u,v);laneC[i]=Paint(p,0,Mathf.Lerp(.28f,1,Mathf.Clamp01(Mathf.Min(u,2.25f-u)/.18f)),true);}
         laneMesh.uv=laneUv;laneMesh.colors=laneC;Swap(lane,laneMesh);
         var repairs=wallRoot.transform.Find("Small road on house side of wall/Irregular concrete repairs");hidden.Add(repairs.gameObject);states.Add(repairs.gameObject.activeSelf);repairs.gameObject.SetActive(false);
        }
        finally{AssetDatabase.StopAssetEditing();}
        review.swaps=swaps.ToArray();visibility.originals=hidden.ToArray();visibility.originalActive=states.ToArray();visibility.notes="Retains any replaced lane patches or road markings for restoration.";EditorUtility.SetDirty(review);EditorUtility.SetDirty(visibility);
        var result=new{root=title,folder,backup,roadSurfaces=number,mainRoadLength=length,pavementTransitionZ=new[]{704,678},terrainUnchanged=true,routes};System.IO.File.WriteAllText("RoadRepairBackup/street-reference-latest.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);bool saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);return new{saved,roadSurfaces=number,mainRoadLength=length,folder};
    }

    private static object BuildDressing()
    {
        var root=GameObject.Find("Felix Amante street reference - dirt mud school and utilities");if(!root||root.transform.Find("Street frontage details"))throw new Exception("Road surface pass is missing or dressing already exists.");
        var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();var info=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("RoadRepairBackup/street-reference-latest.json"));string folder=(string)info["folder"];
        var plan=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Editor/ReferenceData/StreetDressing.json"));Vector2 P(Newtonsoft.Json.Linq.JToken p)=>new Vector2((float)p[0],(float)p[1]);Vector3 V(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
        var road=plan["road"].Select(p=>new Vector3((float)p[0],(float)p[1],(float)p[2])).ToArray();var current=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&c.sharedMesh&&c.gameObject.activeInHierarchy).ToArray();var grounds=current.Where(c=>c.name.StartsWith("Measured terrain")).ToArray();
        var studyRoad=GameObject.Find("Study Area Roads").transform;var obstacles=current.Where(c=>!c.name.StartsWith("Measured terrain")&&!c.transform.IsChildOf(studyRoad)&&c.bounds.max.x> -162&&c.bounds.min.x<40&&c.bounds.max.z>604&&c.bounds.min.z<825&&!c.name.Contains("water")&&!c.name.Contains("bank")&&!c.name.Contains("road")&&!c.name.Contains("lot")).ToArray();
        bool Hit(MeshCollider c,Vector2 p,out float y){y=0;var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)return false;if(!c.Raycast(new Ray(V(p,40),Vector3.down),out var h,80))return false;y=h.point.y;return true;}
        float Ground(Vector2 p){foreach(var c in grounds)if(Hit(c,p,out float y))return y;return float.NaN;}
        bool Clear(Vector2 p,float radius=.3f){float g=Ground(p);if(!float.IsFinite(g))return false;foreach(var d in new[]{Vector2.zero,Vector2.right*radius,Vector2.left*radius,Vector2.up*radius,Vector2.down*radius})foreach(var c in obstacles)if(Hit(c,p+d,out float y)&&y>g+.48f)return false;return true;}
        (Vector2 point,Vector2 tangent,float distance) Nearest(Vector2 p){float best=999;Vector2 qbest=default,dir=default;for(int i=0;i<road.Length-1;i++){var a=new Vector2(road[i].x,road[i].z);var b=new Vector2(road[i+1].x,road[i+1].z);var d=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);var q=a+d*t;float dist=Vector2.Distance(p,q);if(dist<best){best=dist;qbest=q;dir=d.normalized;}}return(qbest,dir,best);}
        Vector2 At(float z){var a=road.OrderBy(p=>Mathf.Abs(p.z-z)).First();return new Vector2(a.x,a.z);}
        GameObject Group(string name){var g=new GameObject(name);g.transform.SetParent(root.transform,false);return g;}
        var front=Group("Street frontage details");var utility=Group("Irregular poles cable bundles and service drops");var trees=Group("Roadside shade and pond edge vegetation");var school=Group("School frontage - fence waiting shelter and palms");var debris=Group("Small debris along dirt shoulders");
        Material Mat(string name,bool metal=false){var m=new Material(Shader.Find("StormSim/Weathered Building Surface")){name=name,enableInstancing=true};m.SetFloat("_VertexColor",1);m.SetFloat("_Weathering",metal?.48f:.26f);m.SetFloat("_Corrugation",metal?1:0);m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
        var matte=Mat("Street timber plaster and foliage");var metal=Mat("Individual weathered tin and tarp roofs",true);
        void Quad(BanagoEnvironmentModels.Parts p,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,Vector3 normal){p.Face(a,b,c,color,normal);p.Face(a,c,d,color,normal);}
        void Box(BanagoEnvironmentModels.Parts p,Vector3 center,Vector3 size,Color color,Quaternion rotation=default){if(rotation==default)rotation=Quaternion.identity;var v=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)}.Select(x=>center+rotation*Vector3.Scale(x,size*.5f)).ToArray();foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}})Quad(p,v[f[0]],v[f[1]],v[f[2]],v[f[3]],color,Vector3.Cross(v[f[1]]-v[f[0]],v[f[2]]-v[f[0]]));}
        void Tube(BanagoEnvironmentModels.Parts p,Vector3 a,Vector3 b,float radius,Color color,int sides=6){var axis=(b-a).normalized;var right=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var forward=Vector3.Cross(axis,right);for(int i=0;i<sides;i++){float aa=i*Mathf.PI*2/sides,bb=(i+1)*Mathf.PI*2/sides;var u=right*Mathf.Cos(aa)+forward*Mathf.Sin(aa);var v=right*Mathf.Cos(bb)+forward*Mathf.Sin(bb);Quad(p,a+u*radius,a+v*radius,b+v*radius,b+u*radius,color,(u+v).normalized);}}
        int assets=0;GameObject Save(string name,BanagoEnvironmentModels.Parts p,Material mat,Transform parent,bool collider=false){if(p.vertices.Count==0)return null;var m=p.Mesh(name);m.uv=m.vertices.Select(v=>new Vector2(v.x+v.z,v.y+v.z*.09f)).ToArray();AssetDatabase.CreateAsset(m,folder+"/Detail "+(++assets)+".asset");var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=m;return g;}
        var roofs=new BanagoEnvironmentModels.Parts();var frames=new BanagoEnvironmentModels.Parts();var small=new BanagoEnvironmentModels.Parts();var palette=new[]{new Color(.58f,.60f,.56f),new Color(.43f,.26f,.19f),new Color(.49f,.29f,.22f),new Color(.27f,.39f,.30f),new Color(.27f,.41f,.52f)};var rng=new System.Random(34821);float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
        int frontageCount=0;
        foreach(var hp in plan["frontages"])
        {
         var center=P(hp["center"]);float ground=Ground(center);if(!float.IsFinite(ground))continue;var out2=P(hp["outward"]);var outward=V(out2,0);var right=new Vector3(outward.z,0,-outward.x);float width=(float)hp["width"],depth=(float)hp["depth"],height=(float)hp["height"];
         var pos=V(center,ground+height);var col=palette[frontageCount%palette.Length];int strips=Mathf.CeilToInt(width/.78f);
         for(int j=0;j<strips;j++){float a=-width*.5f+width*j/strips,b=-width*.5f+width*(j+1)/strips;Quad(roofs,pos+right*a-outward*depth*.5f+Vector3.up*.14f,pos+right*b-outward*depth*.5f+Vector3.up*.14f,pos+right*b+outward*depth*.5f,pos+right*a+outward*depth*.5f,col*R(.91f,1.06f),Vector3.up);}
         foreach(float sign in new[]{-1f,1f}){var p=center+new Vector2(right.x,right.z)*width*.45f*sign+out2*depth*.42f;if(Nearest(p).distance<3.7f)continue;float y=Ground(p);if(!float.IsFinite(y))continue;Tube(frames,V(p,y),V(p,ground+height),.035f,new Color(.41f,.35f,.25f));}
         var counterPos=V(center-out2*depth*.32f,ground+.82f);Box(frames,counterPos,new Vector3(width*.70f,.085f,.33f),new Color(.46f,.38f,.27f),Quaternion.LookRotation(outward));
         // Small facade additions stay beneath awnings and out of the travelled strip.
         if(frontageCount%3==0)for(int k=0;k<3;k++)Box(small,counterPos+right*(-.40f+k*.32f)+Vector3.up*.15f,new Vector3(.20f,.25f,.18f),new Color(.34f+.10f*k,.40f,.27f));
         frontageCount++;
        }
        Save("Mismatched corrugated shop and house awnings",roofs,metal,front.transform);Save("Timber awning supports and counters",frames,matte,front.transform);Save("Small frontage containers",small,matte,front.transform);
        // Recolour only the roof triangles of nearby retained homes; no neighbouring footprint changes.
        var review=root.GetComponent<BanagoReferenceAreasReview>();var swaps=review.swaps.ToList();var hplans=plan["nearHouses"].Select((h,i)=>new{poly=h["footprint"].Select(P).ToArray(),color=palette[(i*7+3)%palette.Length]}).ToArray();
        bool In(Vector2 p,Vector2[] poly){bool b=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)b=!b;return b;}
        int roofMeshes=0;
        foreach(var f in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(f=>f.name=="metal"&&f.transform.parent&&f.transform.parent.name.StartsWith("Neighbourhood ")).ToArray())
        {
         var r=f.GetComponent<Renderer>();if(r.bounds.max.x< -155||r.bounds.min.x>16||r.bounds.max.z<640||r.bounds.min.z>815)continue;var src=f.sharedMesh;var colors=src.colors;if(colors.Length!=src.vertexCount)continue;var vertices=src.vertices;var tri=src.triangles;int count=0;
         for(int i=0;i<tri.Length;i+=3){var p=f.transform.TransformPoint((vertices[tri[i]]+vertices[tri[i+1]]+vertices[tri[i+2]])/3);var h=hplans.FirstOrDefault(h=>In(new Vector2(p.x,p.z),h.poly));if(h==null)continue;for(int k=0;k<3;k++)colors[tri[i+k]]=Color.Lerp(colors[tri[i+k]],h.color,.75f);count++;}
         if(count==0)continue;var m=UnityEngine.Object.Instantiate(src);m.name=src.name+" - varied Felix Amante roofs";m.colors=colors;AssetDatabase.CreateAsset(m,folder+"/Retained roof palette "+(++roofMeshes)+".asset");var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=src,after=m,materialsBefore=r.sharedMaterials,materialsAfter=r.sharedMaterials,colliderBefore=c?c.sharedMesh:null});f.sharedMesh=m;if(c)c.sharedMesh=m;
        }
        var poles=new List<Vector3>();var poleMesh=new BanagoEnvironmentModels.Parts();var cableMesh=new BanagoEnvironmentModels.Parts();var black=new Color(.11f,.12f,.11f);int poleIndex=0;
        for(float z=799;z>=613;z-=19.5f)
        {
         var center=At(z);var near=Nearest(center);var right=new Vector2(near.tangent.y,-near.tangent.x);Vector2 found=default;bool okay=false;int preferred=poleIndex%3==0?-1:1;
         foreach(int sign in new[]{preferred,-preferred}){foreach(float offset in new[]{4.5f,5.0f,5.6f,6.2f}){var p=center+right*offset*sign;if(Clear(p,.17f)){found=p;okay=true;break;}}if(okay)break;}
         if(!okay)continue;float y=Ground(found);var bottom=V(found,y);float height=7.6f+R(-.3f,.45f);var top=bottom+Vector3.up*height;Tube(poleMesh,bottom,top,.105f,new Color(.48f,.46f,.38f),8);var arm=V(right,0)*1.15f;Tube(poleMesh,top-arm-Vector3.up*.35f,top+arm-Vector3.up*.35f,.05f,new Color(.38f,.34f,.27f));
         for(int k=0;k<4;k++)Box(poleMesh,top+arm*(-.8f+k*.53f)-Vector3.up*.20f,new Vector3(.08f,.20f,.08f),new Color(.57f,.61f,.57f));
         for(int loop=0;loop<3;loop++)for(int k=0;k<22;k++){float a=k*Mathf.PI*2/22,b=(k+1)*Mathf.PI*2/22;var cp=top-Vector3.up*(1.1f+loop*.22f);Tube(cableMesh,cp+new Vector3(Mathf.Cos(a)*.30f,Mathf.Sin(a)*.30f,loop*.025f),cp+new Vector3(Mathf.Cos(b)*.30f,Mathf.Sin(b)*.30f,loop*.025f),.014f,black,5);}
         poles.Add(top);poleIndex++;
        }
        float minCable=999;int spans=0;
        for(int i=0;i<poles.Count-1;i++)for(int wire=0;wire<10;wire++)
        {
         var a=poles[i]+new Vector3((wire%5-2)*.12f,-.35f-(wire/5)*.38f,0);var b=poles[i+1]+new Vector3((wire%5-2)*.13f,-.35f-(wire/5)*.35f,0);float sag=.44f+(wire%4)*.13f;
         Vector3 Curve(float t)=>Vector3.Lerp(a,b,t)-Vector3.up*(4*sag*t*(1-t));
         for(int j=0;j<28;j++){var from=Curve(j/28f);var to=Curve((j+1)/28f);Tube(cableMesh,from,to,wire%3==0?.017f:.010f,black,5);var p=(from+to)*.5f;float g=Ground(new Vector2(p.x,p.z));if(float.IsFinite(g))minCable=Mathf.Min(minCable,p.y-g);}
         spans++;
        }
        Save("Irregular concrete and timber utility poles",poleMesh,matte,utility.transform,true);Save("Sagging cable bundles and loops",cableMesh,matte,utility.transform);
        // Shared near-detail tropical meshes add a canopy instead of a regular planting grid.
        var meshMango=BanagoDenseCanopyModels.Mango(2,true);var meshBamboo=BanagoDenseCanopyModels.Bamboo(1,true);var meshPalm=BanagoDenseCanopyModels.Coconut(1,true);AssetDatabase.CreateAsset(meshMango,folder+"/Roadside mango canopy.asset");AssetDatabase.CreateAsset(meshBamboo,folder+"/Pond edge bamboo.asset");AssetDatabase.CreateAsset(meshPalm,folder+"/School frontage coconut.asset");
        int treeCount=0,palms=0;var positions=new List<Vector2>();
        bool Plant(Vector2 p,Mesh m,Transform parent,float scale,string name){if(!Clear(p,.65f)||positions.Any(v=>Vector2.Distance(v,p)<3))return false;var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=V(p,Ground(p)-.04f);g.transform.rotation=Quaternion.Euler(0,R(0,360),0);g.transform.localScale=Vector3.one*scale;g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=matte;positions.Add(p);treeCount++;return true;}
        for(float z=790;z>682;z-=20){var c=At(z);var n=Nearest(c);var side=new Vector2(n.tangent.y,-n.tangent.x);foreach(int sign in new[]{-1,1})foreach(float offset in new[]{7.2f,8.3f,9.5f})if(Plant(c+side*offset*sign,meshMango,trees.transform,R(1.0f,1.25f),"Roadside spreading shade tree "+treeCount))break;}
        foreach(var p in new[]{new Vector2(-4,736),new Vector2(3,735),new Vector2(10,737),new Vector2(19,733),new Vector2(21,722),new Vector2(18,709),new Vector2(28,700)})Plant(p,treeCount%3==0?meshMango:meshBamboo,trees.transform,R(1.0f,1.35f),"Irregular pond-school green edge "+treeCount);
        foreach(float z in new[]{642f,630f,619f}){var c=At(z);var n=Nearest(c);var east=new Vector2(-n.tangent.y,n.tangent.x);foreach(float offset in new[]{4.8f,5.5f,6.1f})if(Plant(c+east*offset,meshPalm,school.transform,R(.78f,.96f),"Tall palm beside school frontage "+(++palms)))break;}
        // The existing landmark already has the correct white colonnade; strengthen its street-facing fence.
        var fence=GameObject.Find("School red base and white grille fence - road kept clear");int schoolFence=0;
        if(fence){var f=fence.GetComponent<MeshFilter>();var r=fence.GetComponent<Renderer>();var src=f.sharedMesh;var m=UnityEngine.Object.Instantiate(src);var colors=m.colors;for(int i=0;i<colors.Length;i++){var c=colors[i];if(c.r>.6f&&c.g>.6f&&c.b>.6f)colors[i]=new Color(.25f,.43f,.31f);}m.colors=colors;AssetDatabase.CreateAsset(m,folder+"/School red base and green steel fence.asset");var collider=fence.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=src,after=m,materialsBefore=r.sharedMaterials,materialsAfter=r.sharedMaterials,colliderBefore=collider?collider.sharedMesh:null});f.sharedMesh=m;if(collider)collider.sharedMesh=m;schoolFence=1;}
        var shelterRoof=new BanagoEnvironmentModels.Parts();var shelterFrame=new BanagoEnvironmentModels.Parts();var sidewalk=new BanagoEnvironmentModels.Parts();var lineT=new Vector2(.515f,-.857f);var inward=new Vector2(.857f,.515f);var shelterCenter=new Vector2(-2.9f,626.0f);float sy=Ground(shelterCenter);
        if(float.IsFinite(sy))
        {
         var right=V(lineT,0);var depth=V(inward,0);var center=V(shelterCenter,sy+2.45f);Quad(shelterRoof,center-right*3.3f-depth*.62f,center+right*3.3f-depth*.62f,center+right*3.3f+depth*.62f+Vector3.up*.18f,center-right*3.3f+depth*.62f+Vector3.up*.18f,new Color(.27f,.38f,.26f),Vector3.up);
         foreach(float s in new[]{-3f,0,3f}){var p=shelterCenter+lineT*s+inward*.38f;float y=Ground(p);if(float.IsFinite(y))Tube(shelterFrame,V(p,y),V(p,sy+2.58f),.035f,new Color(.25f,.42f,.30f));}
         Box(shelterFrame,V(shelterCenter,sy+.44f),new Vector3(5.5f,.09f,.30f),new Color(.33f,.40f,.25f),Quaternion.LookRotation(V(inward,0)));
        }
        var sa=new Vector2(-13.0f,640.5f);var sb=new Vector2(.15f,618.4f);
        for(int i=0;i<32;i++){var a=Vector2.Lerp(sa,sb,i/32f);var b=Vector2.Lerp(sa,sb,(i+1)/32f);var aa=a-inward*.15f;var ab=a-inward*1.40f;var ba=b-inward*.15f;var bb=b-inward*1.40f;var pts=new[]{aa,ab,bb,ba};if(pts.Any(p=>!float.IsFinite(Ground(p))||Nearest(p).distance<3.7f))continue;Quad(sidewalk,V(aa,Ground(aa)+.045f),V(ab,Ground(ab)+.045f),V(bb,Ground(bb)+.045f),V(ba,Ground(ba)+.045f),new Color(.64f,.63f,.57f),Vector3.up);}
        Save("Green corrugated waiting shelter",shelterRoof,metal,school.transform);Save("Waiting shed posts and bench",shelterFrame,matte,school.transform);Save("Paved school frontage sidewalk",sidewalk,matte,school.transform,true);
        var stones=new BanagoEnvironmentModels.Parts();int scattered=0;
        for(int i=0;i<230;i++){float z=R(688,811);var c=At(z);var n=Nearest(c);var side=new Vector2(n.tangent.y,-n.tangent.x);var p=c+side*R(3.7f,4.9f)*(i%2==0?1:-1)+n.tangent*R(-1.5f,1.5f);if(!Clear(p,.05f))continue;var center=V(p,Ground(p)+.025f);var col=i%8==0?new Color(.56f,.62f,.61f):i%11==0?new Color(.38f,.47f,.61f):new Color(.48f,.44f,.35f);Box(stones,center,new Vector3(R(.045f,.15f),R(.018f,.065f),R(.04f,.14f)),col,Quaternion.Euler(R(-9,9),R(0,360),R(-9,9)));scattered++;}
        Save("Gravel scraps and small debris at shoulders",stones,matte,debris.transform);
        review.swaps=swaps.ToArray();EditorUtility.SetDirty(review);AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);bool saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        var result=new{saved,frontages=frontageCount,variedRoofMeshes=roofMeshes,utilityPoles=poles.Count,cableSpans=spans,minimumCableClearance=minCable,trees=treeCount,schoolFence,shoulderDebris=scattered,assets};System.IO.File.WriteAllText("RoadRepairBackup/street-dressing-result.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }

    private static object FitCanopies()
    {
        var root=GameObject.Find("Felix Amante street reference - dirt mud school and utilities");var source=root.transform.Find("Roadside shade and pond edge vegetation");var all=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&c.sharedMesh&&!c.transform.IsChildOf(root.transform)&&!c.name.StartsWith("Measured terrain")&&!c.name.Contains("road")&&!c.name.Contains("Road")).ToArray();int count=0;
        foreach(var f in source.GetComponentsInChildren<MeshFilter>()){if(!f.sharedMesh.name.Contains("mango")&&!f.sharedMesh.name.Contains("Mango"))continue;var t=f.transform;float radius=4.2f,highest=t.position.y;for(int j=0;j<12;j++){float a=j*Mathf.PI/6;var p=t.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*.82f;foreach(var c in all){var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.z<b.min.z||p.z>b.max.z)continue;if(c.Raycast(new Ray(new Vector3(p.x,40,p.z),Vector3.down),out var h,70))highest=Mathf.Max(highest,h.point.y);}}float height=Mathf.Clamp((highest-t.position.y+1.0f)/.42f,10.5f,20f);t.localScale=new Vector3(radius,height,radius);count++;}
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.scene);return new{fullSizeShadeTrees=count};
    }
}
