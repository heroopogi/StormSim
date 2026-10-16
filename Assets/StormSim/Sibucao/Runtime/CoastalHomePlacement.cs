using System.Linq;
using UnityEngine;
using StormSim.Preparedness;

namespace StormSim.Sibucao
{
    public static class CoastalHomePlacement
    {
        static Bounds BoundsOf(Transform t)
        {
            var renderers=t.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)&&!r.GetComponent<TextMesh>()).ToArray();
            if(renderers.Length==0)return new Bounds(t.position,Vector3.zero);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        static float Ground(Vector3 p,bool includeRaisedFloor=false)
        {
            var hits=Physics.RaycastAll(p+Vector3.up*30,Vector3.down,100,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider.name.StartsWith("Measured terrain")||h.collider.name.StartsWith("Ground ")||(includeRaisedFloor&&h.collider.name.Contains("raised timber floors"))).OrderBy(h=>h.distance).ToArray();
            return hits.Length>0?hits[0].point.y:p.y;
        }
        static bool StandingClear(Vector3 p,Transform platform=null)
        {
            return !Physics.OverlapCapsule(p+Vector3.up*.32f,p+Vector3.up*1.5f,.28f,~0,QueryTriggerInteraction.Ignore).Any(c=>
                !(platform&&c.transform.IsChildOf(platform))&&!c.GetComponentInParent<PreparednessPlayer>()&&!c.GetComponentInParent<SibucaoPlayer>()&&
                c.GetComponentInParent<PreparednessItem>()?.id!="porch-ladder");
        }
        public static void Fit(PreparednessSession prep)
        {
            Physics.SyncTransforms();var root=prep.transform;
            var windowFastener=prep.items.FirstOrDefault(i=>i.id=="fastener-2");
            if(windowFastener){var p=windowFastener.transform.localPosition;p.y=1.85f;windowFastener.transform.localPosition=p;}
            var cable=prep.items.FirstOrDefault(i=>i.id=="cable");
            if(cable){var p=cable.transform.localPosition;p.x=0;p.z=-2.7f;cable.transform.localPosition=p;}
            // Keep the drinking water clear of the fixed kitchen counter.
            for(int n=0;n<10;n++)
            {
                var water=prep.items.FirstOrDefault(i=>i.id=="water-"+n);
                if(!water)continue;
                var p=water.transform.localPosition;
                p.x=-2.15f+(n%3)*.38f;p.z=.4f+(n/3)*.35f;
                water.transform.localPosition=p;
            }
            var session=Object.FindFirstObjectByType<SibucaoSession>();
            var home=session?session.homes.FirstOrDefault(h=>Vector3.Distance(h.transform.position,root.position)<.01f):null;
            var profile=session?session.GetComponent<CoastalAreaProfile>():null;
            int index=home?System.Array.IndexOf(session.homes,home):-1;
            if(profile&&index>=0)
            {
                var desired=profile.yardOffsets!=null&&index<profile.yardOffsets.Length?profile.yardOffsets[index]:Vector3.zero;
                var delta=desired-profile.fittedYardOffset;
                if(delta.sqrMagnitude>.000001f)
                {
                    var group=new System.Collections.Generic.HashSet<Transform>();
                    foreach(Transform t in root)if(t.name.StartsWith("Porch equipment bench")||t.name=="Accessible surface drain"||t.name.StartsWith("Cleanup bin"))group.Add(t);
                    foreach(var item in prep.items.Where(i=>i.id.StartsWith("litter-")||new[]{"gloves","brush","waste-bags","drybox","outdoor-0","outdoor-1","tongs"}.Contains(i.id)))group.Add(item.transform);
                    foreach(var t in group)t.localPosition+=delta;
                    profile.fittedYardOffset=desired;
                }
            }
            var cabinet=home?home.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="TV wooden cabinet"):null;
            var phone=prep.items.FirstOrDefault(i=>i.id=="phone");
            if(cabinet&&phone)
            {
                float front=float.PositiveInfinity;var b=cabinet.localBounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    front=Mathf.Min(front,root.InverseTransformPoint(cabinet.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)))).z);
                var p=phone.transform.localPosition;
                p.z=front+PreparednessSurfacePlacement.BoundsIn(phone,root).extents.z+.02f;
                phone.transform.localPosition=p;
            }
            var setup=prep.items.FirstOrDefault(i=>i.id=="place-ladder");
            if(setup)
            {
                if(profile&&index>=0&&profile.ladderWallOffsets!=null&&index<profile.ladderWallOffsets.Length)
                    foreach(var id in new[]{"place-ladder","ladder-access","repair-roof-panel"})
                    {var item=prep.items.FirstOrDefault(i=>i.id==id);if(item){var p=item.transform.localPosition;p.z=profile.ladderWallOffsets[index];item.transform.localPosition=p;}}
                var bounds=BoundsOf(setup.transform);var delta=Vector3.up*(Ground(bounds.center,true)+.005f-bounds.min.y);
                setup.transform.position+=delta;
                var task=setup.GetComponent<PreparednessHouseTask>();
                if(task&&task.after)
                {
                    task.after.transform.position+=delta;
                    var ladder=task.after.GetComponent<PreparednessLadder>();
                    if(ladder)
                    {
                        // The copied ladder's serialized destinations belong to Sibucao.
                        // Derive both endpoints from this home's actual platform instead.
                        var p=ladder.transform.position-root.right*.65f;
                        p.y=Ground(p,true)+.05f;ladder.basePosition=p;
                        var platform=ladder.transform.Find("Working platform");
                        var roof=prep.items.FirstOrDefault(i=>i.id=="repair-roof-panel");
                        if(platform&&roof&&profile)
                        {
                            // Fit the supported ladder height to the selected house's eave.
                            float previous=Mathf.Max(.01f,profile.fittedLadderScale);
                            float baseHeight=(BoundsOf(platform).max.y+.04f-ladder.transform.position.y)/previous;
                            float factor=Mathf.Max(1,(roof.transform.position.y-ladder.transform.position.y-1.75f)/Mathf.Max(.1f,baseHeight));
                            var scale=ladder.transform.localScale;scale.y*=factor/previous;ladder.transform.localScale=scale;profile.fittedLadderScale=factor;
                        }
                        Physics.SyncTransforms();
                        if(!StandingClear(p))
                            foreach(float distance in new[]{.85f,1.05f,1.25f})
                            {
                                bool found=false;
                                foreach(float along in new[]{0f,.4f,-.4f,.8f,-.8f})
                                {var candidate=ladder.transform.position-root.right*distance+root.forward*along;candidate.y=Ground(candidate,true)+.05f;if(!StandingClear(candidate))continue;ladder.basePosition=candidate;found=true;break;}
                                if(found)break;
                            }
                        if(platform)
                        {
                            var top=BoundsOf(platform);var center=new Vector3(top.center.x,top.max.y+.04f,top.center.z);ladder.workingPosition=center;
                            foreach(float outward in new[]{0f,.12f,.22f})
                            {var candidate=center-root.right*outward;if(!StandingClear(candidate,ladder.transform))continue;ladder.workingPosition=candidate;break;}
                        }
                    }
                }
            }
            var bench=root.Find("Porch equipment bench");
            if(bench)
            {
                var legs=root.Cast<Transform>().Where(t=>t.name=="Porch equipment bench leg").ToArray();
                float ground=legs.Max(t=>Ground(t.position));
                float offset=ground+.76f-BoundsOf(bench).max.y;
                bench.position+=Vector3.up*offset;
                foreach(var id in new[]{"gloves","brush","waste-bags","drybox"})
                {var item=prep.items.FirstOrDefault(i=>i.id==id);if(item)item.transform.position+=Vector3.up*offset;}
                foreach(var leg in legs)
                {
                    float bottom=Ground(leg.position)-.025f,top=BoundsOf(bench).min.y;
                    var bounds=BoundsOf(leg);var scale=leg.localScale;
                    scale.y*=Mathf.Max(.05f,top-bottom)/Mathf.Max(.001f,bounds.size.y);leg.localScale=scale;
                    leg.position+=Vector3.up*((bottom+top)*.5f-BoundsOf(leg).center.y);
                }
            }
            foreach(var id in new[]{"outdoor-0","outdoor-1","tongs","porch-ladder"})
            {var item=prep.items.FirstOrDefault(i=>i.id==id);if(item){var b=BoundsOf(item.transform);item.transform.position+=Vector3.up*(Ground(b.center)+.005f-b.min.y);}}
            var drain=root.Find("Accessible surface drain");
            if(drain)
            {
                var bounds=BoundsOf(drain);drain.position+=Vector3.up*(Ground(bounds.center)+.005f-bounds.min.y);
                var top=BoundsOf(drain).max.y;
                foreach(var item in prep.items.Where(i=>i.id.StartsWith("litter-")))
                {var b=BoundsOf(item.transform);item.transform.position+=Vector3.up*(top+.003f-b.min.y);}
            }
            var bin=root.Find("Cleanup bin");
            if(bin)
            {
                var bounds=BoundsOf(bin);float shift=Ground(bounds.center)+.005f-bounds.min.y;bin.position+=Vector3.up*shift;
                var zone=root.Find("Cleanup bin — collect dry litter");if(zone)zone.position+=Vector3.up*shift;
            }
            foreach(Transform t in root)if(t.name.StartsWith("Clear accessible drain grate"))
            {var bounds=BoundsOf(t);t.position+=Vector3.up*(Ground(bounds.center,true)+.005f-bounds.min.y);}
            Physics.SyncTransforms();
            foreach(var item in prep.items.Where(i=>i.portable&&i.transform.IsChildOf(root)))item.SynchronizePose();
        }
    }
}

