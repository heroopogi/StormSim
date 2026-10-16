using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;
namespace StormSim.Sibucao.Editor
{
    public static class StormSimMenuPreviewBuilder
    {
        public const string Path="Assets/Scenes/STORMSIM_MainMenu_Preview.unity";
        public static string Create()
        {
            if(System.IO.File.Exists(Path))return "Preview scene already exists.";
            var original=EditorSceneManager.GetActiveScene();
            var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);EditorSceneManager.SetActiveScene(preview);
            var rig=new GameObject("XR Origin - Menu Preview");var origin=rig.AddComponent<XROrigin>();
            var offset=new GameObject("Camera Floor Offset");offset.transform.SetParent(rig.transform,false);offset.transform.localPosition=Vector3.up*1.6f;origin.CameraFloorOffsetObject=offset;
            var eye=new GameObject("Preview Camera");eye.tag="MainCamera";eye.transform.SetParent(offset.transform,false);var cam=eye.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.039f,.082f,.098f);cam.nearClipPlane=.03f;cam.farClipPlane=20;cam.fieldOfView=60;eye.AddComponent<AudioListener>();origin.Camera=cam;
            var driver=eye.AddComponent<TrackedPoseDriver>();driver.positionInput=new InputActionProperty(new InputAction("Head position",InputActionType.Value,"<XRHMD>/centerEyePosition"));driver.rotationInput=new InputActionProperty(new InputAction("Head rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation"));driver.trackingStateInput=new InputActionProperty(new InputAction("Tracking state",InputActionType.Value,"<XRHMD>/trackingState"));
            var light=new GameObject("Preview directional light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.3f;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StormSim/Sibucao/UI/MainMenu/STORMSIM_MainMenu.prefab");
            var menu=((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<StormSimMainMenu>();menu.previewWithoutSession=true;menu.canvas.worldCamera=cam;menu.transform.SetPositionAndRotation(eye.transform.position+Vector3.forward*1.8f,Quaternion.identity);
            var es=new GameObject("XR and mouse UI input");es.AddComponent<EventSystem>();es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(preview,Path);EditorSceneManager.CloseScene(preview,true);EditorSceneManager.SetActiveScene(original);return "Created isolated XR-ready Main Menu preview scene.";
        }
    }
}

