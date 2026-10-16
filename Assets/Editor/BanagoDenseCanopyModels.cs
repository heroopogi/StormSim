using System;
using System.Linq;
using UnityEngine;

/// <summary>Shared near/far tropical silhouettes. No existing vegetation assets are edited.</summary>
public static class BanagoDenseCanopyModels
{
    sealed class Shape
    {
        public readonly BanagoEnvironmentModels.Parts p=new BanagoEnvironmentModels.Parts();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,Vector3 normal)
        {p.Face(a,b,c,color,normal);p.Face(a,c,d,color,normal);}
        public void Tube(Vector3 a,Vector3 b,float radius,float top,Color color,int segments=8)
        {
            var axis=(b-a).normalized;var right=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var forward=Vector3.Cross(axis,right);
            for(int k=0;k<segments;k++){float a1=k*Mathf.PI*2/segments,a2=(k+1)*Mathf.PI*2/segments;var v1=right*Mathf.Cos(a1)+forward*Mathf.Sin(a1);var v2=right*Mathf.Cos(a2)+forward*Mathf.Sin(a2);Quad(a+v1*radius,a+v2*radius,b+v2*top,b+v1*top,color,v1+v2);}
        }
        public void Leaf(Vector3 a,Vector3 b,float width,Color color,Vector3 normal)
        {
            var side=Vector3.Cross(b-a,normal).normalized*width;var mid=Vector3.Lerp(a,b,.43f)+normal*width*.15f;
            p.Face(a,mid+side,b,color,normal);p.Face(a,b,mid-side,color*.94f,normal);
        }
        public Mesh Finish(string name)
        {var m=p.Mesh(name);m.uv=p.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();m.RecalculateTangents();return m;}
    }
    static float R(System.Random r,float a,float b)=>a+(b-a)*(float)r.NextDouble();
    public static Mesh Coconut(int variant,bool detail)
    {
        var s=new Shape();var rng=new System.Random(427+variant*293);float lean=.65f+variant*.19f;
        var bark=new Color(.46f,.40f,.28f);var green=new Color(.31f,.44f,.16f)*(1+variant*.025f);
        Func<float,Vector3> trunk=t=>new Vector3(Mathf.Sin(t*Mathf.PI)*.18f,12f*t,lean*t*t);
        int steps=detail?32:12;
        for(int i=0;i<steps;i++){float a=i/(float)steps,b=(i+1f)/steps;float radius=Mathf.Lerp(.19f,.105f,a);s.Tube(trunk(a),trunk(b),radius,Mathf.Lerp(.19f,.105f,b),bark*(i%3==0?.86f:1),detail?10:7);if(detail)s.Tube(trunk(a),trunk(a+.005f),radius+.008f,radius+.005f,bark*.64f,10);}
        var crown=trunk(1);int leaves=detail?19:16;
        for(int f=0;f<leaves;f++)
        {
            float angle=f*2.39996f+R(rng,-.17f,.17f);var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,dir);
            float length=R(rng,3.5f,4.65f),lift=f%4==0?2.0f:R(rng,.6f,1.4f),droop=R(rng,1.2f,2.4f);
            Func<float,Vector3> curve=t=>crown+dir*length*t+Vector3.up*(lift*Mathf.Sin(t*Mathf.PI)-droop*t*t);
            int pieces=detail?19:10;var col=green*R(rng,.78f,1.14f);if(f%9==0)col=Color.Lerp(col,new Color(.48f,.44f,.20f),.34f);
            for(int j=0;j<pieces;j++)
            {
                float a=j/(float)pieces,b=(j+1f)/pieces;var p1=curve(a);var p2=curve(b);float width=(.13f+.88f*Mathf.Sin(b*Mathf.PI))*(1-b*.25f);
                if(detail)
                {
                    s.Tube(p1,p2,Mathf.Lerp(.019f,.006f,a),Mathf.Lerp(.019f,.006f,b),col*.72f,5);
                    foreach(int sign in new[]{-1,1})s.Leaf(p1,p2+side*width*sign+dir*.18f-Vector3.up*(.15f+width*.35f),.055f+width*.035f,col*R(rng,.90f,1.12f),Vector3.up);
                }
                else
                {
                    float w1=(.13f+.88f*Mathf.Sin(a*Mathf.PI))*(1-a*.25f);
                    foreach(int sign in new[]{-1,1})s.Quad(p1,p2,p2+side*width*sign-Vector3.up*.28f,p1+side*w1*sign-Vector3.up*.25f,col,Vector3.up);
                }
            }
        }
        if(detail)for(int k=0;k<6;k++){float a=k*2.4f;var p=crown+new Vector3(Mathf.Cos(a)*.24f,-.28f,Mathf.Sin(a)*.24f);s.Tube(p-Vector3.up*.12f,p+Vector3.up*.12f,.12f,.095f,new Color(.39f,.36f,.14f),8);}
        return s.Finish("Curved coconut palm "+variant+(detail?" near":" far"));
    }
    public static Mesh Mango(int variant,bool detail)
    {
        var mesh=BanagoEnvironmentModels.Tree(0,variant,detail,new Color(.25f,.36f,.13f)*(1+variant*.035f));
        mesh.name="Dense rounded mango canopy "+variant+(detail?" near":" far");mesh.uv=mesh.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();mesh.RecalculateTangents();return mesh;
    }
    public static Mesh Bamboo(int variant,bool detail)
    {
        var s=new Shape();var rng=new System.Random(193+variant*517);int stems=detail?9:6;
        for(int j=0;j<stems;j++)
        {
            float angle=j*2.39996f;var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var start=outward*R(rng,.18f,.6f);float height=R(rng,3.1f,4.7f);
            var tip=start+Vector3.up*height+outward*.42f;var green=new Color(.38f,.43f,.19f)*R(rng,.83f,1.13f);
            int steps=detail?12:6;for(int k=0;k<steps;k++){float a=k/(float)steps,b=(k+1f)/steps;var p=Vector3.Lerp(start,tip,a);s.Tube(p,Vector3.Lerp(start,tip,b),.031f,.025f,green,detail?7:5);if(detail)s.Tube(p,p+Vector3.up*.024f,.039f,.039f,green*.7f,7);}
            int branches=detail?7:5;for(int b=0;b<branches;b++)
            {
                float t=.45f+b*.50f/branches;var p=Vector3.Lerp(start,tip,t);float a=angle+b*2.4f;var dir=new Vector3(Mathf.Cos(a),.2f,Mathf.Sin(a));var end=p+dir*R(rng,.6f,1.1f);s.Tube(p,end,.012f,.004f,green*.87f,5);
                int ls=detail?7:4;for(int k=1;k<=ls;k++){var v=Vector3.Lerp(p,end,k/(float)ls);var side=Vector3.Cross(Vector3.up,dir)*(k%2==0?1:-1);s.Leaf(v,v+side*.30f+dir*.17f-Vector3.up*.10f,detail?.046f:.10f,new Color(.28f,.40f,.13f)*R(rng,.82f,1.2f),Vector3.up);}
            }
        }
        return s.Finish("Irregular fence-line bamboo "+variant+(detail?" near":" far"));
    }
}
