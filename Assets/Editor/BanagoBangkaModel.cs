using UnityEngine;
using Parts = BanagoEnvironmentModels.Parts;

/// <summary>6.6 m bangka, based on the supplied white/yellow boat photograph.</summary>
public static class BanagoBangkaModel
{
    static readonly Color White = new Color(.88f,.88f,.81f);
    static readonly Color Yellow = new Color(.91f,.65f,.10f);
    static readonly Color Blue = new Color(.045f,.33f,.53f);
    static readonly Color Wood = new Color(.36f,.25f,.13f);
    static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
    static void Quad(Parts p,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,Vector3 normal)
    { p.Face(a,b,c,color,normal);p.Face(a,c,d,color,normal); }
    static void Pole(Parts p,Vector3 a,Vector3 b,float radius,Color color,int sides=8)
    {
        var dir=(b-a).normalized;var u=Vector3.Cross(dir,Vector3.up).normalized;
        if(u.sqrMagnitude<.1f)u=Vector3.right;
        var v=Vector3.Cross(dir,u);
        for(int j=0;j<sides;j++)
        {
            var n=u*Mathf.Cos(j*2*Mathf.PI/sides)+v*Mathf.Sin(j*2*Mathf.PI/sides);
            var n2=u*Mathf.Cos((j+1)*2*Mathf.PI/sides)+v*Mathf.Sin((j+1)*2*Mathf.PI/sides);
            Quad(p,a+n*radius,a+n2*radius,b+n2*radius,b+n*radius,color,n+n2);
            p.Face(a,a+n2*radius,a+n*radius,color,-dir);p.Face(b,b+n*radius,b+n2*radius,color,dir);
        }
    }
    static float Width(float t)=>.52f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*Mathf.Clamp01(t))),.68f)+.012f;
    static float Rim(float t)=>.52f+.19f*Mathf.Pow(Mathf.Abs(t*2-1),4);
    public static Mesh Create()=>CreateVariant("White yellow blue bangka - two katig outriggers",White,Yellow,Blue,Yellow,Wood);
    public static Mesh CreateVariant(string name,Color hull,Color accent,Color trimColor,Color canopy,Color timber)
    {
        var p=new Parts();const int stations=28;
        // Open, curved, pointed hull with a yellow lower panel, blue stripe and white upper plank.
        for(int i=0;i<stations;i++)
        {
            float t=i/(float)stations,t2=(i+1)/(float)stations,z=-3.3f+6.6f*t,z2=-3.3f+6.6f*t2;
            float w=Width(t),w2=Width(t2),h=Rim(t),h2=Rim(t2);
            foreach(int side in new[]{-1,1})
            {
                var a=V(0,-.19f+.20f*Mathf.Pow(Mathf.Abs(t*2-1),3),z);
                var b=V(side*w*.74f,.06f,z);var c=V(side*w,.20f,z);var d=V(side*w,h,z);
                var a2=V(0,-.19f+.20f*Mathf.Pow(Mathf.Abs(t2*2-1),3),z2);
                var b2=V(side*w2*.74f,.06f,z2);var c2=V(side*w2,.20f,z2);var d2=V(side*w2,h2,z2);
                var n=V(side,.1f,0);
                Quad(p,a,a2,b2,b,accent,n);Quad(p,b,b2,c2,c,accent,n);
                Quad(p,c,c2,d2,d,hull,n);
                var trim=Vector3.right*(side*.003f);
                Quad(p,c+trim+Vector3.up*.012f,c2+trim+Vector3.up*.012f,c2+trim+Vector3.up*.057f,c+trim+Vector3.up*.057f,trimColor,n);
                Quad(p,b*1f+V(-side*.035f,.06f,0),d+V(-side*.038f,-.015f,0),d2+V(-side*.038f,-.015f,0),b2+V(-side*.035f,.06f,0),timber,-n);
                Pole(p,d,d2,.024f,trimColor);
            }
            Quad(p,V(-w*.68f,.095f,z),V(w*.68f,.095f,z),V(w2*.68f,.095f,z2),V(-w2*.68f,.095f,z2),timber,Vector3.up);
        }
        // Small painted decks at the pointed ends; open middle and timber benches.
        foreach(int end in new[]{-1,1})
            for(int i=0;i<6;i++)
            {
                float z=end*(2.1f+i*.2f),z2=end*(2.1f+(i+1)*.2f);
                float t=(z+3.3f)/6.6f,t2=(z2+3.3f)/6.6f;
                Quad(p,V(-Width(t),Rim(t)-.035f,z),V(Width(t),Rim(t)-.035f,z),V(Width(t2),Rim(t2)-.035f,z2),V(-Width(t2),Rim(t2)-.035f,z2),accent,Vector3.up);
            }
        foreach(float z in new[]{-1.5f,-.5f,.65f,1.5f})
            Quad(p,V(-.43f,.37f,z-.09f),V(.43f,.37f,z-.09f),V(.43f,.37f,z+.09f),V(-.43f,.37f,z+.09f),timber*1.15f,Vector3.up);
        // Two slim bamboo floats and curved white katig crossarms.
        foreach(int side in new[]{-1,1})
        {
            for(int j=0;j<20;j++)
            {
                float z=-2.65f+j*.265f,z2=z+.265f;
                float y=.055f+.27f*Mathf.Pow(Mathf.Abs(z/2.65f),5),y2=.055f+.27f*Mathf.Pow(Mathf.Abs(z2/2.65f),5);
                Pole(p,V(side*2.0f,y,z),V(side*2.0f,y2,z2),.065f,White,10);
            }
            foreach(float z in new[]{-1.15f,1.15f})
            {
                var points=new[]{V(side*.20f,.60f,z),V(side*.7f,.70f,z),V(side*1.25f,.67f,z),V(side*1.72f,.42f,z),V(side*2,.11f,z)};
                for(int j=0;j<points.Length-1;j++)Pole(p,points[j],points[j+1],.028f,White);
                Pole(p,V(side*.5f,.48f,z+.20f),V(side*1.7f,.39f,z+.20f),.022f,White);
            }
            Pole(p,V(side*.43f,.50f,-1.8f),V(side*2,.12f,1.15f),.013f,White*.84f);
        }
        // Short mast, rigging and the small yellow sun shade visible in the reference.
        Pole(p,V(0,.10f,.55f),V(0,2.45f,.55f),.025f,White);
        foreach(float x in new[]{-.48f,.48f})foreach(float z in new[]{-1.50f,-.35f})
            Pole(p,V(x,.44f,z),V(x,1.22f,z),.014f,White);
        Quad(p,V(-.57f,1.22f,-1.65f),V(.57f,1.22f,-1.65f),V(.57f,1.29f,-.2f),V(-.57f,1.29f,-.2f),canopy,Vector3.up);
        Pole(p,V(0,2.40f,.55f),V(0,.69f,3.22f),.006f,White*.72f,5);
        Pole(p,V(0,2.40f,.55f),V(0,.69f,-3.22f),.006f,White*.72f,5);
        Pole(p,V(0,.51f,-2.1f),V(.21f,.58f,1.65f),.024f,timber);
        return p.Mesh(name);
    }
}
