using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoAftermathBuilder
    {
        const string ScenePath = "Assets/Scenes/SIBUCAO_AFTERMATH.unity";
        const string Folder = "Assets/StormSim/Sibucao/Art/Aftermath";
        const string RootName = "Aftermath - school relief and storm debris";
        static Transform root;
        static MeshCollider[] ground;
        static Material wood, metal, mud, water, leaf, cardboard, blue, cream;
        static int pieces;
        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        static Material Mat(string name,Color color,float smooth=0)
        {
            var path=Folder+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);return m;
        }
        static float Ground(float x,float z)
        {
            float y=float.NegativeInfinity;
            foreach(var c in ground)
                if(c.Raycast(new Ray(V(x,40,z),Vector3.down),out var hit,80))y=Mathf.Max(y,hit.point.y);
            if(!float.IsFinite(y))throw new InvalidOperationException("No measured terrain at "+x+", "+z);
            return y;
        }
        static GameObject Shape(string name,Vector3 p,Vector3 scale,Material mat,PrimitiveType type=PrimitiveType.Cube,bool solid=false)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(root,false);
            g.transform.position=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());
            pieces++;return g;
        }
        static void Patch(string name,float x,float z,float sx,float sz,Material mat)
        {
            // Conform each vertex to the baked terrain so stains do not float over slopes.
            const int count=18;var vertices=new Vector3[count+1];var triangles=new int[count*3];
            vertices[0]=V(x,Ground(x,z)+.028f,z);
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;float r=1+.1f*Mathf.Sin(i*5.7f);
                float px=x+Mathf.Cos(a)*sx*.5f*r,pz=z+Mathf.Sin(a)*sz*.5f*r;
                vertices[i+1]=V(px,Ground(px,pz)+.028f,pz);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%count+1;triangles[i*3+2]=i+1;
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();
            string path=Folder+"/Patch "+pieces+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
            var g=new GameObject(name);g.transform.SetParent(root,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial=mat;pieces++;
        }
        static void Branch(float x,float z,float angle)
        {
            float y=Ground(x,z);var trunk=Shape("Fallen branch - stripped timber",V(x,y+.18f,z),V(.24f,1.7f,.24f),wood,PrimitiveType.Cylinder);
            trunk.transform.rotation=Quaternion.Euler(90,angle,0);
            for(int i=0;i<3;i++)
            {
                var offset=Quaternion.Euler(0,angle,0)*V((i%2==0?1:-1)*.35f,0,(i-1)*.65f);
                var twig=Shape("Broken side branch",V(x,y+.16f,z)+offset,V(.075f,.55f,.075f),wood,PrimitiveType.Cylinder);
                twig.transform.rotation=Quaternion.Euler(75,angle+45*(i%2==0?1:-1),0);
                Shape("Storm-stripped leaves",V(x,y+.13f,z)+offset*1.5f,V(.58f,.16f,.72f),leaf,PrimitiveType.Sphere);
            }
        }
        static void Sheet(float x,float z,float angle)
        {
            var p=V(x,Ground(x,z)+.13f,z);var rotation=Quaternion.Euler(4,angle,-6);
            var panel=Shape("Dislodged galvanized roof sheet",p,V(1.1f,.045f,1.8f),metal);panel.transform.rotation=rotation;
            for(int i=0;i<8;i++)
            {
                var rib=Shape("Corrugated metal ridge",p+rotation*V(-.48f+i*.137f,.035f,0),V(.035f,.035f,1.8f),metal);
                rib.transform.rotation=rotation;
            }
        }
        static void ReliefStation()
        {
            float x=8,z=661,y=Ground(x,z);
            Shape("Relief distribution table",V(x,y+.8f,z),V(3,.12f,1.1f),cream,PrimitiveType.Cube,true);
            foreach(float dx in new[]{-1.25f,1.25f})foreach(float dz in new[]{-.4f,.4f})
                Shape("Table leg",V(x+dx,y+.39f,z+dz),V(.09f,.78f,.09f),metal);
            for(int i=0;i<6;i++)
            {
                var p=V(x-1+i%3*.65f,y+1.04f+(i/3)*.36f,z);
                Shape("Packed relief box",p,V(.55f,.34f,.6f),cardboard,PrimitiveType.Cube,true);
                Shape("Relief box tape",p+V(0,.172f,0),V(.07f,.006f,.6f),cream);
            }
            for(int i=0;i<4;i++)Shape("Drinking-water container",V(x+2,y+.35f,z-1+i*.65f),V(.38f,.68f,.36f),blue,PrimitiveType.Cube,true);
            foreach(float dx in new[]{-2.3f,2.3f})foreach(float dz in new[]{-1.8f,1.8f})
                Shape("Relief canopy pole",V(x+dx,y+1.35f,z+dz),V(.06f,2.7f,.06f),metal);
            var roof=Shape("Blue relief canopy",V(x,y+2.8f,z),V(5,.08f,4),blue);roof.transform.rotation=Quaternion.Euler(0,0,4);
            var board=Shape("Relief station sign board",V(x,y+2,z-1.82f),V(2.7f,.55f,.035f),cream);
            var label=new GameObject("RELIEF SUPPLIES sign");label.transform.SetParent(board.transform,false);
            label.transform.localPosition=V(0,0,-.6f);label.transform.localRotation=Quaternion.identity;
            label.transform.localScale=V(1/2.7f,1/.55f,1/.035f);
            var text=label.AddComponent<TextMesh>();text.text="RELIEF SUPPLIES";text.anchor=TextAnchor.MiddleCenter;
            text.characterSize=.035f;text.fontSize=64;text.color=new Color(.12f,.2f,.22f);
        }
        [MenuItem("StormSim/Aftermath/Dress school and approach")]
        public static void Install()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            if(EditorSceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open SIBUCAO_AFTERMATH first.");
            if(GameObject.Find(RootName))throw new InvalidOperationException("Aftermath dressing already exists; preserve manual edits.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            ground=Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.name.StartsWith("Measured terrain")).ToArray();
            if(ground.Length==0)throw new InvalidOperationException("Measured ground missing.");
            root=new GameObject(RootName).transform;pieces=0;
            wood=Mat("Waterlogged timber",new Color(.22f,.14f,.085f));metal=Mat("Dented galvanized metal",new Color(.38f,.43f,.43f),.32f);
            mud=Mat("Deposited mud",new Color(.22f,.18f,.12f),.15f);water=Mat("Shallow muddy puddles",new Color(.26f,.32f,.32f),.9f);
            leaf=Mat("Fallen olive leaves",new Color(.27f,.3f,.12f));cardboard=Mat("Relief cartons",new Color(.55f,.38f,.2f));
            blue=Mat("Relief blue",new Color(.09f,.28f,.48f));cream=Mat("Relief off white",new Color(.83f,.82f,.72f));
            // Roadside clusters leave the central entrance and route open.
            var session=Object.FindFirstObjectByType<SibucaoSession>();
            for(int i=0;i<8;i++)
            {
                float distance=session.route.length-44-i*5.4f;
                var center=session.route.Sample(distance);
                var tangent=session.route.Sample(distance+1)-session.route.Sample(distance-1);tangent.y=0;
                var p=center+Vector3.Cross(Vector3.up,tangent.normalized)*(i%2==0?-5.2f:5.4f);
                float x=p.x,z=p.z;
                Patch("Silt deposited along school approach",x,z,3.2f,4.1f,mud);
                Patch("Receded flood puddle",x+.4f,z+1,1.8f,2.1f,water);
                Branch(x,z,25+i*31);
                for(int j=0;j<3;j++)
                {
                    float px=x-.7f+j*.6f,pz=z-1.2f;
                    var plank=Shape("Scattered broken fence board",V(px,Ground(px,pz)+.06f,pz),V(.16f,.065f,.9f),wood);
                    plank.transform.rotation=Quaternion.Euler(0,i*37+j*48,0);
                }
            }
            Sheet(-36,655,28);Sheet(-34,676,-35);Sheet(-48,680,62);
            Patch("Silt beside evacuation entrance",-5,659,3,4,mud);
            Branch(-6,662,80);ReliefStation();
            session.player.transform.position=session.initialPlayerSpawn.position;
            var weather=session.weather;weather.aftermath=true;
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Aftermath sky.mat");
            if(!sky){sky=new Material(Shader.Find("StormSim/Aftermath Overcast Sky"));AssetDatabase.CreateAsset(sky,Folder+"/Aftermath sky.mat");}
            sky.shader=Shader.Find("StormSim/Aftermath Overcast Sky");weather.stormSky=sky;RenderSettings.skybox=sky;
            if(weather.rain){weather.rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=weather.rain.main;main.playOnAwake=false;}
            foreach(var audio in new[]{weather.rainAudio,weather.thunderAudio})if(audio){audio.Stop();audio.playOnAwake=false;audio.volume=0;}
            if(weather.windAudio)weather.windAudio.volume=.045f;
            if(weather.sun){weather.sun.intensity=.65f;weather.sun.color=new Color(1,.95f,.85f);weather.sun.transform.rotation=Quaternion.Euler(42,-35,0);}
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.7f,.74f);
            RenderSettings.ambientEquatorColor=new Color(.48f,.51f,.51f);RenderSettings.ambientGroundColor=new Color(.24f,.23f,.21f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.65f,.69f,.71f);RenderSettings.fogDensity=.0018f;
            var flood=session.GetComponent<FloodSimulationManager>();if(flood)flood.enabled=false;
            foreach(var sea in Object.FindObjectsByType<BanagoSeaWaves>(FindObjectsSortMode.None)){sea.waveHeight=.12f;sea.waveSpeed=.35f;sea.currentSpeed=.025f;}
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()))throw new IOException("Could not save aftermath scene.");
            Directory.CreateDirectory("Captures/Aftermath");
            File.WriteAllText("Captures/Aftermath/install.txt","PASS: "+pieces+" dressing pieces; storm precipitation stopped; runoff disabled; calm sea; school spawn retained.\nStatic visual pass only; original new-game flow remains.\n");
            Capture("school-entrance",V(-1,1,651),V(7,0,666));
            Capture("school-approach",V(-50,4,685),V(-28,-.5f,654));
            Capture("school-overview",V(-27,24,629),V(9,0,658));
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var go=new GameObject("Temporary aftermath review camera");var cam=go.AddComponent<Camera>();
            cam.transform.position=position;cam.transform.LookAt(target);cam.fieldOfView=65;cam.farClipPlane=1500;
            var rt=new RenderTexture(1600,900,24);cam.targetTexture=rt;cam.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("Captures/Aftermath/"+name+".png",image.EncodeToPNG());
            RenderTexture.active=previous;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);
        }
        public static void Batch()
        {
            try{EditorSceneManager.OpenScene(ScenePath);Install();EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
