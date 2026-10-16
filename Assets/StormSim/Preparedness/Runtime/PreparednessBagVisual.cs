using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Preparedness
{
    // Soft backpack shell and zipper follow the authored backpack's centre origin.
    public sealed class PreparednessBagVisual : MonoBehaviour
    {
        public PreparednessSession session;
        PreparednessItem bag;Transform contents,pull;Mesh clothMesh;LineRenderer zipper;
        Material fabric,lining,metal;float opening,pulse;string signature="";
        const int Segments=48,Rows=9;
        public Vector3 Opening=>transform.TransformPoint(new Vector3(0,.20f,0));
        public bool Contains(Vector3 point)
        {var p=transform.InverseTransformPoint(point);return Mathf.Abs(p.x)<.24f&&Mathf.Abs(p.z)<.19f&&p.y>.10f&&p.y<.43f;}
        void Awake()
        {
            bag=GetComponent<PreparednessItem>();
            foreach(var r in GetComponentsInChildren<Renderer>(true))
                if(r.name=="Rounded fabric main compartment"||r.name=="Main zipper seam"||r.name=="Zipper pull")r.enabled=false;
            fabric=MakeMaterial("Flexible teal canvas",new Color(.045f,.13f,.16f));
            lining=MakeMaterial("Dark cloth lining",new Color(.018f,.028f,.034f));
            metal=MakeMaterial("Silver zipper",new Color(.65f,.7f,.7f));metal.SetFloat("_Metallic",.65f);
            var shell=new GameObject("Soft zippered backpack shell");shell.transform.SetParent(transform,false);
            clothMesh=new Mesh{name="Deforming rounded backpack cloth"};shell.AddComponent<MeshFilter>().sharedMesh=clothMesh;
            shell.AddComponent<MeshRenderer>().sharedMaterials=new[]{fabric,lining};
            var zip=new GameObject("Continuous zipper track");zip.transform.SetParent(transform,false);zipper=zip.AddComponent<LineRenderer>();zipper.useWorldSpace=false;zipper.loop=true;zipper.positionCount=Segments;zipper.widthMultiplier=.006f;zipper.sharedMaterial=metal;
            var tab=GameObject.CreatePrimitive(PrimitiveType.Capsule);tab.name="Sliding zipper pull";pull=tab.transform;pull.SetParent(transform,false);pull.localScale=new Vector3(.014f,.025f,.009f);tab.GetComponent<Renderer>().sharedMaterial=metal;var c=tab.GetComponent<Collider>();c.enabled=false;Destroy(c);
            contents=new GameObject("Packed supply previews").transform;contents.SetParent(transform,false);
            opening=bag.closed?0:1;BuildCloth();Refresh();
        }
        Material MakeMaterial(string label,Color color)=>new Material(Shader.Find("Universal Render Pipeline/Lit")){name=label,color=color};
        void BuildCloth()
        {
            var vertices=new Vector3[Rows*Segments];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int row=0;row<Rows;row++)
            {
                float u=row/(float)(Rows-1),y=Mathf.Lerp(-.265f,.22f,u);
                float width=row==0?.075f:row==1?.17f:row<6?.205f:Mathf.Lerp(.20f,.155f,(row-5)/3f);
                float depth=row==0?.055f:row==1?.115f:row<6?.13f:Mathf.Lerp(.125f,Mathf.Lerp(.003f,.095f,opening),(row-5)/3f);
                for(int n=0;n<Segments;n++)
                {
                    float a=n*Mathf.PI*2/Segments,wrinkle=1+.018f*Mathf.Sin(a*7+u*5)*Mathf.Sin(u*Mathf.PI);
                    int index=row*Segments+n;vertices[index]=new Vector3(Mathf.Cos(a)*width*wrinkle,y+opening*.008f*Mathf.Sin(a*3)*u,Mathf.Sin(a)*depth*wrinkle);uv[index]=new Vector2(n/(float)Segments,u);
                    if(row<Rows-1){int next=row*Segments+(n+1)%Segments;triangles.AddRange(new[]{index,next,index+Segments,next,next+Segments,index+Segments});}
                }
            }
            clothMesh.Clear();clothMesh.vertices=vertices.Concat(vertices).ToArray();clothMesh.uv=uv.Concat(uv).ToArray();clothMesh.subMeshCount=2;
            var outside=triangles.ToArray();for(int i=0;i<outside.Length;i+=3){int t=outside[i];outside[i]=outside[i+2];outside[i+2]=t;}
            clothMesh.SetTriangles(outside,0);clothMesh.SetTriangles(triangles.Select(i=>i+vertices.Length).ToArray(),1);clothMesh.RecalculateNormals();clothMesh.RecalculateBounds();
            for(int n=0;n<Segments;n++)zipper.SetPosition(n,vertices[(Rows-1)*Segments+n]+Vector3.up*.002f);
            float angle=Mathf.Lerp(0,Mathf.PI*1.85f,opening);pull.localPosition=new Vector3(Mathf.Cos(angle)*.157f,.207f,Mathf.Sin(angle)*Mathf.Lerp(.005f,.097f,opening));pull.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,15);
        }
        public void ConfirmPacked(){pulse=.65f;signature="";Refresh();}
        void LateUpdate()
        {
            if(!bag)return;float next=Mathf.MoveTowards(opening,bag.closed?0:1,Time.deltaTime*3);
            if(!Mathf.Approximately(next,opening)){opening=next;BuildCloth();}
            contents.gameObject.SetActive(opening>.7f);pulse=Mathf.Max(0,pulse-Time.deltaTime);metal.color=pulse>0?new Color(.3f,.9f,.55f):new Color(.65f,.7f,.7f);Refresh();
        }
        void Refresh()
        {
            if(!session||!contents)return;var packed=session.items.Where(i=>i.IsPacked).ToArray();var key=string.Join("|",packed.Select(i=>i.id));if(key==signature)return;signature=key;
            foreach(Transform child in contents)Destroy(child.gameObject);
            for(int i=0;i<Mathf.Min(4,packed.Length);i++)
            {
                var source=packed[i];var group=new GameObject(source.displayName+" preview").transform;group.SetParent(contents,false);
                var bounds=new Bounds();bool first=true;
                foreach(var f in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r=f.GetComponent<Renderer>();if(!r||!f.sharedMesh)continue;var go=new GameObject(f.name);go.transform.SetParent(group,false);
                    go.transform.localPosition=source.transform.InverseTransformPoint(f.transform.position);go.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*f.transform.rotation;go.transform.localScale=f.transform.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh=f.sharedMesh;go.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
                    var local=new Bounds(go.transform.localPosition,Vector3.Scale(f.sharedMesh.bounds.size,go.transform.localScale));if(first){bounds=local;first=false;}else bounds.Encapsulate(local);
                }
                float scale=.075f/Mathf.Max(.01f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));group.localScale=Vector3.one*scale;
                group.localPosition=new Vector3(i%2==0?-.065f:.065f,.155f,(i/2==0?-.035f:.035f))-bounds.center*scale;
            }
        }
        void OnDestroy(){if(fabric)Destroy(fabric);if(lining)Destroy(lining);if(metal)Destroy(metal);if(clothMesh)Destroy(clothMesh);}
    }
}
