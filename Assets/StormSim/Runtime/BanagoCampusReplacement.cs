using System.Collections.Generic;
using UnityEngine;

/// <summary>Removes only old streamed school triangles inside the replacement campus.
/// Original meshes, materials and all triangles outside the campus are retained.</summary>
[ExecuteAlways]
public sealed class BanagoCampusReplacement : MonoBehaviour
{
    public Transform streamedBuildings;
    public Transform georeference;
    public Vector2[] campus;
    readonly Dictionary<MeshFilter,(Mesh original,Mesh clipped)> replaced=new();
    readonly Dictionary<MeshFilter,Mesh> observed=new();
    float nextCheck;
    bool Inside(Vector3 p)
    {
        bool inside=false;
        for(int i=0,j=campus.Length-1;i<campus.Length;j=i++)
        {
            var a=campus[i];var b=campus[j];
            if((a.y>p.z)!=(b.y>p.z) && p.x<(b.x-a.x)*(p.z-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside;
    }
    void OnEnable(){nextCheck=0;Apply();}
    void Update(){if(Time.realtimeSinceStartup>nextCheck){nextCheck=Time.realtimeSinceStartup+1.5f;Apply();}}
    public void Apply()
    {
        if(!streamedBuildings||!georeference||campus==null||campus.Length<3)return;
        foreach(var filter in streamedBuildings.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
            // Cesium can reuse a filter for a newly streamed tile after leaving Play mode.
            if(replaced.TryGetValue(filter,out var prior))
            {
                if(filter.sharedMesh==prior.clipped)continue;
                replaced.Remove(filter);
                observed.Remove(filter);
                if(prior.clipped){if(Application.isPlaying)Destroy(prior.clipped);else DestroyImmediate(prior.clipped);}
            }
            if(observed.TryGetValue(filter,out var previous)&&previous==filter.sharedMesh)continue;
            observed[filter]=filter.sharedMesh;
            var original=filter.sharedMesh;var vertices=original.vertices;var local=new Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++)local[i]=georeference.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
            var indices=new List<int>[original.subMeshCount];bool changed=false;
            for(int s=0;s<indices.Length;s++)
            {
                var t=original.GetTriangles(s);var keep=new List<int>(t.Length);
                for(int i=0;i<t.Length;i+=3)
                {
                    var center=(local[t[i]]+local[t[i+1]]+local[t[i+2]])/3;
                    if(Inside(center)){changed=true;continue;}keep.Add(t[i]);keep.Add(t[i+1]);keep.Add(t[i+2]);
                }
                indices[s]=keep;
            }
            if(!changed)continue;
            var copy=Instantiate(original);copy.name=original.name+" - retained outside campus";copy.hideFlags=HideFlags.DontSave;
            for(int s=0;s<indices.Length;s++)copy.SetTriangles(indices[s],s);
            filter.sharedMesh=copy;var collider=filter.GetComponent<MeshCollider>();if(collider&&collider.sharedMesh==original)collider.sharedMesh=copy;
            replaced[filter]=(original,copy);
        }
    }
    void OnDisable()
    {
        foreach(var item in replaced)
        {
            if(item.Key){if(item.Key.sharedMesh==item.Value.clipped)item.Key.sharedMesh=item.Value.original;var c=item.Key.GetComponent<MeshCollider>();if(c&&c.sharedMesh==item.Value.clipped)c.sharedMesh=item.Value.original;}
            if(item.Value.clipped){if(Application.isPlaying)Destroy(item.Value.clipped);else DestroyImmediate(item.Value.clipped);}
        }
        replaced.Clear();observed.Clear();
    }
}
