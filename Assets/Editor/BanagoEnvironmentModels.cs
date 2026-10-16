using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public static class BanagoEnvironmentModels
{
    public sealed class Parts
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        public readonly List<Vector3> normals=new List<Vector3>();
        public readonly List<Color32> colors=new List<Color32>();
        public readonly List<int> triangles=new List<int>();
        public void Face(Vector3 a,Vector3 b,Vector3 c,Color color,Vector3 expected, bool smooth=false,Vector3 origin=default)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),expected)<0){var t=b;b=c;c=t;}
            var n=Vector3.Cross(b-a,c-a).normalized;int start=vertices.Count;
            foreach(var v in new[]{a,b,c}){vertices.Add(v);colors.Add(color);normals.Add(smooth?(v-origin).normalized:n);}
            triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
        }
        public Mesh Mesh(string name)
        {
            var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(vertices);m.SetNormals(normals);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateBounds();return m;
        }
        public void Normalize(float radial=1,float height=1)
        {
            float maxR=vertices.Max(v=>new Vector2(v.x,v.z).magnitude),maxY=vertices.Max(v=>v.y);
            var scale=new Vector3(radial*.975f/maxR,height/maxY,radial*.975f/maxR);
            for(int i=0;i<vertices.Count;i++){vertices[i]=Vector3.Scale(vertices[i],scale);normals[i]=new Vector3(normals[i].x/scale.x,normals[i].y/scale.y,normals[i].z/scale.z).normalized;}
        }
    }
    static float Rand(System.Random r,float a,float b)=>a+(b-a)*(float)r.NextDouble();
    static Vector3 Direction(System.Random rng)
    {
        float z=Rand(rng,-1,1),a=Rand(rng,0,Mathf.PI*2),s=Mathf.Sqrt(1-z*z);return new Vector3(s*Mathf.Cos(a),z,s*Mathf.Sin(a));
    }
    public static Mesh Tree(int kind,int variant,bool detailed,Color leaf)
    {
        var p=new Parts();var rng=new System.Random(531+kind*307+variant*717);var bark=new Color(.30f,.245f,.17f);
        if(kind==2) Palm(p,rng,detailed,leaf,bark);
        else
        {
            var top=new Vector3(Rand(rng,-.22f,.22f),4.9f,Rand(rng,-.2f,.2f));
            Cylinder(p,new Vector3(0,-.15f,0),top,.19f,.065f,bark);
            int count=kind==0?9:7;
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996f+variant*.61f;
                var c=new Vector3(Mathf.Cos(a)*(i<6?1.3f:.45f), i<6?Rand(rng,4.7f,5.9f):Rand(rng,6.1f,6.6f),Mathf.Sin(a)*(i<6?1.3f:.45f));
                var size=kind==0?new Vector3(1.5f,Rand(rng,1.1f,1.5f),1.45f):new Vector3(1.15f,Rand(rng,1.4f,1.9f),1.3f);
                Cylinder(p,new Vector3(0,3.5f,0),c,.09f,.023f,bark*.9f);
                Crown(p,c,size*(detailed?.66f:1f),leaf*(detailed?.85f:.93f),rng,detailed?7:5);
                if(detailed) for(int j=0;j<350;j++)
                {
                    var normal=Direction(rng);var point=c+Vector3.Scale(normal*Rand(rng,.65f,1.06f),size);
                    var axis=Vector3.Cross(normal,Vector3.up).normalized;if(axis.sqrMagnitude<.01f)axis=Vector3.right;
                    var length=(axis*.65f+Vector3.up*Rand(rng,-.7f,.5f)).normalized*Rand(rng,.24f,.38f);
                    Leaf(p,point-length*.3f,point+length,normal, Rand(rng,.08f,.13f),leaf*Rand(rng,.87f,1.20f));
                }
            }
        }
        p.Normalize();return p.Mesh((kind==2?"Palm fronds":kind==0?"Spreading broadleaf":"Oval broadleaf")+" "+variant+(detailed?" detail":" distant"));
    }
    static void Palm(Parts p,System.Random rng,bool detail,Color leaf,Color bark)
    {
        Vector3 start=new Vector3(0,-.12f,0),crown=new Vector3(.38f,7.9f,.2f);
        for(int i=0;i<10;i++)
        {
            Vector3 a=Vector3.Lerp(start,crown,i/10f),b=Vector3.Lerp(start,crown,(i+1)/10f);
            Cylinder(p,a,b,Mathf.Lerp(.17f,.11f,i/10f),Mathf.Lerp(.17f,.11f,(i+1)/10f),bark*(i%2==0?1:.83f));
        }
        for(int f=0;f<12;f++)
        {
            float angle=f*Mathf.PI*2/12+Rand(rng,-.12f,.12f);
            var outwards=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=new Vector3(-outwards.z,0,outwards.x);
            float length=Rand(rng,2.5f,3.2f);
            Func<float,Vector3> curve=t=>crown+outwards*(t*length)+Vector3.up*(.9f*Mathf.Sin(t*Mathf.PI)-1.55f*t*t);
            for(int s=0;s<12;s++)
            {
                float t=s/12f,t2=(s+1)/12f;var a=curve(t);var b=curve(t2);
                Cylinder(p,a,b,.019f,.013f,leaf*.72f);
                if(detail || s%2==0) foreach(int sign in new[]{-1,1})
                {
                    float width=(.14f+.75f*Mathf.Sin(Mathf.PI*t2))*(detail?1:1.1f);
                    var tip=b+side*(width*sign)+outwards*.20f-Vector3.up*(.18f+width*.2f);
                    Leaf(p,a,tip,Vector3.up,detail?.055f:.10f,leaf*Rand(rng,.86f,1.12f));
                }
            }
        }
    }
    public static Mesh Shrub(int variant,bool detail,Color color)
    {
        var p=new Parts();var rng=new System.Random(144+variant);
        for(int i=0;i<4;i++)
        {
            float a=i*2.4f;var c=new Vector3(Mathf.Cos(a)*.3f,.52f,Mathf.Sin(a)*.3f);var scale=new Vector3(.57f,.48f,.55f);
            Crown(p,c,scale,color*(detail?.72f:1),rng,5);
            if(detail)for(int j=0;j<110;j++)
            {
                var n=Direction(rng);var v=c+Vector3.Scale(n,scale);Leaf(p,v,v+new Vector3(Rand(rng,-.09f,.09f),.045f,Rand(rng,-.09f,.09f)),n,.025f,color*Rand(rng,.9f,1.13f));
            }
        }
        p.Normalize();return p.Mesh("Leafy shrub "+variant+(detail?" detail":" distant"));
    }
    public static void Grass(Parts p,Vector3 center,float h,float yaw,Color color,int seed,bool distant)
    {
        var rng=new System.Random(seed);int count=distant?1:6;
        for(int i=0;i<count;i++)
        {
            float a=yaw*Mathf.Deg2Rad+i*2.39996f;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            var side=new Vector3(-direction.z,0,direction.x);var bottom=center+direction*Rand(rng,.03f,.17f);
            var mid=bottom+Vector3.up*h*.55f+direction*.03f;var tip=bottom+Vector3.up*h+direction*.13f;
            float width=Rand(rng,.018f,.035f);
            var c=color*Rand(rng,.9f,1.1f);
            p.Face(bottom-side*width,bottom+side*width,mid+side*width*.5f,c,side);
            p.Face(bottom-side*width,mid+side*width*.5f,tip,c*1.04f,side);
        }
    }
    public static Mesh Flowers(int variant)
    {
        var p=new Parts();var rng=new System.Random(983+variant);var stem=new Color(.18f,.32f,.11f);
        Color flower=variant==0?new Color(.85f,.79f,.60f):variant==1?new Color(.69f,.39f,.43f):new Color(.89f,.86f,.72f);
        for(int i=0;i<7;i++)
        {
            float a=i*2.4f;var bottom=new Vector3(Mathf.Cos(a)*Rand(rng,.1f,.3f),0,Mathf.Sin(a)*Rand(rng,.1f,.3f));
            var top=bottom+Vector3.up*Rand(rng,.20f,.34f);
            Cylinder(p,bottom,top,.007f,.004f,stem);
            Leaf(p,bottom+Vector3.up*.08f,bottom+new Vector3(.09f,.15f,.02f),Vector3.up,.025f,stem);
            for(int k=0;k<5;k++)
            {
                float theta=k*Mathf.PI*2/5;var d=new Vector3(Mathf.Cos(theta),.3f,Mathf.Sin(theta));
                Leaf(p,top,top+d*.045f,Vector3.up,.019f,flower);
            }
        }
        p.Normalize();return p.Mesh("Sparse garden flowers "+variant);
    }
    public static Mesh Clutter(int variant)
    {
        var p=new Parts();var rng=new System.Random(900+variant);
        if(variant==0)Crown(p,new Vector3(0,.06f,0),new Vector3(.22f,.10f,.18f),new Color(.34f,.32f,.27f),rng,4);
        else for(int i=0;i<8;i++)
        {
            float a=Rand(rng,0,Mathf.PI*2);var start=new Vector3(Rand(rng,-.14f,.14f),.012f,Rand(rng,-.14f,.14f));
            Leaf(p,start,start+new Vector3(Mathf.Cos(a)*.13f,.007f,Mathf.Sin(a)*.13f),Vector3.up,.035f,new Color(.37f,.29f,.16f)*Rand(rng,.8f,1.2f));
        }
        p.Normalize();return p.Mesh("Ground detail "+variant);
    }
    static void Leaf(Parts p,Vector3 start,Vector3 tip,Vector3 normal,float width,Color color)
    {
        var side=Vector3.Cross(tip-start,normal).normalized*width;
        if(side.sqrMagnitude<.00001f)side=Vector3.right*width;
        var mid=Vector3.Lerp(start,tip,.48f)+normal*width*.15f;
        p.Face(start,mid+side,tip,color,normal);p.Face(start,tip,mid-side,color*.94f,normal);
    }
    static void Crown(Parts p,Vector3 center,Vector3 scale,Color color,System.Random rng,int rings)
    {
        int seg=rings*2;var grid=new Vector3[rings+1,seg];
        for(int y=0;y<=rings;y++)for(int x=0;x<seg;x++)
        {
            float t=Mathf.PI*y/rings,a=Mathf.PI*2*x/seg;var v=new Vector3(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a));
            grid[y,x]=center+Vector3.Scale(v,scale)*Rand(rng,.97f,1.03f);
        }
        for(int y=0;y<rings;y++)for(int x=0;x<seg;x++)
        {
            int n=(x+1)%seg;var a=grid[y,x];var b=grid[y+1,x];var c=grid[y+1,n];var d=grid[y,n];
            Color tint=color*Rand(rng,.94f,1.06f);
            p.Face(a,b,c,tint,(a+b+c)/3-center,true,center);p.Face(a,c,d,tint,(a+c+d)/3-center,true,center);
        }
    }
    static void Cylinder(Parts p,Vector3 start,Vector3 end,float bottom,float top,Color color)
    {
        var axis=(end-start).normalized;var right=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;var front=Vector3.Cross(axis,right).normalized;
        for(int i=0;i<7;i++)
        {
            float a=i*Mathf.PI*2/7,b=(i+1)*Mathf.PI*2/7;var da=right*Mathf.Cos(a)+front*Mathf.Sin(a);var db=right*Mathf.Cos(b)+front*Mathf.Sin(b);
            var v1=start+da*bottom;var v2=start+db*bottom;var v3=end+db*top;var v4=end+da*top;
            p.Face(v1,v2,v3,color,da+db);p.Face(v1,v3,v4,color,da+db);
        }
    }
}
