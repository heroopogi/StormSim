using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Preparedness.Editor
{
    public static class PreparednessChecks
    {
        public static string Run()
        {
            if(!Application.isPlaying)throw new Exception("Run in Play mode after Start.");
            var s=UnityEngine.Object.FindFirstObjectByType<PreparednessSession>();var p=s.player;var baseline=s.Capture();var passed=new List<string>();
            void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);passed.Add(label);}
            PreparednessItem Item(string id)=>s.items.First(i=>i.id==id);
            bool Place(string id,string zone)
            {
                p.Release();var item=Item(id);item.gameObject.SetActive(true);item.transform.SetParent(s.transform,true);item.transform.position=Item(zone).transform.position+Vector3.up*.3f;
                item.storageDoor=null;if(zone=="bag-zone")item.gathered=true;
                if(!p.Grab(item))throw new Exception("Could not grab "+id);return s.Store(item,Item(zone));
            }
            try
            {
                s.SetPhase(PrepPhase.MorningPacking);s.bagRetrieved=true;
                foreach(var door in s.items.Where(i=>i.kind==PrepKind.StorageDoor)){door.closed=false;door.ApplyVisual();}
                Check(s.items.Length==s.items.Select(i=>i.id).Distinct().Count(),"Unique save IDs");
                Check(Place("water-0","supply-zone")&&!s.decisions[0],"One 5 L container cannot satisfy household water target");
                for(int i=1;i<10;i++)Place("water-"+i,"supply-zone");Check(s.decisions[0],"50 L storage satisfies 45.4 L baseline");
                Check(!Place("water-0","bag-zone"),"Household water cannot be packed as an unlimited bag item");p.Release();
                Place("flashlight","bag-zone");Check(!s.decisions[2],"Untested flashlight does not pass");
                p.Warp(s.Bag.transform.position+Vector3.back,Quaternion.identity);s.Unpack();p.Release();
                p.Grab(Item("cells-0"));s.Use(Item("flashlight"),p.held);p.Release();s.Use(Item("flashlight"));
                Place("flashlight","bag-zone");Check(s.decisions[2]&&Item("cells-0").cells==0,"Battery pair consumed and tested flashlight verified");
                s.Use(Item("documents"));Place("documents","bag-zone");Check(s.decisions[7],"Sealed document pouch verifies");
                p.Warp(s.Bag.transform.position+Vector3.back,Quaternion.identity);s.Unpack();s.Use(Item("documents"));Check(!s.decisions[7],"Reopened/unpacked documents invalidate completion");p.Release();
                s.Bag.closed=true;Check(!Place("hygiene","bag-zone"),"Closed bag rejects packing");p.Release();s.Bag.closed=false;
                Place("phone","charger");Item("phone").charge=50;Place("phone","bag-zone");Check(!s.decisions[8],"Half charged phone not credited as fully charged");
                Check(!p.Grab(Item("electronics")),"Connected appliance cannot be carried");s.Use(Item("electronics"));Check(Place("electronics","high-zone")&&s.decisions[12],"Disconnected raised appliance verifies");
                Check(!p.Grab(s.items.First(i=>i.kind==PrepKind.Litter)),"Bare-hand litter collection rejected");
                var snapshot=s.Capture();var json=JsonUtility.ToJson(snapshot);s.primaryRoute=!s.primaryRoute;
                Check(s.Restore(JsonUtility.FromJson<PrepSave>(json)),"Save data JSON round-trip accepted");
                Check(s.primaryRoute==snapshot.primaryRoute&&Item("phone").charge==50&&Item("flashlight").IsPacked,"Partial charge, routes and packed items survive restore");
                Check(Item("bag-zone").transform.parent==s.Bag.transform,"Bag storage follows restored bag");
                p.Release();s.Bag.closed=true;p.Grab(s.Bag);s.SetPhase(PrepPhase.ReadyForStorm);var carried=s.Capture();p.Release();Check(s.Restore(carried)&&p.held==s.Bag&&s.decisions[11],"Carried go-bag survives handoff save restore");s.SetPhase(PrepPhase.Preparing);p.Release();
                var invalid=JsonUtility.FromJson<PrepSave>(json);invalid.version=99;Check(!s.Restore(invalid),"Unsupported save version rejected without mutation");
                s.advisoryRead=true;s.IssueEvacuation();Check(!s.advisoryRead,"New advisory invalidates previous acknowledgement");s.advisoryRead=true;s.Finish(false);Check(s.CanPrepare,"Sleep blocked during evacuation order");
                p.Release();Check(!p.Grab(Item("outdoor-0")),"Outdoor tasks blocked after evacuation advisory");
                s.Restore(baseline);s.Restart();Check(!s.decisions.Any(v=>v),"Restart clears preparation progress");
                var report="PASS: "+passed.Count+" checks\n"+string.Join("\n",passed);File.WriteAllText("Documentation/TORMSIM_PREPAREDNESS_CHECKS.txt",report);return report;
            }
            finally{s.Restore(baseline);}
        }
    }
}
