using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    public static class CellularFloodChecks
    {
        [UnityEditor.MenuItem("StormSim/Validation/Cellular Flood Solver")]
        public static void RunFromMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var go = new GameObject("CA isolated validation");
            var f = go.AddComponent<FloodSimulationManager>(); f.enabled = false;
            var t = ScriptableObject.CreateInstance<SibucaoFloodTerrain>();
            t.width = t.height = 5; t.cellSize = 2;
            t.ground = new float[25]; t.crestX = new float[25]; t.crestZ = new float[25];
            t.kind = Enumerable.Repeat((byte)1,25).ToArray();
            t.covered = Enumerable.Repeat(true,25).ToArray(); f.terrain = t;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(FloodSimulationManager);
            Action<string,object> set = (n,v) => type.GetField(n,flags).SetValue(f,v);
            Action step = () => type.GetMethod("Advance",flags).Invoke(f,new object[]{.1f});
            Action<bool,string> check = (ok,message) => { if(!ok)throw new Exception(message); };
            float[] d = null;
            Action reset = () => {
                d = new float[25]; d[12] = .3f;
                set("depth",d); set("kind",(byte[])t.kind.Clone());
                foreach(var n in new[]{"delta","outgoing","qx","qz"})set(n,new float[25]);
                set("currents",new Vector2[25]);
                type.GetProperty("SimulatedSeconds").SetValue(f,960f);
            };
            try
            {
                reset(); step();
                check(d[11]>0 && d[13]>0 && d[7]>0 && d[17]>0,"Four-neighbor transfer failed");
                check(Math.Abs(d.Sum()-.3f)<.00001f && d.All(v=>v>=0),"Conservation/nonnegative depth failed");
                var first=(float[])d.Clone(); reset(); step();
                check(d.SequenceEqual(first),"Deterministic replay failed");
                for(int i=0;i<25;i++)t.crestX[i]=t.crestZ[i]=100;
                reset(); step(); check(d[12]==.3f && d[11]==0,"Barrier leaked");
                Array.Clear(t.crestX,0,25); Array.Clear(t.crestZ,0,25);
                t.kind[13]=2; reset(); double before=f.DrainedVolume; step();
                check(f.DrainedVolume>before && Math.Abs(d.Where((v,i)=>t.kind[i]==1).Sum()*4+f.DrainedVolume-before-1.2)<.00001,"Drain conservation failed");
                t.kind[13]=1; Array.Clear(t.covered,0,25); reset();
                double rain=f.RainVolume; step();
                check(f.RainVolume>rain && Math.Abs(d.Sum()*4-1.2-(f.RainVolume-rain))<.00001,"Rain conservation failed");
                string report="PASS: four-neighbor transfer, nonnegative depths, water conservation, deterministic replay, barrier blocking, outlet accounting, rainfall accounting. Isolated production-solver fixtures; full route balance and headset performance remain unverified.";
                System.IO.File.WriteAllText("Reports/CellularFloodChecks.txt",report); return report;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(t); }
        }
    }
}
