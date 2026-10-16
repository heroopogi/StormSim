using System;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoVehicleValidation
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        public static string Run()
        {
            Check(Application.isPlaying,"Run in Play mode.");
            var s=SibucaoSession.Instance;s.StartRun();int checkedCount=0;
            foreach(var v in SibucaoVehicle.All)
            {
                s.player.Teleport(v.transform.position-v.transform.right*2,v.transform.forward);
                Check(v.TryEnter(s.player),v.name+" cannot enter");
                Check(!s.player.GetComponent<CharacterController>().enabled,"Walking collider active while seated");
                Check(Vector3.Dot(s.player.transform.forward,v.transform.forward)>.99f,"Driver is not facing forward");
                Vector3 start=v.transform.position;
                for(int i=0;i<50;i++)v.Drive(1,0,false,.02f);
                Check(Vector3.Distance(start,v.transform.position)>.2f,v.name+" cannot move");
                for(int i=0;i<100;i++)v.Drive(0,0,true,.02f);
                Check(Mathf.Abs(v.Speed)<.001f,"Braking failed");
                Check(v.TryExit(),v.name+" cannot exit");
                Check(s.player.GetComponent<CharacterController>().enabled,"Walking collider not restored");
                Check(v.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff),"Vehicle artwork not restored");
                v.ResetVehicle();checkedCount++;
            }
            var car=SibucaoVehicle.All.First(v=>v.name=="car 185 - ");
            s.player.Teleport(car.transform.position-car.transform.right*2,car.transform.forward);car.TryEnter(s.player);
            var initial=car.transform.position;
            for(int i=0;i<40;i++)car.Drive(-1,.25f,false,.02f);
            Check(Vector3.Dot(car.transform.position-initial,car.transform.forward)<-.1f,"Reverse failed");
            var save=JsonUtility.FromJson<VehicleState>(JsonUtility.ToJson(car.Capture()));
            car.ResetVehicle();car.Restore(save);
            Check(Vector3.Distance(car.transform.position,save.position)<.001f,"Vehicle restore failed");
            car.ResetVehicle();s.player.Teleport(car.transform.position-car.transform.right*2,car.transform.forward);car.TryEnter(s.player);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name="Temporary vehicle collision validation";
                wall.transform.SetPositionAndRotation(car.transform.position+car.transform.forward*6+Vector3.up,car.transform.rotation);
                wall.transform.localScale=new Vector3(8,3,.5f);Physics.SyncTransforms();
                var start=car.transform.position;
                for(int i=0;i<300;i++)car.Drive(1,0,false,.02f);
                Check(Vector3.Distance(start,car.transform.position)<3.7f,"Vehicle passed through wall");
                Check(Mathf.Abs(car.Speed)<.001f,"Vehicle did not stop against wall");
            }
            finally{UnityEngine.Object.DestroyImmediate(wall);car.ResetVehicle();s.Pause();}
            return "PASS: "+checkedCount+" vehicles enter, accelerate, brake, exit and restore artwork; reverse/steering, JSON vehicle state, wall collision.";
        }
    }
}
