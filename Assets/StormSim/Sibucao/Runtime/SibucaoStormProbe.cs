#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    public sealed class SibucaoStormProbe:MonoBehaviour
    {
        public string status="Idle";
        public List<string> log=new List<string>();
        public bool holdClock;
        public float clock;
        SibucaoSession S=>SibucaoSession.Instance;
        FloodSimulationManager F=>S.GetComponent<FloodSimulationManager>();
        public static SibucaoStormProbe Get(){var p=FindFirstObjectByType<SibucaoStormProbe>();return p?p:new GameObject("EDITOR ONLY storm and refuge probe").AddComponent<SibucaoStormProbe>();}
        public void Run(){StartCoroutine(Guard(Check()));}
        void LateUpdate(){if(holdClock&&S)S.state.homeSeconds=clock;}
        void Record(string value){status=value;log.Add(value);System.IO.File.WriteAllLines("Documentation/SIBUCAO_STORM_REFUGE_PROBE.txt",log);}
        void Require(bool ok,string value){if(!ok)throw new Exception(value);Record("PASS "+value);}
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            while(stack.Count>0)
            {
                object value=null;bool more=false;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}
                catch(Exception e){Record("FAIL "+e.Message);yield break;}
                if(!more){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
            Record("PASS storm, refuge and repeatability checks complete. Scripted Editor play test; not participant/VR evaluation.");
        }
        IEnumerator Stage(float seconds)
        {
            clock=SibucaoStormTimeline.FloodStart(S.home)+seconds;holdClock=true;
            S.state.homeSeconds=clock;float deadline=Time.realtimeSinceStartup+120;
            while(F.SimulatedSeconds<seconds-.15f&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(F.SimulatedSeconds>=seconds-.15f,"flood advanced to "+seconds+" s");
            yield return new WaitForSeconds(.3f);
            int dry=0;float max=0;
            for(float x=-28;x<=94;x+=2)for(float z=596;z<=704;z+=2)
            {
                var p=new Vector3(x,-1.5f,z);if(!F.dryRefuge.Contains(p))continue;dry++;
                RequireDry(p);
            }
            foreach(var p in F.groundSamples)max=Mathf.Max(max,F.GetWaterDepth(p));
            var vertices=F.SurfaceMesh.vertices;var triangles=F.SurfaceMesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var p=F.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                if(F.dryRefuge.Contains(p))throw new Exception("Rendered water triangle inside refuge at "+p);
            }
            Require(Math.Abs(F.InitialVolume+F.RainVolume-F.DrainedVolume-F.WaterVolume)<Math.Max(1,F.RainVolume*.0001),"mass balance within 0.01 percent");
            Record("PASS stage "+seconds+" s: "+dry+" campus points dry; no water triangles; rate "+F.RainfallRate.ToString("F5")+" scenario m/s; route max depth "+max.ToString("F2")+" m; rain volume "+F.RainVolume.ToString("F2")+" m3; CA last/peak step "+F.LastStepMilliseconds.ToString("F2")+" / "+F.PeakStepMilliseconds.ToString("F2")+" ms");
        }
        void RequireDry(Vector3 p)
        {
            if(F.TrySurface(p,out _,out _)||F.GetWaterDepth(p)!=0||F.GetWaterTier(p)!=WaterTier.Tier1||F.GetFlowVelocity(p)!=Vector3.zero)throw new Exception("Wet refuge query at "+p);
        }
        IEnumerator Check()
        {
            var other=FindFirstObjectByType<SibucaoPlaythroughProbe>();if(other)Destroy(other);
            S.homes=new[]{S.homes[0]};S.StartRun();clock=90;holdClock=true;S.state.homeSeconds=clock;
            yield return new WaitForSeconds(1);
            var residents=FindObjectsByType<SibucaoResident>(FindObjectsSortMode.None).Where(r=>r.distributeFromCurrentHome).ToArray();
            Require(residents.Length>=32,"background population "+residents.Length);
            Require(residents.All(r=>r.casting&&r.casting.animator.avatar.isValid&&r.casting.animator.avatar.isHuman&&r.casting.idle&&r.casting.walk),"every background resident has a valid Humanoid avatar and Idle/Walk clips");
            yield return new WaitForSeconds(3);
            int moving=residents.Count(r=>r.casting.MoveSpeed>.1f),running=residents.Count(r=>r.casting.UsingPanicLocomotion&&r.casting.MoveSpeed>.1f);
            Require(moving>16&&running>0,"moving background residents "+moving+", using owned run clip "+running);
            float duration=S.home.floodRiseDuration;
            yield return Stage(duration/3);double early=F.RainVolume;
            yield return Stage(duration*2/3);double middle=F.RainVolume;
            yield return Stage(duration);double peakRain=F.RainVolume,peakWater=F.WaterVolume,peakDrain=F.DrainedVolume;
            Require(peakRain-middle>(middle-early)&&middle-early>early,"rain added per equal interval accelerates: "+early.ToString("F0")+", "+(middle-early).ToString("F0")+", "+(peakRain-middle).ToString("F0")+" m3");
            yield return Stage(duration*2);
            Require(S.weather.Intensity>1.25f,"rain visuals/audio intensified to "+S.weather.Intensity.ToString("F2"));
            // Repeat the same fixed-step rainfall/terrain case; visual wave phase does not affect volume.
            clock=90;S.state.homeSeconds=clock;yield return null;yield return null;yield return null;
            Require(F.SimulatedSeconds<.1f,"new scenario clock resets accumulated water");
            yield return Stage(duration);
            Require(Math.Abs(F.RainVolume-peakRain)<.01&&Math.Abs(F.WaterVolume-peakWater)<.01&&Math.Abs(F.DrainedVolume-peakDrain)<.01,"identical initial conditions reproduce rain/storage/outflow within 0.01 m3");
            var arrival=F.dryRefuge.arrivalPoint.position;
            S.player.Teleport(arrival+Vector3.up*.1f,Vector3.forward);yield return new WaitForSeconds(.5f);
            Require(!S.player.swimming.IsSwimming&&!S.player.swimming.IsUnderwater,"school arrival stays on foot with clear above-water view");
            Require(S.Mode==SibucaoMode.Complete,"arrival fires completion even during peak storm");
        }
    }
}
#endif
