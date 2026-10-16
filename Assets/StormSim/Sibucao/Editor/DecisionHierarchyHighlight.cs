using UnityEngine;
using UnityEditor;
namespace StormSim.Sibucao.Editor
{
    [InitializeOnLoad]
    public static class DecisionHierarchyHighlight
    {
        static DecisionHierarchyHighlight(){EditorApplication.hierarchyWindowItemByEntityIdOnGUI+=Draw;}
        static void Draw(EntityId id,Rect rect)
        {
            var go=EditorUtility.EntityIdToObject(id) as GameObject;if(!go)return;
            var marker=go.GetComponent<DecisionTriggerMarker>();if(!marker)return;
            EditorGUI.DrawRect(rect,new Color(.18f,.48f,.62f,.18f));
            var badge=new Rect(rect.xMax-42,rect.y,40,rect.height);
            GUI.Label(badge,"B-"+marker.decision,EditorStyles.miniBoldLabel);
        }
    }
}
