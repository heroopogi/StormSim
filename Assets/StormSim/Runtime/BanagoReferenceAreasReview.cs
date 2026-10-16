using System;
using UnityEngine;

/// <summary>Retains the pre-reference assets and rooftop prop positions. No original assets are overwritten.</summary>
[ExecuteAlways]
public sealed class BanagoReferenceAreasReview : MonoBehaviour
{
    [Serializable] public class Swap
    {
        public MeshFilter target;
        public Mesh before, after;
        public Material[] materialsBefore, materialsAfter;
        public Mesh colliderBefore;
    }
    [Serializable] public class Prop
    {
        public Transform target;
        public Vector3 before, after;
    }
    public Swap[] swaps = Array.Empty<Swap>();
    public Prop[] props = Array.Empty<Prop>();
    [TextArea(4,12)] public string notes;
    public string backupPath;
    static bool Same(Material[] a, Material[] b)
    {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    public void Apply()
    {
        foreach(var s in swaps)
        {
            if(!s.target)continue;
            if(s.after&&(s.target.sharedMesh==s.before||s.target.sharedMesh==s.after))s.target.sharedMesh=s.after;
            var r=s.target.GetComponent<Renderer>();if(r&&(Same(r.sharedMaterials,s.materialsBefore)||Same(r.sharedMaterials,s.materialsAfter)))r.sharedMaterials=s.materialsAfter;
            var c=s.target.GetComponent<MeshCollider>();if(c&&s.after&&(c.sharedMesh==s.colliderBefore||c.sharedMesh==s.after))c.sharedMesh=s.after;
        }
        foreach(var p in props)if(p.target&&(p.target.position==p.before||p.target.position==p.after))p.target.position=p.after;
    }
    void OnEnable(){Apply();}
    void OnDisable()
    {
        foreach(var s in swaps)
        {
            if(!s.target)continue;
            if(s.after&&s.target.sharedMesh==s.after)s.target.sharedMesh=s.before;
            var r=s.target.GetComponent<Renderer>();if(r&&Same(r.sharedMaterials,s.materialsAfter))r.sharedMaterials=s.materialsBefore;
            var c=s.target.GetComponent<MeshCollider>();if(c&&s.after&&c.sharedMesh==s.after)c.sharedMesh=s.colliderBefore;
        }
        foreach(var p in props)if(p.target&&p.target.position==p.after)p.target.position=p.before;
    }
}
