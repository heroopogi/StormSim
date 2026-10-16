using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoResidentMeshBaker
    {
        public static string Bake()
        {
            int count=0;AssetDatabase.StartAssetEditing();
            try
            {
                foreach(var npc in Object.FindObjectsByType<SibucaoResident>())
                {
                    if(npc.GetComponent<SkinnedMeshRenderer>())continue;
                    var parts=npc.GetComponentsInChildren<MeshFilter>();var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var materials=new List<Material>();var triangles=new List<List<int>>();var bones=new List<Transform>();var binds=new List<Matrix4x4>();
                    foreach(var part in parts)
                    {
                        var renderer=part.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!part.sharedMesh)continue;
                        var mesh=part.sharedMesh;var matrix=npc.transform.worldToLocalMatrix*part.transform.localToWorldMatrix;int start=vertices.Count;int bone=bones.Count;bones.Add(part.transform);binds.Add(part.transform.worldToLocalMatrix*npc.transform.localToWorldMatrix);
                        var vs=mesh.vertices;var ns=mesh.normals;var us=mesh.uv;
                        for(int i=0;i<vs.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(vs[i]));normals.Add(matrix.inverse.transpose.MultiplyVector(ns.Length>i?ns[i]:Vector3.up).normalized);uv.Add(us.Length>i?us[i]:Vector2.zero);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
                        for(int sub=0;sub<mesh.subMeshCount;sub++){var material=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];int index=materials.IndexOf(material);if(index<0){index=materials.Count;materials.Add(material);triangles.Add(new List<int>());}foreach(int t in mesh.GetTriangles(sub))triangles[index].Add(start+t);}
                        renderer.enabled=false;
                    }
                    var combined=new Mesh{name=npc.name+" articulated mesh",indexFormat=IndexFormat.UInt32};combined.SetVertices(vertices);combined.SetNormals(normals);combined.SetUVs(0,uv);combined.boneWeights=weights.ToArray();combined.bindposes=binds.ToArray();combined.subMeshCount=materials.Count;for(int i=0;i<materials.Count;i++)combined.SetTriangles(triangles[i],i);combined.RecalculateBounds();
                    AssetDatabase.CreateAsset(combined,"Assets/StormSim/Sibucao/Art/Expansion/Resident combined "+npc.variation+".asset");var skin=npc.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=combined;skin.sharedMaterials=materials.ToArray();skin.bones=bones.ToArray();skin.rootBone=npc.transform;skin.localBounds=new Bounds(Vector3.up*.9f,new Vector3(1.6f,2.4f,1.8f));skin.updateWhenOffscreen=false;count++;
                }
            }
            finally{AssetDatabase.StopAssetEditing();}
            AssetDatabase.SaveAssets();var scene=EditorSceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Combined "+count+" residents into articulated skinned meshes; retained editable joint hierarchy.";
        }
    }
}
