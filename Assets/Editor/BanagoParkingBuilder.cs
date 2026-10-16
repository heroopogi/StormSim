using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

/// <summary>A compact additive lot. Existing buildings, trees and road geometry are not edited.</summary>
public static class BanagoParkingBuilder
{
    public const string RootName="Banago informal parking - roadside pocket";
    static string folder;
    static Transform root;
    static Material rough,wet;
    static readonly Color Concrete=new Color(.50f,.49f,.44f),Dirt=new Color(.37f,.31f,.23f),White=new Color(.71f,.70f,.62f),Wood=new Color(.38f,.29f,.20f);
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static float R(System.Random r,float a,float b)=>a+(float)r.NextDouble()*(b-a);
    static float Ground(float x,float z)
    {var p=root.TransformPoint(V(x,0,z));if(!BanagoParkingSite.Height(new Vector2(p.x,p.z),out var y))throw new Exception("No measured ground at parking vertex");return y-root.position.y;}
    sealed class Shape
    {
        public readonly BanagoEnvironmentModels.Parts p=new BanagoEnvironmentModels.Parts();
        public void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color col,Vector3 normal)
        {p.Face(a,b,c,col,normal);p.Face(a,c,d,col,normal);}
        public void Box(Vector3 center,Vector3 size,Color color,Quaternion rot=default)
        {
            if(rot==default)rot=Quaternion.identity;
            var vs=new[]{V(-1,-1,-1),V(1,-1,-1),V(1,1,-1),V(-1,1,-1),V(-1,-1,1),V(1,-1,1),V(1,1,1),V(-1,1,1)}.Select(v=>center+rot*Vector3.Scale(v,size/2)).ToArray();
            int[][] faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}};
            foreach(var f in faces)Q(vs[f[0]],vs[f[1]],vs[f[2]],vs[f[3]],color,Vector3.Cross(vs[f[1]]-vs[f[0]],vs[f[2]]-vs[f[0]]));
        }
        public void Strip(Vector2 a,Vector2 b,float width,Color col,float offset=.05f)
        {
            var dir=(b-a).normalized;var side=new Vector2(-dir.y,dir.x)*width/2;Func<Vector2,Vector3> at=v=>V(v.x,Ground(v.x,v.y)+offset,v.y);
            Q(at(a-side),at(a+side),at(b+side),at(b-side),col,Vector3.up);
        }
    }
    static GameObject Save(string name,Shape shape,Material mat,bool collider=false)
    {
        var mesh=shape.p.Mesh(name);mesh.uv=shape.p.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,folder+"/"+name+".asset");
        var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
    }
    static GameObject Place(GameObject source,string name,float x,float z,float yaw,bool store=false)
    {
        var go=UnityEngine.Object.Instantiate(source,root);go.name=name;go.transform.localScale=Vector3.one;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localPosition=V(x,Ground(x,z)+.055f,z);
        if(store)foreach(var t in go.GetComponentsInChildren<Transform>())if(t.name.Contains("Ground-following foundation"))t.gameObject.SetActive(false);
        // Base models are authored in metres with their wheels/floor at local y=0.
        var group=go.GetComponent<LODGroup>();if(group)group.RecalculateBounds();return go;
    }
    public static object Build()
    {
        if(GameObject.Find(RootName))throw new Exception("Parking area already exists.");var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Wrong scene.");
        var site=BanagoParkingSite.Find(14,14,10).FirstOrDefault(s=>Vector3.Distance(s.center,new Vector3(185.98f,-.43f,179.77f))<1);if(site==null)throw new Exception("Previously inspected clear site is no longer available; nothing changed.");
        var sourceRoot=GameObject.Find(BanagoNeighborhoodDetailsBuilder.RootName).transform;
        Func<string,int,GameObject> src=(kind,index)=>sourceRoot.Find(kind).GetChild(index).gameObject;
        var scooter=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Organized Assets/Vehicles/Scooters/Prefabs/Scooty.prefab");if(!scooter)throw new Exception("Scooter source missing.");
        string backupDir="RoadRepairBackup/Parking_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backupDir);string backup=backupDir+"/MAIN_GAME.before-parking.unity";
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Parking backup failed.");
        var originals=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t,t=>new{t.position,t.localRotation,t.localScale,active=t.gameObject.activeSelf});
        folder="Assets/StormSim/Generated/InformalParking_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var shader=Shader.Find("StormSim/Banago Lived In PBR");rough=new Material(shader){name="Worn concrete dirt and weathered timber"};rough.SetFloat("_Kind",2);rough.SetFloat("_Smoothness",.07f);rough.SetFloat("_Metallic",0);AssetDatabase.CreateAsset(rough,folder+"/Rough parking surfaces.mat");
        wet=new Material(rough){name="Dark wet patches"};wet.SetFloat("_Smoothness",.72f);AssetDatabase.CreateAsset(wet,folder+"/Wet patches.mat");
        var go=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(go,"Add informal parking area");root=go.transform;root.position=site.center;root.rotation=Quaternion.Euler(0,site.yaw,0);var rng=new System.Random(7963);
        var floor=new Shape();float step=.35f;
        for(int ix=0;ix<40;ix++)for(int iz=0;iz<40;iz++)
        {
            float x=-7+ix*step,z=-7+iz*step;var verts=new[]{V(x,0,z),V(x+step,0,z),V(x+step,0,z+step),V(x,0,z+step)};
            var cols=verts.Select(v=>{float n=Mathf.PerlinNoise(v.x*2.2f+9,v.z*2.2f+12);float dirt=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(v.x+.4f)-4.4f+n*.65f)/1.4f));if(v.z<-4.3f)dirt=Mathf.Max(dirt,.75f);return Color.Lerp(Concrete,Dirt,dirt)*(.89f+.17f*n);}).ToArray();
            for(int i=0;i<4;i++)verts[i].y=Ground(verts[i].x,verts[i].z)+.04f;
            floor.p.Face(verts[0],verts[1],verts[2],(cols[0]+cols[1]+cols[2])/3,Vector3.up);floor.p.Face(verts[0],verts[2],verts[3],(cols[0]+cols[2]+cols[3])/3,Vector3.up);
        }
        // A narrow worn dirt entrance meets the lane; the lane itself remains unchanged.
        for(float z=-7-site.entryGap;z<-7;z+=.35f)for(float x=-3.2f;x<3.2f;x+=.4f)
        {float zz=Mathf.Min(-7,z+.35f);floor.Q(V(x,Ground(x,z)+.043f,z),V(x+.4f,Ground(x+.4f,z)+.043f,z),V(x+.4f,Ground(x+.4f,zz)+.043f,zz),V(x,Ground(x,zz)+.043f,zz),Dirt*(.9f+.1f*Mathf.PerlinNoise(x,z)),Vector3.up);}
        Save("Cracked concrete and packed dirt lot",floor,rough,true);
        var cracks=new Shape();
        for(int i=0;i<36;i++)
        {
            var p=new Vector2(R(rng,-5.4f,4.6f),R(rng,-3.6f,6.1f));float angle=R(rng,0,6.28f);var d=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
            for(int j=0;j<6;j++){var q=p+d*R(rng,.17f,.38f)+new Vector2(R(rng,-.10f,.10f),R(rng,-.10f,.10f));if(Mathf.Abs(q.x)>5.7f||q.y>6.5f||q.y<-4)break;cracks.Strip(p,q,R(rng,.009f,.023f),new Color(.27f,.25f,.21f),.047f);p=q;}
        }
        Save("Irregular hairline concrete cracks",cracks,rough);
        var paint=new Shape();
        foreach(float x in new[]{-6.25f,-3.15f,.05f,3.20f,6.30f})for(float z=.10f;z<6.2f;z+=.19f)
            if(rng.NextDouble()>.25){float xx=x+R(rng,-.035f,.035f);paint.Strip(new Vector2(xx,z),new Vector2(xx+R(rng,-.015f,.015f),z+R(rng,.08f,.16f)),R(rng,.045f,.085f),White*R(rng,.86f,1.02f),.054f);}
        Save("Worn hand-painted parking guides",paint,rough);
        var puddles=new Shape();
        foreach(var c in new[]{new Vector2(2.6f,-3.0f),new Vector2(-1.9f,-5.4f),new Vector2(5.7f,.3f)})
        {float y=Ground(c.x,c.y)+.05f;var center=V(c.x,y,c.y);for(int i=0;i<15;i++){float a=i*Mathf.PI*2/15,b=(i+1)*Mathf.PI*2/15;var p=c+new Vector2(Mathf.Cos(a)*.65f,Mathf.Sin(a)*.27f)*R(rng,.85f,1.1f);var q=c+new Vector2(Mathf.Cos(b)*.65f,Mathf.Sin(b)*.27f)*R(rng,.85f,1.1f);puddles.p.Face(center,V(p.x,Ground(p.x,p.y)+.052f,p.y),V(q.x,Ground(q.x,q.y)+.052f,q.y),new Color(.24f,.265f,.25f),Vector3.up);}}
        Save("Shallow rain-darkened patches",puddles,wet);
        var wall=new Shape();
        // Low rear CHB boundary, with an uneven plaster cap and discrete block joints.
        for(int x=0;x<32;x++){float xx=-6.5f+x*.41f;float y=Ground(xx,6.6f);for(int row=0;row<4;row++)wall.Box(V(xx,y+.125f+row*.21f,6.6f),V(.395f,.196f,.17f),Concrete*R(rng,.74f,.94f));}
        for(float z=-6.4f;z<6.4f;z+=.9f){float y=Ground(-6.7f,z);wall.Box(V(-6.7f,y+.55f,z),V(.09f,1.1f,.11f),Wood*R(rng,.75f,1.08f),Quaternion.Euler(R(rng,-3,3),0,R(rng,-2,2)));}
        foreach(float yy in new[]{.35f,.80f})for(float z=-6.2f;z<6.1f;z+=.8f)wall.Box(V(-6.7f,Ground(-6.7f,z)+yy,z),V(.065f,.085f,.84f),Wood*.83f);
        Save("Low hollow-block wall and weathered fence",wall,rough,true);
        var vehicles=new List<GameObject>();
        vehicles.Add(Place(src("jeepney",0),"Parked jeepney - blue",-4.75f,2.8f,178));
        vehicles.Add(Place(src("jeepney",1),"Parked jeepney - faded red",-1.58f,2.85f,183));
        vehicles.Add(Place(src("car",2),"Private car - slightly angled",1.6f,3.8f,174));
        vehicles.Add(Place(src("car",3),"Private car - silver",4.73f,3.7f,185));
        vehicles.Add(Place(src("tricycle",0),"Waiting tricycle - left",-4.8f,-2.7f,-7));
        vehicles.Add(Place(src("tricycle",1),"Waiting tricycle - store side",4.8f,-1.65f,11));
        for(int i=0;i<3;i++)vehicles.Add(Place(scooter,"Parked scooter "+(i+1),-5.9f+i*1.2f,-5.5f,new[]{-12f,4f,-3f}[i]));
        var store=Place(src("store",0),"Parking sari-sari store",4.9f,-4.95f,180,true);
        var pad=new Shape();pad.Box(V(4.9f,Ground(4.9f,-4.95f)+.05f,-4.95f),V(2.7f,.10f,2.1f),Concrete);Save("Small store step",pad,rough,true);
        var errors=new List<string>();foreach(var kv in originals){var t=kv.Key;var a=kv.Value;if(!t||t.position!=a.position||t.localRotation!=a.localRotation||t.localScale!=a.localScale||t.gameObject.activeSelf!=a.active)errors.Add(t?t.name:"missing original");}
        var result=new{root=RootName,backup,folder,width=14,depth=14,vehicles=vehicles.Count,jeepneys=2,cars=2,tricycles=2,scooters=3,newTrees=0,existingObjectsChecked=originals.Count,preservationErrors=errors,site=new{center=site.center.ToString(),site.yaw,site.road,site.nearbyHomes}};
        File.WriteAllText(backupDir+"/parking-report.json",JsonConvert.SerializeObject(result,Formatting.Indented));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();return result;
    }
}
