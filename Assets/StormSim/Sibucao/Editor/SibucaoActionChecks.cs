using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoActionChecks
    {
        static void Require(bool value,string text){if(!value)throw new Exception(text);}
        public static string Run()
        {
            Require(Application.isPlaying,"Run in Play mode");var s=SibucaoSession.Instance;s.StartRun();s.state.homeSeconds=90;s.SetMode(SibucaoMode.Playing);var m=s.physicalDecisions;
            var pass=new List<string>();
            Action<int> reset=i=>{m.Data.results[i]=-1;m.Data.started[i]=s.state.elapsed;m.Data.attempts[i]=new PhysicalAttempt();m.stations[i].bodyEnteredWater=false;};
            foreach(int i in new[]{0,1,2,3,6,8})
            {
                reset(i);var st=m.stations[i];int r=i==1||i==2?1:0;if(i==6)r=2;
                st.EnterRoute(r,2,Vector3.forward*5);Require(!st.Resolved,"Exit alone resolved B"+(i+1));
                st.EnterRoute(r,0,Vector3.zero);st.EnterRoute(r,1,Vector3.forward*2);Require(!st.Resolved,"Passage alone resolved B"+(i+1));
                st.EnterRoute(r,2,Vector3.forward*5);Require(st.Resolved,"Ordered route failed B"+(i+1));s.SetMode(SibucaoMode.Playing);
            }
            pass.Add("Six traversal decisions require approach, passage and exit in order");
            reset(0);s.state.elapsed+=120;m.stations[0].SendMessage("Update");Require(!m.stations[0].Resolved,"Waiting completed the water decision");pass.Add("Waiting does not complete water assessment");
            var bag=Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include).First(x=>x.kind==PhysicalItemKind.Bag&&x.transform.IsChildOf(s.home.transform));
            var supply=Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include).First(x=>x.kind==PhysicalItemKind.Supply&&x.transform.IsChildOf(s.home.transform));
            m.Data.backpackId=null;s.inventory.Collect(supply);Require(supply.gameObject.activeSelf,"No-bag pickup removed supply");
            s.inventory.Collect(bag);s.inventory.Collect(supply);Require(s.inventory.HasBag&&s.inventory.Count>0&&!supply.gameObject.activeSelf,"Inventory direct pickup failed");
            Require(!SibucaoInventory.Storable(m.stations[0].GetComponentInChildren<PhysicalGrabbable>()),"Probe enters routine inventory");pass.Add("Bag required; routine supply stores immediately; probes excluded");
            var packedKit=Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include).First(x=>x.kind==PhysicalItemKind.FirstAid);
            Require(!packedKit.PhysicalTool,"Routine medical pickup incorrectly allows dragging before collection");
            s.inventory.Collect(packedKit);int kitSlot=Array.IndexOf(m.Data.inventorySlots,packedKit.persistentId);s.inventory.Selected=kitSlot;s.inventory.EquipSelected();
            Require(packedKit.PhysicalTool&&packedKit.gameObject.activeSelf&&!packedKit.IsHeld&&s.inventory.Slot(kitSlot)==null,"Taking out medical kit duplicated storage or automatically gripped it");
            pass.Add("Medical kit uses direct pickup, then separate take-out and mouse grip");
            reset(0);var probe=m.stations[0].GetComponentInChildren<PhysicalGrabbable>();probe.holder=m.Grabber;
            m.stations[0].bodyEnteredWater=true;m.stations[0].Probe(probe);Require(m.stations[0].Attempt.probeSamples==0,"Late probing accepted after body entry");
            m.stations[0].bodyEnteredWater=false;m.stations[0].Probe(probe);probe.transform.position+=Vector3.right*.4f;m.stations[0].Probe(probe);Require(m.stations[0].Attempt.probeSamples==1,"Same-location probe accepted twice");
            probe.transform.position+=Vector3.right*.3f;m.stations[0].Probe(probe);probe.holder=null;
            Require(m.stations[0].Attempt.probeSamples==2&&!m.stations[0].Resolved,"Probe samples should await actual traversal");pass.Add("Two separated probe positions required before crossing; late probing rejected");
            reset(4);Require(m.SpillBag(m.stations[4]),"Spill failed");var b5=m.stations[4];b5.spill.SetActive(true);
            var strap=b5.spill.GetComponentsInChildren<PhysicalGrabbable>().First(x=>x.kind==PhysicalItemKind.Strap);var socket=b5.spill.GetComponentsInChildren<PhysicalDecisionSensor>().First(x=>x.kind==PhysicalSensorKind.StrapSocket);
            strap.transform.rotation=socket.transform.rotation;strap.transform.position=socket.transform.position-strap.transform.TransformVector(strap.tip.localPosition);strap.gripTravel=.3f;Physics.SyncTransforms();
            strap.holder=m.Grabber;socket.Contact(strap.GetComponent<Collider>());Require(!b5.Resolved,"Socket contact auto-completed strap");strap.holder=null;
            strap.transform.position+=Vector3.right*.15f;Physics.SyncTransforms();Require(!b5.ReleaseAtTarget(strap)&&!b5.Resolved,"Strap near miss accepted");
            strap.transform.position=socket.transform.position-strap.transform.TransformVector(strap.tip.localPosition);Physics.SyncTransforms();Require(b5.ReleaseAtTarget(strap)&&b5.Resolved,"Aligned release rejected");pass.Add("Strap rejects contact/near miss, accepts aligned deliberate release");
            foreach(int i in new[]{5,7})
            {
                reset(i);var st=m.stations[i];var item=st.assist;var dst=st.GetComponentsInChildren<PhysicalDecisionSensor>().First(x=>x.kind==PhysicalSensorKind.AssistDestination);
                st.Attempt.assistGripped=true;st.Attempt.assistDistance=0;item.transform.position=dst.transform.position-Vector3.up;Physics.SyncTransforms();
                Require(!st.ReleaseAtTarget(item),"Assist ignored physical movement requirement");st.Attempt.assistDistance=2;
                Require(st.ReleaseAtTarget(item)&&st.Resolved,"Assist release failed B"+(i+1));
            }
            pass.Add("Both assists require grip, physical movement and full-body release destination without an artificial ramp");
            reset(7);var medical=m.stations[7].GetComponentsInChildren<PhysicalDecisionSensor>().First(x=>x.kind==PhysicalSensorKind.MedicalContact);var kit=Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include).First(x=>x.kind==PhysicalItemKind.FirstAid);kit.gameObject.SetActive(true);kit.transform.position=medical.transform.position;kit.holder=m.Grabber;Physics.SyncTransforms();medical.Contact(kit.GetComponent<Collider>());Require(!kit.delivered,"Medical proximity consumed kit");kit.holder=null;kit.gripTravel=.3f;Require(m.stations[7].ReleaseAtTarget(kit)&&kit.delivered&&!m.stations[7].Resolved,"Medical placement failed");pass.Add("First aid requires placement/release; treatment alone does not auto-assist");
            var f=s.GetComponent<FloodSimulationManager>();s.state.homeSeconds=s.home.wakeDuration+s.home.newsDuration+6+s.home.preparationDuration+s.home.floodRiseDuration;var sample=f.groundSamples.First(p=>Mathf.Abs(p.z-s.home.floodSurface.position.z)>s.home.floodSize.y/2+10);Require(f.TrySurface(sample,out float water,out _),"Route water absent outside home");
            float[] depths={.1f,.3f,.7f,1.2f,1.7f};for(int i=0;i<5;i++){var p=sample;p.y=water-depths[i];Require((int)f.GetWaterTier(p)==i,"Tier depth mismatch "+i);}pass.Add("Route flood returns all five tiers outside home basin");
            reset(2);m.stations[2].EnterRoute(1,0,Vector3.zero);m.stations[2].Attempt.talked=true;var json=JsonUtility.ToJson(m.Data);var restored=JsonUtility.FromJson<PhysicalRunData>(json);Require(restored.attempts[2].routeSteps[1]==1&&restored.attempts[2].talked,"Sub-step save lost");pass.Add("Sub-step state survives save round trip");
            Require(Object.FindObjectsByType<PlaceholderCharacter>(FindObjectsInactive.Include).Length==0,"Live placeholders remain");pass.Add("No PLACEHOLDER character instances in live scene");
            var report="PASS\n"+string.Join("\n",pass);System.IO.File.WriteAllText("Documentation/SIBUCAO_ACTION_CHECKS.txt",report);s.StartRun();return report;
        }
    }
}
