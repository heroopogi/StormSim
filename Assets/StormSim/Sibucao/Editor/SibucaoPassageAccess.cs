using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

// Validate operating space against actual static buildings. Merely finding a
// clear point on a road is insufficient if a nearby wall hides the handle.
public static class SibucaoPassageAccess
{
    static Transform root;
    static bool ParkBlocksTask(SibucaoSession s,SibucaoStormPassage p,int home)
    {
        float start=s.route.Progress(s.homes[home].standingPoint.position);
        foreach(var st in s.physicalDecisions.stations)
        {
            float d=start+(s.route.length-start-12)*(st.index+1)/10;
            var position=s.route.Sample(d);var forward=s.route.Sample(d+2)-s.route.Sample(d-2);forward.y=0;
            var rotation=Quaternion.LookRotation(forward);
            foreach(var branch in st.branches)
            {
                var layout=branch.layouts.FirstOrDefault(l=>l.home==home);if(layout==null)continue;
                var points=layout.points.Select(v=>position+rotation*st.transform.InverseTransformPoint(branch.transform.TransformPoint(v))).ToArray();
                for(int i=1;i<points.Length;i++)
                {
                    int n=Mathf.CeilToInt(Vector3.Distance(points[i-1],points[i])/.25f);
                    for(int k=0;k<=n;k++)
                    {
                        var local=p.transform.InverseTransformPoint(Vector3.Lerp(points[i-1],points[i],(float)k/Mathf.Max(1,n)))-new Vector3(p.width+1.1f,0,2.2f);
                        if(Mathf.Abs(local.x)<p.width*.5f+.5f&&Mathf.Abs(local.z)<.7f&&Mathf.Abs(local.y)<2)return true;
                    }
                }
            }
        }
        return false;
    }
    static void ReplaceWithRack(SibucaoStormPassage p,SibucaoStormPassage source)
    {
        float scale=p.width/source.width;p.kind=StormPassageKind.TiltRack;p.description="Fallen market rack";
        p.moving.name=p.description;p.moving.localScale=new Vector3(scale,1,1);
        p.moving.GetComponent<MeshFilter>().sharedMesh=source.moving.GetComponent<MeshFilter>().sharedMesh;
        p.moving.GetComponent<MeshRenderer>().sharedMaterials=source.moving.GetComponent<MeshRenderer>().sharedMaterials;
        p.moving.localPosition=new Vector3(0,2.25f,0);p.moving.localRotation=Quaternion.identity;
        var box=p.moving.GetComponent<BoxCollider>();box.center=new Vector3(0,-.9f,0);box.size=new Vector3(source.width,1.8f,.2f);
        p.grip.transform.localPosition=new Vector3(0,-.9f,-.24f);p.grip.transform.localScale=new Vector3(1/scale,1,1);
    }
    static bool Transient(Collider c)=>c.isTrigger||c.attachedRigidbody||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<SibucaoResident>()||c.GetComponentInParent<PhysicalDecisionStation>();
    static bool Stand(SibucaoStormPassage gate,Vector3 proposed,out Vector3 floor)
    {
        floor=proposed;float y=float.NegativeInfinity;
        foreach(var hit in Physics.RaycastAll(proposed+Vector3.up*1.3f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore))
            if(!Transient(hit.collider)&&!hit.transform.IsChildOf(root)&&hit.normal.y>.72f&&hit.point.y<gate.transform.position.y+.8f)y=Mathf.Max(y,hit.point.y);
        if(!float.IsFinite(y))return false;floor.y=y+.055f;
        return !Physics.OverlapCapsule(floor+Vector3.up*.31f,floor+Vector3.up*1.5f,.28f,~0,QueryTriggerInteraction.Ignore).Any(c=>!Transient(c));
    }
    static bool See(Vector3 eye,SibucaoPassageHandle target)
    {
        var delta=target.GetComponent<Collider>().bounds.center-eye;if(delta.magnitude>2.9f)return false;
        foreach(var hit in Physics.RaycastAll(eye,delta.normalized,delta.magnitude+.05f,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
        {
            var handle=hit.collider.GetComponent<SibucaoPassageHandle>();if(handle)return handle==target;
            if(Transient(hit.collider))continue;return false;
        }
        return false;
    }
    static bool Walk(SibucaoStormPassage p,Vector3 start,Vector3 end)
    {
        int n=Mathf.CeilToInt(Vector3.Distance(start,end)/.25f);
        for(int i=0;i<=n;i++)if(!Stand(p,Vector3.Lerp(start,end,(float)i/Mathf.Max(1,n)),out _))return false;
        return true;
    }
    static bool TryApproach(SibucaoStormPassage p,out Vector3 local)
    {
        local=Vector3.zero;
        foreach(float z in new[]{-.75f,-1.1f,-.55f,-1.45f})
        foreach(float x in new[]{p.kind==StormPassageKind.SwingFence?p.width*.3f:0,-p.width*.25f,p.width*.25f,0})
        {
            var proposed=p.transform.TransformPoint(x,.08f,z);if(!Stand(p,proposed,out var floor))continue;
            var eye=floor+Vector3.up*1.63f;
            if(!See(eye,p.grip)||p.fasteners.Any(t=>!See(eye,t.GetComponent<SibucaoPassageHandle>())))continue;
            var end=floor+(p.kind==StormPassageKind.PushCart?p.transform.right*(p.width+1.4f):-p.transform.forward*2.8f);
            if(!Walk(p,floor,end))continue;
            var original=p.moving.localPosition;var rotation=p.moving.localRotation;
            if(p.kind==StormPassageKind.SwingFence){p.moving.localPosition=new Vector3(-p.width*.5f,0,0);p.moving.localRotation=Quaternion.Euler(0,100,0);}
            else if(p.kind==StormPassageKind.PushCart)p.moving.localPosition=new Vector3(p.width+1.1f,0,2.2f);
            else{p.moving.localPosition=new Vector3(0,2.25f,0);p.moving.localRotation=Quaternion.Euler(-100,0,0);}
            Physics.SyncTransforms();
            var approach=p.transform.TransformPoint(x,.08f,-.6f);var entry=p.transform.TransformPoint(0,.08f,-.5f);var exit=p.transform.TransformPoint(0,.08f,1.35f);
            bool cross=Walk(p,end,approach)&&Walk(p,approach,entry)&&Walk(p,entry,exit);
            p.moving.SetLocalPositionAndRotation(original,rotation);Physics.SyncTransforms();
            if(cross){local=p.transform.InverseTransformPoint(floor);return true;}
        }
        return false;
    }
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Operating-space audit requires Edit Mode");
        var s=Object.FindFirstObjectByType<SibucaoSession>();var director=s.passages;root=director.layouts[0].transform.parent;
        var report=new List<string>();
        // Handle hit areas participate in gaze selection, not body collision.
        foreach(var handle in root.GetComponentsInChildren<SibucaoPassageHandle>(true))handle.GetComponent<Collider>().isTrigger=true;
        for(int home=0;home<4;home++)
        {
            for(int i=0;i<4;i++)director.layouts[i].SetActive(i==home);Physics.SyncTransforms();
            var all=director.passages.Where(p=>p.home==home).ToArray();
            var rack=all.First(p=>p.kind==StormPassageKind.TiltRack&&Mathf.Abs(p.moving.localScale.x-1)<.01f);
            foreach(var p in all)
            {
                if(p.kind==StormPassageKind.PushCart&&ParkBlocksTask(s,p,home))
                {ReplaceWithRack(p,rack);report.Add(p.persistentId+": replaced cart whose parking pocket obstructed an authored task with an overhead rack.");Physics.SyncTransforms();}
                p.canOperate=TryApproach(p,out var local);
                if(p.canOperate)p.approachLocal=local;else p.core=false;
                EditorUtility.SetDirty(p);
            }
            float start=s.route.Progress(s.homes[home].standingPoint.position);
            for(int zone=0;zone<9;zone++)
            {
                var usable=all.Where(p=>p.zone==zone&&p.canOperate).ToArray();
                if(zone!=1&&!usable.Any(p=>p.core)&&usable.Length>0)
                {
                    var target=s.route.Sample(start+(s.route.length-start-12)*(zone+1)/10);
                    var nearest=usable.OrderBy(p=>(p.transform.position-target).sqrMagnitude).First();nearest.core=true;EditorUtility.SetDirty(nearest);
                    report.Add($"H{home} B{zone+1}: linked alternative working passage {nearest.persistentId}, {Vector3.Distance(target,nearest.transform.position):F1}m from encounter.");
                }
                report.Add($"H{home} B{zone+1}: {usable.Length} operable passages, {usable.Count(p=>p.core)} linked to encounter; {all.Count(p=>p.zone==zone&&!p.canOperate)} wedged obstructions remain solid.");
            }
        }
        for(int i=0;i<4;i++)director.layouts[i].SetActive(i==0);Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
        if(!EditorSceneManager.SaveScene(s.gameObject.scene))throw new Exception("Could not save audited scene");
        File.WriteAllLines("tmp/passages/operating-space.txt",report);
        return string.Join("\n",report);
    }
}
