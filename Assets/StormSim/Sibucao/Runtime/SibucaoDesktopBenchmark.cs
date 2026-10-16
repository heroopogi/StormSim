#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace StormSim.Sibucao
{
    // Opt-in diagnostics only. Never included in a non-development player.
    public sealed class SibucaoDesktopBenchmark:MonoBehaviour
    {
        [Serializable] public class Sample {public string location,mode;public Vector3 plannedPosition,capturedPosition;public bool viewpointValid;public float medianMs,p95Ms,worstMs,cpuMedianMs,gpuMedianMs;public long memoryBytes,gcBytesPerFrame,drawCallsPerFrame;public int frames;public bool cpuTimingAvailable,gpuTimingAvailable,gcCounterAvailable,renderCounterAvailable;}
        [Serializable] public class Report {public string build="2026.09.30-b4-7-rc2",device,cpu,gpu,graphicsApi,resolution,scope;public int ramMB;public List<Sample> samples=new List<Sample>();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-stormsim-benchmark")>=0)new GameObject("Desktop diagnostic benchmark").AddComponent<SibucaoDesktopBenchmark>();}
        IEnumerator Start()
        {
            Application.runInBackground=true;var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-stormsim-report");
            string folder=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"benchmark");Directory.CreateDirectory(folder);
            while(!SibucaoSession.Instance)yield return null;
            var s=SibucaoSession.Instance;s.UseTestSaveDirectory(Path.Combine(folder,"isolated-saves"));
            int oldHome=PlayerPrefs.GetInt("SibucaoLastHome",-1),oldSpawn=PlayerPrefs.GetInt("SibucaoLastSpawn",-1);
            var report=new Report{device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),ramMB=SystemInfo.systemMemorySize,resolution=Screen.width+"x"+Screen.height,scope="Development player; staged desktop viewpoints, no continuous player or participant validation. Target: median <= 33.3 ms, p95 <= 50 ms at 1920x1080."};
            s.StartWithRole("Student");s.StartWithDifficulty(2);yield return null;
            var bridge=s.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();if(bridge&&bridge.Active)bridge.SkipToDuring();
            if(s.home)s.home.SkipTV();
            var flood=s.GetComponent<FloodSimulationManager>();
            float openingEnd=s.home?s.home.wakeDuration+s.home.newsDuration+s.home.standDuration:75;
            float preparation=s.home?s.home.preparationDuration:180;
            string[] locations={"first-junction-dry","middle-route-rising","school-late"};float[] progress={36,340,s.route.length-12},clock={openingEnd+10,openingEnd+preparation+240,openingEnd+preparation+720};
            for(int location=0;location<locations.Length;location++)
            {
                s.SetMode(SibucaoMode.Playing);s.state.homeSeconds=clock[location];s.state.elapsed=clock[location];
                // Restoring a clock before the opening ends re-locks the camera at home.
                // Settle the home pose before placing the diagnostic camera on the route.
                if(s.home)s.home.Restore();flood.RestoreClock();yield return null;yield return null;
                var point=s.route.Sample(progress[location]);s.player.Teleport(point+Vector3.up*.15f,s.route.Sample(progress[location]+4)-point);
                yield return new WaitForSecondsRealtime(8);yield return null;
                bool validView=(!s.home||!s.home.MovementLocked)&&Vector3.Distance(s.player.transform.position,point)<25;
                var frames=new List<float>(4096);var cpu=new List<float>(4096);var gpu=new List<float>(4096);var timing=new FrameTiming[1];
                using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
                using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
                {
                    long gcTotal=0,drawTotal=0;float until=Time.realtimeSinceStartup+20;
                    while(Time.realtimeSinceStartup<until)
                    {
                        yield return null;frames.Add(Time.unscaledDeltaTime*1000);FrameTimingManager.CaptureFrameTimings();
                        if(FrameTimingManager.GetLatestTimings(1,timing)>0){if(timing[0].cpuFrameTime>0)cpu.Add((float)timing[0].cpuFrameTime);if(timing[0].gpuFrameTime>0)gpu.Add((float)timing[0].gpuFrameTime);}
                        if(gc.Valid)gcTotal+=gc.LastValue;if(draw.Valid)drawTotal+=draw.LastValue;
                    }
                    frames.Sort();cpu.Sort();gpu.Sort();
                    report.samples.Add(new Sample{location=locations[location],mode=s.Mode.ToString(),plannedPosition=point,capturedPosition=s.player.transform.position,viewpointValid=validView&&Vector3.Distance(s.player.transform.position,point)<25,frames=frames.Count,medianMs=frames[frames.Count/2],p95Ms=frames[Mathf.Min(frames.Count-1,(int)(frames.Count*.95f))],worstMs=frames[frames.Count-1],memoryBytes=Profiler.GetTotalAllocatedMemoryLong(),cpuMedianMs=cpu.Count>0?cpu[cpu.Count/2]:-1,gpuMedianMs=gpu.Count>0?gpu[gpu.Count/2]:-1,cpuTimingAvailable=cpu.Count>0,gpuTimingAvailable=gpu.Count>0,gcBytesPerFrame=gc.Valid?gcTotal/frames.Count:-1,drawCallsPerFrame=draw.Valid?drawTotal/frames.Count:-1,gcCounterAvailable=gc.Valid,renderCounterAvailable=draw.Valid});
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,locations[location]+".png"));yield return null;
            }
            File.WriteAllText(Path.Combine(folder,"desktop-benchmark.json"),JsonUtility.ToJson(report,true));
            if(oldHome<0)PlayerPrefs.DeleteKey("SibucaoLastHome");else PlayerPrefs.SetInt("SibucaoLastHome",oldHome);
            if(oldSpawn<0)PlayerPrefs.DeleteKey("SibucaoLastSpawn");else PlayerPrefs.SetInt("SibucaoLastSpawn",oldSpawn);PlayerPrefs.Save();
            Application.Quit();
        }
    }
}
#endif
