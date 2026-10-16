using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Additive, shared-mesh neighborhood props. Does not alter existing objects.</summary>
public static class BanagoNeighborhoodDetailsBuilder
{
    public const string RootName="Banago Neighborhood Life - reference additions";
    public static string Status="Not built";
    public static string Folder;
    static readonly Color Steel=new Color(.64f,.68f,.69f), Rubber=new Color(.055f,.06f,.065f), Glass=new Color(.13f,.23f,.27f), Wood=new Color(.33f,.22f,.13f);
    static readonly Color[] Paint={new Color(.08f,.40f,.58f),new Color(.66f,.16f,.13f),new Color(.13f,.40f,.25f),new Color(.76f,.55f,.12f)};
    static readonly Color[] Petals={new Color(.92f,.18f,.48f),new Color(.93f,.43f,.15f),new Color(.77f,.22f,.70f),new Color(.93f,.83f,.40f)};
    static Dictionary<string,Material> materials;
    static Dictionary<string,Model> models=new Dictionary<string,Model>();
    sealed class Model { public Dictionary<string,Mesh> parts=new Dictionary<string,Mesh>(); }
    sealed class Shape
    {
        public BanagoEnvironmentModels.Parts p=new BanagoEnvironmentModels.Parts();
        public void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {var n=Vector3.Cross(b-a,c-a).normalized;p.Face(a,b,c,color,n);p.Face(a,c,d,color,n);}
        public void Box(Vector3 center,Vector3 size,Color color,Quaternion rot=default)
        {
            if(rot==default)rot=Quaternion.identity;
            var v=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)}.Select(q=>center+rot*Vector3.Scale(q,size*.5f)).ToArray();
            int[][] faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}};
            foreach(var f in faces)Q(v[f[0]],v[f[1]],v[f[2]],v[f[3]],color);
        }
        public void Tube(Vector3 a,Vector3 b,float radius,Color color,float top=-1,int n=10)
        {
            if(top<0)top=radius;var axis=(b-a).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
            for(int i=0;i<n;i++){float t=i*Mathf.PI*2/n,t2=(i+1)*Mathf.PI*2/n;var x=u*Mathf.Cos(t)+v*Mathf.Sin(t);var y=u*Mathf.Cos(t2)+v*Mathf.Sin(t2);Q(a+x*radius,a+y*radius,b+y*top,b+x*top,color);p.Face(a,a+y*radius,a+x*radius,color,-axis);p.Face(b,b+x*top,b+y*top,color,axis);}
        }
        public void Ellipsoid(Vector3 c,Vector3 size,Color color,int rings=5,int seg=10)
        {
            Func<int,int,Vector3> at=(i,j)=>{float a=i*Mathf.PI/rings,b=j*Mathf.PI*2/seg;return c+Vector3.Scale(new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b)),size);};
            for(int i=0;i<rings;i++)for(int j=0;j<seg;j++){var a=at(i,j);var b=at(i+1,j);var d=at(i,j+1);var e=at(i+1,j+1);p.Face(a,b,e,color,(a+b+e)/3-c,true,c);p.Face(a,e,d,color,(a+e+d)/3-c,true,c);}
        }
        public void Wheel(Vector3 c,float r,float width)
        {
            Tube(c-Vector3.right*width/2,c+Vector3.right*width/2,r,Rubber,-1,20);
            foreach(int side in new[]{-1,1}){var hub=c+Vector3.right*(width/2+.006f)*side;Tube(hub-Vector3.right*.014f,hub+Vector3.right*.014f,r*.62f,Steel,-1,16);for(int s=0;s<8;s++){float a=s*Mathf.PI/4;Tube(hub,hub+new Vector3(0,Mathf.Sin(a),Mathf.Cos(a))*r*.50f,.018f,new Color(.32f,.34f,.35f),-1,5);}}
        }
        public Mesh Finish(string name){var m=p.Mesh(name);m.uv=p.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();m.RecalculateTangents();return m;}
    }
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static float R(System.Random rng,float a,float b)=>a+(float)rng.NextDouble()*(b-a);
    static Model SaveModel(string name,params (string,Shape)[] shapes)
    {
        var model=new Model();bool first=true;string path=Folder+"/"+name+".asset";
        foreach(var pair in shapes){if(pair.Item2.p.vertices.Count==0)continue;var mesh=pair.Item2.Finish(name+" "+pair.Item1);if(first){AssetDatabase.CreateAsset(mesh,path);first=false;}else AssetDatabase.AddObjectToAsset(mesh,path);model.parts[pair.Item1]=mesh;}
        return model;
    }
    static Model Frontage(int variant)
    {
        var wall=new Shape();var metal=new Shape();var plants=new Shape();var rng=new System.Random(992+variant);var color=Paint[variant];
        foreach(int side in new[]{-1,1})
        {
            float x=side*1.51f;wall.Box(V(x,.59f,-.23f),V(1.43f,1.18f,.24f),new Color(.44f,.43f,.38f));
            if(variant%2==0)
            {
                // Individually raised stone faces, with staggered joints.
                for(int row=0;row<4;row++)for(int col=0;col<4;col++){float xx=x-.61f+col*.38f+(row%2)*.055f;wall.Box(V(xx,.15f+row*.28f,-.075f),V(.33f,.23f,.085f),new Color(.42f,.43f,.40f)*R(rng,.80f,1.18f));}
            }
            else for(int row=1;row<5;row++)wall.Box(V(x,row*.22f,-.10f),V(1.42f,.014f,.018f),new Color(.27f,.28f,.25f));
            wall.Box(V(x,1.20f,-.23f),V(1.50f,.10f,.32f),new Color(.57f,.55f,.48f));
            foreach(float xx in new[]{side*.80f,side*2.19f}){wall.Box(V(xx,.82f,-.23f),V(.23f,1.64f,.30f),new Color(.67f,.65f,.57f));wall.Box(V(xx,1.67f,-.23f),V(.31f,.10f,.37f),new Color(.49f,.47f,.40f));}
            for(float xx=x-.62f;xx<x+.68f;xx+=.18f)metal.Tube(V(xx,1.24f,-.23f),V(xx,1.64f,-.23f),.015f,color,-1,5);
            metal.Tube(V(x-.7f,1.57f,-.23f),V(x+.7f,1.57f,-.23f),.025f,color,-1,6);
            // Both leaves are opened 76 degrees; the center remains walkable.
            var hinge=V(side*.76f,.0f,-.23f);var rot=Quaternion.Euler(0,side*76,0);
            Func<float,float,Vector3> pt=(u,y)=>hinge+rot*V(-side*u,y,0);
            foreach(float u in new[]{0f,.69f})metal.Tube(pt(u,.08f),pt(u,1.48f),.027f,color,-1,6);
            foreach(float y in new[]{.12f,.63f,1.45f})metal.Tube(pt(0,y),pt(.69f,y),.025f,color,-1,6);
            for(float u=.1f;u<.69f;u+=.10f)metal.Tube(pt(u,.65f),pt(u,1.45f),.014f,color,-1,5);
            metal.Box(pt(.345f,.36f),V(.69f,.48f,.035f),color,rot);
        }
        return SaveModel("Frontage "+variant,("masonry",wall),("paint",metal));
    }
    static Model Flowers(int variant)
    {
        var mesh=new Shape();var rng=new System.Random(717+variant);
        for(int pot=0;pot<3;pot++)
        {
            var c=V((pot-1)*.43f,0,pot==1?.11f:0);var pc=pot==1?Paint[variant]:new Color(.57f,.30f,.19f);
            mesh.Tube(c+V(0,.02f,0),c+V(0,.31f,0),.115f,pc,.17f,12);mesh.Tube(c+V(0,.28f,0),c+V(0,.33f,0),.185f,pc,-1,12);mesh.Tube(c+V(0,.332f,0),c+V(0,.334f,0),.154f,new Color(.17f,.12f,.075f),-1,12);
            for(int j=0;j<12;j++)
            {
                float a=j*2.399f;var top=c+V(Mathf.Cos(a)*R(rng,.06f,.16f),R(rng,.52f,.86f),Mathf.Sin(a)*R(rng,.06f,.16f));
                mesh.Tube(c+V(0,.33f,0),top,.006f,new Color(.19f,.34f,.10f),.003f,4);
                mesh.Ellipsoid(top-V(0,.15f,0),V(.10f,.033f,.055f),new Color(.19f,.37f,.12f),3,6);
                for(int petal=0;petal<5;petal++){float b=petal*Mathf.PI*2/5;mesh.Ellipsoid(top+V(Mathf.Cos(b)*.031f,.005f,Mathf.Sin(b)*.031f),V(.032f,.018f,.026f),Petals[variant],3,6);}mesh.Ellipsoid(top+V(0,.014f,0),V(.014f,.016f,.014f),new Color(.94f,.73f,.12f),3,6);
            }
        }
        return SaveModel("Potted flowering gardens "+variant,("detail",mesh));
    }
    static Model Jeepney(int variant)
    {
        var body=new Shape();var trim=new Shape();var color=Paint[variant];var accent=Paint[(variant+3)%4];
        body.Box(V(0,.65f,-.15f),V(1.96f,.28f,5.6f),color);body.Box(V(0,1.10f,2.00f),V(1.68f,.48f,1.42f),color);
        body.Box(V(0,1.27f,2.0f),V(1.72f,.10f,1.48f),color*1.14f);
        foreach(int side in new[]{-1,1})
        {
            body.Box(V(side*.98f,1.04f,-.75f),V(.055f,.56f,4.30f),color);
            body.Box(V(side*1.016f,1.09f,-.75f),V(.022f,.095f,4.28f),accent);
            body.Box(V(side*.95f,2.06f,-.72f),V(.09f,.14f,4.45f),color);
            for(float z=-2.63f;z<1.35f;z+=.78f)trim.Tube(V(side*.95f,1.30f,z),V(side*.95f,2.04f,z),.034f,Steel,-1,7);
            trim.Tube(V(side*1.015f,1.41f,-2.67f),V(side*1.015f,1.41f,1.1f),.021f,Steel,-1,6);
            body.Box(V(side*.66f,.90f,-.90f),V(.48f,.20f,3.5f),new Color(.24f,.17f,.13f));body.Box(V(side*.83f,1.18f,-.9f),V(.13f,.50f,3.5f),new Color(.27f,.19f,.14f));
            trim.Box(V(side*1.02f,.63f,.65f),V(.18f,.09f,1.0f),Steel);
            trim.Wheel(V(side*.96f,.47f,1.75f),.47f,.25f);trim.Wheel(V(side*.96f,.47f,-1.85f),.47f,.25f);
            trim.Tube(V(side*.87f,1.68f,1.30f),V(side*1.13f,1.70f,1.60f),.022f,Steel);trim.Box(V(side*1.13f,1.82f,1.60f),V(.10f,.24f,.12f),Rubber);
            for(float z=-2.4f;z<.8f;z+=.8f)body.Box(V(side*1.018f,.97f,z),V(.023f,.18f,.35f),Paint[(variant+(int)(z+5))%4]);
        }
        // Open passenger rear with steps, seats, a split windscreen and chrome grille.
        body.Box(V(0,2.16f,-.68f),V(2.05f,.14f,4.8f),color);body.Box(V(0,2.01f,1.30f),V(1.90f,.18f,.08f),accent);
        trim.Box(V(0,1.68f,1.30f),V(1.72f,.54f,.027f),Glass);trim.Tube(V(0,1.40f,1.325f),V(0,1.96f,1.325f),.021f,Steel);
        foreach(int side in new[]{-1,1})trim.Tube(V(side*.87f,1.35f,1.32f),V(side*.87f,2.01f,1.32f),.037f,Steel);
        trim.Box(V(0,.75f,2.78f),V(1.98f,.14f,.20f),Steel);trim.Box(V(0,.98f,2.73f),V(.78f,.46f,.025f),Rubber);
        for(float x=-.34f;x<.37f;x+=.095f)trim.Box(V(x,1.0f,2.75f),V(.04f,.40f,.04f),Steel);
        foreach(int side in new[]{-1,1}){trim.Tube(V(side*.63f,1.07f,2.70f),V(side*.63f,1.07f,2.77f),.17f,Steel,-1,16);trim.Tube(V(side*.63f,1.07f,2.77f),V(side*.63f,1.07f,2.79f),.125f,new Color(.94f,.91f,.65f),-1,16);trim.Box(V(side*.72f,.94f,-2.98f),V(.16f,.17f,.04f),new Color(.68f,.07f,.045f));}
        trim.Box(V(0,.34f,-3.07f),V(1.1f,.10f,.34f),Steel);trim.Box(V(0,.57f,-2.95f),V(1.1f,.10f,.28f),Steel);
        for(float z=-2.6f;z<1.4f;z+=.75f)trim.Tube(V(-.87f,2.36f,z),V(.87f,2.36f,z),.022f,Steel);
        foreach(int side in new[]{-1,1}){trim.Tube(V(side*.9f,2.37f,-2.70f),V(side*.9f,2.37f,1.45f),.028f,Steel);for(float z=-2.65f;z<1.5f;z+=.65f)trim.Tube(V(side*.9f,2.20f,z),V(side*.9f,2.40f,z),.018f,Steel);}
        return SaveModel("Philippine jeepney "+variant,("paint",body),("chrome",trim));
    }
    static Model Tricycle(int variant)
    {
        var body=new Shape();var trim=new Shape();var c=Paint[variant];float bike=-.57f;
        trim.Wheel(V(bike,.31f,.90f),.31f,.13f);trim.Wheel(V(bike,.31f,-.84f),.31f,.16f);trim.Wheel(V(.74f,.31f,-.43f),.31f,.14f);
        trim.Tube(V(bike,.33f,-.85f),V(bike,.63f,.10f),.035f,Steel);trim.Tube(V(bike,.63f,.1f),V(bike,.33f,.9f),.035f,Steel);trim.Tube(V(bike,.33f,.9f),V(bike,1.05f,.63f),.03f,Steel);
        body.Ellipsoid(V(bike,.81f,.1f),V(.23f,.17f,.34f),c);trim.Box(V(bike,.83f,-.42f),V(.39f,.15f,.76f),Rubber);trim.Box(V(bike,.46f,-.12f),V(.29f,.27f,.36f),Steel);
        trim.Tube(V(bike-.34f,1.08f,.54f),V(bike+.34f,1.08f,.54f),.025f,Steel);trim.Tube(V(bike,.91f,.77f),V(bike,.91f,.84f),.115f,new Color(.90f,.90f,.74f),-1,12);
        body.Box(V(.40f,.43f,-.05f),V(.94f,.19f,1.64f),c);body.Box(V(.40f,.73f,-.81f),V(.94f,.66f,.075f),c);body.Box(V(.86f,.64f,-.1f),V(.06f,.43f,1.37f),c);
        trim.Box(V(.40f,.62f,-.36f),V(.77f,.17f,.53f),new Color(.20f,.15f,.12f));trim.Box(V(.40f,.87f,-.61f),V(.76f,.42f,.12f),new Color(.23f,.17f,.14f));
        foreach(float x in new[]{-.07f,.87f})foreach(float z in new[]{-.86f,.79f})trim.Tube(V(x,.52f,z),V(x,1.60f,z),.027f,Steel);
        trim.Box(V(.40f,1.21f,.77f),V(.87f,.59f,.025f),Glass);trim.Tube(V(-.05f,.90f,.80f),V(.86f,.90f,.80f),.028f,Steel);
        body.Box(V(.10f,1.64f,-.02f),V(1.79f,.10f,1.96f),new Color(.12f,.14f,.13f));body.Box(V(.40f,.59f,.86f),V(.94f,.36f,.04f),c);
        for(float x=.02f;x<.86f;x+=.15f)trim.Tube(V(x,.69f,-.87f),V(x,1.12f,-.87f),.011f,Steel);
        return SaveModel("Motorcycle sidecar tricycle "+variant,("paint",body),("chrome",trim));
    }
    static Model Car(int variant)
    {
        var body=new Shape();var trim=new Shape();var c=variant==3?new Color(.67f,.69f,.68f):Paint[variant];
        body.Box(V(0,.63f,0),V(1.80f,.43f,4.12f),c);body.Box(V(0,.84f,1.31f),V(1.77f,.11f,1.38f),c);body.Box(V(0,.85f,-1.63f),V(1.77f,.13f,.88f),c);
        var a=V(-.86f,.90f,1.01f);var b=V(.86f,.90f,1.01f);var d=V(-.69f,1.53f,.49f);var e=V(.69f,1.53f,.49f);trim.Q(a,d,e,b,Glass);
        trim.Q(V(-.86f,.90f,-1.19f),V(.86f,.90f,-1.19f),V(.69f,1.52f,-.79f),V(-.69f,1.52f,-.79f),Glass);
        body.Box(V(0,1.56f,-.15f),V(1.45f,.09f,1.35f),c);
        foreach(int side in new[]{-1,1}){trim.Q(V(side*.866f,.90f,-1.15f),V(side*.866f,.90f,.95f),V(side*.70f,1.51f,.49f),V(side*.70f,1.51f,-.77f),Glass);body.Tube(V(side*.86f,.91f,-.10f),V(side*.70f,1.54f,-.10f),.045f,c);body.Box(V(side*.909f,.88f,-.45f),V(.015f,.06f,.16f),Steel);trim.Wheel(V(side*.89f,.34f,1.27f),.34f,.19f);trim.Wheel(V(side*.89f,.34f,-1.30f),.34f,.19f);trim.Box(V(side*.69f,.74f,2.076f),V(.37f,.18f,.025f),new Color(.92f,.91f,.77f));trim.Box(V(side*.68f,.73f,-2.08f),V(.32f,.15f,.025f),new Color(.62f,.07f,.045f));body.Box(V(side*.97f,1.03f,.66f),V(.18f,.11f,.23f),c);}
        trim.Box(V(0,.42f,2.10f),V(1.55f,.10f,.07f),Rubber);trim.Box(V(0,.67f,2.076f),V(.58f,.20f,.026f),Rubber);trim.Box(V(0,.58f,-2.085f),V(.34f,.11f,.026f),new Color(.86f,.85f,.73f));
        return SaveModel("Parked passenger car "+variant,("paint",body),("chrome",trim));
    }
    static Model Store(int variant)
    {
        var shell=new Shape();var stock=new Shape();var c=Paint[variant];var rng=new System.Random(932+variant);
        shell.Box(V(0,.075f,0),V(2.50f,.15f,1.65f),new Color(.53f,.51f,.44f));shell.Box(V(0,1.20f,-.72f),V(2.45f,2.2f,.08f),new Color(.48f,.45f,.33f));
        foreach(int side in new[]{-1,1}){shell.Box(V(side*1.17f,1.13f,0),V(.09f,2.16f,1.48f),c);shell.Tube(V(side*1.18f,.12f,.73f),V(side*1.18f,2.26f,.73f),.047f,Wood);}
        shell.Box(V(0,.73f,.69f),V(2.3f,.99f,.07f),c);shell.Box(V(0,1.25f,.64f),V(2.40f,.09f,.30f),new Color(.58f,.49f,.34f));shell.Box(V(0,2.23f,.70f),V(2.48f,.36f,.10f),c*.72f);
        shell.Box(V(0,2.47f,.01f),V(2.64f,.07f,1.94f),new Color(.58f,.33f,.24f),Quaternion.Euler(7,0,0));
        for(float x=-1.25f;x<1.3f;x+=.15f)shell.Tube(V(x,2.61f,-.90f),V(x,2.36f,.93f),.014f,new Color(.42f,.32f,.26f),-1,5);
        for(float y=.60f;y<1.96f;y+=.43f){shell.Box(V(0,y,-.54f),V(2.13f,.04f,.32f),Wood);for(int j=0;j<12;j++){float x=-.98f+j*.175f;stock.Box(V(x,y+.115f,-.51f),V(.115f,.21f,.105f),Paint[(j+variant)%4]*R(rng,.85f,1.15f));stock.Box(V(x,y+.13f,-.447f),V(.088f,.035f,.008f),new Color(.9f,.86f,.65f));}}
        for(float x=-1.10f;x<1.11f;x+=.16f)shell.Tube(V(x,1.30f,.72f),V(x,2.03f,.72f),.012f,new Color(.26f,.30f,.28f),-1,5);
        // Customer opening remains open at the right side of the grille.
        for(int j=0;j<5;j++){float x=-.85f+j*.29f;for(int row=0;row<3;row++)stock.Box(V(x,2.09f-row*.12f,.765f),V(.19f,.10f,.025f),Petals[(row+j+variant)%4]);}
        for(int j=0;j<5;j++)stock.Tube(V(-.85f+j*.23f,1.31f,.51f),V(-.85f+j*.23f,1.57f,.51f),.045f,new Color(.12f,.36f,.20f),.032f,8);
        shell.Box(V(.70f,.35f,.25f),V(.70f,.49f,.55f),new Color(.85f,.86f,.78f));shell.Box(V(.70f,.61f,.25f),V(.73f,.05f,.57f),new Color(.16f,.40f,.54f));
        return SaveModel("Stocked sari-sari storefront "+variant,("masonry",shell),("detail",stock));
    }
    static Model Drain(int variant)
    {
        var concrete=new Shape();var bars=new Shape();concrete.Box(V(0,.021f,0),V(.53f,.04f,3.55f),new Color(.12f,.16f,.13f));
        foreach(int side in new[]{-1,1})concrete.Box(V(side*.237f,.115f,0),V(.08f,.23f,3.55f),new Color(.43f,.43f,.38f));
        for(float z=-1.70f;z<1.75f;z+=.15f)if(variant==0||Mathf.Abs(z)>1)bars.Box(V(0,.231f,z),V(.45f,.035f,.045f),new Color(.22f,.25f,.24f));
        return SaveModel("Roadside grated drainage "+variant,("masonry",concrete),("chrome",bars));
    }
    static Model Streetlight(int variant)
    {
        var pole=new Shape();pole.Box(V(0,.14f,0),V(.46f,.28f,.46f),new Color(.46f,.46f,.41f));pole.Tube(V(0,.14f,0),V(0,7.9f,0),.095f,Steel,.055f,12);pole.Tube(V(0,7.6f,0),V(0,8.12f,.65f),.045f,Steel);pole.Tube(V(0,8.12f,.65f),V(0,8.12f,1.85f),.04f,Steel);
        pole.Box(V(0,8.08f,1.80f),V(.34f,.12f,.72f),new Color(.31f,.35f,.36f));pole.Box(V(0,8.011f,1.80f),V(.25f,.018f,.56f),new Color(.94f,.89f,.67f));
        for(float x=-.13f;x<=.13f;x+=.26f)for(float z=-.13f;z<=.13f;z+=.26f)pole.Tube(V(x,.26f,z),V(x,.31f,z),.021f,Steel,-1,6);
        return SaveModel("Roadside LED mast "+variant,("chrome",pole));
    }
    static Model Debris(int variant)
    {
        var s=new Shape();var rng=new System.Random(48+variant);
        if(variant==1)s.Box(V(0,.055f,0),V(.52f,.08f,.13f),Wood,Quaternion.Euler(0,21,4));
        else if(variant==2){s.Ellipsoid(V(0,.03f,0),V(.17f,.033f,.10f),new Color(.39f,.47f,.41f),3,7);s.Box(V(.10f,.025f,.09f),V(.13f,.012f,.17f),new Color(.65f,.63f,.52f));}
        else for(int i=0;i<5;i++)s.Ellipsoid(V(R(rng,-.17f,.17f),.045f,R(rng,-.17f,.17f)),V(R(rng,.04f,.13f),R(rng,.04f,.095f),R(rng,.05f,.13f)),new Color(.47f,.45f,.39f)*R(rng,.75f,1.2f),3,6);
        return SaveModel("Tidal rubble and weathered litter "+variant,("masonry",s));
    }
    static Model Mango(int variant)
    {
        var m=new Model();var tree=BanagoEnvironmentModels.Tree(0,variant,true,new Color(.17f,.32f,.105f));var vs=tree.vertices;for(int i=0;i<vs.Length;i++)vs[i]=Vector3.Scale(vs[i],V(2.85f,7.2f,2.85f));tree.vertices=vs;tree.RecalculateNormals();tree.RecalculateBounds();tree.name="Mango-like spreading crown "+variant;AssetDatabase.CreateAsset(tree,Folder+"/Mango canopy "+variant+".asset");m.parts["foliage"]=tree;
        var fruit=new Shape();var rng=new System.Random(301+variant);for(int i=0;i<23;i++){float a=R(rng,0,Mathf.PI*2),r=R(rng,1.3f,2.2f);var q=V(Mathf.Cos(a)*r,R(rng,4.6f,5.8f),Mathf.Sin(a)*r);fruit.Tube(q+Vector3.up*.12f,q,.003f,Wood,-1,4);fruit.Ellipsoid(q-V(0,.065f,0),V(.060f,.10f,.045f),i%4==0?new Color(.79f,.60f,.12f):new Color(.32f,.47f,.12f),4,8);}var fm=fruit.Finish("Small mango fruit clusters "+variant);AssetDatabase.AddObjectToAsset(fm,tree);m.parts["detail"]=fm;return m;
    }
    static Model GetModel(string kind,int variant)
    {
        string key=kind+variant;if(models.TryGetValue(key,out var model))return model;
        switch(kind){case "frontage":model=Frontage(variant);break;case "flowers":model=Flowers(variant);break;case "jeepney":model=Jeepney(variant);break;case "tricycle":model=Tricycle(variant);break;case "car":model=Car(variant);break;case "store":model=Store(variant);break;case "mango":model=Mango(variant);break;case "drainage":model=Drain(variant);break;case "streetlight":model=Streetlight(variant);break;case "shoreDebris":model=Debris(variant);break;default:throw new Exception(kind);}
        models[key]=model;return model;
    }
    static Material Material(string kind)
    {
        var m=new Material(Shader.Find(kind=="masonry"?"StormSim/Weathered Building Surface":"StormSim/Environment Lit")){name="Neighborhood "+kind,enableInstancing=true};m.SetColor("_BaseColor",Color.white);m.SetFloat("_VertexColor",1);m.SetFloat("_Cull",0);m.SetFloat("_Smoothness",kind=="chrome"?.55f:kind=="paint"?.29f:.07f);m.SetFloat("_Metallic",kind=="chrome"?.48f:0);if(m.HasProperty("_Weathering"))m.SetFloat("_Weathering",.22f);AssetDatabase.CreateAsset(m,Folder+"/"+kind+".mat");return m;
    }
    static void Label(Transform parent,string message,Vector3 position,float size,Material lettering)
    {
        var go=new GameObject(message);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,180,0);var t=go.AddComponent<TextMesh>();t.text=message;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.97f,.93f,.73f);go.GetComponent<MeshRenderer>().sharedMaterial=lettering;go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
    }
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first");if(GameObject.Find(RootName))throw new Exception("Additions already exist; refusing duplicate build");
        var scene=EditorSceneManager.GetActiveScene();
        // Capture the current in-memory scene, including any unsaved user work.
        string recovery="RoadRepairBackup/MAIN_GAME_before_neighborhood_live_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        if(!EditorSceneManager.SaveScene(scene,recovery,true))throw new Exception("Could not save recovery scene");
        var plan=JObject.Parse(File.ReadAllText("RoadRepairBackup/neighborhood-detail-plan.json"));
        Folder="Assets/StormSim/Generated/NeighborhoodLife_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();models.Clear();materials=new Dictionary<string,Material>();foreach(string kind in new[]{"masonry","paint","chrome","detail","foliage"})materials[kind]=Material(kind);
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");font.RequestCharactersInTexture("SARI-SARI TINDAHAN BANAGO BACOLOD LOAD ICE COLD DRINKS BIGAS",64);var lettering=new Material(Shader.Find("StormSim/World Sign Text")){name="Neighborhood sign lettering"};lettering.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(lettering,Folder+"/Sign lettering.mat");
        var root=new GameObject(RootName);root.transform.SetParent(GameObject.Find("CesiumGeoreference").transform,false);Undo.RegisterCreatedObjectUndo(root,"Add Banago neighborhood details");var signs=root.AddComponent<BanagoWorldSigns>();signs.lettering=lettering;
        var groups=new Dictionary<string,Transform>();var lightList=new List<Light>();int count=0;var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.name.StartsWith("Measured terrain")).ToArray();
        foreach(var item in plan["items"])
        {
            string kind=(string)item["kind"];int variant=(int)item["variant"];if(!groups.TryGetValue(kind,out var group)){var g=new GameObject(kind);g.transform.SetParent(root.transform,false);group=g.transform;groups[kind]=group;}
            var go=new GameObject(kind+" "+(++count).ToString("000")+" - "+(string)item["host"]);go.transform.SetParent(group,false);go.transform.localPosition=new Vector3((float)item["position"][0],(float)item["position"][1],(float)item["position"][2]);go.transform.localRotation=Quaternion.Euler(0,(float)item["yaw"],0);
            if(kind!="shoreDebris"){var p=go.transform.position;foreach(var c in ground){RaycastHit hit;if(c.Raycast(new Ray(p+Vector3.up*60,Vector3.down),out hit,120)){p.y=hit.point.y+.015f;go.transform.position=p;break;}}}
            var model=GetModel(kind,variant);var renderers=new List<Renderer>();foreach(var part in model.parts){var p=new GameObject(part.Key);p.transform.SetParent(go.transform,false);p.AddComponent<MeshFilter>().sharedMesh=part.Value;var r=p.AddComponent<MeshRenderer>();r.sharedMaterial=materials[part.Key];r.receiveShadows=true;renderers.Add(r);if(kind=="frontage"||kind=="drainage"||kind=="store")p.AddComponent<MeshCollider>().sharedMesh=part.Value;}
            if(kind=="car"||kind=="jeepney"||kind=="tricycle"){var collider=go.AddComponent<BoxCollider>();collider.center=V(0,.8f,0);collider.size=V((float)item["width"]-.15f,1.4f,(float)item["depth"]-.2f);}
            if(kind=="mango"){var c=go.AddComponent<CapsuleCollider>();c.center=V(0,1.7f,0);c.height=3.4f;c.radius=.23f;}
            if(kind=="streetlight"){var c=go.AddComponent<CapsuleCollider>();c.center=V(0,3.9f,0);c.height=7.8f;c.radius=.13f;var lamp=new GameObject("Warm LED - automatic dusk light");lamp.transform.SetParent(go.transform,false);lamp.transform.localPosition=V(0,7.96f,1.8f);lamp.transform.localRotation=Quaternion.Euler(90,0,0);var light=lamp.AddComponent<Light>();light.type=LightType.Spot;light.range=15;light.spotAngle=105;light.intensity=3.0f;light.color=new Color(1,.85f,.62f);light.shadows=LightShadows.None;light.enabled=false;lightList.Add(light);}
            if(kind=="store"){Label(go.transform,"SARI-SARI",V(0,2.23f,.762f),.045f,lettering);Label(go.transform,"LOAD  |  BIGAS  |  COLD DRINKS",V(0,.75f,.737f),.021f,lettering);}
            if(kind=="jeepney"){Label(go.transform,"BANAGO - BACOLOD",V(0,2.02f,1.355f),.022f,lettering);Label(go.transform,"BANAGO",V(0,.95f,2.80f),.026f,lettering);}
            if(kind=="tricycle")Label(go.transform,(100+count).ToString(),V(.40f,.60f,.90f),.040f,lettering);
            var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(kind=="flowers"?.012f:.0025f,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        }
        var clock=root.AddComponent<BanagoNeighborhoodStreetlights>();clock.sun=GameObject.Find("Directional Light").GetComponent<Light>();clock.lamps=lightList.ToArray();clock.Apply();
        var sd=plan["shoreMesh"];if(sd["vertices"].Count()>0){var m=new Mesh{name="Additive dirty sand shoreline",indexFormat=IndexFormat.UInt32};m.vertices=sd["vertices"].Select(t=>V((float)t[0],(float)t[1],(float)t[2])).ToArray();m.normals=sd["normals"].Select(t=>V((float)t[0],(float)t[1],(float)t[2])).ToArray();m.colors=sd["colors"].Select(t=>new Color((float)t[0],(float)t[1],(float)t[2])).ToArray();m.uv=sd["uv"].Select(t=>new Vector2((float)t[0],(float)t[1])).ToArray();m.triangles=sd["triangles"].Values<int>().ToArray();m.RecalculateBounds();m.RecalculateTangents();AssetDatabase.CreateAsset(m,Folder+"/Wider dirty shoreline.asset");var shore=new GameObject("Wider rough sand - existing water retained");shore.transform.SetParent(root.transform,false);shore.AddComponent<MeshFilter>().sharedMesh=m;shore.AddComponent<MeshRenderer>().sharedMaterial=materials["masonry"];shore.AddComponent<MeshCollider>().sharedMesh=m;}
        File.WriteAllText(Folder+"/PlacementPlan.json",plan.ToString());AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Status="Built "+count+" additive details; awaiting live validation before save";Selection.activeGameObject=root;
    }
}
