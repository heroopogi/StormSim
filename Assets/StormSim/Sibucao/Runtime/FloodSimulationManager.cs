using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace StormSim.Sibucao
{
    /// <summary>Scenario inundation or experimental rainfall/runoff over baked elevations. Neither mode is a calibrated hazard forecast.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class FloodSimulationManager : MonoBehaviour
    {
        public Material temporaryWaterMaterial;
        public Vector3[] groundSamples;
        public float routeHalfWidth=7,maximumDepth=1.85f;
        public float[] tierThresholds={.15f,.5f,1f,1.5f};
        public Vector3 flowDirection=new Vector3(.25f,0,1);
        public bool temporaryPlaceholderFlood=true;
        [Tooltip("Leave disabled for cellular automata rainfall/runoff. Enable only for legacy authored storm depths.")]
        public bool groundRelativeInundation;
        public double StormInflowVolume {get;private set;}
        public SibucaoFloodTerrain terrain;
        public BanagoSeaWaves sea;
        public SibucaoDryRefuge dryRefuge;
        [Tooltip("Maximum runoff speed in metres per second. Rainfall time is compressed for the scenario.")]
        public float maximumCurrent=1.4f;
        public float SimulatedSeconds {get;private set;}
        public double WaterVolume {get;private set;}
        public double RainVolume {get;private set;}
        public double DrainedVolume {get;private set;}
        public double CapOverflowVolume {get;private set;}
        public int RequiredMissionStage=>S&&S.Mode==SibucaoMode.Playing&&(!S.home||!S.home.MovementLocked)&&S.physicalDecisions?Mathf.Clamp(S.physicalDecisions.NextCoreStage,0,9):-1;
        public float DepthLimit=>FloodDifficultySettings.Current.EffectiveLimit(SimulatedSeconds,RequiredMissionStage);
        public string WarningStage=>DepthLimit<.15f?"MONITORING":DepthLimit<.60f?"ALERT":"EVACUATE";
        public string RouteCondition=>DepthLimit<.15f
            ? "Marked routes are being monitored; inspect each passage before entering."
            : DepthLimit<.60f
                ? "Low passages may be unsafe. Use marked raised or side access and do not enter floodwater."
                : "Low routes are unavailable. Follow raised marked access; if no safe passage exists, stop at a safe point and seek help.";
        public float MaximumSimulatedDepth {get;private set;}
        public double InitialVolume {get;private set;}
        public float RainfallRate=>S?FloodDifficultySettings.Current.RainRate(SimulatedSeconds):0;
        public float LastStepMilliseconds {get;private set;}
        public float PeakStepMilliseconds {get;private set;}
        public float LastFrameMilliseconds {get;private set;}
        public float SimulationClock=>S?Mathf.Max(0,S.state.homeSeconds-S.state.floodClockOffset):0;
        public float CatchUpProgress => S ? Mathf.Clamp01(SimulatedSeconds / Mathf.Max(.1f,SimulationClock)) : 1;
        public string ModelDescription => groundRelativeInundation ? "Authored storm depths with terrain variation; currents are gameplay cues, not a flood forecast." : "Cellular automata flood model: four-neighbor rainfall/runoff over terrain; not a calibrated flood forecast.";
        byte[] kind;
        public Mesh SurfaceMesh=>mesh;
        public bool IsCatchingUp=>S&&depth!=null&&SimulationClock>SimulatedSeconds+.5f;
        bool replayingSave;
        float[] depth,delta,outgoing,qx,qz,surfaceHeight,scenarioScale;
        Vector2[] currents,wetness;
        Vector3[] vertices;
        Mesh mesh;Material material;GameObject surface;float lastClock=-1,nextMesh;
        SibucaoEvacuationExperience experience;
        float waveHeight,waveSpeed;
        FloodDifficultyProfile profile;
        const float Step=.1f,Wet=.018f;
        SibucaoSession S=>SibucaoSession.Instance;
        public float Severity=>S&&S.home?S.home.Severity:0;
        public WaterTier GetWaterTier()=>GetWaterTier(S?S.player.transform.position:Vector3.zero);
        public WaterTier GetWaterTier(Vector3 p){float d=GetWaterDepth(p);for(int i=0;i<4;i++)if(d<=tierThresholds[i])return (WaterTier)i;return WaterTier.Tier5;}
        public float GetWaterDepth()=>S?GetWaterDepth(S.player.transform.position):0;
        public float GetWaterDepth(Vector3 p)=>TrySurface(p,out float h,out _)?Mathf.Max(0,h-p.y):0;
        void Start()
        {
            experience=GetComponent<SibucaoEvacuationExperience>();
            if(!terrain||terrain.ground==null||terrain.ground.Length!=terrain.width*terrain.height||!temporaryWaterMaterial){Debug.LogError("Sibucao flood needs a baked terrain grid.",this);return;}
            int count=terrain.ground.Length;
            // Copy the baked classifications: the shared terrain asset remains unchanged.
            kind=(byte[])terrain.kind.Clone();
            if(dryRefuge)for(int i=0;i<count;i++)
            {
                var p=terrain.Position(i);
                if(dryRefuge.Contains(p,terrain.cellSize*.25f))kind[i]=0;
                // Outflow is accounted for, rather than letting water build against an invisible wall.
                else if(kind[i]==1&&dryRefuge.Contains(p,dryRefuge.drainageWidth))kind[i]=2;
            }
            depth=new float[count];delta=new float[count];outgoing=new float[count];qx=new float[count];qz=new float[count];surfaceHeight=new float[count];currents=new Vector2[count];wetness=new Vector2[count];vertices=new Vector3[count];
            scenarioScale=new float[count];
            for(int i=0;i<count;i++)
            {
                float low=terrain.ground[i];int x=i%terrain.width,z=i/terrain.width;
                if(x>0&&kind[i-1]==1)low=Mathf.Min(low,terrain.ground[i-1]);
                if(x+1<terrain.width&&kind[i+1]==1)low=Mathf.Min(low,terrain.ground[i+1]);
                if(z>0&&kind[i-terrain.width]==1)low=Mathf.Min(low,terrain.ground[i-terrain.width]);
                if(z+1<terrain.height&&kind[i+terrain.width]==1)low=Mathf.Min(low,terrain.ground[i+terrain.width]);
                scenarioScale[i]=Mathf.Clamp(1-(terrain.ground[i]-low)*.6f,.4f,1);
            }
            surface=new GameObject("Terrain-connected rainfall and runoff");surface.transform.SetParent(transform,false);
            material=new Material(temporaryWaterMaterial);material.SetFloat("_TerrainAware",1);
            waveHeight=material.GetFloat("_WaveHeight");waveSpeed=material.GetFloat("_WaveSpeed");
            // Reuse the active ocean's appearance while retaining flood-specific depth and flow.
            var seaMaterial=sea?sea.GetComponent<Renderer>()?.sharedMaterial:null;
            if(seaMaterial)foreach(var property in new[]{"_ShallowColor","_DeepColor","_RippleNormal","_NormalStrength","_RippleSpeed","_GlintStrength","_FresnelStrength"})
                if(material.HasProperty(property)&&seaMaterial.HasProperty(property))
                {if(property=="_RippleNormal")material.SetTexture(property,seaMaterial.GetTexture(property));else if(property.EndsWith("Color"))material.SetColor(property,seaMaterial.GetColor(property));else material.SetFloat(property,seaMaterial.GetFloat(property));}
            surface.AddComponent<MeshRenderer>().sharedMaterial=material;
            mesh=new Mesh{name="Sibucao wet / dry terrain surface",indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();
            var triangles=new System.Collections.Generic.List<int>();
            for(int z=0;z<terrain.height;z++)for(int x=0;x<terrain.width;x++)
            {
                int i=z*terrain.width+x;vertices[i]=transform.InverseTransformPoint(terrain.Position(i));
                if(x==terrain.width-1||z==terrain.height-1)continue;
                int b=i+1,c=i+terrain.width,d=c+1;
                if(WaterTriangle(i,b,c)){triangles.Add(i);triangles.Add(c);triangles.Add(b);}
                if(WaterTriangle(b,c,d)){triangles.Add(b);triangles.Add(c);triangles.Add(d);}
            }
            mesh.vertices=vertices;mesh.SetTriangles(triangles,0);surface.AddComponent<MeshFilter>().sharedMesh=mesh;ResetWater();
        }
        void ResetWater()
        {
            replayingSave=false;
            profile=FloodDifficultySettings.Current;
            Array.Clear(depth,0,depth.Length);Array.Clear(currents,0,currents.Length);SimulatedSeconds=0;RainVolume=DrainedVolume=WaterVolume=InitialVolume=CapOverflowVolume=StormInflowVolume=0;MaximumSimulatedDepth=0;
            if(terrain.initialDepth!=null&&terrain.initialDepth.Length==depth.Length)
                for(int i=0;i<depth.Length;i++)if(kind[i]==1){depth[i]=Mathf.Max(0,terrain.initialDepth[i]);InitialVolume+=depth[i]*terrain.cellSize*terrain.cellSize;}
            WaterVolume=InitialVolume;EnforceDepthLimit(0);LastStepMilliseconds=PeakStepMilliseconds=0;RebuildSurface();
        }
        void Update()
        {
            if(depth==null||!S)return;
            float clock=SimulationClock;
            if(clock<lastClock-.2f||profile!=FloodDifficultySettings.Current)ResetWater();lastClock=clock;
            float target=Mathf.Max(0,clock); // The storm also develops during the home preparation phase.
            long frameStart=System.Diagnostics.Stopwatch.GetTimestamp();
            if(S.Mode==SibucaoMode.Playing&&groundRelativeInundation&&target-SimulatedSeconds>=Step)
            {
                EvaluateScenario(target);LastStepMilliseconds=Milliseconds(frameStart);PeakStepMilliseconds=Mathf.Max(PeakStepMilliseconds,LastStepMilliseconds);
            }
            // Experimental runoff gets a wall-clock budget, including during a restore.
            else if(S.Mode==SibucaoMode.Playing&&!groundRelativeInundation)for(int n=0;n<24&&SimulatedSeconds+Step<=target;n++)
            {
                long begin=System.Diagnostics.Stopwatch.GetTimestamp();Advance(Step);SimulatedSeconds+=Step;
                LastStepMilliseconds=Milliseconds(begin);PeakStepMilliseconds=Mathf.Max(PeakStepMilliseconds,LastStepMilliseconds);
                if(Milliseconds(frameStart)>=(replayingSave?30:6))break;
            }
            if(replayingSave&&!IsCatchingUp)replayingSave=false;
            if(Time.unscaledTime>=nextMesh){RebuildSurface();nextMesh=Time.unscaledTime+.1f;}
            LastFrameMilliseconds=Milliseconds(frameStart);
        }
        static float Milliseconds(long start)=>(float)((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
        public void RestoreClock(){if(depth!=null){ResetWater();replayingSave=true;lastClock=-1;nextMesh=0;}}
        // Deterministic scenario evaluation: restore cost is independent of saved storm age.
        // This mode does not solve rainfall hydraulics; its bounded currents are gameplay cues.
        void EvaluateScenario(float seconds)
        {
            float limit=Mathf.Max(0,profile.EffectiveLimit(seconds,RequiredMissionStage)-Mathf.Abs(waveHeight));
            double area=terrain.cellSize*terrain.cellSize,volume=0;int exposed=0;
            MaximumSimulatedDepth=0;
            for(int i=0;i<depth.Length;i++)
            {
                depth[i]=kind[i]==1?limit*scenarioScale[i]:0;
                surfaceHeight[i]=terrain.ground[i]+depth[i];
                if(kind[i]==1){volume+=depth[i]*area;if(!terrain.covered[i])exposed++;MaximumSimulatedDepth=Mathf.Max(MaximumSimulatedDepth,depth[i]);}
            }
            for(int i=0;i<depth.Length;i++)
            {
                currents[i]=Vector2.zero;if(kind[i]!=1||depth[i]<=Wet)continue;
                int x=i%terrain.width,z=i/terrain.width;float h=surfaceHeight[i];Vector2 slope=Vector2.zero;
                if(x>0)ScenarioSlope(i-1,terrain.crestX[i-1],Vector2.left,h,ref slope);
                if(x+1<terrain.width)ScenarioSlope(i+1,terrain.crestX[i],Vector2.right,h,ref slope);
                if(z>0)ScenarioSlope(i-terrain.width,terrain.crestZ[i-terrain.width],Vector2.down,h,ref slope);
                if(z+1<terrain.height)ScenarioSlope(i+terrain.width,terrain.crestZ[i],Vector2.up,h,ref slope);
                currents[i]=Vector2.ClampMagnitude(slope,maximumCurrent)*Mathf.Clamp01(depth[i]/.6f);
            }
            double rain=profile.peakDepth*(1.8*(profile.Fraction(seconds)-profile.Fraction(SimulatedSeconds))+.00004*Mathf.Max(0,seconds-SimulatedSeconds))*exposed*area;
            double change=volume-WaterVolume;RainVolume+=rain;StormInflowVolume+=Math.Max(0,change-rain);DrainedVolume+=Math.Max(0,rain-change);
            WaterVolume=volume;SimulatedSeconds=seconds;
        }
        void ScenarioSlope(int b,float barrier,Vector2 direction,float height,ref Vector2 slope)
        {
            if(kind[b]==0||height<=barrier)return;
            float other=kind[b]==2?terrain.ground[b]:surfaceHeight[b];
            slope+=direction*Mathf.Max(0,(height-other)/terrain.cellSize)*.8f;
        }
        // Continuous-state CA with a four-cell (von Neumann) neighborhood.
        // Compute every edge from the same post-rain state, then apply the buffered
        // transfers together. Shared donor limits prevent creating or overdrawing water.
        void Advance(float dt)
        {
            Array.Clear(delta,0,delta.Length);Array.Clear(outgoing,0,outgoing.Length);
            // The storm keeps raining after the initial rise; runoff can leave through the coast.
            float rain=FloodDifficultySettings.Current.RainRate(SimulatedSeconds+dt*.5f)*dt;
            float area=terrain.cellSize*terrain.cellSize;
            for(int i=0;i<depth.Length;i++)if(kind[i]==1&&!terrain.covered[i]){depth[i]+=rain;RainVolume+=rain*area;}
            for(int z=0;z<terrain.height;z++)for(int x=0;x<terrain.width;x++)
            {
                int i=z*terrain.width+x;qx[i]=x+1<terrain.width?Flux(i,i+1,terrain.crestX[i],dt):0;qz[i]=z+1<terrain.height?Flux(i,i+terrain.width,terrain.crestZ[i],dt):0;
                AddOutgoing(i,i+1,qx[i]);AddOutgoing(i,i+terrain.width,qz[i]);
            }
            Array.Clear(currents,0,currents.Length);
            for(int z=0;z<terrain.height;z++)for(int x=0;x<terrain.width;x++)
            {
                int i=z*terrain.width+x;
                if(x+1<terrain.width)Transfer(i,i+1,qx[i],Vector2.right,dt);
                if(z+1<terrain.height)Transfer(i,i+terrain.width,qz[i],Vector2.up,dt);
            }
            WaterVolume=0;
            for(int i=0;i<depth.Length;i++)
            {
                if(kind[i]==1){depth[i]=Mathf.Max(0,depth[i]+delta[i]);WaterVolume+=depth[i]*area;currents[i]=Vector2.ClampMagnitude(currents[i],maximumCurrent);}
                else if(kind[i]==2)DrainedVolume+=delta[i]*area;
            }
            EnforceDepthLimit(SimulatedSeconds+dt);
        }
        void EnforceDepthLimit(float seconds)
        {
            // Controlled overflow is accounted as outflow, never silently discarded.
            // Reserve the full small-wave amplitude so visual crests also respect the peak.
            float limit=Mathf.Max(0,FloodDifficultySettings.Current.EffectiveLimit(seconds,RequiredMissionStage)-Mathf.Abs(waveHeight));
            double area=terrain.cellSize*terrain.cellSize;WaterVolume=0;MaximumSimulatedDepth=0;
            for(int i=0;i<depth.Length;i++)if(kind[i]==1)
            {
                if(groundRelativeInundation && depth[i]<limit)
                { StormInflowVolume+=(limit-depth[i])*area; depth[i]=limit; }
                float overflow=Mathf.Max(0,depth[i]-limit);depth[i]-=overflow;
                CapOverflowVolume+=overflow*area;DrainedVolume+=overflow*area;WaterVolume+=depth[i]*area;
                MaximumSimulatedDepth=Mathf.Max(MaximumSimulatedDepth,depth[i]);
            }
        }
        float Flux(int a,int b,float crest,float dt)
        {
            if(kind[a]==0||kind[b]==0)return 0;
            float ha=terrain.ground[a]+depth[a],hb=terrain.ground[b]+depth[b];
            float wetHead=Mathf.Max(0,Mathf.Max(ha,hb)-crest);
            float difference=ha-hb;
            float velocity=Mathf.Sign(difference)*Mathf.Min(maximumCurrent,2*Mathf.Sqrt(9.81f*wetHead*Mathf.Abs(difference)/terrain.cellSize));
            // The head-difference limit prevents four neighbours overshooting a level pool.
            float transfer=velocity*wetHead*dt/terrain.cellSize;
            return Mathf.Sign(transfer)*Mathf.Min(Mathf.Abs(transfer),Mathf.Abs(difference)*.24f);
        }
        void AddOutgoing(int a,int b,float q){if(q>0)outgoing[a]+=q;else if(q<0&&b<outgoing.Length)outgoing[b]-=q;}
        void Transfer(int a,int b,float q,Vector2 direction,float dt)
        {
            if(q==0)return;int donor=q>0?a:b;
            // All four edges share the same volume limit: no negative water, no uphill pumping.
            float available=kind[donor]==2?0:depth[donor];
            q*=Mathf.Min(1,available/Mathf.Max(.000001f,outgoing[donor]));delta[a]-=q;delta[b]+=q;
            float velocity=q*terrain.cellSize/dt*.5f;
            currents[a]+=direction*(velocity/Mathf.Max(.04f,depth[a]));currents[b]+=direction*(velocity/Mathf.Max(.04f,depth[b]));
        }
        void RebuildSurface()
        {
            int w=terrain.width;
            var toLocal=transform.worldToLocalMatrix;
            if(!experience)experience=GetComponent<SibucaoEvacuationExperience>();
            float drainStrength=experience?experience.DrainStrength:0;
            var drainCenter=drainStrength>0?experience.DrainCenter:Vector3.zero;
            for(int i=0;i<depth.Length;i++)
            {
                float h=terrain.ground[i]+depth[i];
                if(depth[i]<=Wet)
                {
                    float total=0;int n=0,x=i%w,z=i/w;
                    if(x>0)Neighbor(i-1,ref total,ref n);if(x<w-1)Neighbor(i+1,ref total,ref n);if(z>0)Neighbor(i-w,ref total,ref n);if(z<terrain.height-1)Neighbor(i+w,ref total,ref n);
                    if(n>0)h=Mathf.Min(h,total/n);
                }
                var p=terrain.Position(i);
                if(drainStrength>0)h=Mathf.Max(terrain.ground[i],h-SibucaoEvacuationExperience.ReliefAt(p,drainCenter,drainStrength));
                surfaceHeight[i]=h;p.y=h;vertices[i]=toLocal.MultiplyPoint3x4(p);wetness[i]=new Vector2(h-terrain.ground[i],0);
            }
            mesh.vertices=vertices;mesh.uv=wetness;mesh.uv2=currents;mesh.RecalculateBounds();
        }
        void Neighbor(int i,ref float total,ref int count){if(kind[i]==1&&depth[i]>Wet){total+=terrain.ground[i]+depth[i];count++;}}
        bool WaterTriangle(int a,int b,int c)=>kind[a]!=0&&kind[b]!=0&&kind[c]!=0&&(kind[a]==1||kind[b]==1||kind[c]==1);
        bool Triangle(Vector3 p,out int a,out int b,out int c,out Vector3 weights)
        {
            a=b=c=0;weights=default;if(depth==null)return false;
            float fx=(p.x-terrain.origin.x)/terrain.cellSize,fz=(p.z-terrain.origin.y)/terrain.cellSize;int x=Mathf.FloorToInt(fx),z=Mathf.FloorToInt(fz);
            if(x<0||z<0||x>=terrain.width-1||z>=terrain.height-1)return false;
            fx-=x;fz-=z;int i=z*terrain.width+x;
            if(fx+fz<=1){a=i;b=i+1;c=i+terrain.width;weights=new Vector3(1-fx-fz,fx,fz);}
            else{a=i+terrain.width+1;b=i+terrain.width;c=i+1;weights=new Vector3(fx+fz-1,1-fx,1-fz);}
            // Mixed coastal triangles join runoff to sea level instead of leaving a cell-wide gap.
            return WaterTriangle(a,b,c);
        }
        public bool TrySurface(Vector3 p,out float height,out float ground)
        {
            height=ground=0;
            if(dryRefuge&&dryRefuge.Contains(p))return false;
            if(Triangle(p,out int a,out int b,out int c,out var w))
            {
                ground=terrain.ground[a]*w.x+terrain.ground[b]*w.y+terrain.ground[c]*w.z;
                height=surfaceHeight[a]*w.x+surfaceHeight[b]*w.y+surfaceHeight[c]*w.z;
                if(height-ground>Wet)
                {
                    // Same displaced triangle as the GPU, including shoreline wave damping.
                    height+=Wave(a)*w.x+Wave(b)*w.y+Wave(c)*w.z;return height-ground>Wet;
                }
            }
            if(sea&&sea.TrySample(p,out var sample)){height=sample.height;ground=height-12;return true;}return false;
        }
        float Wave(int i)
        {
            var p=terrain.Position(i);float t=Time.time*waveSpeed,amplitude=waveHeight;
            return amplitude*(Mathf.Sin(p.x*1.3f+p.z*.65f-t*1.8f)*.55f+Mathf.Sin(p.x*-.8f+p.z*1.7f-t*1.3f)*.3f+Mathf.Sin(p.x*2.4f+p.z*1.1f+t*2.1f)*.15f)*Mathf.Clamp01((surfaceHeight[i]-terrain.ground[i])/.3f);
        }
        public Vector3 GetFlowVelocity(Vector3 p)
        {
            if(dryRefuge&&dryRefuge.Contains(p))return Vector3.zero;
            if(Triangle(p,out int a,out int b,out int c,out var w)&&TrySurface(p,out _,out _)){var v=currents[a]*w.x+currents[b]*w.y+currents[c]*w.z;return new Vector3(v.x,0,v.y);}
            return sea&&sea.TrySample(p,out var water)?water.velocity:Vector3.zero;
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}


