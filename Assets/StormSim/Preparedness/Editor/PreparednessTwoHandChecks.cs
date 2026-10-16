using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace StormSim.Preparedness.Editor
{
    public static class PreparednessTwoHandChecks
    {
        public static string Run()
        {
            var b=UnityEngine.Object.FindFirstObjectByType<SibucaoPreparationBridge>();var s=b.preparation;var p=s.player;
            if(!Application.isPlaying||!b.Active)throw new Exception("Start preparation first.");
            var before=s.Capture();var results=new List<string>();bool existed=File.Exists(s.SavePath);string previous=existed?File.ReadAllText(s.SavePath):null;
            void Check(bool value,string label){if(!value)throw new Exception(label+": "+s.message);results.Add(label);}
            PreparednessItem I(string id)=>s.items.First(i=>i.id==id);
            void BagAtHand(){s.Bag.transform.position=p.view.transform.TransformPoint(new Vector3(.4f,-.69f,.78f));s.Bag.transform.rotation=Quaternion.identity;s.Bag.SynchronizePose();Physics.SyncTransforms();}
            try
            {
                s.Restart();s.SetPhase(PrepPhase.Preparing);Check(!p.Grab(s.Bag),"Closed closet still blocks bag retrieval");s.Use(I("closet-door"));
                Check(p.Grab(s.Bag)&&p.carriedBag==s.Bag&&!p.held,"Bag occupies the right hand while the supply hand stays free");BagAtHand();
                p.ToggleBag();Check(!s.Bag.closed,"Bag can be opened while carried");s.Use(I("chest-lid"));
                Check(p.Grab(I("radio"))&&p.carriedBag==s.Bag&&p.held==I("radio"),"Supply and bag can be carried simultaneously");
                var saved=s.Capture();Check(saved.bagInRightHand&&saved.heldId=="radio"&&s.Restore(JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(saved)))&&p.carriedBag&&p.held==I("radio"),"Save/load restores both occupied hands");BagAtHand();
                Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.P));InputSystem.Update();
                typeof(PreparednessPlayer).GetMethod("UpdateDesktop",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();Check(p.Packing,"P starts a visible packing motion");
                var fixedUpdate=typeof(PreparednessPlayer).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);for(int n=0;n<18;n++)fixedUpdate.Invoke(p,null);
                Check(I("radio").IsPacked&&!p.held&&p.carriedBag,"Packing before sleep retains the bag in the right hand");
                s.Unpack();Check(p.held==I("radio")&&p.carriedBag,"Unpacking uses the free hand");
                s.Bag.closed=true;Check(!p.BeginPacking(),"Closed bag blocks insertion");s.Bag.closed=false;
                var visual=s.Bag.GetComponent<PreparednessBagVisual>();Check(visual&&visual.Contains(visual.Opening),"Bag has an accessible physical insertion opening");
                p.held.transform.position+=visual.Opening-p.held.WorldBounds().center;p.held.SynchronizePose();Check(p.TryInsertAtOpening()&&I("radio").IsPacked,"Releasing a supply into the opening packs it");
                s.Use(I("chest-lid"));I("chest-lid").closed=false;
                p.Grab(I("flashlight"));p.held.transform.position=visual.Opening+Vector3.left*1f;p.held.SynchronizePose();Check(!p.TryInsertAtOpening(),"Releasing outside the opening does not auto-pack");p.Release();I("flashlight").SetPhysics(false);
                s.advisoryRead=true;var npc=s.items.First(i=>i.id.StartsWith("family-")).GetComponent<PreparednessNeighbor>();
                npc.Talk(s);Check(npc.GetComponent<PreparednessItem>().prepared&&s.WarnedHouseholds==1&&s.PeopleReached==4,"One confirmed warning records one household and four people reached");
                npc.Talk(s);npc.Talk(s);Check(s.WarnedHouseholds==1&&s.PeopleReached==4,"Repeated dialogue does not duplicate warning credit");
                var progress=s.Capture();s.Restore(progress);Check(s.WarnedHouseholds==1,"Confirmed warnings survive save/load");
                s.Finish(true);Check(s.phase==PrepPhase.Preparing,"Going outside to warn neighbors does not start the storm");
                s.Finish(false);Check(s.phase==PrepPhase.Preparing,"Bed first displays a packing and warning summary");s.Finish(false);Check(s.phase==PrepPhase.Fade&&!p.carriedBag&&s.Bag.closed,"Sleep stages and closes the carried bag before fading");
                s.CompleteTransition();Check(s.phase==PrepPhase.ReadyForStorm&&I("radio").IsPacked&&s.WarnedHouseholds==1,"Sleeping leads directly to storm handoff with packing and warnings preserved");
                var report="PASS: "+results.Count+" two-hand flow checks\n"+string.Join("\n",results);File.WriteAllText("output/preparedness/two-hand-checks.txt",report);return report;
            }
            finally{s.Restore(before);if(existed)File.WriteAllText(s.SavePath,previous);else if(File.Exists(s.SavePath))File.Delete(s.SavePath);}
        }
    }
}
