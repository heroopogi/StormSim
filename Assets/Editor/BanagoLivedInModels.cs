using System;
using System.Linq;
using UnityEngine;

/// <summary>Small reusable models, with geometry shared across the neighborhood.</summary>
public static class BanagoLivedInModels
{
    sealed class Shape
    {
        public BanagoEnvironmentModels.Parts p = new BanagoEnvironmentModels.Parts();
        public void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        { var n = Vector3.Cross(b-a,c-a).normalized; p.Face(a,b,c,col,n);p.Face(a,c,d,col,n); }
        public void Box(Vector3 c, Vector3 s, Color col)
        {
            var v=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)}.Select(x=>c+Vector3.Scale(x,s*.5f)).ToArray();
            foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}})Q(v[f[0]],v[f[1]],v[f[2]],v[f[3]],col);
        }
        public void Tube(Vector3 a,Vector3 b,float r,Color c,float top=-1,int n=16)
        {
            if(top<0)top=r;var axis=(b-a).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
            for(int i=0;i<n;i++){float t=i*Mathf.PI*2/n,t2=(i+1)*Mathf.PI*2/n;var x=u*Mathf.Cos(t)+v*Mathf.Sin(t);var y=u*Mathf.Cos(t2)+v*Mathf.Sin(t2);Q(a+x*r,a+y*r,b+y*top,b+x*top,c);p.Face(a,a+y*r,a+x*r,c,-axis);p.Face(b,b+x*top,b+y*top,c,axis);}
        }
        public Mesh Finish(string name)
        {var mesh=p.Mesh(name);mesh.uv=p.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();mesh.RecalculateTangents();return mesh;}
    }
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static readonly Color Steel=new Color(.56f,.59f,.59f),Wood=new Color(.39f,.29f,.17f),Dark=new Color(.10f,.12f,.12f);
    public static Mesh Tank(int variant)
    {
        var s=new Shape();var color=variant==0?new Color(.18f,.43f,.57f):new Color(.31f,.37f,.32f);
        s.Box(V(0,.035f,0),V(1.13f,.07f,1.13f),new Color(.47f,.47f,.42f));
        foreach(float x in new[]{-.45f,.45f})foreach(float z in new[]{-.45f,.45f})s.Tube(V(x,-.52f,z),V(x,0,z),.055f,Steel,-1,8);
        s.Tube(V(0,.08f,0),V(0,1.22f,0),.44f,color,.44f,32);
        foreach(float y in new[]{.18f,.33f,.75f,1.08f})s.Tube(V(0,y,0),V(0,y+.035f,0),.458f,color*.88f,-1,32);
        s.Tube(V(0,1.22f,0),V(0,1.36f,0),.44f,color,.28f,32);s.Tube(V(0,1.36f,0),V(0,1.41f,0),.19f,color*.72f,-1,24);
        s.Tube(V(.44f,.23f,0),V(.63f,.23f,0),.025f,new Color(.69f,.72f,.69f),-1,10);
        s.Tube(V(.63f,.23f,0),V(.63f,-.55f,0),.025f,new Color(.69f,.72f,.69f),-1,10);
        s.Box(V(.56f,.27f,0),V(.06f,.015f,.12f),new Color(.66f,.15f,.12f));
        return s.Finish("Ribbed PVC water tank "+variant);
    }
    public static Mesh Dish(int variant)
    {
        var s=new Shape();var c=variant==0?new Color(.71f,.72f,.68f):new Color(.41f,.45f,.45f);
        s.Box(V(0,-.035f,0),V(.36f,.06f,.35f),Steel);s.Tube(V(0,-.35f,0),V(0,.39f,0),.035f,Steel,-1,12);
        var rot=Quaternion.Euler(48,0,0);var center=V(0,.55f,0);
        Func<float,float,Vector3> at=(r,a)=>center+rot*V(r*Mathf.Cos(a),r*Mathf.Sin(a),r*r*.45f);
        for(int row=0;row<5;row++)for(int j=0;j<28;j++){float r=row*.074f,r2=(row+1)*.074f,a=j*Mathf.PI*2/28,b=(j+1)*Mathf.PI*2/28;s.Q(at(r,a),at(r2,a),at(r2,b),at(r,b),c);}
        for(int j=0;j<28;j++)s.Tube(at(.37f,j*Mathf.PI*2/28),at(.37f,(j+1)*Mathf.PI*2/28),.009f,Steel,-1,5);
        var receiver=center+rot*V(0,0,.29f);s.Tube(center+rot*V(0,-.36f,.06f),receiver,.016f,Steel,-1,6);s.Box(receiver,V(.07f,.06f,.10f),Dark);
        return s.Finish("Rooftop satellite dish "+variant);
    }
    public static Mesh Laundry(int variant)
    {
        var s=new Shape();var colors=new[]{new Color(.70f,.31f,.31f),new Color(.72f,.75f,.70f),new Color(.24f,.45f,.63f),new Color(.70f,.58f,.29f),new Color(.43f,.34f,.48f)};
        foreach(float x in new[]{-1.12f,1.12f})s.Tube(V(x,-.10f,0),V(x,1.88f,0),.027f,Wood,-1,8);
        for(int k=0;k<16;k++){float x=-1.12f+k*2.24f/16,x2=x+2.24f/16;s.Tube(V(x,1.84f-.075f*(1-x*x/1.26f),0),V(x2,1.84f-.075f*(1-x2*x2/1.26f),0),.006f,Dark,-1,5);}
        for(int k=0;k<4;k++)
        {
            float x=-.87f+k*.51f,y=1.79f;var col=colors[(k+variant)%colors.Length];
            for(int j=0;j<7;j++){float a=x+j*.43f/7,b=x+(j+1)*.43f/7;float za=Mathf.Sin(j*1.7f+variant)*.035f,zb=Mathf.Sin((j+1)*1.7f+variant)*.035f;s.Q(V(a,y,0),V(b,y,0),V(b,y-.58f,zb),V(a,y-.58f,za),col*(.91f+(j%3)*.045f));}
            foreach(float xx in new[]{x+.04f,x+.38f})s.Box(V(xx,y+.01f,0),V(.025f,.065f,.025f),Wood);
            if(k%2==0){s.Q(V(x-.07f,y-.03f,0),V(x+.01f,y,0),V(x+.05f,y-.25f,.01f),V(x-.12f,y-.24f,0),col);s.Q(V(x+.42f,y,0),V(x+.50f,y-.03f,0),V(x+.55f,y-.24f,0),V(x+.38f,y-.25f,.01f),col);}
        }
        return s.Finish("Hanging laundry line "+variant);
    }
    public static Mesh Banana(int variant)
    {
        var s=new Shape();var rng=new System.Random(310+variant);var leaf=new Color(.29f,.42f,.13f);
        for(int stem=0;stem<3;stem++)
        {
            var baseP=V((stem-1)*.26f,0,stem==1?.16f:-.09f);float h=2.4f+stem*.43f;
            s.Tube(baseP-V(0,.10f,0),baseP+V(.13f,h,0),.115f,new Color(.47f,.49f,.25f),.06f,12);
            for(int l=0;l<7;l++)
            {
                float angle=l*2.4f+stem+variant*.5f;var dir=V(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,dir);var start=baseP+V(.13f,h,0);float len=1.25f+(float)rng.NextDouble()*.6f;
                Func<float,Vector3> mid=t=>start+dir*(len*t)+V(0,.5f*Mathf.Sin(t*Mathf.PI)-.45f*t*t,0);
                for(int j=0;j<9;j++){float a=j/9f,b=(j+1)/9f,w1=Mathf.Sin(a*Mathf.PI)*.24f,w2=Mathf.Sin(b*Mathf.PI)*.24f;var c=leaf*(.83f+(l%4)*.08f);float tear=j%3==1?.78f:1;foreach(int sign in new[]{-1,1})s.Q(mid(a),mid(b),mid(b)+side*(w2*sign*tear)-V(0,.065f,0),mid(a)+side*(w1*sign)-V(0,.065f,0),c);s.Tube(mid(a),mid(b),.009f,leaf*1.17f,-1,5);}
            }
        }
        return s.Finish("Split broad banana leaves "+variant);
    }
    public static Mesh Kubo(int variant)
    {
        var s=new Shape();var bamboo=new Color(.58f,.48f,.29f);float w=variant==0?1.12f:.97f,d=1.00f;
        foreach(float x in new[]{-w,w})foreach(float z in new[]{-d,d})s.Tube(V(x,-.2f,z),V(x,2.32f,z),.047f,bamboo,-1,10);
        s.Box(V(0,.30f,0),V(w*2,.08f,d*2),Wood);
        foreach(float z in new[]{-d,d})
            for(int j=0;j<20;j++){float x=-w+j*w*2/19;if(z==-d&&Mathf.Abs(x)<.36f)continue;s.Tube(V(x,.35f,z),V(x,1.98f,z),.024f,bamboo*(j%3==0?.85f:1),-1,6);}
        foreach(float x in new[]{-w,w})for(int j=0;j<19;j++){float z=-d+j*d*2/18;s.Tube(V(x,.35f,z),V(x,1.98f,z),.024f,bamboo*(j%4==0?.82f:1),-1,6);}
        foreach(float y in new[]{.5f,.8f,1.1f,1.4f,1.7f}){s.Tube(V(-w,y,d),V(w,y,d),.018f,Wood,-1,6);foreach(float x in new[]{-w,w})s.Tube(V(x,y,-d),V(x,y,d),.018f,Wood,-1,6);}
        for(int side=-1;side<=1;side+=2)for(int row=0;row<8;row++)
        {float x0=side*(row/8f)*(w+.25f),x1=side*((row+1)/8f)*(w+.25f),y0=2.84f-row*.12f,y1=2.84f-(row+1)*.12f;s.Q(V(x0,y0,-1.25f),V(x0,y0,1.25f),V(x1,y1,1.29f),V(x1,y1,-1.29f),new Color(.48f,.39f,.23f)*(row%2==0?1:.91f));}
        s.Box(V(0,.15f,-1.17f),V(.64f,.16f,.35f),Wood);return s.Finish("Woven bamboo and nipa shelter "+variant);
    }
}
