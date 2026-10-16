using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    public static class PreparednessUsabilityChecks
    {
        static PrepSave physicsExpected;
        public static string BeginPhysics()
        {
            var s=Object.FindFirstObjectByType<SibucaoPreparationBridge>().preparation;s.Restart();s.SetPhase(PrepPhase.Preparing);
            var radio=s.items.First(i=>i.id=="radio");radio.held=false;radio.SetCarried(false);radio.transform.SetParent(s.transform,true);
            radio.transform.position=s.homeStart.position+Vector3.up*.55f;radio.ResolveReleaseOverlap(null);radio.SetPhysics(true);
            // Reproduce the old failure: an overlap/depenetration spike attempted to
            // launch a small prop out of the house.
            radio.Body.linearVelocity=Vector3.up*18+Vector3.right*8;
            physicsExpected=s.Capture();return "Physics check started: a forced launch was applied to the radio; every other prop remains locked at its restored pose.";
        }
        public static string EndPhysics()
        {
            var s=Object.FindFirstObjectByType<SibucaoPreparationBridge>().preparation;var radio=s.items.First(i=>i.id=="radio");
            var drift=physicsExpected.items.Where(saved=>saved.id!="radio"&&s.items.First(i=>i.id==saved.id).portable).Where(saved=>Vector3.Distance(saved.position,s.items.First(i=>i.id==saved.id).transform.position)>.025f).Select(saved=>saved.id).ToArray();
            if(drift.Length>0)throw new Exception("Stored props drifted: "+string.Join(",",drift));
            var bounds=radio.WorldBounds();
            bool supported=Physics.RaycastAll(bounds.center+Vector3.up*.04f,Vector3.down,bounds.extents.y+.24f,~0,QueryTriggerInteraction.Ignore)
                .Any(hit=>!hit.collider.transform.IsChildOf(radio.transform)&&Mathf.Abs(hit.point.y-bounds.min.y)<.16f);
            if(!radio.Body.isKinematic||radio.Body.linearVelocity.magnitude>.05f||radio.Body.angularVelocity.magnitude>.05f||!supported)throw new Exception("Launched radio was not recovered and locked on a physical surface.");
            string result="PASS: forced launch was contained; released radio settled and locked on the floor; every other restored prop remained stable across physics frames.";
            File.WriteAllText("output/preparedness/physics-checks.txt",result);s.Restart();return result;
        }
        public static string Run()=>PreparednessTwoHandChecks.Run();
        public static string Doors()
        {
            var bridge=Object.FindFirstObjectByType<SibucaoPreparationBridge>();var p=bridge.preparation.player;var was=p.gameObject.activeSelf;p.gameObject.SetActive(true);var pos=p.transform.position;var rot=p.transform.rotation;
            var failures=new List<string>();var motor=p.GetComponent<CharacterController>();
            foreach(var h in Object.FindObjectsByType<PreparednessHomeInterior>(FindObjectsSortMode.None))
            {p.Warp(h.entrance.position-h.entrance.forward*1.15f+Vector3.up*.10f,h.entrance.rotation);Physics.SyncTransforms();for(int n=0;n<60;n++)motor.Move(h.entrance.forward*.04f+Vector3.down*.008f);var local=h.entrance.InverseTransformPoint(p.transform.position);if(local.z<.85f)failures.Add(h.name+" z="+local.z.ToString("F2"));}
            p.Warp(pos,rot);p.gameObject.SetActive(was);File.WriteAllText("output/preparedness/doorway-failures.txt",string.Join("\n",failures));return "Failed entrances: "+failures.Count+"\n"+string.Join("\n",failures);
        }
    }
}
