using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>Expanded low-panel green school gate from the supplied street photograph.</summary>
public static class BanagoExpandedSchoolGateBuilder
{
    [MenuItem("Tools/StormSim/Create Expanded School Photo Gate")]
    public static void CreateFromMenu() => Build();

    public static object Build()
    {
        const string title="School entrance - expanded double gate from photo";
        var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Open MAIN_GAME in Edit mode; this gate correction already exists if present.");
        var oldGate=GameObject.Find("Wide closed green double gate with pink posts");if(!oldGate)throw new Exception("The gate to replace was not found.");
        var along=new Vector2(.54f,-.84f).normalized;var inward=new Vector2(-along.y,along.x);var center=new Vector2(-22.4f,654.4f)+along;var rotation=Quaternion.LookRotation(new Vector3(inward.x,0,inward.y));
        var grounds=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&c.sharedMesh&&c.name.StartsWith("Measured terrain")).ToArray();
        Vector2 XZ(float x,float z)=>center+along*x+inward*z;
        float Ground(Vector2 p){foreach(var c in grounds){var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)continue;if(c.Raycast(new Ray(new Vector3(p.x,30,p.y),Vector3.down),out var hit,70))return hit.point.y;}throw new Exception("Missing ground at "+p);}
        float floor=(Ground(XZ(-4.25f,0))+Ground(XZ(6.85f,0)))*.5f+.045f;
        Vector3 P(float x,float y,float z){var p=XZ(x,z);return new Vector3(p.x,floor+y,p.y);}
        float Base(float x)=>Ground(XZ(x,0))-floor+.055f;
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss"),backup="RoadRepairBackup/ExpandedSchoolGate_"+stamp,folder="Assets/StormSim/Generated/ExpandedSchoolGate_"+stamp;System.IO.Directory.CreateDirectory(backup);
        if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-expanded-gate.unity",true))throw new Exception("Backup failed.");
        AssetDatabase.CreateFolder("Assets/StormSim/Generated","ExpandedSchoolGate_"+stamp);var root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Expand school gate to the supplied photograph");
        var review=root.AddComponent<BanagoLocalDetailReview>();var hidden=new List<GameObject>{oldGate};
        var entrances=GameObject.Find("School side entrances - orange frontage and green gates");
        foreach(var name in new[]{"Older entrance red base and aged metal grilles","Faded green covered entry roof","Banago Elementary curved entrance sign"}){var t=entrances.transform.Find(name);if(t)hidden.Add(t.gameObject);}
        foreach(var text in entrances.GetComponentsInChildren<TextMesh>()){var p=text.transform.position;var d=new Vector2(p.x,p.z)-center;float x=Vector2.Dot(d,along),z=Vector2.Dot(d,inward);if(x< -4&&x> -10&&Mathf.Abs(z)<3)hidden.Add(text.gameObject);}
        review.originals=hidden.Distinct().ToArray();review.originalActive=review.originals.Select(g=>g.activeSelf).ToArray();review.notes="Supersedes the tall solid-panel gate and older arched side entrance. Original objects are retained inactive; disabling this group restores them.";review.Apply();EditorUtility.SetDirty(review);
        Material Mat(string name,bool roof=false){var m=new Material(Shader.Find("StormSim/Weathered Building Surface")){name=name};m.SetFloat("_VertexColor",1);m.SetFloat("_Weathering",roof?.30f:.19f);m.SetFloat("_Corrugation",roof?1:0);m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
        var metal=Mat("Dark green painted gate steel");var masonry=Mat("Faded pink posts and cream render");var roofMat=Mat("Worn green entrance canopy",true);var signMat=Mat("School entrance notice background");
        var green=new Color(.12f,.29f,.21f);var rail=new Color(.16f,.34f,.25f);var pink=new Color(.66f,.47f,.39f);var cream=new Color(.78f,.77f,.65f);var steel=new Color(.27f,.31f,.27f);
        void Quad(BanagoEnvironmentModels.Parts b,Vector3 a,Vector3 c,Vector3 d,Vector3 e,Color col,Vector3 normal){b.Face(a,c,d,col,normal);b.Face(a,d,e,col,normal);}
        void Box(BanagoEnvironmentModels.Parts b,Vector3 c,Vector3 size,Color col,Quaternion q=default){if(q==default)q=Quaternion.identity;var v=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)}.Select(s=>c+q*Vector3.Scale(s,size*.5f)).ToArray();foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}})Quad(b,v[f[0]],v[f[1]],v[f[2]],v[f[3]],col,Vector3.Cross(v[f[1]]-v[f[0]],v[f[2]]-v[f[0]]));}
        void Beam(BanagoEnvironmentModels.Parts b,Vector3 a,Vector3 c,float width,Color col)=>Box(b,(a+c)*.5f,new Vector3(width,Vector3.Distance(a,c),width),col,Quaternion.FromToRotation(Vector3.up,(c-a).normalized));
        int asset=0;GameObject Save(string name,BanagoEnvironmentModels.Parts parts,Material mat,bool collider=false,Transform parent=null){var m=parts.Mesh(name);m.uv=m.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();AssetDatabase.CreateAsset(m,folder+"/Mesh "+(++asset)+".asset");var g=new GameObject(name);g.transform.SetParent(parent?parent:root.transform,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=m;return g;}
        // Keep each leaf on its own hinge so it can be adjusted independently in the scene.
        float left=-4.03f,right=6.63f,mid=(left+right)*.5f,half=(right-left-.055f)*.5f;int bars=0;
        foreach(int side in new[]{-1,1})
        {
         float hinge=side<0?left:right;var pivot=new GameObject(side<0?"Main gate - left leaf hinge":"Main gate - right leaf hinge");pivot.transform.SetParent(root.transform,false);pivot.transform.SetPositionAndRotation(P(hinge,0,0),rotation);var b=new BanagoEnvironmentModels.Parts();
         Vector3 L(float x,float y,float z)=>new Vector3((x-hinge),y,z);
         float lo=side<0?left:mid+.0275f,hi=side<0?mid-.0275f:right;
         float bottomA=Base(lo),bottomB=Base(hi);Quad(b,L(lo,bottomA,.014f),L(hi,bottomB,.014f),L(hi,bottomB+.66f,.014f),L(lo,bottomA+.66f,.014f),green,Vector3.back);
         for(float x=lo+.055f;x<hi-.02f;x+=.105f)Box(b,L(x,Base(x)+.33f,-.009f),new Vector3(.019f,.64f,.035f),rail);
         Beam(b,L(lo,bottomA,-.035f),L(hi,bottomB,-.035f),.052f,rail);Beam(b,L(lo,bottomA+.68f,-.03f),L(hi,bottomB+.68f,-.03f),.046f,rail);
         foreach(float x in new[]{lo,hi})Beam(b,L(x,Base(x),0),L(x,2.18f,0),.065f,rail);
         foreach(float y in new[]{1.26f,2.18f})Beam(b,L(lo,y,0),L(hi,y,0),.046f,rail);
         for(float x=lo+.14f;x<hi-.10f;x+=.16f){Beam(b,L(x,Base(x)+.67f,0),L(x,2.27f,0),.023f,green);bars++;}
         var handleX=side<0?hi-.14f:lo+.14f;Beam(b,L(handleX,.94f,-.085f),L(handleX,1.28f,-.085f),.033f,steel);Box(b,L(side<0?hi-.03f:lo+.03f,1.04f,-.07f),new Vector3(.10f,.12f,.055f),steel);
         Save(side<0?"Open-bar green main gate left leaf":"Open-bar green main gate right leaf",b,metal,true,pivot.transform);
        }
        var posts=new BanagoEnvironmentModels.Parts();
        foreach(var q in new[]{new Vector2(-9.75f,2.50f),new Vector2(-7.10f,2.78f),new Vector2(-4.25f,2.95f),new Vector2(6.85f,2.38f)})
        {
         float low=Ground(XZ(q.x,0))-floor-.08f;float height=q.y;Box(posts,P(q.x,low+height*.5f,0),new Vector3(.43f,height,.49f),pink,rotation);Box(posts,P(q.x,low+height+.045f,0),new Vector3(.50f,.11f,.55f),cream,rotation);
         if(q.x== -4.25f)Box(posts,P(q.x,2.62f,.005f),new Vector3(.47f,.68f,.52f),cream,rotation);
        }
        Save("Expanded entrance pink masonry posts",posts,masonry,true);
        var pedestrian=new BanagoEnvironmentModels.Parts();float pl=-6.87f,pr=-4.49f;
        Box(pedestrian,P((pl+pr)*.5f,.48f,0),new Vector3(pr-pl,.74f,.06f),green,rotation);
        foreach(float x in new[]{pl,pr})Beam(pedestrian,P(x,Base(x),0),P(x,2.40f,0),.060f,rail);
        foreach(float y in new[]{.10f,.87f,1.47f,2.39f})Beam(pedestrian,P(pl,y,0),P(pr,y,0),.045f,rail);
        for(float x=pl+.13f;x<pr;x+=.15f)Beam(pedestrian,P(x,.89f,0),P(x,2.38f,0),.022f,green);
        Beam(pedestrian,P(pr-.16f,1.02f,-.07f),P(pr-.16f,1.31f,-.07f),.032f,steel);
        Save("Separate covered pedestrian gate",pedestrian,metal,true);
        var fixedFence=new BanagoEnvironmentModels.Parts();float fl=-9.52f,fr=-7.33f;
        Box(fixedFence,P((fl+fr)*.5f,.45f,.10f),new Vector3(fr-fl,.70f,.07f),green,rotation);foreach(float y in new[]{.10f,.81f,1.58f,2.34f})Beam(fixedFence,P(fl,y,.10f),P(fr,y,.10f),.05f,rail);for(float x=fl;x<fr;x+=.15f)Beam(fixedFence,P(x,.80f,.10f),P(x,2.34f,.10f),.022f,green);
        Save("Short green fence beside pedestrian entry",fixedFence,metal,true);
        var canopy=new BanagoEnvironmentModels.Parts();
        Quad(canopy,P(-7.36f,2.62f,-.65f),P(-3.94f,2.62f,-.65f),P(-3.94f,2.83f,1.15f),P(-7.36f,2.83f,1.15f),new Color(.22f,.36f,.27f),Vector3.up);
        foreach(float y in new[]{2.45f,2.56f,2.67f})Box(canopy,P(-5.65f,y,-.64f),new Vector3(3.45f,.13f,.18f),rail,rotation);
        Save("Green canopy and rolled fascia over side entrance",canopy,roofMat);
        // A modest concrete approach makes the expanded courtyard opening visible through the bars.
        var apron=new BanagoEnvironmentModels.Parts();
        for(float x=-7.25f;x<6.95f;x+=.50f)for(float z=-.52f;z<3.2f;z+=.45f){float xx=Mathf.Min(x+.5f,6.95f),zz=Mathf.Min(z+.45f,3.2f);var cs=new[]{XZ(x,z),XZ(xx,z),XZ(xx,zz),XZ(x,zz)};var ps=cs.Select(p=>new Vector3(p.x,Ground(p)+.027f,p.y)).ToArray();Quad(apron,ps[0],ps[3],ps[2],ps[1],new Color(.62f,.60f,.52f),Vector3.up);}
        Save("Widened concrete entrance apron",apron,masonry,true);
        // The registration banner is a separate editable sign, attached to the right gate leaf area.
        var banner=new BanagoEnvironmentModels.Parts();float bx=4.10f,by=1.43f,bw=2.55f,bh=1.05f;
        Quad(banner,P(bx-bw*.5f,by-bh*.5f,-.065f),P(bx-bw*.5f,by+bh*.5f,-.065f),P(bx+bw*.5f,by+bh*.5f,-.065f),P(bx+bw*.5f,by-bh*.5f,-.065f),new Color(.87f,.88f,.85f),-new Vector3(inward.x,0,inward.y));
        banner.Face(P(bx+bw*.5f-.64f,by+bh*.5f,-.074f),P(bx+bw*.5f,by+bh*.5f,-.074f),P(bx+bw*.5f,by-.09f,-.074f),new Color(.65f,.13f,.14f),-new Vector3(inward.x,0,inward.y));
        banner.Face(P(bx+bw*.5f-.37f,by-bh*.5f,-.074f),P(bx+bw*.5f,by-bh*.5f,-.074f),P(bx+bw*.5f,by-.15f,-.074f),new Color(.66f,.14f,.16f),-new Vector3(inward.x,0,inward.y));
        Save("White and red early registration banner",banner,signMat);
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var letters=new Material(Shader.Find("StormSim/World Sign Text")){name="Expanded gate lettering"};letters.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(letters,folder+"/Banner lettering.mat");root.AddComponent<BanagoWorldSigns>().lettering=letters;
        void Text(string name,string message,float x,float y,float z,float width,float height,Color col){var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.transform.SetPositionAndRotation(P(x,y,z),rotation);var t=g.AddComponent<TextMesh>();t.font=font;t.fontSize=64;t.text=message;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=col;t.fontStyle=name.StartsWith("Registration banner heading")||name.StartsWith("Registration school year")?FontStyle.Bold:FontStyle.Normal;font.RequestCharactersInTexture(message,64,t.fontStyle);float advance=1;var lines=message.Split('\n');foreach(var line in lines){float sum=0;foreach(char ch in line){if(font.GetCharacterInfo(ch,out var c,64,t.fontStyle))sum+=c.advance;else sum+=32;}advance=Mathf.Max(advance,sum);}t.characterSize=Mathf.Min(width*10/advance,height*10/(64*lines.Length));g.GetComponent<Renderer>().sharedMaterial=letters;}
        Text("Registration banner heading","EARLY REGISTRATION",bx-.15f,1.66f,-.078f,2.05f,.20f,new Color(.08f,.12f,.15f));Text("Registration school year","SY 2026-2027",bx-.31f,1.43f,-.078f,1.25f,.16f,new Color(.14f,.18f,.20f));Text("Registration invitation","Magpalista na!",bx-.15f,1.12f,-.078f,1.50f,.18f,new Color(.51f,.15f,.16f));
        var board=new BanagoEnvironmentModels.Parts();Box(board,P(-4.34f,2.77f,-.32f),new Vector3(1.04f,.64f,.055f),cream,rotation);Save("Small cream entrance signboard",board,masonry);Text("Side entrance sign","BANAGO ELEMENTARY\nSCHOOL I",-4.34f,2.78f,-.356f,.92f,.32f,new Color(.22f,.31f,.25f));
        // Couple the notice to its leaf so moving the hinge also moves the attached banner.
        var rightHinge=root.transform.Find("Main gate - right leaf hinge");foreach(string name in new[]{"White and red early registration banner","Registration banner heading","Registration school year","Registration invitation"})root.transform.Find(name).SetParent(rightHinge,true);
        var result=new{root=title,folder,backup,previousOpeningMetres=8.2f,mainOpeningMetres=right-left,openingIncreasePercent=((right-left)/8.2f-1)*100,pedestrianOpeningMetres=pr-pl,solidLowerPanelMetres=.66f,upperRailHeightMetres=2.18f,verticalBars=bars,replacedObjects=review.originals.Length};System.IO.File.WriteAllText("RoadRepairBackup/expanded-school-gate-latest.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);bool saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);Selection.activeGameObject=root;return new{saved,result};
    }
}
