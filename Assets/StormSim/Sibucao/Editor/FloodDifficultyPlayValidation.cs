using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Explicit setup fixtures: these check bindings/state/physics, not successful player interaction.
    public static class FloodDifficultyPlayValidation
    {
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        public static string Run()
        {
            Require(Application.isPlaying,"Run in play mode after entering DURING.");
            var s=SibucaoSession.Instance;var m=s.physicalDecisions;
            s.SetMode(SibucaoMode.Paused);int mapped=0,clearSamples=0,fallbacks=0;
            var blocked=new HashSet<string>();
            for(int level=1;level<=5;level++)
            {
                s.state.difficultyLevel=level;m.ResetRun();
                foreach(var st in m.stations.Where(x=>!x.supplementary))
                {
                    m.Data.floodVariants[st.index]=new FloodVariantState{band=level,localDepth=s.floodDifficulty.Get(level).peakDepth,lift=s.floodDifficulty.Get(level).peakDepth+.5f,variantId="B"+(st.index+1)+"-D"+level+"-W"+level};
                    st.FloodAdapter.RestorePresentation();
                }
                Physics.SyncTransforms();
                foreach(var st in m.stations.Where(x=>!x.supplementary))
                {
                    int n=st.index;for(int i=0;i<n;i++)m.Data.results[i]=0;
                    m.Data.causal.path=SibucaoCausalModel.Path(m.Data);m.EnterCausalStage(n);
                    Require(st.branches.Any(b=>m.ChoiceAvailable(st,b.choiceIndex)),"All choices locked at D"+level+" B"+(n+1));fallbacks++;
                    foreach(var b in st.branches)
                    {
                        var source=st.node.choices[b.choiceIndex];var choice=b.Choice;
                        Require(choice!=source&&choice.id==source.id&&choice.onSuccess==source.onSuccess&&choice.onFailure==source.onFailure&&choice.requiredItemTag==source.requiredItemTag&&choice.consumedItemTag==source.consumedItemTag,"Changed branch identity/consequence");
                        Require(!m.FinishPhysicalChoice(st,b.choiceIndex),"Unfinished variant resolved");mapped++;
                        foreach(var point in b.path)Require(Mathf.Abs(point.position.y-st.FloodAdapter.DeckCenter.y)<.02f,"Path below platform");
                        for(int step=0;step<=20;step++)
                        {
                            var p=Vector3.Lerp(b.path[0].position,b.path[b.path.Length-1].position,step/20f);
                            foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.35f,p+Vector3.up*1.5f,.2f,~0,QueryTriggerInteraction.Ignore))
                            {
                                if(!c.gameObject.activeInHierarchy||c.GetComponentInParent<PhysicalGrabbable>()||c.GetComponentInParent<DiegeticWorkPart>()||c.GetComponentInParent<SibucaoPlayer>())continue;
                                blocked.Add("D"+level+" B"+(n+1)+" C"+b.choiceIndex+": "+c.name);
                            }
                            clearSamples++;
                        }
                    }
                }
            }
            // Save/load the selected difficulty, elevated geometry, partial work and real branch ancestry.
            m.ResetRun();var station=m.stations[0];var branch=station.branches[1];
            m.Data.floodVariants[0]=new FloodVariantState{band=5,lift=4,variantId="B1-D5-W5",parentPath=""};station.FloodAdapter.RestorePresentation();
            m.Data.started[0]=s.state.elapsed;var attempt=m.Branch(0);attempt.engaged=true;attempt.selected=1;
            var progress=branch.Encounter.Progress;progress.displacement[0]=.4f;progress.activeSeconds=2.5f;
            m.SavePhysical();string saved=JsonUtility.ToJson(s.state);s.state=JsonUtility.FromJson<RunState>(saved);m.RestoreRun();
            Require(s.state.difficultyLevel==5&&station.FloodAdapter.Lift==4&&branch.Encounter.Progress.displacement[0]==.4f&&branch.Encounter.Progress.activeSeconds==2.5f,"Partial progress/difficulty lost on restore");
            Require(m.Data.history.Count==0&&m.Data.causal.path=="","Restore invented completed choices");
            var report="PASS: "+mapped+" runtime variant identity/consequence bindings; "+fallbacks+" stage/difficulty fallback checks; unfinished commits rejected; partial work and difficulty save/restore. Geometry samples="+clearSamples+".\n";
            report+=blocked.Count==0?"PASS: no static colliders intersect sampled walking lanes in the current preparation departure route.\n":"BLOCKED GEOMETRY:\n"+string.Join("\n",blocked)+"\n";
            report+=TestGrid(s)+"\nFixtures do not certify a completed B1-B9 physical playthrough, other home layouts, or headset interaction.";
            Directory.CreateDirectory("Reports");File.WriteAllText("Reports/FloodDifficultyRuntime.txt",report);return report;
        }
        static string TestGrid(SibucaoSession s)
        {
            var terrain=ScriptableObject.CreateInstance<SibucaoFloodTerrain>();terrain.origin=new Vector2(100000,100000);terrain.width=5;terrain.height=5;terrain.cellSize=2;
            terrain.ground=new float[25];terrain.crestX=new float[25];terrain.crestZ=new float[25];terrain.covered=new bool[25];terrain.kind=Enumerable.Repeat((byte)1,25).ToArray();terrain.initialDepth=new float[25];terrain.initialDepth[12]=10;
            var go=new GameObject("Temporary flood regression fixture");var f=go.AddComponent<FloodSimulationManager>();f.enabled=false;f.terrain=terrain;f.temporaryWaterMaterial=s.GetComponent<FloodSimulationManager>().temporaryWaterMaterial;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(FloodSimulationManager);
            type.GetMethod("Start",flags).Invoke(f,null);
            try
            {
                foreach(var profile in s.floodDifficulty.profiles)
                {
                    s.state.difficultyLevel=profile.level;type.GetMethod("ResetWater",flags).Invoke(f,null);
                    Require(Math.Abs(f.InitialVolume-f.DrainedVolume-f.WaterVolume)<.001,"Initial cap mass mismatch");
                    var depth=(float[])type.GetField("depth",flags).GetValue(f);depth[12]=profile.peakDepth*3;
                    double added=depth[12]*4;
                    type.GetProperty("InitialVolume").GetSetMethod(true).Invoke(f,new object[]{f.InitialVolume+added});
                    type.GetProperty("SimulatedSeconds").GetSetMethod(true).Invoke(f,new object[]{profile.peakSeconds});
                    type.GetMethod("EnforceDepthLimit",flags).Invoke(f,new object[]{profile.peakSeconds});
                    for(int i=0;i<20;i++)type.GetMethod("Advance",flags).Invoke(f,new object[]{.1f});
                    Require(f.MaximumSimulatedDepth<=profile.peakDepth+.0001f,"Depth exceeded profile cap");
                    Require(depth[11]>0||depth[13]>0||depth[7]>0||depth[17]>0,"No local cellular transfer");
                    Require(Math.Abs(f.InitialVolume+f.RainVolume-f.DrainedVolume-f.WaterVolume)<.003,"Mass balance drift");
                }
                return "PASS: five cellular-grid cap, neighbour-transfer and overflow mass-balance fixtures.";
            }
            finally{UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(terrain);}
        }
    }
}
