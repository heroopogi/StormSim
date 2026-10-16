using System.Linq;
using UnityEngine;

namespace StormSim.Preparedness
{
    // Work in the surface's frame so rotated houses retain the same usable storage area.
    public static class PreparednessSurfacePlacement
    {
        public static Bounds BoundsIn(PreparednessItem item, Transform frame)
        {
            bool first=true;var result=new Bounds();
            foreach(var renderer in item.GetComponentsInChildren<Renderer>())
            {
                var b=renderer.localBounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var point=frame.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                    if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                }
            }
            foreach(var collider in item.GetComponentsInChildren<BoxCollider>())
            {
                // PhysX boxes use rotation and lossy scale, not a sheared parent matrix.
                var center=collider.transform.TransformPoint(collider.center);
                var extents=Vector3.Scale(collider.size*.5f,collider.transform.lossyScale);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var point=frame.InverseTransformPoint(center+collider.transform.rotation*Vector3.Scale(extents,new Vector3(x,y,z)));
                    if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                }
            }
            return first?new Bounds(frame.InverseTransformPoint(item.transform.position),Vector3.zero):result;
        }

        public static bool TryPlace(PreparednessSession session,PreparednessItem item,PreparednessItem zone,bool findSpace,out Vector3 position,out Quaternion rotation)
        {
            position=item.transform.position;rotation=findSpace?zone.transform.rotation:item.transform.rotation;
            var box=zone.GetComponent<BoxCollider>();if(!box)return false;
            var oldRotation=item.transform.rotation;item.transform.rotation=rotation;
            var shape=BoundsIn(item,zone.transform);item.transform.rotation=oldRotation;
            var half=box.size*.5f;
            float minX=box.center.x-half.x+shape.extents.x+.01f,maxX=box.center.x+half.x-shape.extents.x-.01f;
            float minZ=box.center.z-half.z+shape.extents.z+.01f,maxZ=box.center.z+half.z-shape.extents.z-.01f;
            if(minX>maxX||minZ>maxZ)return false;
            var occupied=session.items.Where(other=>other!=item&&other.portable&&!other.IsPacked&&other.gameObject.activeInHierarchy)
                .Select(other=>BoundsIn(other,zone.transform)).ToArray();
            bool Free(Vector3 center)
            {
                var bounds=new Bounds(center,shape.size+new Vector3(.07f,0,.07f));
                return !occupied.Any(other=>bounds.Intersects(other));
            }
            var candidate=new Vector3(Mathf.Clamp(shape.center.x,minX,maxX),shape.extents.y+.01f,Mathf.Clamp(shape.center.z,minZ,maxZ));
            if(findSpace||!Free(candidate))
            {
                if(!findSpace)return false;
                bool found=false;
                int nx=Mathf.Max(1,Mathf.CeilToInt((maxX-minX)/.025f)),nz=Mathf.Max(1,Mathf.CeilToInt((maxZ-minZ)/.025f));
                for(int z=0;z<=nz&&!found;z++)for(int x=0;x<=nx&&!found;x++)
                {candidate.x=Mathf.Lerp(minX,maxX,(float)x/nx);candidate.z=Mathf.Lerp(minZ,maxZ,(float)z/nz);found=Free(candidate);}
                if(!found)return false;
            }
            var pivot=zone.transform.InverseTransformPoint(item.transform.position);
            position=zone.transform.TransformPoint(pivot+candidate-shape.center);return true;
        }
    }
}
