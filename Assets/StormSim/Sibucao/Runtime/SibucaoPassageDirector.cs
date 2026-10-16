using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao
{
    [DefaultExecutionOrder(180)] public sealed class SibucaoPassageDirector:MonoBehaviour
    {
        public GameObject[] layouts;
        public SibucaoStormPassage[] passages;
        public SibucaoStormPassage Held {get;set;}
        public SibucaoPassageHandle Focused {get;private set;}
        public string LastCompletion {get;private set;}
        public float CompletionUntil {get;private set;}
        SibucaoSession S=>SibucaoSession.Instance;
        SibucaoStormPassage[] active=new SibucaoStormPassage[0];
        readonly bool[] hasWork=new bool[9];
        public bool Installed=>layouts!=null&&layouts.Length==4&&passages!=null&&passages.Length>0;
        public bool WorkDone(int zone)=>Installed&&S.state.physical.flags.Contains("passage_work_"+zone);
        public bool ZoneHasObstacle(int zone)=>Installed&&zone>=0&&zone<hasWork.Length&&hasWork[zone];
        public SibucaoStormPassage WorkPassage(int zone)
            =>active.Where(p=>p.zone==zone&&p.core&&p.canOperate&&!p.State.crossed).OrderBy(p=>(p.transform.position-S.player.transform.position).sqrMagnitude).FirstOrDefault();
        public void Restore()
        {
            Held=null;Focused=null;CompletionUntil=0;if(!Installed)return;
            for(int i=0;i<layouts.Length;i++)layouts[i].SetActive(i==S.state.homeIndex);
            active=passages.Where(p=>p&&p.home==S.state.homeIndex).ToArray();
            System.Array.Clear(hasWork,0,hasWork.Length);
            foreach(var p in active){p.Restore();if(p.core)hasWork[p.zone]=true;if(p.State.crossed)S.physicalDecisions.SetFlag("passage_work_"+p.zone);}Physics.SyncTransforms();
        }
        void Update()
        {
            Focused=null;if(!S||S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked)return;
            if(S.inventory&&S.inventory.IsOpen||S.missionGuide&&S.missionGuide.IsOpen){Held=null;return;}
            var view=S.player.view.transform;
            foreach(var hit in Physics.RaycastAll(view.position,view.forward,3,~0,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance))
            {if(hit.transform.IsChildOf(S.player.transform))continue;var handle=hit.collider.GetComponent<SibucaoPassageHandle>();if(hit.collider.isTrigger&&!handle)continue;Focused=handle;break;}
            if(Held&&Vector3.Distance(S.player.transform.position,Held.grip.transform.position)>6)Held=null;
            foreach(var p in active)if((p.transform.position-S.player.transform.position).sqrMagnitude<144)p.Tick(Time.deltaTime);
        }
        // Residents use the same physical opening. Closed debris makes them wait;
        // securing it lets the queue move without a scored celebration cue.
        public SibucaoStormPassage ResidentPassage(Vector3 position,Vector3 forward,string passed)
        {
            SibucaoStormPassage best=null;float nearest=81;
            foreach(var p in active)
            {
                if(!p.canOperate||p.persistentId==passed)continue;
                var d=p.transform.position-position;d.y=0;
                if(Vector3.Dot(d,forward)<-.4f||Mathf.Abs(Vector3.Cross(forward,d).y)>3.8f)continue;
                if(d.sqrMagnitude<nearest){nearest=d.sqrMagnitude;best=p;}
            }
            return best;
        }
        public bool Interact()
        {
            if(Held&&Held.State.opening>=.98f)return Held.Interact(2);
            return Focused&&Focused.passage.Interact(Focused.part);
        }
        public string Prompt=>Held?Held.Prompt(2):Focused?Focused.passage.Prompt(Focused.part):null;
        public void Crossed(SibucaoStormPassage p)
        {
            S.physicalDecisions.SetFlag("passage_crossed:"+p.persistentId);
            S.physicalDecisions.SetFlag("passage_work_"+p.zone);
            if(!p.core)S.physicalDecisions.SetFlag("side_passage_"+p.zone);
            S.Notify("Passage cleared",p.core?"Continue through the situation.":"You are continuing through a side passage.");
        }
        public void Completed(PhysicalDecisionStation station,int choice)
        {
            string[] facts={"Street crossing completed. The opening stays clear.","You passed the stalled vehicles.","You crossed the junction.","You passed the fallen cable.",choice==0?"The bag strap is fastened.":choice==1?"You collected the scattered supplies.":"You left the damaged bag behind.",choice==2?"You continued past the resident.":"The resident reached the landing.","You left the rescue checkpoint.",choice==2?"You continued past the evacuee.":"The evacuee reached the landing.","Creek crossing completed."};
            LastCompletion=facts[station.index];CompletionUntil=Time.unscaledTime+12;
            S.Notify("Situation completed",LastCompletion);
            if(station.index==0)S.physicalDecisions.SetFlag("first_crossing_open");
        }
        public void RecordWork(PhysicalChoiceResult record)
        {
            var work=active.Where(p=>p.zone==record.node&&p.State.crossed).Select(p=>p.State).ToArray();
            record.clearedPassages=work.Select(p=>p.id).ToArray();
            record.passageWorkSeconds=work.Sum(p=>p.workSeconds);record.passageWorkDistance=work.Sum(p=>p.distance);
            // Keep obstruction work separate from branch movement: the two may overlap.
            // The storm has already advanced by this actual time; never charge it twice.
            foreach(var p in work)if(p.startedAt>=0)record.startedAt=Mathf.Min(record.startedAt,p.startedAt);
        }
    }
}
