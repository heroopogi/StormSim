using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    public static class PreparednessNeighborhoodBuilder
    {
        const string Folder="Assets/StormSim/Preparedness/Art/Neighborhood";
        public const string RootName="Spawn neighborhood - accessible family homes";
        static Material wood,cloth,stone,skin,shirt,hair,metal,white;
        class House {public BanagoHousePreview source;public Vector3 center;public Quaternion rotation;public float w,d;public Transform room;public Bounds cutBounds;}
        struct V
        {
            public Vector3 p,n;public Vector2 uv;public Color c;
            public static V Lerp(V a,V b,float t)=>new V{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};
        }
        static Material Mat(string name,Color color)
        {string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color,enableInstancing=true};m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);}return m;}
        static GameObject Box(Transform p,string name,Vector3 pos,Vector3 scale,Material material,bool collision=true)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static House Measure(BanagoHousePreview h,BanagoHousePreview[] all)
        {
            var vs=h.GetComponent<MeshFilter>().sharedMesh.vertices;float bottom=vs.Min(v=>v.y);
            var floor=vs.Where(v=>Mathf.Abs(v.y-bottom)<.02f).Select(v=>new Vector2(v.x,v.z)).Distinct().ToArray();
            // Minimum-area rectangle follows the measured orientation of each original footprint.
            float area=float.MaxValue;Vector2 ax=Vector2.right,lo=Vector2.zero,hi=Vector2.zero;
            for(int i=0;i<floor.Length;i++)for(int j=i+1;j<floor.Length;j++)
            {var a=(floor[j]-floor[i]).normalized;var b=new Vector2(-a.y,a.x);var low=new Vector2(floor.Min(v=>Vector2.Dot(v,a)),floor.Min(v=>Vector2.Dot(v,b)));var high=new Vector2(floor.Max(v=>Vector2.Dot(v,a)),floor.Max(v=>Vector2.Dot(v,b)));float ar=(high.x-low.x)*(high.y-low.y);if(ar<area){area=ar;ax=a;lo=low;hi=high;}}
            var az=new Vector2(-ax.y,ax.x);var middle=ax*((lo.x+hi.x)*.5f)+az*((lo.y+hi.y)*.5f);var center=h.transform.TransformPoint(new Vector3(middle.x,.035f,middle.y));
            var rotation=Quaternion.LookRotation(new Vector3(az.x,0,az.y));float w=hi.x-lo.x,d=hi.y-lo.y;
            float best=float.MinValue;Quaternion bestRot=rotation;float bw=w,bd=d;
            for(int side=0;side<4;side++)
            {
                var r=rotation*Quaternion.Euler(0,side*90,0);float sw=side%2==0?w:d,sd=side%2==0?d:w;
                var outside=center+r*new Vector3(0,0,-sd*.5f-1.1f);
                float clearance=all.Where(a=>a!=h).Min(a=>Vector2.Distance(new Vector2(outside.x,outside.z),new Vector2(a.GetComponent<Renderer>().bounds.ClosestPoint(outside).x,a.GetComponent<Renderer>().bounds.ClosestPoint(outside).z)));
                float score=Mathf.Min(clearance,3)*4+Mathf.Min(sw,8)*.05f;
                if(score>best){best=score;bestRot=r;bw=sw;bd=sd;}
            }
            return new House{source=h,center=center,rotation=bestRot,w=bw,d=bd};
        }
        public static string Build()
        {
            if(Application.isPlaying)throw new Exception("Exit Play mode first.");
            if(GameObject.Find(RootName))throw new Exception("Neighborhood already installed.");
            var bridge=Object.FindFirstObjectByType<SibucaoPreparationBridge>();if(!bridge)throw new Exception("Sibucao preparation bridge missing.");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("output/preparedness/backups");
            EditorSceneManager.SaveScene(bridge.gameObject.scene,"output/preparedness/backups/SIBUCAO_RAIN.before-neighborhood.unity",true);
            wood=Mat("Warm timber",new Color(.34f,.20f,.12f));cloth=Mat("Faded teal fabric",new Color(.2f,.45f,.43f));stone=Mat("Interior concrete",new Color(.56f,.55f,.49f));skin=Mat("Placeholder skin",new Color(.58f,.35f,.22f));shirt=Mat("Ochre clothing",new Color(.75f,.45f,.17f));hair=Mat("Dark hair",new Color(.055f,.035f,.025f));metal=Mat("Brushed metal",new Color(.4f,.43f,.44f));white=Mat("Linen",new Color(.8f,.76f,.65f));
            var all=Object.FindObjectsByType<BanagoHousePreview>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(h=>h.GetComponent<MeshFilter>()&&h.GetComponent<Renderer>()).ToArray();
            var spawn=bridge.firstHome.transform.position;
            var selected=all.Where(h=>Vector2.Distance(new Vector2(h.transform.position.x,h.transform.position.z),new Vector2(spawn.x,spawn.z))<=65).OrderBy(h=>h.name).ToArray();
            var houses=selected.Select(h=>Measure(h,all)).ToArray();var root=new GameObject(RootName);
            var familyRoot=new GameObject("Spawn neighborhood families - fictional placeholders");familyRoot.transform.SetParent(bridge.content.transform,false);
            var items=bridge.preparation.items.ToList();
            foreach(var h in houses)
            {
                var room=new GameObject(h.source.name+" - furnished interior");room.transform.SetParent(root.transform,false);room.transform.SetPositionAndRotation(h.center,h.rotation);h.room=room.transform;
                var info=room.AddComponent<PreparednessHomeInterior>();info.footprintId=h.source.footprintId;info.width=h.w;info.depth=h.d;
                var entrance=new GameObject("Open doorway - 1.35m clear");entrance.transform.SetParent(room.transform,false);entrance.transform.localPosition=new Vector3(0,0,-h.d*.5f);info.entrance=entrance.transform;
                h.cutBounds=new Bounds(entrance.transform.position+Vector3.up*1.1f,new Vector3(3,2.8f,3));
                Furnish(h);
                info.family=Family(h,familyRoot.transform,items);
                CombineStatic(room.transform,"Furnishing mesh",Folder+"/"+h.source.name+" interior.asset");
            }
            int cuts=CutDoorways(houses);bridge.preparation.items=items.ToArray();EditorUtility.SetDirty(bridge.preparation);
            int repaired=RepairMaterials();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
            string report="Installed "+houses.Length+" furnished original footprints; "+houses.Length*4+" family placeholders; "+cuts+" exterior mesh batches with cut doorways; "+repaired+" material slots repaired.";
            File.WriteAllText("output/preparedness/neighborhood-install.txt",report);return report;
        }
        static void Furnish(House h)
        {
            var p=h.room;float w=h.w-.28f,d=h.d-.28f;
            Box(p,"Solid household floor",new Vector3(0,-.10f,0),new Vector3(w,.2f,d),stone);
            // Low thresholds and a short apron keep the new entrances walkable on the terrain.
            Box(p,"Door threshold",new Vector3(0,-.10f,-h.d*.5f),new Vector3(1.5f,.18f,.65f),stone);
            Box(p,"Left jamb",new Vector3(-.77f,1.1f,-h.d*.5f),new Vector3(.13f,2.2f,.18f),wood);
            Box(p,"Right jamb",new Vector3(.77f,1.1f,-h.d*.5f),new Vector3(.13f,2.2f,.18f),wood);
            Box(p,"Lintel",new Vector3(0,2.3f,-h.d*.5f),new Vector3(1.68f,.15f,.2f),wood);
            float bedW=Mathf.Min(1.3f,w*.34f),bedD=Mathf.Min(1.9f,d*.37f);
            var bed=new Vector3(-w*.5f+bedW*.5f+.1f,.26f,d*.5f-bedD*.5f-.1f);
            Box(p,"Family bed frame",bed,new Vector3(bedW,.36f,bedD),wood);Box(p,"Mattress",bed+Vector3.up*.26f,new Vector3(bedW,.18f,bedD),white);
            Box(p,"Folded blanket",bed+new Vector3(0,.37f,-bedD*.26f),new Vector3(bedW,.07f,bedD*.4f),cloth);
            Box(p,"Pillow",bed+new Vector3(0,.39f,bedD*.32f),new Vector3(bedW*.7f,.12f,.35f),white);
            // A stacked sleeping platform supplies four sleeping places in compact footprints.
            Box(p,"Upper sleeping platform",bed+Vector3.up*1.15f,new Vector3(bedW,.12f,bedD),wood);Box(p,"Upper mattress",bed+Vector3.up*1.29f,new Vector3(bedW,.15f,bedD),cloth);
            foreach(float x in new[]{-.45f,.45f})Box(p,"Bunk post",bed+new Vector3(x*bedW,.55f,bedD*.43f),new Vector3(.07f,1.8f,.07f),wood);
            float cx=w*.5f-.36f,cz=d*.5f-.68f;
            Box(p,"Kitchen cabinet",new Vector3(cx,.43f,cz),new Vector3(.57f,.86f,Mathf.Min(1.2f,d*.3f)),wood);
            Box(p,"Kitchen worktop",new Vector3(cx,.9f,cz),new Vector3(.63f,.08f,Mathf.Min(1.25f,d*.31f)),stone);
            Box(p,"Wash basin",new Vector3(cx,.97f,cz+.2f),new Vector3(.4f,.08f,.34f),metal);
            Box(p,"Stove",new Vector3(cx,.99f,cz-.3f),new Vector3(.4f,.12f,.35f),hair);
            Box(p,"High supply shelf",new Vector3(cx,1.7f,cz),new Vector3(.45f,.08f,1.05f),wood);
            Box(p,"Stored household supplies",new Vector3(cx,1.86f,cz+.25f),new Vector3(.24f,.25f,.22f),white);
            // Narrow wall-side dining table leaves the entrance-to-center aisle clear.
            float tx=-w*.5f+.43f,tz=-d*.15f;
            Box(p,"Dining tabletop",new Vector3(tx,.73f,tz),new Vector3(.64f,.07f,.7f),wood);
            Box(p,"Table support",new Vector3(tx,.35f,tz),new Vector3(.12f,.7f,.12f),wood);
            for(int i=0;i<2;i++)
            {float z=tz+(i==0?-.57f:.57f);Box(p,"Dining chair seat",new Vector3(tx,.41f,z),new Vector3(.4f,.08f,.4f),wood);Box(p,"Chair base",new Vector3(tx,.2f,z),new Vector3(.09f,.4f,.09f),wood);}
            Box(p,"Family storage chest",new Vector3(w*.5f-.38f,.25f,-d*.5f+.47f),new Vector3(.6f,.5f,.65f),cloth);
            Box(p,"Floor mat",new Vector3(0,.013f,-d*.15f),new Vector3(Mathf.Min(1.15f,w*.4f),.012f,Mathf.Min(1.5f,d*.3f)),cloth,false);
            Box(p,"Meal bowl",new Vector3(tx,.8f,tz),new Vector3(.2f,.065f,.2f),white,false);
        }
        static GameObject Family(House h,Transform parent,List<PreparednessItem> items)
        {
            var family=new GameObject(h.source.name+" family of four");family.transform.SetParent(parent,false);family.transform.SetPositionAndRotation(h.center,h.rotation);
            var item=family.AddComponent<PreparednessItem>();item.id="family-"+h.source.footprintId;item.kind=PrepKind.Neighbor;item.portable=false;item.displayName=h.source.name+" household";
            var npc=family.AddComponent<PreparednessNeighbor>();npc.residentName="Neighbor";npc.activity="family at home";npc.need="There are four of us, including two children. We will prepare our supplies and keep the children with an adult.";items.Add(item);
            float w=h.w-.28f,d=h.d-.28f;
            Person(family.transform,"Adult - preparing a meal",new Vector3(w*.5f-.87f,0,d*.5f-.65f),90,1,false,shirt);
            Person(family.transform,"Adult - seated at table",new Vector3(-w*.5f+.43f,0,-d*.15f-.57f),0,1,true,cloth);
            Person(family.transform,"Child - seated with family",new Vector3(-w*.5f+.43f,0,-d*.15f+.57f),180,.72f,true,shirt);
            Person(family.transform,"Child - sorting clothes",new Vector3(w*.5f-.43f,0,-d*.5f+1.05f),180,.65f,false,cloth);
            return family;
        }
        static void Person(Transform parent,string name,Vector3 pos,float yaw,float scale,bool seated,Material top)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var model=new GameObject("Replaceable character placeholder");model.transform.SetParent(g.transform,false);model.transform.localScale=Vector3.one*scale;var t=model.transform;float hip=seated?.55f:.9f;
            Box(t,"Torso",new Vector3(0,hip+.24f,0),new Vector3(.42f,.48f,.24f),top,false);
            var head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.name="Head";head.transform.SetParent(t,false);head.transform.localPosition=new Vector3(0,hip+.66f,0);head.transform.localScale=new Vector3(.27f,.32f,.27f);head.GetComponent<Renderer>().sharedMaterial=skin;Object.DestroyImmediate(head.GetComponent<Collider>());
            Box(t,"Hair",new Vector3(0,hip+.8f,-.02f),new Vector3(.27f,.07f,.24f),hair,false);
            foreach(float side in new[]{-1f,1f})
            {
                Box(t,"Eye",new Vector3(side*.052f,hip+.69f,.128f),new Vector3(.027f,.027f,.025f),hair,false);
                Box(t,"Upper arm",new Vector3(side*.25f,hip+.22f,0),new Vector3(.13f,.32f,.15f),top,false);
                Box(t,"Forearm",new Vector3(side*.25f,hip+.04f,.12f),new Vector3(.105f,.13f,.32f),skin,false);
                if(seated){Box(t,"Thigh",new Vector3(side*.12f,hip-.04f,.15f),new Vector3(.16f,.17f,.43f),hair,false);Box(t,"Calf",new Vector3(side*.12f,.25f,.32f),new Vector3(.14f,.44f,.14f),hair,false);}
                else Box(t,"Leg",new Vector3(side*.12f,.44f,0),new Vector3(.17f,.83f,.18f),hair,false);
                Box(t,"Shoe",new Vector3(side*.12f,.065f,seated?.39f:.07f),new Vector3(.18f,.12f,.3f),wood,false);
            }
            var cap=g.AddComponent<CapsuleCollider>();cap.height=(seated?1.43f:1.8f)*scale;cap.radius=.22f*scale;cap.center=Vector3.up*cap.height*.5f;
            CombineStatic(t,"Family member",Folder+"/"+parent.name+" "+name+".asset");
        }
        static void CombineStatic(Transform root,string name,string path)
        {
            var filters=root.GetComponentsInChildren<MeshFilter>();var mats=filters.Select(f=>f.GetComponent<Renderer>().sharedMaterial).Distinct().ToArray();var sub=new List<Mesh>();
            foreach(var mat in mats)
            {var combines=filters.Where(f=>f.GetComponent<Renderer>().sharedMaterial==mat).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(combines,true,true);sub.Add(m);}
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(sub.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);AssetDatabase.CreateAsset(mesh,path);
            foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}foreach(var m in sub)Object.DestroyImmediate(m);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=root.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=mats;r.shadowCastingMode=ShadowCastingMode.Off;
        }
        static float PlaneDistance(V v,House h,int plane)
        {
            var p=Quaternion.Inverse(h.rotation)*(v.p-h.center);p.z+=h.d*.5f;
            switch(plane){case 0:return p.x+.69f;case 1:return .69f-p.x;case 2:return p.y+.3f;case 3:return 2.22f-p.y;case 4:return p.z+.85f;default:return (h.source.name=="House 2145"?2f:.85f)-p.z;}
        }
        static List<V> Clip(List<V> polygon,House h,int plane,bool inside)
        {
            var result=new List<V>();if(polygon.Count==0)return result;var a=polygon[polygon.Count-1];float da=PlaneDistance(a,h,plane)*(inside?1:-1);
            foreach(var b in polygon){float db=PlaneDistance(b,h,plane)*(inside?1:-1);if((da>=0)!=(db>=0))result.Add(V.Lerp(a,b,da/(da-db)));if(db>=0)result.Add(b);a=b;da=db;}return result;
        }
        static int CutDoorways(House[] houses)
        {
            int changed=0;
            var root=GameObject.Find("Banago Houses - full detail redesign");
            var detailed=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.transform.parent&&houses.Any(h=>f.transform.parent.name==h.source.name+" - detailed"));
            foreach(var f in root.GetComponentsInChildren<MeshFilter>().Concat(detailed).Distinct())
            {
                var r=f.GetComponent<Renderer>();var relevant=houses.Where(h=>r.bounds.Intersects(h.cutBounds)).ToArray();if(relevant.Length==0)continue;
                var original=f.sharedMesh;var positions=original.vertices;var normals=original.normals;var uv=original.uv;var colors=original.colors;var output=new List<V>();var triangles=new List<int[]>();int touched=0;
                V Vertex(int i)=>new V{p=f.transform.TransformPoint(positions[i]),n=normals.Length>i?f.transform.TransformDirection(normals[i]):Vector3.up,uv=uv.Length>i?uv[i]:Vector2.zero,c=colors.Length>i?colors[i]:Color.white};
                for(int sub=0;sub<original.subMeshCount;sub++)
                {
                    var indices=new List<int>();var ts=original.GetTriangles(sub);
                    for(int n=0;n<ts.Length;n+=3)
                    {
                        var poly=new List<V>{Vertex(ts[n]),Vertex(ts[n+1]),Vertex(ts[n+2])};var bounds=new Bounds(poly[0].p,Vector3.zero);bounds.Encapsulate(poly[1].p);bounds.Encapsulate(poly[2].p);
                        var pieces=new List<List<V>>{poly};
                        foreach(var h in relevant.Where(h=>h.cutBounds.Intersects(bounds)))
                        {
                            var next=new List<List<V>>();foreach(var piece in pieces)
                            {
                                var intersection=piece;for(int plane=0;plane<6;plane++)intersection=Clip(intersection,h,plane,true);
                                if(intersection.Count<3){next.Add(piece);continue;}touched++;
                                var remaining=piece;for(int plane=0;plane<6;plane++){var outside=Clip(remaining,h,plane,false);if(outside.Count>=3)next.Add(outside);remaining=Clip(remaining,h,plane,true);if(remaining.Count==0)break;}
                            }pieces=next;
                        }
                        foreach(var piece in pieces)for(int k=1;k<piece.Count-1;k++){foreach(var v in new[]{piece[0],piece[k],piece[k+1]}){indices.Add(output.Count);output.Add(v);}}
                    }triangles.Add(indices.ToArray());
                }
                if(touched==0)continue;
                var mesh=new Mesh{name=original.name+" - accessible doors",indexFormat=IndexFormat.UInt32};mesh.vertices=output.Select(v=>f.transform.InverseTransformPoint(v.p)).ToArray();mesh.normals=output.Select(v=>f.transform.InverseTransformDirection(v.n)).ToArray();mesh.uv=output.Select(v=>v.uv).ToArray();mesh.colors=output.Select(v=>v.c).ToArray();mesh.subMeshCount=triangles.Count;for(int s=0;s<triangles.Count;s++)mesh.SetTriangles(triangles[s],s);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Opened exterior "+changed+".asset"));f.sharedMesh=mesh;var col=f.GetComponent<MeshCollider>();if(col)col.sharedMesh=mesh;EditorUtility.SetDirty(f);changed++;
            }
            return changed;
        }
        public static string RepairOpenings()
        {
            if(Application.isPlaying)throw new Exception("Repair the saved scene in Edit mode.");
            AssetDatabase.StartAssetEditing();int reverse=0,cuts=0;
            try
            {
                var sources=Object.FindObjectsByType<BanagoHousePreview>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                var homes=Object.FindObjectsByType<PreparednessHomeInterior>(FindObjectsSortMode.None);
                var houses=homes.Select(h=>new House{source=sources.First(x=>x.footprintId==h.footprintId),center=h.transform.position,rotation=h.transform.rotation,w=h.width,d=h.depth,room=h.transform,cutBounds=new Bounds(h.entrance.position+Vector3.up*1.1f,new Vector3(5,2.8f,5))}).ToArray();
                cuts=CutDoorways(houses);
                // Import the new mesh assets before using their paths to update reverse colliders.
                AssetDatabase.StopAssetEditing();AssetDatabase.StartAssetEditing();
                foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh).Contains("Preparedness/Art/Neighborhood/Opened exterior")))
                foreach(var col in f.GetComponentsInChildren<MeshCollider>())
                {
                    if(col.gameObject==f.gameObject||col.name!="Solid wall reverse faces")continue;
                    var m=Object.Instantiate(f.sharedMesh);for(int s=0;s<m.subMeshCount;s++){var ts=m.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){var a=ts[i];ts[i]=ts[i+1];ts[i+1]=a;}m.SetTriangles(ts,s);}
                    AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Opened reverse walls "+reverse+".asset"));col.sharedMesh=m;EditorUtility.SetDirty(col);reverse++;
                }
                var blocked=homes.First(h=>h.name.StartsWith("House 2183 "));
                foreach(var c in Physics.OverlapSphere(blocked.entrance.position,1.5f).Where(c=>c.name=="PROP_Planter_Pail"))
                {var point=blocked.transform.TransformPoint(new Vector3(-blocked.width*.5f+.5f,0,blocked.depth*.5f-.5f));point.y=c.transform.position.y;c.transform.position=point;EditorUtility.SetDirty(c.transform);}
            }
            finally{AssetDatabase.StopAssetEditing();}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "Additional cuts: "+cuts+"; reverse collision meshes: "+reverse;
        }
        public static int RepairMaterials()
        {
            Directory.CreateDirectory(Folder);var fallback=Mat("Repaired chair timber",new Color(.35f,.23f,.14f));int count=0;
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var materials=r.sharedMaterials;bool dirty=false;
                for(int i=0;i<materials.Length;i++)if(!materials[i]||!materials[i].shader||materials[i].shader.name=="Hidden/InternalErrorShader"){materials[i]=fallback;dirty=true;count++;}
                var f=r.GetComponent<MeshFilter>();if(f&&f.sharedMesh&&f.sharedMesh.name=="Boole"&&materials.Length>f.sharedMesh.subMeshCount){materials=materials.Take(f.sharedMesh.subMeshCount).ToArray();dirty=true;}
                if(dirty){r.sharedMaterials=materials;EditorUtility.SetDirty(r);}
            }return count;
        }
    }
}
