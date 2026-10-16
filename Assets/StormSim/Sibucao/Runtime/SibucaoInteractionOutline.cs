using UnityEngine;
namespace StormSim.Sibucao
{
    // A single restrained outline communicates focus without changing the asset's material.
    public sealed class SibucaoInteractionOutline:MonoBehaviour
    {
        LineRenderer line;Material material;readonly Vector3[] points=new Vector3[16];float next;
        void Awake()
        {
            var child=new GameObject("Focused object outline");child.transform.SetParent(transform,false);line=child.AddComponent<LineRenderer>();
            material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(.9f,.75f,.32f);line.sharedMaterial=material;line.useWorldSpace=true;line.widthMultiplier=.009f;line.positionCount=16;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.enabled=false;
        }
        void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.08f;var s=SibucaoSession.Instance;
            if(!s||s.Mode!=SibucaoMode.Playing||!s.settings.interactPrompts||s.inventory&&s.inventory.IsOpen||s.missionGuide&&s.missionGuide.IsOpen){line.enabled=false;return;}
            var grab=s.player.grabber;Transform target=grab&&grab.CanReach&&grab.FocusedItem?grab.FocusedItem.transform:null;
            var routes=s.physicalDecisions?s.physicalDecisions.GetComponent<SibucaoRouteChoices>():null;if(!target&&routes){var mission=routes.Focused();if(mission&&mission.Step!=null)target=mission.Step.item;}
            if(!target||Vector3.Distance(s.player.view.transform.position,target.position)>3.4f){line.enabled=false;return;}
            var renderer=target.GetComponentInChildren<Renderer>();if(!renderer||renderer.bounds.size.magnitude>4){line.enabled=false;return;}
            Bounds b=renderer.bounds;b.Expand(.024f);Vector3 a=b.min,z=b.max;
            points[0]=new Vector3(a.x,a.y,a.z);points[1]=new Vector3(z.x,a.y,a.z);points[2]=new Vector3(z.x,a.y,z.z);points[3]=new Vector3(a.x,a.y,z.z);points[4]=points[0];
            points[5]=new Vector3(a.x,z.y,a.z);points[6]=new Vector3(z.x,z.y,a.z);points[7]=points[1];points[8]=points[6];points[9]=new Vector3(z.x,z.y,z.z);points[10]=points[2];points[11]=points[9];points[12]=new Vector3(a.x,z.y,z.z);points[13]=points[3];points[14]=points[12];points[15]=points[5];
            line.SetPositions(points);line.enabled=true;
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
