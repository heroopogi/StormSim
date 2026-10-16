#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    public sealed class SibucaoSwimProbe:MonoBehaviour
    {
        public string status="Idle";public Vector3 deepPoint;
        public List<string> log=new List<string>();
        bool holdStorm;float fixedStormClock;
        SibucaoSession S=>SibucaoSession.Instance;
        FloodSimulationManager F=>S.GetComponent<FloodSimulationManager>();
        public static SibucaoSwimProbe Get(){var p=FindFirstObjectByType<SibucaoSwimProbe>();return p?p:new GameObject("EDITOR ONLY swimming probe").AddComponent<SibucaoSwimProbe>();}
        public void Run(){SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;StartCoroutine(Guard(Check()));}
        void LateUpdate(){if(holdStorm&&S)S.state.homeSeconds=fixedStormClock;}
        void Record(string text){status=text;log.Add(text);System.IO.File.WriteAllLines("Documentation/SIBUCAO_SWIMMING_PROBE.txt",log);}
        void Require(bool ok,string text){if(!ok)throw new Exception(text);Record("PASS "+text);}
        IEnumerator Guard(IEnumerator routine)
        {
            while(true){object current=null;bool more=false;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception e){Record("FAIL "+e.Message);SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.Dive=SibucaoEditorInput.Ascend=false;yield break;}if(!more)break;yield return current;}
            Record("PASS swimming checks finished; driven through production keyboard-input branches, not native keyboard automation.");
        }
        IEnumerator Check()
        {
            S.homes=new[]{S.homes[0]};S.StartRun();S.state.homeSeconds=90;yield return new WaitForSeconds(1);
            Require(!S.player.swimming.IsSwimming&&S.player.GetComponent<CharacterController>().height>1.7f,"dry walking capsule and no underwater state");
            Record("Building flood to 170 scenario seconds");S.state.homeSeconds=S.home.wakeDuration+S.home.newsDuration+S.home.standDuration+S.home.preparationDuration+170;
            float deadline=Time.realtimeSinceStartup+90;while(F.SimulatedSeconds<169&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(F.SimulatedSeconds>=169,"flood clock catches up after save/test clock change");
            fixedStormClock=S.state.homeSeconds;holdStorm=true;
            while(F.IsCatchingUp)yield return null;
            Record("Storm stage held for repeatable locomotion checks; buoyancy, collision, animation and current remain live.");
            Require(Math.Abs(F.InitialVolume+F.RainVolume-F.DrainedVolume-F.WaterVolume)<F.RainVolume*.0001,"water volume conserved (initial storage plus rain minus coastal outflow), error "+Math.Abs(F.InitialVolume+F.RainVolume-F.DrainedVolume-F.WaterVolume).ToString("F4")+" m3");
            Require(S.homes.All(h=>!h.floodSurface.gameObject.activeSelf),"obsolete house water planes disabled");
            var candidates=F.groundSamples.Where(p=>F.GetWaterDepth(p)>2f&&S.route.Progress(p)<S.route.length*.82f).OrderByDescending(p=>F.GetWaterDepth(p)).ToArray();
            Record("Route depths: "+string.Join("; ",F.groundSamples.Where((p,i)=>i%10==0).Select(p=>p+" = "+F.GetWaterDepth(p).ToString("F2"))));
            Require(candidates.Length>0,"deep flood exists on the actual route");deepPoint=candidates[0];
            F.TrySurface(deepPoint,out float surface,out _);var position=deepPoint;position.y=surface-1.45f;S.player.Teleport(position,Vector3.forward);
            deadline=Time.realtimeSinceStartup+20;while(!S.player.swimming.IsSwimming&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSeconds(1);
            Require(S.player.swimming.IsSwimming,"automatic swim entry at chest depth");
            Require(!S.player.swimming.IsUnderwater&&Mathf.Abs(S.player.view.transform.position.y-S.player.swimming.SurfaceHeight-.16f)<.2f,"buoyancy holds eyes above surface without teleporting");
            var start=S.player.transform.position;SibucaoEditorInput.Dive=true;yield return new WaitForSeconds(2);SibucaoEditorInput.Dive=false;yield return new WaitForSeconds(.2f);
            Require(S.player.transform.position.y<start.y-.5f&&S.player.swimming.IsUnderwater,"held Ctrl/C dives with downward movement");
            var underwater=S.player.view.GetComponent<SibucaoUnderwater>();Require(underwater.Blend>.8f&&underwater.FilteredSources>0,"submerged head activates sediment visibility and audio filters");
            start=S.player.transform.position;SibucaoEditorInput.Move=Vector2.up;yield return new WaitForSeconds(1.5f);SibucaoEditorInput.Move=Vector2.zero;
            Require(Vector3.Distance(start,S.player.transform.position)>.65f,"underwater WASD propels the collision controller");
            start=S.player.transform.position;S.Pause();yield return new WaitForSecondsRealtime(.5f);Require(Vector3.Distance(start,S.player.transform.position)<.01f,"pause freezes swimming");S.SetMode(SibucaoMode.Playing);
            SibucaoEditorInput.Ascend=true;yield return new WaitForSeconds(4);SibucaoEditorInput.Ascend=false;yield return new WaitForSeconds(.5f);
            Require(!S.player.swimming.IsUnderwater&&underwater.Blend<.05f,"held Space resurfaces; clear vision and normal audio restore");
            Require(S.player.GetComponent<CharacterController>().height<1.3f,"swimming uses bent-leg collision capsule");
            var hand=S.player.hands.Find("Left hand");var handStart=hand.localPosition;yield return new WaitForSeconds(.3f);Require(Vector3.Distance(handStart,hand.localPosition)>.01f,"first-person hands visibly tread water");
            var dry=F.groundSamples.OrderBy(p=>F.GetWaterDepth(p)).First();S.player.Teleport(dry+Vector3.up*.1f,Vector3.forward);yield return new WaitForSeconds(.5f);
            Require(!S.player.swimming.IsSwimming&&S.player.GetComponent<CharacterController>().height>1.7f,"dry-ground transition restores walking capsule");
            var residents=FindObjectsByType<SibucaoResident>(FindObjectsSortMode.None);Record("Background residents swimming now: "+residents.Count(r=>r.Swimming)+" / "+residents.Length);
            Require(F.SurfaceMesh&&F.SurfaceMesh.vertexCount==F.terrain.ground.Length,"rendered flood uses the baked terrain grid");
            Require(S.homes.All(h=>h.GetComponentsInChildren<TextMesh>(true).All(t=>t.name!="Calendar")),"all home calendars are mesh props without calendar TextMesh");
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
    }
}
#endif
