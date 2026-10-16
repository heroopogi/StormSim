using System;
using System.Linq;
using UnityEngine;

/// <summary>Meter-scale coconut meshes. Alpha identifies bark (0) and leaf (1) to the PBR shader.</summary>
public static class BanagoCoconutRepairModels
{
    sealed class Shape
    {
        public readonly BanagoEnvironmentModels.Parts p=new BanagoEnvironmentModels.Parts();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,Vector3 n)
        {p.Face(a,b,c,color,n);p.Face(a,c,d,color,n);}
        public void Tube(Vector3 a,Vector3 b,float ra,float rb,Color color,int sides)
        {
            var axis=(b-a).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
            for(int k=0;k<sides;k++){float aa=k*Mathf.PI*2/sides,bb=(k+1)*Mathf.PI*2/sides;var x=u*Mathf.Cos(aa)+v*Mathf.Sin(aa);var y=u*Mathf.Cos(bb)+v*Mathf.Sin(bb);Quad(a+x*ra,a+y*ra,b+y*rb,b+x*rb,color,x+y);}
        }
        public void Leaf(Vector3 a,Vector3 b,float width,Color color)
        {
            var side=Vector3.Cross(b-a,Vector3.up).normalized*width;
            var mid=Vector3.Lerp(a,b,.42f)+Vector3.up*width*.32f;
            p.Face(a,mid+side,b,color,Vector3.up);p.Face(a,b,mid-side,color*.94f,Vector3.up);
        }
    }
    static float R(System.Random r,float a,float b)=>a+(b-a)*(float)r.NextDouble();
    public static Mesh Build(int variant,bool detailed)
    {
        var s=new Shape();var rng=new System.Random(9517+variant*197);float lean=.35f+variant*.065f;
        var bark=new Color(.47f,.445f,.385f,0);
        Func<float,Vector3> stem=t=>new Vector3(Mathf.Sin(t*Mathf.PI)*.12f,8.2f*t,lean*t*t);
        int steps=detailed?42:16;
        for(int i=0;i<steps;i++)
        {
            float a=i/(float)steps,b=(i+1f)/steps;
            float ra=Mathf.Lerp(.19f,.115f,Mathf.Sqrt(a)),rb=Mathf.Lerp(.19f,.115f,Mathf.Sqrt(b));
            s.Tube(stem(a),stem(b),ra,rb,bark*(i%4==0?.94f:1),detailed?10:7);
            if(detailed&&i>0)s.Tube(stem(a),stem(a+.0022f),ra+.0045f,ra+.003f,bark*.76f,10);
        }
        var crown=stem(1);int count=21+variant%3;
        for(int f=0;f<count;f++)
        {
            float angle=f*2.39996f+R(rng,-.12f,.12f);var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,dir);
            bool spear=f>=count-3;float length=spear?R(rng,1.6f,2.2f):R(rng,2.55f,3.2f);
            float lift=spear?R(rng,1.5f,2.0f):R(rng,.48f,1.12f),drop=spear?R(rng,.1f,.3f):R(rng,.85f,1.5f);
            Func<float,Vector3> curve=t=>crown+dir*length*t+Vector3.up*(lift*Mathf.Sin(t*Mathf.PI*.8f)-drop*t*t);
            var col=Color.Lerp(new Color(.24f,.37f,.11f,1),new Color(.43f,.51f,.18f,1),R(rng,0,1));
            if(f%9==0)col=Color.Lerp(col,new Color(.54f,.49f,.24f,1),.36f);
            int pieces=detailed?18:8;
            for(int j=0;j<pieces;j++)
            {
                float a=j/(float)pieces,b=(j+1f)/pieces;var p=curve(a);var q=curve(b);
                // sin(pi) can round slightly below zero; fractional powers must stay nonnegative.
                float width=(.035f+.49f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(b*Mathf.PI)),.65f))*(1-b*.20f);
                if(detailed)s.Tube(p,q,Mathf.Lerp(.018f,.003f,a),Mathf.Lerp(.018f,.003f,b),new Color(.40f,.43f,.18f,1),4);
                foreach(int sign in new[]{-1,1})
                {
                    var tip=q+side*width*sign+dir*.13f-Vector3.up*(.04f+width*.40f);
                    // Separate tapered leaflets preserve sky gaps instead of solid paddle-like fronds.
                    s.Leaf(p,tip,detailed?.035f+.028f*Mathf.Sin(a*Mathf.PI):.075f+.035f*Mathf.Sin(a*Mathf.PI),col*R(rng,.91f,1.08f));
                }
            }
        }
        if(detailed)for(int k=0;k<5;k++){float a=k*2.4f;var p=crown+new Vector3(Mathf.Cos(a)*.18f,-.16f,Mathf.Sin(a)*.18f);s.Tube(p-Vector3.up*.10f,p+Vector3.up*.10f,.11f,.07f,new Color(.42f,.40f,.20f,1),8);}
        var mesh=s.p.Mesh("House-scale coconut "+variant+(detailed?" LOD0":" LOD1"));
        if(s.p.vertices.Any(p=>float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z)))throw new Exception("Invalid palm vertex; refuse to apply mesh.");
        mesh.uv=s.p.vertices.Select(p=>new Vector2(p.x+p.z,p.y)).ToArray();mesh.RecalculateTangents();return mesh;
    }
}
