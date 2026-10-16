using System;
using UnityEditor;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoEncounterChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        public static string Run()
        {
            int count=0;int[] choices={3,4,4,3,3};
            for(int station=0;station<5;station++)for(int choice=0;choice<choices[station];choice++)
            {
                var d=PhysicalEncounterCatalog.Create(station,choice);
                try
                {
                    Require(d&&d.Valid,"Definition missing");
                    var p=new PhysicalEncounterProgress{physicalVersion=3,displacement=new float[d.OperationCount]};
                    Require(!p.ReleasePhysical(d),"Release skipped an unmoved object");
                    for(int i=0;i<500;i++)p.MovePhysical(d,0,.05f);
                    Require(p.activeSeconds==0,"Static hold counted");
                    p.MovePhysical(d,1,100);Require(p.activeSeconds<=.1001f,"Hitch bypassed physical speed");
                    p.MovePhysical(d,0,.1f);Require(p.displacement[0]==0,"Could not reverse movement");
                    int frames=0,releases=0;
                    while(!p.Complete(d)&&frames<10000)
                    {
                        p.MovePhysical(d,1,.02f);frames++;
                        if(p.displacement[d.PhaseOffset(p.phase)+p.operation]>=1)
                        {
                            Require(p.ReleasePhysical(d),"Physical release failed");releases++;
                            p=JsonUtility.FromJson<PhysicalEncounterProgress>(JsonUtility.ToJson(p));
                        }
                    }
                    Require(p.Complete(d)&&frames*.02f>=d.MinimumSeconds&&releases==d.OperationCount,"Physical duration floor or joints failed");
                    var old=new PhysicalEncounterProgress{phase=3,activeSeconds=100};Require(!old.Complete(d),"Old UI save bypassed physical work");
                    count++;
                }
                finally{UnityEngine.Object.DestroyImmediate(d);}
            }
            return "PASS: "+count+" physical definitions; displacement speed floors, idle, reversal, release, persistence and old UI-save rejection.";
        }
        [MenuItem("StormSim/Validation/Physical encounters B1-B5")]
        public static void RunMenu(){Debug.Log(Run());}
    }
}
