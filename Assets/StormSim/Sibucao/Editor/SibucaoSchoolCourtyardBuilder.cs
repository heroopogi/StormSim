using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoSchoolCourtyardBuilder
    {
        const string RootName = "School quadrangle - gardens shelters and assembly plaza";
        const string Folder = "Assets/StormSim/Sibucao/Art/SchoolCourtyard";
        static readonly Quaternion Rotation = Quaternion.Euler(0, -14, 0);
        static readonly Vector3 Origin = new Vector3(34, 0, 653);
        static readonly Color Concrete = new Color(.56f, .59f, .57f);
        static readonly Color Green = new Color(.19f, .36f, .28f);
        static readonly Color Wood = new Color(.43f, .29f, .19f);
        static readonly Color Steel = new Color(.35f, .44f, .45f);
        static readonly Color White = new Color(.83f, .84f, .76f);
        static GameObject root;
        static Material material;
        static MeshCollider[] ground;
        static int meshIndex;

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector3 Point(float x, float z) => Origin + Rotation * V(x, 0, z);
        static float Ground(Vector3 point)
        {
            float result = float.NegativeInfinity;
            foreach (var c in ground)
                if (c.Raycast(new Ray(point + Vector3.up * 40, Vector3.down), out var hit, 80))
                    result = Mathf.Max(result, hit.point.y);
            if (!float.IsFinite(result)) throw new InvalidOperationException("No courtyard ground at " + point);
            return result;
        }

        sealed class Model
        {
            public readonly GameObject go;
            readonly BanagoEnvironmentModels.Parts parts = new BanagoEnvironmentModels.Parts();
            public Model(string name, float x, float z)
            {
                go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var p = Point(x, z);
                p.y = Ground(p);
                go.transform.SetPositionAndRotation(p, Rotation);
            }
            public void Box(Vector3 center, Vector3 size, Color color, bool solid = false)
            {
                var v = new[] { V(-1,-1,-1), V(1,-1,-1), V(1,1,-1), V(-1,1,-1),
                    V(-1,-1,1), V(1,-1,1), V(1,1,1), V(-1,1,1) }
                    .Select(p => center + Vector3.Scale(p, size * .5f)).ToArray();
                foreach (var f in new[] { new[]{0,3,2,1}, new[]{4,5,6,7}, new[]{0,4,7,3},
                    new[]{1,2,6,5}, new[]{3,7,6,2}, new[]{0,1,5,4} })
                {
                    var normal = Vector3.Cross(v[f[1]] - v[f[0]], v[f[2]] - v[f[0]]).normalized;
                    parts.Face(v[f[0]], v[f[1]], v[f[2]], color, normal);
                    parts.Face(v[f[0]], v[f[2]], v[f[3]], color, normal);
                }
                if (solid) { var c = go.AddComponent<BoxCollider>(); c.center = center; c.size = size; }
            }
            public void Finish()
            {
                var mesh = parts.Mesh(go.name);
                mesh.uv = mesh.vertices.Select(v => new Vector2(v.x + v.z, v.y)).ToArray();
                mesh = SaveMesh(mesh, Folder + "/Courtyard " + (++meshIndex) + ".asset");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        static void Path(string name, Vector3 a, Vector3 b, float width, Color color, bool marking = false)
        {
            var p = new BanagoEnvironmentModels.Parts();
            var side = Vector3.Cross(Vector3.up, (b-a).normalized) * width * .5f;
            int count = Mathf.CeilToInt(Vector3.Distance(a,b) / .6f);
            for (int i = 0; i < count; i++)
            {
                Vector3 l = Vector3.Lerp(a,b,(float)i/count), r = Vector3.Lerp(a,b,(float)(i+1)/count);
                var vertices = new[] {l-side,l+side,r+side,r-side};
                for (int j=0;j<4;j++) vertices[j].y = Ground(vertices[j]) + (marking ? .055f : .035f);
                p.Face(vertices[0],vertices[1],vertices[2],color,Vector3.up);
                p.Face(vertices[0],vertices[2],vertices[3],color,Vector3.up);
            }
            var mesh = p.Mesh(name);
            mesh.uv = mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();
            mesh = SaveMesh(mesh,Folder+"/Courtyard "+(++meshIndex)+".asset");
            var g = new GameObject(name);
            g.transform.SetParent(root.transform,false);
            g.AddComponent<MeshFilter>().sharedMesh=mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial=material;
            if (!marking) g.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        static void Bench(float x, float z)
        {
            var m = new Model("Concrete and timber school bench",x,z);
            foreach(float s in new[]{-1f,1f}) m.Box(V(s*.85f,.22f,0),V(.25f,.54f,.52f),Concrete,true);
            for(int i=0;i<3;i++) m.Box(V(0,.52f,(i-1)*.16f),V(2.3f,.09f,.145f),Wood,true);
            foreach(float s in new[]{-1f,1f}) m.Box(V(s*.85f,.77f,.22f),V(.08f,.65f,.08f),Steel);
            for(int i=0;i<2;i++) m.Box(V(0,.86f+i*.18f,.24f),V(2.3f,.14f,.06f),Wood);
            m.Finish();
        }

        static Model Shelter(string name,float x,float z,float width,float length)
        {
            var m = new Model(name,x,z);
            m.Box(V(0,-.1f,0),V(width,.32f,length),Concrete,true);
            foreach(float sx in new[]{-1f,1f})
                for(float zz=-length*.5f+.3f;zz<=length*.5f;zz+=length*.5f-.3f)
                    m.Box(V(sx*(width*.5f-.25f),1.55f,zz),V(.15f,3.15f,.15f),Green,true);
            m.Box(V(0,3.1f,0),V(width+.6f,.13f,length+.6f),Steel);
            for(float xx=-width*.5f-.25f;xx<width*.5f+.3f;xx+=.22f)
                m.Box(V(xx,3.18f,0),V(.035f,.035f,length+.6f),Concrete);
            foreach(float sx in new[]{-1f,1f}) m.Box(V(sx*(width*.5f+.15f),3.03f,0),V(.1f,.22f,length+.6f),Green);
            return m;
        }

        static void Sign(Model m,string title,Vector3 local,float size)
        {
            var g=new GameObject(title);
            g.transform.SetParent(m.go.transform,false);
            g.transform.localPosition=local;
            g.transform.localRotation=Quaternion.Euler(0,180,0);
            var t=g.AddComponent<TextMesh>();
            t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text=title;t.characterSize=size;t.fontSize=64;t.anchor=TextAnchor.MiddleCenter;t.color=White;
            g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
        }

        public static string Install(bool rebuild = false)
        {
            if(Application.isPlaying || EditorSceneManager.GetActiveScene().path!="Assets/Scenes/SIBUCAO_RAIN.unity")
                throw new InvalidOperationException("Open SIBUCAO_RAIN in Edit mode.");
            var previous = GameObject.Find(RootName);
            if(previous && !rebuild) return "Courtyard already installed; preserved.";
            if(previous) Undo.DestroyObjectImmediate(previous);
            ground=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .Where(c=>c.enabled&&c.name.StartsWith("Measured terrain")).ToArray();
            var shader=Shader.Find("StormSim/Weathered Building Surface");
            if(!shader||ground.Length==0) throw new InvalidOperationException("School terrain or surface shader missing.");
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Courtyard surfaces.mat");
            if(!material)
            {
                material=new Material(shader){name="Courtyard painted masonry timber and metal"};
                AssetDatabase.CreateAsset(material,Folder+"/Courtyard surfaces.mat");
            }
            material.SetFloat("_VertexColor",1);material.SetFloat("_Weathering",.22f);material.SetFloat("_Cull",0);
            root=new GameObject(RootName);meshIndex=0;
            Undo.RegisterCreatedObjectUndo(root,"Furnish school quadrangle");

            // Ground-following paving leaves the original terrain and evacuation triggers untouched.
            Path("Flag ceremony assembly plaza",Point(-2,2),Point(10,2),10,new Color(.51f,.58f,.56f));
            Path("Main entrance to quadrangle",V(0,0,657),Point(0,0),3,Concrete);
            Path("North classroom approach",Point(6,4),Point(6,19),3,Concrete);
            Path("East classroom approach",Point(0,0),Point(24,0),3,Concrete);
            Path("South lunch shelter approach",Point(0,0),Point(0,-24),2.4f,Concrete);
            Path("Garden cross path",Point(-5,-14),Point(8,-14),2.2f,Concrete);
            // Painted lanes give the assembly area a recognizable schoolyard use.
            for(int i=0;i<6;i++) Path("Assembly row "+(i+1),Point(-1+i*1.8f,1),Point(-1+i*1.8f,6),.055f,White,true);

            var walkway=Shelter("Covered classroom walkway",13,4,3,17);walkway.Finish();
            var lunch=Shelter("Open sided lunch and study shelter",0,-20,8,6);
            foreach(float xx in new[]{-2f,2f})
            {
                lunch.Box(V(xx,.78f,0),V(1.45f,.1f,3),Wood,true);
                foreach(float zz in new[]{-1f,1f}) lunch.Box(V(xx,.4f,zz),V(.85f,.72f,.17f),Green,true);
                foreach(float s in new[]{-1f,1f})
                {
                    lunch.Box(V(xx+s*.95f,.48f,0),V(.38f,.1f,3),Wood,true);
                    foreach(float zz in new[]{-1f,1f}) lunch.Box(V(xx+s*.95f,.24f,zz),V(.2f,.48f,.2f),Concrete,true);
                }
            }
            lunch.Box(V(0,2.66f,-3.05f),V(3.2f,.55f,.08f),Green);
            Sign(lunch,"SILUNGAN",V(0,2.66f,-3.1f),.16f);lunch.Finish();

            var wash=Shelter("Six tap school handwashing station",-5,-9,5,2.6f);
            wash.Box(V(0,.7f,.48f),V(4.3f,.16f,.72f),Concrete,true);
            wash.Box(V(0,.87f,.83f),V(4.3f,.55f,.12f),White);
            wash.Box(V(0,.73f,.12f),V(4.3f,.27f,.09f),White);
            foreach(float xx in new[]{-1.8f,1.8f})wash.Box(V(xx,.35f,.48f),V(.18f,.7f,.65f),Green,true);
            for(int i=0;i<6;i++)
            {
                float x=-1.75f+i*.7f;
                wash.Box(V(x,1.09f,.60f),V(.045f,.045f,.35f),Steel);
                wash.Box(V(x,1.13f,.74f),V(.16f,.04f,.04f),Steel);
                wash.Box(V(x,1.04f,.43f),V(.045f,.12f,.045f),Steel);
            }
            wash.Box(V(0,2.62f,-1.34f),V(4.6f,.55f,.07f),Green);
            Sign(wash,"HUGASAN NG KAMAY",V(0,2.62f,-1.39f),.11f);wash.Finish();

            var foliage=BanagoEnvironmentModels.Tree(0,21,true,new Color(.27f,.43f,.17f));
            foliage=SaveMesh(foliage,Folder+"/Courtyard broadleaf.asset");
            foreach(var p in new[]{new Vector2(-22,2),new Vector2(-13,14),new Vector2(10,17),new Vector2(19,10),new Vector2(-10,-25),new Vector2(9,-23)})
            {
                var planter=new Model("Raised shade tree planter",p.x,p.y);
                foreach(float s in new[]{-1f,1f})
                {planter.Box(V(s*1.4f,.18f,0),V(.16f,.46f,2.95f),Concrete,true);planter.Box(V(0,.18f,s*1.4f),V(2.8f,.46f,.16f),Concrete,true);}
                planter.Box(V(0,.04f,0),V(2.7f,.12f,2.7f),new Color(.25f,.29f,.17f));planter.Finish();
                var tree=new GameObject("Schoolyard shade tree");tree.transform.SetParent(planter.go.transform,false);
                tree.transform.localScale=V(2.7f,6.3f,2.7f);
                tree.AddComponent<MeshFilter>().sharedMesh=foliage;tree.AddComponent<MeshRenderer>().sharedMaterial=material;
                var trunk=planter.go.AddComponent<CapsuleCollider>();trunk.center=V(0,1.8f,0);trunk.height=3.6f;trunk.radius=.2f;
            }
            foreach(var p in new[]{new Vector2(-22,-1),new Vector2(-13,11),new Vector2(10,14),new Vector2(19,7),new Vector2(-10,-28),new Vector2(9,-26)}) Bench(p.x,p.y);

            for(int i=0;i<4;i++)
            {
                var bed=new Model("Gulayan raised vegetable bed "+(i+1),-17+(i%2)*4,-18-(i/2)*4);
                foreach(float s in new[]{-1f,1f})
                {bed.Box(V(s*1.4f,.15f,0),V(.12f,.4f,2.8f),Concrete,true);bed.Box(V(0,.15f,s*1.4f),V(2.8f,.4f,.12f),Concrete,true);}
                bed.Box(V(0,.06f,0),V(2.68f,.12f,2.68f),new Color(.26f,.22f,.16f));
                for(int x=0;x<4;x++)for(int z=0;z<4;z++)
                {bed.Box(V(-1+x*.65f,.22f,-1+z*.65f),V(.36f,.15f,.13f),Green);bed.Box(V(-1+x*.65f,.25f,-1+z*.65f),V(.13f,.14f,.38f),new Color(.34f,.49f,.21f));}
                bed.Finish();
            }
            var notice=new Model("Covered school notice board",-6,-5);
            foreach(float s in new[]{-1f,1f})notice.Box(V(s*.95f,1.1f,0),V(.1f,2.2f,.1f),Green,true);
            notice.Box(V(0,1.5f,0),V(2.2f,1.25f,.12f),Wood,true);
            notice.Box(V(0,2.2f,0),V(2.5f,.1f,.65f),Steel);
            for(int i=0;i<3;i++)notice.Box(V(-.68f+i*.68f,1.55f,-.069f),V(.48f,.64f,.008f),White);
            notice.Finish();
            var bins=new Model("Covered segregated school waste bins",-5,-17);
            var binColors=new[]{Green,new Color(.24f,.40f,.57f),new Color(.63f,.47f,.18f)};
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*.68f;
                bins.Box(V(x,.43f,0),V(.56f,.86f,.56f),binColors[i],true);
                bins.Box(V(x,.9f,0),V(.61f,.08f,.61f),Steel);
                bins.Box(V(x,.905f,-.12f),V(.3f,.085f,.16f),new Color(.12f,.15f,.14f));
            }
            bins.Finish();
            var gardenSign=new Model("Gulayan school garden sign",-18,-15.8f);
            foreach(float s in new[]{-1f,1f})gardenSign.Box(V(s*.95f,.75f,0),V(.08f,1.6f,.08f),Wood,true);
            gardenSign.Box(V(0,1.35f,0),V(2.3f,.52f,.08f),Green,true);
            Sign(gardenSign,"GULAYAN SA PAARALAN",V(0,1.35f,-.045f),.055f);gardenSign.Finish();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject=root;
            return "Installed courtyard: assembly plaza, five connecting paths, two shelters, six-tap wash station, six shade trees, six benches, four vegetable beds, waste bins and notice board. Existing buildings and gameplay unchanged.";
        }
    }
}
