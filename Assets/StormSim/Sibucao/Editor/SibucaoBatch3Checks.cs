using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using StormSim.Preparedness;

namespace StormSim.Sibucao.Editor
{
    // Deterministic fixtures, not a substitute for a continuous player or headset playtest.
    public static class SibucaoBatch3Checks
    {
        const string Folder="Captures/Batch3_20260930/";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Hidden).Invoke(target,args);
        static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Hidden).GetValue(target);
        [MenuItem("StormSim/Validation/Batch 3 flood and movement")]
        public static void Menu()=>Debug.Log(Run());
        public static string Run()
        {
            if(!Application.isPlaying||!SibucaoSession.Instance)return "Enter Play Mode first.";
            Directory.CreateDirectory(Folder);
            var logs=new List<string>();
            void Check(bool condition,string label)=>logs.Add((condition?"PASS ":"FAIL ")+label);
            void Test(string label,Action action){try{action();}catch(Exception ex){logs.Add("FAIL "+label+": "+(ex.InnerException??ex).Message);}}
            var s=SibucaoSession.Instance;var saved=s.state;var home=s.home;var position=s.player.transform.position;var forward=s.player.transform.forward;
            var f=s.GetComponent<FloodSimulationManager>();var manager=s.physicalDecisions;
            try
            {
                s.UseTestSaveDirectory(Path.GetFullPath(Folder+"fixture-saves"));SibucaoEditorInput.Active=true;s.home=null;
                s.state=new RunState{homeIndex=0,difficultyLevel=2};s.home=s.homes[0];manager.ResetRun();s.home=null;s.SetMode(SibucaoMode.Paused);
                Test("survival",()=>
                {
                    var d=new PhysicalRunData{stamina=30};
                    for(int n=0;n<180;n++)StormSurvivalVitals.StepVitals(d,1,true,false,false,false,0,false);
                    Check(d.stamina>99,"three minutes of dry walking restores stamina");
                    d.stamina=100;for(int n=0;n<20;n++)StormSurvivalVitals.StepVitals(d,1,true,true,false,false,0,false);
                    Check(d.stamina<=21&&d.stamina>=19,"twenty seconds of sprinting has a bounded exertion cost");
                    Check(StormSurvivalVitals.StaminaRate(true,false,false,.7f)<0,"wading spends stamina");
                    Check(StormSurvivalVitals.StaminaRate(true,false,false,0,true)<=0,"heavy carrying does not gain free walking stamina");
                    Check(StormSurvivalVitals.StaminaRate(false,false,false,0,false,100)<StormSurvivalVitals.StaminaRate(false,false,false,0,false,0),"hunger reduces recovery without passive health damage");
                    d=new PhysicalRunData();for(int n=0;n<40;n++)StormSurvivalVitals.StepVitals(d,1,false,false,true,true,2,false);
                    Check(d.health==100&&d.submergedSeconds==40,"breath lasts forty seconds before damage");
                    for(int n=0;n<10;n++)StormSurvivalVitals.StepVitals(d,1,false,false,true,true,2,false);
                    Check(d.health==0&&d.needsRescue,"prolonged submersion requires assisted recovery");
                    StormSurvivalVitals.StepVitals(d,5,false,false,false,false,0,false);
                    Check(d.submergedSeconds==30,"surfacing replenishes breath over time");
                    var clone=JsonUtility.FromJson<PhysicalRunData>(JsonUtility.ToJson(d));
                    Check(clone.needsRescue&&clone.submergedSeconds==d.submergedSeconds,"breath and rescue requirement survive serialization");
                    Check(!SibucaoSwimming.WantsSwimming(false,true,1.2f)&&SibucaoSwimming.WantsSwimming(false,true,1.3f)&&SibucaoSwimming.WantsSwimming(true,true,1)&&!SibucaoSwimming.WantsSwimming(true,true,.8f),"swimming entry/exit thresholds prevent waterline flicker");
                    Check(SibucaoPlayer.WaterSpeedFactor(.1f)==1&&SibucaoPlayer.WaterSpeedFactor(.8f)>.45f&&SibucaoPlayer.WaterSpeedFactor(1.25f)==.45f,"dry/shallow walking remains fast and deep wading slows progressively");
                    Check(!s.player.SprintAllowed(.6f,100,false)&&!s.player.SprintAllowed(0,0,false)&&!s.player.SprintAllowed(0,10,false)&&s.player.SprintAllowed(0,15,false),"running restrictions and exhaustion recovery work");
                });
                Test("scenario field",()=>{int level=s.state.difficultyLevel;try{ScenarioGrid(s,f,Check);}finally{s.state.difficultyLevel=level;}});
                Test("fixed access",()=>
                {
                    foreach(var section in s.RouteChoices.sections)
                    {
                        var before=section.left.transform.position;
                        Check(section.FloodLift>0&&section.station.FloodAdapter.Lift==section.FloodLift,"B"+(section.station.index+1)+" center/side fixed access is prepared before storm rise");
                        s.state.homeSeconds=960;section.PrepareAccess();
                        Check(Vector3.Distance(before,section.left.transform.position)<.0001f,"B"+(section.station.index+1)+" storm clock cannot lift occupied side access");
                    }
                    s.state.homeSeconds=0;
                    int ramps=0;bool grades=true;float maximum=0;
                    foreach(var section in s.RouteChoices.sections)foreach(var layout in section.floodLayouts)
                    {
                        if(!layout.fittedRamps){grades=false;continue;}
                        foreach(var pair in new[]{new[]{layout.entryGround,layout.approach},new[]{layout.exitGround,layout.merge}})
                        {
                            var delta=pair[1]-pair[0];float run=new Vector2(delta.x,delta.z).magnitude-.9f;
                            float angle=Mathf.Atan2(Mathf.Abs(delta.y),Mathf.Max(.01f,run))*Mathf.Rad2Deg;maximum=Mathf.Max(maximum,angle);grades&=angle<s.player.GetComponent<CharacterController>().slopeLimit;ramps++;
                        }
                    }
                    Check(grades&&ramps>=360,"all home/difficulty fitted ramps are below the controller slope limit ("+ramps+" ramps, max "+maximum.ToString("0.0")+" degrees)");
                    manager.SavePhysical();var lifts=s.state.physical.sectionLifts.ToArray();string json=JsonUtility.ToJson(s.state);s.state=JsonUtility.FromJson<RunState>(json);manager.RestoreRun();
                    Check(s.RouteChoices.sections.Select((section,i)=>Mathf.Abs(section.FloodLift-lifts[i])<.001f).All(x=>x),"fixed access survives save/restore without changing height");
                });
                Test("recovery",()=>
                {
                    int history=manager.Data.history.Count;manager.Data.health=0;manager.Data.needsRescue=true;s.OpenRecovery();s.SetMode(SibucaoMode.Playing);
                    Check(s.Mode==SibucaoMode.Recovery,"Escape/resume cannot bypass zero-health recovery");
                    Check(s.ReturnToUnfinishedJunction(),"assistance finds supported footing at the unfinished junction");
                    Check(manager.Data.health==50&&manager.Data.stamina==60&&!manager.Data.needsRescue&&manager.Data.assistedRecoveries==1&&manager.Data.history.Count==history,"assistance records use and retains progress without awarding outcomes");
                    s.SetMode(SibucaoMode.Paused);
                    var vehicle=UnityEngine.Object.FindFirstObjectByType<SibucaoVehicle>();
                    Check(vehicle&&vehicle.TravelRestriction(s.RouteChoices.Current.station.transform.position,s.player)!=null,"vehicles cannot enter pedestrian task areas");
                    s.state.companion=true;Check(vehicle&&vehicle.TravelRestriction(new Vector3(100000,0,100000),s.player)!=null,"accompanied residents prevent vehicle entry");s.state.companion=false;
                });
                Test("actual grid profile",()=>
                {
                    s.state.difficultyLevel=5;s.state.homeSeconds=3600;f.RestoreClock();
                    var durations=new List<double>();
                    for(int i=0;i<6;i++){var sw=System.Diagnostics.Stopwatch.StartNew();Call(f,"EvaluateScenario",3600f);Call(f,"RebuildSurface");sw.Stop();durations.Add(sw.Elapsed.TotalMilliseconds);}
                    Check(!f.IsCatchingUp&&f.SimulatedSeconds==3600,"one-hour scenario restores directly without historical replay");
                    Check(f.MaximumSimulatedDepth<=3.5f,"actual scene water respects Extreme depth cap");
                    var mat=Field<Material>(f,"material");var ocean=f.sea.GetComponent<Renderer>().sharedMaterial;
                    Check(mat.GetTexture("_RippleNormal")==ocean.GetTexture("_RippleNormal")&&mat.GetColor("_DeepColor")==ocean.GetColor("_DeepColor"),"flood reuses active ocean ripple texture and palette");
                    Check(mat.shader.isSupported,"flood shader is supported by the active graphics device");
                    var report="Editor direct evaluation + mesh upload, grid "+f.terrain.width+"x"+f.terrain.height+".\nSamples ms: "+string.Join(", ",durations.Select(x=>x.ToString("0.00")))+"\nMedian (warm samples): "+durations.Skip(1).OrderBy(x=>x).ElementAt(2).ToString("0.00")+" ms\nThis is an Editor CPU microbenchmark, not standalone FPS.\n";
                    File.WriteAllText(Folder+"flood-profile.txt",report);
                });
            }
            finally
            {
                SibucaoEditorInput.Reset();s.state=saved;s.home=home;manager.RestoreRun();f.RestoreClock();s.player.Teleport(position,forward);s.SetMode(SibucaoMode.Paused);
            }
            File.WriteAllLines(Folder+"flood-movement-checks.txt",logs);return string.Join("\n",logs);
        }
        public static string Compatibility()
        {
            if(!Application.isPlaying)return "Enter Play Mode first.";
            var s=SibucaoSession.Instance;var manager=s.physicalDecisions;var saved=s.state;var home=s.home;
            var position=s.player.transform.position;var forward=s.player.transform.forward;var logs=new List<string>();
            void Check(bool value,string label)=>logs.Add((value?"PASS ":"FAIL ")+label);
            try
            {
                SibucaoEditorInput.Active=true;s.UseTestSaveDirectory(Path.GetFullPath(Folder+"compatibility-saves"));
                s.state=new RunState{homeIndex=0,difficultyLevel=2,homeSeconds=480};s.home=s.homes[0];manager.ResetRun();s.home=null;s.SetMode(SibucaoMode.Paused);
                var mission=s.RouteChoices.sections[0].left;mission.State.step=2;mission.State.amount=.35f;
                manager.Data.flags.Add("batch3-kept");manager.SavePhysical();s.player.Teleport(s.RouteChoices.Current.Approach,Vector3.forward);
                Check(s.SaveCheckpoint(),"legacy compatibility fixture writes an isolated checkpoint");
                var legacy=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(s.state));legacy.physical.schemaVersion=2;
                SibucaoSaveStore.Write(s.SlotPath(true),legacy,null);
                Check(s.LoadSlot(true)&&manager.Data.schemaVersion==3&&manager.RestoredLegacyAccess,"schema-2 checkpoint migrates to fixed access");
                Check(manager.Data.flags.Contains("batch3-kept")&&mission.State.step==2&&Mathf.Abs(mission.State.amount-.35f)<.001f,"legacy migration retains flags and partial work");
                Check(s.toastTitle=="Raised access updated"&&s.toastText.Contains("fixed evacuation"),"load result retains the walkway migration notice");
                Check(SibucaoRouteChoices.Flat(s.player.transform.position,s.RouteChoices.Current.Approach)<2,"legacy migration returns to its unfinished junction");
                s.home=null;s.SetMode(SibucaoMode.Paused);
                var vehicle=SibucaoVehicle.All.First();vehicle.ResetVehicle();s.player.Teleport(vehicle.transform.TransformPoint(vehicle.seat),vehicle.transform.forward);
                Check(vehicle.TryEnter(s.player,true)&&s.SaveCheckpoint()&&s.LoadSlot(true)&&s.player.vehicle==vehicle,"occupied-vehicle checkpoint restores the cockpit before travel restrictions apply");
                vehicle.Release();s.home=null;s.SetMode(SibucaoMode.Paused);manager.Data.health=0;manager.Data.needsRescue=true;
                Check(s.SaveCheckpoint()&&s.LoadSlot(true)&&s.Mode==SibucaoMode.Recovery&&manager.Data.health==0,"zero-health checkpoint cannot resume past assistance recovery");
                bool rejected=false;var invalid=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(s.state));invalid.physical.submergedSeconds=float.NaN;
                try{SibucaoSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}
                Check(rejected,"save validation rejects a non-finite breath value");
                manager.Data.health=100;manager.Data.needsRescue=false;s.home=null;s.state.difficultyLevel=5;s.state.homeSeconds=960;
                var flood=s.GetComponent<FloodSimulationManager>();flood.RestoreClock();Call(flood,"EvaluateScenario",960f);Call(flood,"RebuildSurface");
                bool submerged=false;
                for(int i=0;i<flood.terrain.ground.Length;i++)
                {
                    var p=flood.terrain.Position(i);if(!flood.TrySurface(p,out var surface,out var ground)||surface-ground<2.8f)continue;
                    p.y=surface-2.2f;s.player.Teleport(p,Vector3.forward);submerged=true;break;
                }
                if(!submerged)throw new InvalidOperationException("No deep-water cell for menu recovery checks.");
                s.SetMode(SibucaoMode.Playing);s.inventory.Open();Call(s.GetComponent<StormSurvivalVitals>(),"Update");
                Check(!s.inventory.IsOpen&&s.toastTitle=="Surface first","submerged bag closes with a surface instruction so swimming is available");
                s.missionGuide.Open();Call(s.GetComponent<StormSurvivalVitals>(),"Update");
                Check(!s.missionGuide.IsOpen&&s.toastTitle=="Surface first","submerged guide closes with a surface instruction so swimming is available");
            }
            catch(Exception ex){logs.Add("FAIL compatibility fixture: "+ex.Message);}
            finally
            {
                if(s.player.vehicle)s.player.vehicle.Release();SibucaoEditorInput.Reset();s.state=saved;s.home=home;
                manager.RestoreRun();s.GetComponent<FloodSimulationManager>().RestoreClock();s.player.Teleport(position,forward);s.SetMode(SibucaoMode.Paused);
            }
            Directory.CreateDirectory(Folder);File.WriteAllLines(Folder+"compatibility-checks.txt",logs);return string.Join("\n",logs);
        }
        static void ScenarioGrid(SibucaoSession s,FloodSimulationManager source,Action<bool,string> check)
        {
            var t=ScriptableObject.CreateInstance<SibucaoFloodTerrain>();t.width=t.height=5;t.cellSize=2;t.origin=new Vector2(100000,100000);
            t.ground=new float[25];t.crestX=new float[25];t.crestZ=new float[25];t.kind=Enumerable.Repeat((byte)1,25).ToArray();t.covered=new bool[25];t.initialDepth=new float[25];
            t.ground[12]=.5f;for(int i=0;i<25;i++){t.crestX[i]=i%5<4?Mathf.Max(t.ground[i],t.ground[i+1]):100;t.crestZ[i]=i<20?Mathf.Max(t.ground[i],t.ground[i+5]):100;}
            var go=new GameObject("Batch 3 isolated flood fixture");var f=go.AddComponent<FloodSimulationManager>();f.enabled=false;f.terrain=t;f.temporaryWaterMaterial=source.temporaryWaterMaterial;f.groundRelativeInundation=true;
            try
            {
                Call(f,"Start");
                for(int level=1;level<=5;level++)
                {
                    s.state.difficultyLevel=level;Call(f,"ResetWater");Call(f,"EvaluateScenario",960f);Call(f,"RebuildSurface");
                    var d=Field<float[]>(f,"depth");var flow=Field<Vector2[]>(f,"currents");
                    check(d[12]<d[11]&&d.Min()>=0&&d.Max()<=s.floodDifficulty.Get(level).peakDepth,"D"+level+" terrain variation creates bounded shallow/deeper water");
                    check(flow.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&v.magnitude<=f.maximumCurrent+.001f),"D"+level+" finite bounded current cues");
                    var direct=d.ToArray();Call(f,"ResetWater");for(int second=1;second<=960;second++)Call(f,"EvaluateScenario",(float)second);
                    check(d.Select((v,i)=>Mathf.Abs(v-direct[i])<.0001f).All(x=>x),"D"+level+" resumed and continuously evaluated scenario depths agree");
                    check(Math.Abs(f.InitialVolume+f.RainVolume+f.StormInflowVolume-f.DrainedVolume-f.WaterVolume)<.02,"D"+level+" scenario inflow/outflow bookkeeping balances");
                }
                for(int i=0;i<25;i++)t.crestX[i]=t.crestZ[i]=100;
                Call(f,"EvaluateScenario",960f);check(Field<Vector2[]>(f,"currents").All(v=>v.sqrMagnitude<.00001f),"barriers suppress current cues through walls");
                f.groundRelativeInundation=false;Call(f,"ResetWater");var depths=Field<float[]>(f,"depth");depths[11]=.5f;
                typeof(FloodSimulationManager).GetProperty("SimulatedSeconds").SetValue(f,960f);
                for(int i=0;i<25;i++)t.crestX[i]=t.crestZ[i]=0;
                Call(f,"Advance",.1f);check(depths[10]>0||depths[6]>0,"experimental runoff still transfers water to neighboring cells");
            }
            finally{UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(t);}
        }
        public static string Access()
        {
            if(!Application.isPlaying)return "Enter Play Mode first.";
            var logs=new List<string>();var s=SibucaoSession.Instance;var manager=s.physicalDecisions;
            var saved=s.state;var home=s.home;var position=s.player.transform.position;var forward=s.player.transform.forward;
            var controller=s.player.GetComponent<CharacterController>();
            try
            {
                s.home=null;SibucaoEditorInput.Active=true;s.SetMode(SibucaoMode.Paused);
                for(int level=1;level<=5;level++)
                {
                    s.state=new RunState{homeIndex=0,difficultyLevel=level};s.home=s.homes[0];manager.ResetRun();s.home=null;Physics.SyncTransforms();
                    foreach(int index in new[]{0,4,8})
                    {
                        var section=s.RouteChoices.sections[index];var ramp=section.decks.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Ground access ramp");
                        if(!ramp){logs.Add("FAIL D"+level+" B"+(index+1)+" missing fitted entry ramp");continue;}
                        var a=ramp.TransformPoint(new Vector3(0,.5f,-.5f));var b=ramp.TransformPoint(new Vector3(0,.5f,.5f));
                        if(a.y>b.y){var swap=a;a=b;b=swap;}
                        bool Walk(Vector3 from,Vector3 to)
                        {
                            s.player.Teleport(from+Vector3.up*.10f,to-from);
                            for(int frame=0;frame<1500;frame++)
                            {
                                var delta=to-s.player.transform.position;delta.y=0;
                                if(delta.magnitude<.18f)break;
                                controller.Move(Vector3.ClampMagnitude(delta,.065f)+Vector3.down*.08f);
                            }
                            return SibucaoRouteChoices.Flat(s.player.transform.position,to)<.35f&&Mathf.Abs(s.player.transform.position.y-to.y)<.4f;
                        }
                        bool up=Walk(a,b),down=Walk(b,a);
                        logs.Add((up&&down?"PASS ":"FAIL ")+"D"+level+" B"+(index+1)+" character traverses fitted entry ramp up/down ("+up+"/"+down+")");
                    }
                    int samples=0,missing=0;
                    foreach(var section in s.RouteChoices.sections)foreach(var mission in new[]{section.left,section.right})
                        for(int segment=1;segment<mission.route.Length;segment++)
                        {
                            var a=mission.route[segment-1];var b=mission.route[segment];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)));
                            for(int n=0;n<=steps;n++)
                            {
                                var p=Vector3.Lerp(a,b,n/(float)steps);samples++;
                                bool supported=Physics.RaycastAll(p+Vector3.up*.45f,Vector3.down,.8f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.65f&&Mathf.Abs(h.point.y-p.y)<.22f&&!h.collider.GetComponentInParent<SibucaoPlayer>());
                                if(!supported)missing++;
                            }
                        }
                    logs.Add((missing==0?"PASS ":"FAIL ")+"D"+level+" companion route support "+(samples-missing)+"/"+samples+" samples");
                }
            }
            catch(Exception ex){logs.Add("FAIL access fixture: "+ex.Message);}
            finally{SibucaoEditorInput.Reset();s.state=saved;s.home=home;manager.RestoreRun();s.player.Teleport(position,forward);s.SetMode(SibucaoMode.Paused);}
            Directory.CreateDirectory(Folder);File.WriteAllLines(Folder+"physical-access-checks.txt",logs);return string.Join("\n",logs);
        }
    }
}
