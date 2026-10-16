using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Sibucao
{
    [DisallowMultipleComponent]
    public sealed class PhysicalEncounterRunner : MonoBehaviour
    {
        SibucaoPhysicalOption option;
        PhysicalEncounterDefinition definition;
        bool ownsDefinition;
        PhysicalEncounterDefinition groundDefinition;
        bool ownsGroundDefinition;
        Transform assembly;
        readonly List<DiegeticWorkPart> parts=new List<DiegeticWorkPart>();
        DiegeticWorkPart held;
        Material wood,metal,cloth;
        AudioSource scrape;
        AudioClip scrapeClip;
        SibucaoSession S=>SibucaoSession.Instance;
        public PhysicalEncounterDefinition Definition=>definition;
        public bool Holding=>held;
        public IReadOnlyList<DiegeticWorkPart> Parts=>parts;
        public PhysicalEncounterProgress Progress
        {
            get
            {
                var p=option.Attempt.encounter;
                int count=definition?definition.OperationCount:0;
                if(p==null||p.physicalVersion!=3||p.displacement==null||p.displacement.Length!=count)
                    option.Attempt.encounter=p=new PhysicalEncounterProgress {physicalVersion=3,displacement=new float[count]};
                return p;
            }
        }
        public bool Complete=>Progress.Complete(definition);
        public bool Working=>option&&option.Engaged&&definition&&definition.Valid&&!Complete;
        public Vector3 TargetPosition=>assembly&&CurrentIndex<parts.Count?parts[CurrentIndex].transform.position:option.actionPoint?option.actionPoint.position:option.transform.position;
        int CurrentIndex=>Progress.phase>=definition.phases.Length?definition.OperationCount:definition.PhaseOffset(Progress.phase)+Progress.operation;
        bool BagWork=>option.mechanism==PhysicalMechanism.RepairStrap;
        string WorkStage=>Progress.phase<definition.phases.Length?definition.phases[Progress.phase].title:"CONSEQUENCE";
        SibucaoCarEnclosure carEnclosure;
        public string Prompt
        {
            get
            {
                if(Complete)return BagWork?"Seat the connector in the buckle and release":"Continue through the opening";
                if(!GatheringDone)return WorkStage+": pick up the remaining supplies beside the bag";
                if(held)return WorkStage+": hold E + drag — "+held.action+" / release E: let go";
                var part=FocusedPart();
                if(part&&part.owner==this)return part.index==CurrentIndex?WorkStage+": hold E + drag — "+part.action:"The earlier action must be finished first";
                return WorkStage+": "+(BagWork?"work the loose strap through the buckle":"find and move the highlighted obstruction");
            }
        }
        public void Initialize(SibucaoPhysicalOption binding,PhysicalEncounterDefinition authored)
        {
            option=binding;definition=authored&&authored.Valid?authored:PhysicalEncounterCatalog.Create(binding.station.index,binding.choiceIndex);
            carEnclosure=binding.FloodAdapted?null:binding.station.GetComponent<SibucaoCarEnclosure>();
            ownsDefinition=definition!=authored;
        }
        public void SetFloodWork(PhysicalEncounterDefinition work,bool preserve)
        {
            if(!work)return;
            if(!groundDefinition){groundDefinition=definition;ownsGroundDefinition=ownsDefinition;}
            else if(ownsDefinition&&definition)Destroy(definition);
            definition=work;ownsDefinition=true;carEnclosure=null;Rebuild();
            // Progress is deliberately unchanged, including partially moved pieces.
        }
        public void RestoreGroundWork()
        {
            if(!groundDefinition)return;
            if(ownsDefinition&&definition)Destroy(definition);
            definition=groundDefinition;ownsDefinition=ownsGroundDefinition;groundDefinition=null;
            carEnclosure=option.station.GetComponent<SibucaoCarEnclosure>();Rebuild();
        }
        bool Ready
        {
            get
            {
                bool focused=Application.isFocused;
#if UNITY_EDITOR
                focused|=SibucaoEditorInput.Active;
#endif
                return S&&focused&&S.Mode==SibucaoMode.Playing&&(!S.home||!S.home.MovementLocked)&&
                    (!S.inventory||!S.inventory.BlocksInput)&&(!S.missionGuide||!S.missionGuide.BlocksInput);
            }
        }
        bool GatheringDone
        {
            get
            {
                if(option.mechanism!=PhysicalMechanism.GatherItems||Progress.phase==0||Progress.gatherItems==null)return true;
                foreach(var id in Progress.gatherItems)
                {
                    var item=S.inventory.Find(id);var data=option.station.manager.Data;
                    if(item&&!item.delivered&&!data.flags.Contains("lost:"+id)&&!data.flags.Contains("consumed:"+id)&&
                        (data.inventorySlots==null||System.Array.IndexOf(data.inventorySlots,id)<0))return false;
                }
                return true;
            }
        }
        public void ResetPresentation()
        {
            held=null;SibucaoControls.ResetGrip();if(scrape)scrape.Stop();
            // Reuse the physical assembly: beginning a choice must not destroy a gripped part.
            if(assembly)foreach(var part in parts)part.Apply(0);
        }
        public void Rebuild()
        {
            held=null;if(assembly){assembly.gameObject.SetActive(false);Destroy(assembly.gameObject);}
            assembly=null;parts.Clear();
        }
        public void Tick()
        {
            if(!definition||!definition.Valid||!S)return;
            if(!assembly)BuildAssembly();
            if(carEnclosure||option.FloodAdapted)
            {
                // Alternative car choices share one work area, not four overlapping piles.
                int selected=option.Attempt.selected;
                assembly.gameObject.SetActive(option.choiceIndex==(selected<0?0:selected));
            }
            if(!option.Engaged){held=null;return;}
            if(!Ready){held=null;if(scrape)scrape.Stop();return;}
            var p=Progress;
            for(int i=0;i<parts.Count;i++)parts[i].Apply(p.displacement[i]);
            if(option.mechanism==PhysicalMechanism.GatherItems&&p.gatherItems==null)
            {
                var ids=new List<string>();
                foreach(var item in FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(item.station==option.station&&item.gameObject.activeInHierarchy&&SibucaoInventory.Storable(item)&&!item.delivered)ids.Add(item.persistentId);
                p.gatherItems=ids.ToArray();
            }
            if(BagWork&&option.station.brokenBag)assembly.SetPositionAndRotation(option.station.brokenBag.transform.position,option.station.brokenBag.transform.rotation);
            if(held&&Vector3.Distance(S.player.view.transform.position,held.transform.position)>3.2f)ReleaseGrip();
        }
        public static DiegeticWorkPart FocusedPart()
        {
            var s=SibucaoSession.Instance;if(!s||!s.player)return null;
            var view=s.player.view.transform;
            var hits=Physics.RaycastAll(view.position,view.forward,2.8f,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(s.player.transform)||hit.collider.GetComponentInParent<PhysicalGrabbable>()?.IsHeld==true)continue;
                return hit.collider.GetComponentInParent<DiegeticWorkPart>();
            }
            // VR/desktop comfort fallback: the current physical piece can be acquired
            // inside a narrow near-hand cone even when its thin edge misses the ray.
            // This never chooses a branch or skips sequence order.
            DiegeticWorkPart nearest=null;float nearestDistance=3f;
            foreach(var part in FindObjectsByType<DiegeticWorkPart>(FindObjectsSortMode.None))
            {
                if(!part||!part.gameObject.activeInHierarchy||!part.owner||part.index!=part.owner.CurrentIndex)continue;
                Vector3 delta=part.GetComponent<Collider>().bounds.center-view.position;float distance=delta.magnitude;
                if(distance<nearestDistance&&Vector3.Dot(view.forward,delta.normalized)>.72f){nearest=part;nearestDistance=distance;}
            }
            if(nearest)return nearest;
            return null;
        }
        public bool Interact()
        {
            if(!Ready||Complete)return false;
            var part=FocusedPart();if(!part||part.owner!=this)return false;
            if(!option.Engaged&&!option.Begin(true))return true;
            if(part.index!=CurrentIndex){S.Say("The loose piece on top has to come out first.",3);return true;}
            if(!GatheringDone){S.Say("There are still supplies on the ground.",3);return true;}
            if(!BagWork&&S.player.grabber&&S.player.grabber.held){S.Say("I need to put this down first.",3);return true;}
            held=part;SibucaoControls.BeginGrip();S.player.Gesture(1);return true;
        }
        // Mouse input moves the gripped object along its physical joint, never a reticle.
        public bool Manipulate(Vector2 drag,bool grip,float realDelta)
        {
            if(!held)return false;
            if(!Working||!Ready||!grip){ReleaseGrip();return false;}
            var view=S.player.view.transform;Vector3 axis=held.PullDirection;
            Vector2 screenAxis=new Vector2(Vector3.Dot(axis,view.right),Vector3.Dot(axis,view.up));
            if(screenAxis.sqrMagnitude<.08f)screenAxis=Vector2.up;
            float requested=Vector2.Dot(drag,screenAxis.normalized)*.045f;
            var p=Progress;float before=p.displacement[held.index];
            float candidate=p.ProposedPosition(definition,requested,realDelta);
            p.MovePhysical(definition,held.AllowedDisplacement(before,candidate),realDelta);
            held.Apply(p.displacement[held.index]);S.player.Gesture(1);
            if(scrape)
            {
                scrape.transform.position=held.transform.position;
                bool moving=Mathf.Abs(p.displacement[held.index]-before)>.00001f;
                scrape.volume=moving?.09f*SibucaoAudioMix.Effects:0;if(moving&&!scrape.isPlaying)scrape.Play();
            }
            return true;
        }
        void ReleaseGrip()
        {
            if(!held)return;
            if(Progress.displacement[held.index]>=1)Progress.ReleasePhysical(definition);
            held=null;if(scrape)scrape.Stop();
        }
        void LateUpdate()
        {
            if(!held||!Ready||!S.player.hands)return;
            var local=S.player.hands.parent.InverseTransformPoint(held.transform.position);
            S.player.hands.localPosition=Vector3.Lerp(S.player.hands.localPosition,Vector3.ClampMagnitude(local,1.35f),Time.deltaTime*12);
        }
        Vector3 RoutePoint(float fraction)
        {
            var path=option.path;if(path==null||path.Length==0)return option.transform.position;
            float remaining=option.PathLength*fraction;
            for(int i=1;i<path.Length;i++)
            {
                float length=Vector3.Distance(path[i-1].position,path[i].position);
                if(remaining<=length)return Vector3.Lerp(path[i-1].position,path[i].position,length>.001f?remaining/length:0);
                remaining-=length;
            }
            return path[path.Length-1].position;
        }
        Material Surface(string materialName,Color fallback)
        {
            foreach(var m in Resources.FindObjectsOfTypeAll<Material>())if(m.name==materialName)return m;
            var created=new Material(Shader.Find("Universal Render Pipeline/Lit"));created.name="Encounter "+materialName;created.color=fallback;return created;
        }
        void BuildAssembly()
        {
            assembly=new GameObject("Physical obstruction - "+option.Choice.id).transform;assembly.SetParent(transform,false);
            if(!wood)wood=Surface("Wet timber",new Color(.24f,.21f,.16f));
            if(!metal)metal=Surface("Wet galvanized metal",new Color(.32f,.35f,.36f));
            if(!cloth)cloth=Surface("Weathered canvas",new Color(.2f,.27f,.2f));
            if(BagWork)
            {
                if(option.station.brokenBag)assembly.SetPositionAndRotation(option.station.brokenBag.transform.position,option.station.brokenBag.transform.rotation);
                else if(option.station.fallbackBag)assembly.SetPositionAndRotation(option.station.fallbackBag.transform.position,option.station.fallbackBag.transform.rotation);
                BuildStrap();
            }
            else for(int phase=0;phase<definition.phases.Length;phase++)
            {
                var group=new GameObject("Lodged debris "+phase).transform;group.SetParent(assembly,false);
                bool anchored=!option.FloodAdapted&&option.coastalWorkAnchors!=null&&phase<option.coastalWorkAnchors.Length;
                group.position=carEnclosure?carEnclosure.WorkPoint(phase):anchored?option.transform.TransformPoint(option.coastalWorkAnchors[phase]):RoutePoint(definition.phases[phase].routeFraction);group.rotation=option.station.transform.rotation;
                BuildDebris(group,phase);
            }
            for(int i=0;i<parts.Count;i++)parts[i].Apply(option.Engaged?Progress.displacement[i]:0);
            BuildScrape();
        }
        void BuildDebris(Transform group,int phase)
        {
            bool sheet=option.mechanism==PhysicalMechanism.Mantle||option.mechanism==PhysicalMechanism.Squeeze||option.mechanism==PhysicalMechanism.FlashlightPassage;
            float width=sheet?1.05f:1.6f;
            int count=definition.phases[phase].sequence.Length;
            for(int i=0;i<count;i++)
            {
                float height=.16f+(count-1-i)*.15f;Vector3 start=new Vector3(0,height,(i%2==0?-.15f:.15f));
                Vector3 end=start+new Vector3(i%2==0?1.65f:-1.65f,-height+.1f,.45f+i*.3f);
                var p=Part(group,definition.PhaseOffset(phase)+i,definition.phases[phase].sequence[i],start,end,
                    new Vector3(width,sheet?.045f:.13f,sheet?.48f:.22f),sheet?metal:wood,Quaternion.Euler(0,(i%2==0?1:-1)*16,0),Quaternion.Euler(0,(i%2==0?1:-1)*65,0));
                if(sheet)for(int ridge=0;ridge<5;ridge++)Detail(p.transform,"Roofing ridge",new Vector3(0,.48f,(ridge-2)*.18f),new Vector3(1,.35f,.035f),metal);
                else for(int nail=0;nail<2;nail++)Detail(p.transform,"Rusted nail",new Vector3(nail==0?-.38f:.38f,.5f,0),new Vector3(.015f,.08f,.08f),metal);
            }
        }
        void BuildStrap()
        {
            for(int phase=0;phase<definition.phases.Length;phase++)
            {
                var buckle=new GameObject("Strap buckle "+phase).transform;buckle.SetParent(assembly,false);buckle.localPosition=new Vector3((phase-1)*.18f,.33f,-.18f);
                Detail(buckle,"Buckle left",new Vector3(-.042f,0,0),new Vector3(.012f,.025f,.13f),metal);
                Detail(buckle,"Buckle right",new Vector3(.042f,0,0),new Vector3(.012f,.025f,.13f),metal);
                Detail(buckle,"Buckle crossbar",new Vector3(0,0,.06f),new Vector3(.095f,.025f,.012f),metal);
                int index=definition.PhaseOffset(phase);string action=definition.phases[phase].sequence[0];
                if(phase==0)Part(buckle,index,action,new Vector3(0,.018f,0),new Vector3(0,.065f,-.025f),new Vector3(.065f,.016f,.09f),metal,Quaternion.identity,Quaternion.Euler(-65,0,0));
                else if(phase==1)Part(buckle,index,action,new Vector3(0,-.012f,-.19f),new Vector3(0,-.012f,-.04f),new Vector3(.06f,.012f,.25f),cloth,Quaternion.identity,Quaternion.identity);
                else Part(buckle,index,action,new Vector3(0,-.029f,.09f),new Vector3(0,-.029f,.25f),new Vector3(.057f,.012f,.25f),cloth,Quaternion.Euler(0,15,0),Quaternion.identity);
            }
        }
        DiegeticWorkPart Part(Transform parent,int index,string action,Vector3 start,Vector3 end,Vector3 size,Material material,Quaternion from,Quaternion to)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=action;go.transform.SetParent(parent,false);go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            var p=go.AddComponent<DiegeticWorkPart>();p.owner=this;p.index=index;p.action=action;p.closed=start;p.open=end;p.closedRotation=from;p.openRotation=to;
            var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;p.Apply(0);parts.Add(p);return p;
        }
        void Detail(Transform parent,string name,Vector3 at,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);
        }
        void BuildScrape()
        {
            if(!scrape){scrape=new GameObject("Work friction").AddComponent<AudioSource>();scrape.transform.SetParent(transform,false);}
            scrape.spatialBlend=1;scrape.loop=true;scrape.volume=0;scrape.maxDistance=6;
            if(!scrapeClip)
            {
                var samples=new float[11025];var random=new System.Random(19);float last=0;
                for(int i=0;i<samples.Length;i++){last=Mathf.Lerp(last,(float)random.NextDouble()*2-1,.18f);samples[i]=last*.25f;}
                scrapeClip=AudioClip.Create("Friction",samples.Length,1,22050,false);scrapeClip.SetData(samples,0);
            }
            scrape.clip=scrapeClip;
        }
        void OnDestroy(){if(ownsGroundDefinition&&groundDefinition)Destroy(groundDefinition);if(ownsDefinition&&definition)Destroy(definition);if(scrapeClip)Destroy(scrapeClip);}
    }
}
