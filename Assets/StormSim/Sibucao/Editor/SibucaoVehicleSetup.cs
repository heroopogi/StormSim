using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoVehicleSetup
    {
        [MenuItem("Tools/StormSim/Enable Existing Sibucao Vehicles")]
        public static string Install()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/SIBUCAO_RAIN.unity")throw new System.InvalidOperationException("Open SIBUCAO_RAIN first.");
            int count=0;
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                string n=t.name.ToLowerInvariant();
                bool vehicle=n.StartsWith("car ")||n.StartsWith("tricycle ")||n.StartsWith("school roadside parked car")||n.StartsWith("private car")||n.StartsWith("waiting tricycle");
                if(!vehicle || !t.GetComponent<BoxCollider>() || t.GetComponentsInChildren<MeshRenderer>().Length==0)continue;
                var v=t.GetComponent<SibucaoVehicle>()??Undo.AddComponent<SibucaoVehicle>(t.gameObject);
                v.vehicleId=t.name;v.displayName=n.Contains("tricycle")?"Tricycle":"Car";v.maxSpeed=n.Contains("tricycle")?7:9;
                foreach(var child in t.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(child.gameObject,0);
                EditorUtility.SetDirty(v);count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Enabled "+count+" existing cars and tricycles.";
        }
    }
}
