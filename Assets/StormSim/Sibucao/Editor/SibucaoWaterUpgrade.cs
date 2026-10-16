using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoWaterUpgrade
    {
        const string Folder="Assets/StormSim/Sibucao/Art/Home/";
        static SibucaoFloodTerrain data;static FloodSimulationManager flood;static SibucaoSession session;static int row;static bool edges;
        public static string Status="Idle";
        [MenuItem("STORMSIM/Sibucao/Bake terrain water and swimming")]
        public static void Install()
        {
            if(Application.isPlaying||EditorSceneManager.GetActiveScene().name!="SIBUCAO_RAIN")throw new InvalidOperationException("Open SIBUCAO_RAIN in Edit mode.");
            session=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();flood=session.GetComponent<FloodSimulationManager>();
            flood.sea=UnityEngine.Object.FindObjectsByType<BanagoSeaWaves>(FindObjectsSortMode.None).FirstOrDefault(x=>x.name=="Calm water - traced sea boundary");
            var swim=session.player.GetComponent<SibucaoSwimming>();if(!swim)swim=session.player.gameObject.AddComponent<SibucaoSwimming>();session.player.swimming=swim;
            var under=session.player.view.GetComponent<SibucaoUnderwater>();if(!under)under=session.player.view.gameObject.AddComponent<SibucaoUnderwater>();
            var fog=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Underwater sediment.mat");if(!fog){fog=new Material(Shader.Find("StormSim/Sibucao Underwater Sediment"));AssetDatabase.CreateAsset(fog,Folder+"Underwater sediment.mat");}under.fogMaterial=fog;
            foreach(var h in session.homes)h.floodSurface.gameObject.SetActive(false);
            data=AssetDatabase.LoadAssetAtPath<SibucaoFloodTerrain>(Folder+"Sibucao terrain runoff.asset");if(!data){data=ScriptableObject.CreateInstance<SibucaoFloodTerrain>();AssetDatabase.CreateAsset(data,Folder+"Sibucao terrain runoff.asset");}
            data.cellSize=2;data.origin=new Vector2(Mathf.Floor((flood.groundSamples.Min(p=>p.x)-65)/2)*2,Mathf.Floor((flood.groundSamples.Min(p=>p.z)-65)/2)*2);
            data.width=Mathf.CeilToInt((flood.groundSamples.Max(p=>p.x)+65-data.origin.x)/2)+1;data.height=Mathf.CeilToInt((flood.groundSamples.Max(p=>p.z)+65-data.origin.y)/2)+1;
            int n=data.width*data.height;data.ground=new float[n];data.crestX=new float[n];data.crestZ=new float[n];data.kind=new byte[n];data.covered=new bool[n];flood.terrain=data;
            Physics.SyncTransforms();row=0;edges=false;Status="Baking ground";EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            InstallCalendars();
        }
        static bool Floor(Collider c)
        {
            string n=c.name.ToLowerInvariant();return n.Contains("measured terrain")||n.StartsWith("residential ")||n.Contains("felix amante")||n.StartsWith("junction ")||n.Contains("yard approach")||n.Contains("tiled floor")||n.Contains("foundation")||n.Contains("front step")||n.Contains("evacuation ramp");
        }
        static bool Barrier(Collider c)
        {
            if(c.attachedRigidbody||c.isTrigger||Floor(c)||c.GetComponentInParent<SibucaoActor>()||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<PhysicalGrabbable>())return false;
            string n=c.name.ToLowerInvariant();return c.bounds.size.y>1&&(n.Contains("wall")||n.Contains("house")||n.Contains("building")||n.Contains("solid")||n.Contains("door")||n.Contains("shell"));
        }
        static void Tick()
        {
            try
            {
                for(int batch=0;batch<3&&row<data.height;batch++,row++)for(int x=0;x<data.width;x++)
                {
                    int i=row*data.width+x;var p=new Vector3(data.origin.x+x*data.cellSize,50,data.origin.y+row*data.cellSize);
                    if(!edges)
                    {
                        var hits=Physics.RaycastAll(p,Vector3.down,100,1,QueryTriggerInteraction.Ignore);
                        var ground=hits.Where(h=>Floor(h.collider)).OrderByDescending(h=>h.point.y).ToArray();
                        if(ground.Length>0){data.ground[i]=ground[0].point.y;data.kind[i]=1;}
                        else data.ground[i]=50;
                        data.covered[i]=hits.Any(h=>h.point.y>data.ground[i]+1&&(h.collider.name.ToLower().Contains("roof")||h.collider.name.ToLower().Contains("ceiling")));
                        if(flood.sea&&flood.sea.TrySample(p,0,out var sea)&&(ground.Length==0||data.ground[i]<sea.baseHeight)) {data.ground[i]=sea.baseHeight;data.kind[i]=2;}
                    }
                    else
                    {
                        data.crestX[i]=x<data.width-1?Crest(i,i+1):50;data.crestZ[i]=row<data.height-1?Crest(i,i+data.width):50;
                    }
                }
                Status=(edges?"Baking wall barriers ":"Baking ground ")+row+"/"+data.height;
                if(row<data.height)return;
                if(!edges){edges=true;row=0;return;}
                EditorApplication.update-=Tick;EditorUtility.SetDirty(data);EditorUtility.SetDirty(flood);EditorUtility.SetDirty(session.player);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(session.gameObject.scene);EditorSceneManager.SaveScene(session.gameObject.scene);
                Status="PASS: "+data.width+" x "+data.height+" terrain grid; "+data.kind.Count(k=>k==1)+" ground cells; "+data.kind.Count(k=>k==2)+" sea outlets; swimming, underwater and four physical calendars saved.";
            }
            catch(Exception e){EditorApplication.update-=Tick;Status="FAIL "+e;Debug.LogException(e);}
        }
        static float Crest(int a,int b)
        {
            float crest=Mathf.Max(data.ground[a],data.ground[b]);if(data.kind[a]==0||data.kind[b]==0)return 50;
            Vector3 start=data.Position(a),end=data.Position(b);start.y=end.y=crest+.25f;
            foreach(var hit in Physics.RaycastAll(start,(end-start).normalized,data.cellSize,1,QueryTriggerInteraction.Ignore))if(Barrier(hit.collider))crest=Mathf.Max(crest,hit.collider.bounds.max.y);
            return crest;
        }
        public static void InstallCalendars()
        {
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Printed coastal calendar.png");if(!tex)throw new InvalidOperationException("Calendar texture not imported.");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Printed calendar paper.mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+"Printed calendar paper.mat");}mat.SetTexture("_BaseMap",tex);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.12f);
            var paper=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Cotton curtains.mat");var metal=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Galvanized roof.mat");
            foreach(var home in UnityEngine.Object.FindObjectsByType<SibucaoHomeOpening>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                foreach(var t in home.GetComponentsInChildren<TextMesh>(true).Where(t=>t.name=="Calendar").ToArray())Undo.DestroyObjectImmediate(t.gameObject);
                var backing=home.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Wall calendar backing");if(!backing)continue;
                var existing=home.transform.Find("Printed wall calendar - physical paper");if(existing)Undo.DestroyObjectImmediate(existing.gameObject);
                var root=new GameObject("Printed wall calendar - physical paper").transform;root.SetParent(home.transform,false);root.localPosition=backing.localPosition+Vector3.back*.022f;
                var page=new GameObject("Curled printed page - no TextMesh");page.transform.SetParent(root,false);page.AddComponent<MeshRenderer>().sharedMaterial=mat;
                const int cols=8,rows=12;var v=new Vector3[(cols+1)*(rows+1)];var uv=new Vector2[v.Length];var tri=new System.Collections.Generic.List<int>();
                for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){int i=y*(cols+1)+x;float u=(float)x/cols,w=(float)y/rows;float curl=Mathf.Pow(1-w,6)*(.017f+.023f*Mathf.Pow(u,4));v[i]=new Vector3((u-.5f)*.635f,(w-.5f)*.846f,-curl);uv[i]=new Vector2(u,w);if(x<cols&&y<rows){int b=i+1,c=i+cols+1;tri.AddRange(new[]{i,c,b,b,c,c+1});}}
                var meshPath=Folder+"Curled calendar page.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(!mesh){mesh=new Mesh{name="Curled paper page"};mesh.vertices=v;mesh.uv=uv;mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);}page.AddComponent<MeshFilter>().sharedMesh=mesh;
                for(int n=0;n<3;n++)Detail("Stacked paper edge",root,new Vector3(0,-.003f*n,.006f+n*.002f),new Vector3(.637f,.85f,.002f),paper);
                Detail("Metal hanging clip",root,new Vector3(0,.435f,-.008f),new Vector3(.11f,.046f,.017f),metal);
                Detail("Wall nail",root,new Vector3(0,.468f,0),new Vector3(.016f,.016f,.03f),metal);
                Undo.RegisterCreatedObjectUndo(root.gameObject,"Replace calendar text with printed prop");
            }
            EditorUtility.SetDirty(mat);
        }
        static void Detail(string name,Transform root,Vector3 p,Vector3 scale,Material mat)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;}
    }
}

