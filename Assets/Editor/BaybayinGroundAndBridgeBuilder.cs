using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Brightens the Baybayin extension and builds the two mapped creek bridges.</summary>
public static class BaybayinGroundAndBridgeBuilder
{
    const string ExpansionName = "Purok Baybayin - satellite matched expansion";
    const string RootName = "Baybayin matched ground roads and mapped bridges";
    const string Folder = "Assets/StormSim/Generated/BaybayinGroundBridges_20260915";
    const string Backup = "RoadRepairBackup/BaybayinExpansion/MAIN_GAME.before-ground-and-bridges.unity";

    static readonly Color AsphaltColor = new Color(.39f,.40f,.39f);
    static readonly Color ConcreteColor = new Color(.73f,.72f,.66f);
    static readonly Color WhiteColor = new Color(.83f,.82f,.76f);
    static readonly Color BlackColor = new Color(.105f,.11f,.105f);
    static readonly Color YellowColor = new Color(.84f,.57f,.12f);
    static readonly Color RailColor = new Color(.29f,.17f,.12f);

    static GameObject expansion, root;
    static Material groundMat, roadMat, laneMat, concreteMat, whiteMat, blackMat, yellowMat, railMat;
    static int asset;

    static Material Mat(string name, Color color, bool vertexColor = false)
    {
        string path = Folder + "/" + name + ".mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (old) return old;
        var shader = Shader.Find(vertexColor ? "StormSim/Weathered Building Surface" : "Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("Required surface shader is missing.");
        var m = new Material(shader) { name = name, enableInstancing = true };
        m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_VertexColor")) m.SetFloat("_VertexColor", vertexColor ? 1 : 0);
        if (m.HasProperty("_Weathering")) m.SetFloat("_Weathering", vertexColor ? .06f : 0);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .08f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 2);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static float SurfaceY(Vector2 p)
    {
        float y = -999;
        var ray = new Ray(new Vector3(p.x, 80, p.y), Vector3.down);
        foreach (var hit in Physics.RaycastAll(ray, 160, ~0, QueryTriggerInteraction.Ignore))
        {
            var t = hit.collider.transform;
            if (!t.IsChildOf(expansion.transform)) continue;
            string path = t.parent ? t.parent.name : "";
            if (path == "Coastal water" || path == "River water" || path.Contains("building") || path.Contains("homes")) continue;
            if (hit.normal.y > .65f && hit.point.y > y) y = hit.point.y;
        }
        return y > -998 ? y : 0;
    }

    static void Face(BanagoEnvironmentModels.Parts p, Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 expected)
        => p.Face(a,b,c,color,expected);

    static void Box(BanagoEnvironmentModels.Parts p, Vector3 center, Vector3 size, Quaternion rotation, Color color)
    {
        var v = new Vector3[8];
        int i = 0;
        foreach (float y in new[]{-.5f,.5f}) foreach (float z in new[]{-.5f,.5f}) foreach (float x in new[]{-.5f,.5f})
            v[i++] = center + rotation * Vector3.Scale(new Vector3(x,y,z), size);
        int[][] f = { new[]{0,1,3,2}, new[]{4,6,7,5}, new[]{0,4,5,1}, new[]{2,3,7,6}, new[]{0,2,6,4}, new[]{1,5,7,3} };
        foreach (var q in f)
        {
            var expected = ((v[q[0]]+v[q[1]]+v[q[2]]+v[q[3]])*.25f-center).normalized;
            Face(p,v[q[0]],v[q[1]],v[q[2]],color,expected); Face(p,v[q[0]],v[q[2]],v[q[3]],color,expected);
        }
    }

    static void Beam(BanagoEnvironmentModels.Parts p, Vector3 a, Vector3 b, float width, float height, Color color)
    {
        var delta=b-a; var horizontal=new Vector3(delta.x,0,delta.z);
        var rotation=horizontal.sqrMagnitude>.0001f?Quaternion.LookRotation(horizontal.normalized):Quaternion.identity;
        Box(p,(a+b)*.5f,new Vector3(width,height,delta.magnitude),rotation,color);
    }

    static GameObject MeshObject(string name, BanagoEnvironmentModels.Parts parts, Material material, bool collider=false)
    {
        var mesh=parts.Mesh(name); AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+(++asset).ToString("000")+" "+name+".asset"));
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        if(collider)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
    }

    static Quaternion RoadRotation(Vector2 a, Vector2 b, float ya, float yb)
    {
        var d=b-a; return Quaternion.LookRotation(new Vector3(d.x,yb-ya,d.y).normalized,Vector3.up);
    }

    static void Deck(BanagoEnvironmentModels.Parts p, Vector2 a, Vector2 b, float width, float lift=.10f)
    {
        float ya=SurfaceY(a)+lift,yb=SurfaceY(b)+lift;var c=new Vector3((a.x+b.x)*.5f,(ya+yb)*.5f-.09f,(a.y+b.y)*.5f);
        Box(p,c,new Vector3(width,.18f,Vector2.Distance(a,b)+1.2f),RoadRotation(a,b,ya,yb),Color.white);
    }

    static void SmallFelixBridge()
    {
        var a=new Vector2(.57f,40.62f);var b=new Vector2(1.19f,66.45f);var d=(b-a).normalized;var side=new Vector2(-d.y,d.x);
        float ya=SurfaceY(a)+.20f,yb=SurfaceY(b)+.20f;
        var deck=new BanagoEnvironmentModels.Parts();Deck(deck,a,b,7.4f,.11f);MeshObject("Felix Amante bridge - lighter worn road deck",deck,roadMat,true);
        var concrete=new BanagoEnvironmentModels.Parts();var yellow=new BanagoEnvironmentModels.Parts();var rails=new BanagoEnvironmentModels.Parts();
        foreach(int sign in new[]{-1,1})
        {
            Vector2 aa=a+side*sign*3.72f,bb=b+side*sign*3.72f;
            Beam(concrete,new Vector3(aa.x,ya+.03f,aa.y),new Vector3(bb.x,yb+.03f,bb.y),.64f,.18f,ConcreteColor);
            Beam(yellow,new Vector3(aa.x,ya+.14f,aa.y),new Vector3(bb.x,yb+.14f,bb.y),.28f,.12f,YellowColor);
            int count=Mathf.CeilToInt(Vector2.Distance(a,b)/2.15f);
            var posts=new List<Vector3>();
            for(int i=0;i<=count;i++)
            {
                float t=i/(float)count;var q=Vector2.Lerp(aa,bb,t);float y=Mathf.Lerp(ya,yb,t);var c=new Vector3(q.x,y+.68f,q.y);
                Box(yellow,c,new Vector3(.24f,1.18f,.24f),Quaternion.identity,YellowColor);posts.Add(c);
            }
            for(int i=0;i<posts.Count-1;i++) foreach(float h in new[]{-.27f,.12f,.43f})
                Beam(rails,posts[i]+Vector3.up*h,posts[i+1]+Vector3.up*h,.105f,.105f,RailColor);
        }
        MeshObject("Felix Amante bridge - concrete walks",concrete,concreteMat,true);
        MeshObject("Felix Amante bridge - yellow posts and curb",yellow,yellowMat,true);
        MeshObject("Felix Amante bridge - dark horizontal rails",rails,railMat,true);
    }

    static void LargeHighwayBridge()
    {
        var a1=new Vector2(87.38f,16.07f);var b1=new Vector2(58.76f,13.54f);
        var a2=new Vector2(59.54f,4.84f);var b2=new Vector2(88.37f,7.34f);
        var deck=new BanagoEnvironmentModels.Parts();Deck(deck,a1,b1,8.5f,.12f);Deck(deck,a2,b2,8.5f,.12f);
        MeshObject("Carlos Hilado bridge - two lighter asphalt carriageways",deck,roadMat,true);

        var white=new BanagoEnvironmentModels.Parts();var yellow=new BanagoEnvironmentModels.Parts();var black=new BanagoEnvironmentModels.Parts();var marks=new BanagoEnvironmentModels.Parts();
        Vector2 dir=((b1-a1).normalized+(a2-b2).normalized).normalized;var side=new Vector2(-dir.y,dir.x);
        var outer1=(a1+b1)*.5f-side*4.35f;var outer2=(a2+b2)*.5f+side*4.35f;
        float length=(Vector2.Distance(a1,b1)+Vector2.Distance(a2,b2))*.5f;
        foreach(var oc in new[]{outer1,outer2})
        {
            var oa=oc-dir*(length*.5f+.5f);var ob=oc+dir*(length*.5f+.5f);float y=SurfaceY(oc)+.25f;
            Beam(yellow,new Vector3(oa.x,y,oa.y),new Vector3(ob.x,y,ob.y),.32f,.16f,YellowColor);
            int n=Mathf.CeilToInt(length/2.45f);var ps=new List<Vector3>();
            for(int i=0;i<=n;i++){var q=Vector2.Lerp(oa,ob,i/(float)n);var c=new Vector3(q.x,y+.62f,q.y);Box(white,c,new Vector3(.22f,1.03f,.22f),Quaternion.identity,WhiteColor);ps.Add(c);}
            for(int i=0;i<ps.Count-1;i++)foreach(float h in new[]{-.18f,.24f})Beam(white,ps[i]+Vector3.up*h,ps[i+1]+Vector3.up*h,.105f,.105f,WhiteColor);
        }

        Vector2 midA=(a1+b2)*.5f,midB=(b1+a2)*.5f;float my=SurfaceY((midA+midB)*.5f)+.43f;
        int blocks=14;for(int i=0;i<blocks;i++)
        {
            var q0=Vector2.Lerp(midA,midB,i/(float)blocks);var q1=Vector2.Lerp(midA,midB,(i+1f)/blocks);var c=(q0+q1)*.5f;
            Box(i%2==0?white:black,new Vector3(c.x,my,c.y),new Vector3(.58f,.66f,Vector2.Distance(q0,q1)+.035f),Quaternion.LookRotation(new Vector3(dir.x,0,dir.y)),i%2==0?WhiteColor:BlackColor);
        }
        foreach(var pair in new[]{new[]{a1,b1},new[]{a2,b2}})
        {
            var dd=(pair[1]-pair[0]).normalized;float total=Vector2.Distance(pair[0],pair[1]);
            for(float t=2;t<total-1;t+=7){var q0=pair[0]+dd*t;var q1=pair[0]+dd*Mathf.Min(t+3,total-1);float y=SurfaceY((q0+q1)*.5f)+.235f;Beam(marks,new Vector3(q0.x,y,q0.y),new Vector3(q1.x,y,q1.y),.13f,.025f,WhiteColor);}
        }
        MeshObject("Carlos Hilado bridge - white outer parapets",white,whiteMat,true);
        MeshObject("Carlos Hilado bridge - yellow outer curbs",yellow,yellowMat,true);
        MeshObject("Carlos Hilado bridge - black white center barrier",black,blackMat,true);
        MeshObject("Carlos Hilado bridge - dashed lane markings",marks,whiteMat,false);
    }

    [MenuItem("Tools/StormSim/Match Baybayin Ground Roads And Bridges")]
    public static void Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new InvalidOperationException("Open MAIN_GAME in Edit mode.");
        expansion=GameObject.Find(ExpansionName);if(!expansion)throw new InvalidOperationException("Baybayin expansion is missing.");
        if(GameObject.Find(RootName))throw new InvalidOperationException("The matched ground and bridges already exist.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        if(!File.Exists(Backup)&&!EditorSceneManager.SaveScene(scene,Backup,true))throw new IOException("Could not create scene backup.");

        groundMat=Mat("Brighter mixed grass and compacted earth",new Color(1.28f,1.24f,1.16f),true);
        roadMat=Mat("Lighter worn neighborhood asphalt",AsphaltColor);
        laneMat=Mat("Light concrete and packed access lanes",new Color(.53f,.51f,.45f));
        concreteMat=Mat("Weathered bridge concrete",ConcreteColor);whiteMat=Mat("Aged white bridge paint",WhiteColor);
        blackMat=Mat("Black bridge markings",BlackColor);yellowMat=Mat("Yellow bridge curb and posts",YellowColor);railMat=Mat("Dark red brown bridge rails",RailColor);

        root=new GameObject(RootName);root.transform.SetParent(expansion.transform,false);Undo.RegisterCreatedObjectUndo(root,"Match Baybayin ground roads and bridges");
        var review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=Backup;
        var swaps=new List<BanagoReferenceAreasReview.Swap>();
        var ground=expansion.transform.Find("Walkable coastal terrain");
        foreach(var f in ground.GetComponentsInChildren<MeshFilter>(true))
        {var r=f.GetComponent<Renderer>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,materialsBefore=r.sharedMaterials,materialsAfter=Enumerable.Repeat(groundMat,Mathf.Max(1,f.sharedMesh.subMeshCount)).ToArray(),colliderBefore=f.GetComponent<MeshCollider>()?f.GetComponent<MeshCollider>().sharedMesh:null});}
        var roads=expansion.transform.Find("Roads and access lanes");
        foreach(var f in roads.GetComponentsInChildren<MeshFilter>(true))
        {var r=f.GetComponent<Renderer>();bool light=f.name.Contains("footway")||f.name.Contains("path")||f.name.Contains("track");swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,materialsBefore=r.sharedMaterials,materialsAfter=Enumerable.Repeat(light?laneMat:roadMat,Mathf.Max(1,f.sharedMesh.subMeshCount)).ToArray(),colliderBefore=f.GetComponent<MeshCollider>()?f.GetComponent<MeshCollider>().sharedMesh:null});}
        var highway=expansion.transform.Find("Four lane highways and planted median/Four lane asphalt carriageways");
        if(highway){var f=highway.GetComponent<MeshFilter>();var r=highway.GetComponent<Renderer>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,materialsBefore=r.sharedMaterials,materialsAfter=Enumerable.Repeat(roadMat,Mathf.Max(1,f.sharedMesh.subMeshCount)).ToArray(),colliderBefore=f.GetComponent<MeshCollider>()?f.GetComponent<MeshCollider>().sharedMesh:null});}
        review.swaps=swaps.ToArray();review.notes="Brighter ground and lighter road surfaces match the original neighborhood. The mapped Felix Amante bridge uses yellow concrete posts with dark horizontal rails; the larger divided Circumferential/Carlos Hilado crossing uses two carriageways, white outer parapets, yellow curbs, dashed lanes, and a black-white center barrier. Registered OSM bridge endpoints are retained. Houses, waterways, road alignments, and terrain heights are unchanged. Disable this root to restore earlier materials and hide the added bridge detail.";review.Apply();
        SmallFelixBridge();LargeHighwayBridge();EditorUtility.SetDirty(review);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=root;SceneView.RepaintAll();
        File.WriteAllText(Folder+"/README.txt",review.notes+"\nGround surfaces: "+ground.GetComponentsInChildren<MeshFilter>(true).Length+". Road surfaces: "+roads.GetComponentsInChildren<MeshFilter>(true).Length+". Scene backup: "+Backup+".\n");
        Debug.Log("Matched Baybayin ground and roads; built the Felix Amante and Carlos Hilado bridge details.",root);
    }
}
