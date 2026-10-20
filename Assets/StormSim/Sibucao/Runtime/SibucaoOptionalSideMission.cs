using System;
using System.Collections.Generic;
using StormSim.Preparedness;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    /// <summary>A short optional roadside simulation running beside the uninterrupted evacuation route.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class SibucaoOptionalSideMission : MonoBehaviour
    {
        const string RootName="CHAPTER 1 - Optional roadside mission";
        const string MissionId="family-supplies";
        const float Reach=4.5f;
        public GameObject parentPrefab,childPrefab;

        enum TargetKind { Parent, Supply }
        sealed class Target { public TargetKind kind;public int index;public Transform visual; }
        readonly Dictionary<Collider,Target> targets=new Dictionary<Collider,Target>();
        readonly Transform[] supplies=new Transform[2];
        readonly Vector3[] supplyStarts=new Vector3[2];
        Transform runtimeRoot,parentNpc,childNpc,cart;
        Animator parentAnimator,childAnimator;
        float missionDistance,familyTravel,workProgress;
        int workingSupply=-1;
        bool ready,familyPositioned;
        float blockedSeconds;
        public Vector3 FamilyPosition=>cart?cart.position:transform.position;
        public bool FamilyWaiting=>SuppliesSecured&&!Has("experience_family_arrived")&&blockedSeconds>2;

        SibucaoSession S=>SibucaoSession.Instance;
        PhysicalRunData Data=>S&&S.state!=null?S.state.physical:null;
        public bool Complete=>Has("side_family_complete");
        public bool Skipped=>Has("side_family_skipped");
        public bool Discovered=>Has("side_family_discovered");
        public bool Accepted=>Has("side_family_accepted");
        public bool SuppliesSecured=>Has("side_family_supplies_secured");
        public bool SideMissionVisible=>Discovered&&!Complete&&!Skipped;
        public bool SideMissionActive=>Accepted&&!Complete&&!Skipped;
        public bool HasFocusedInteraction=>Focused()!=null;
        public string ShortTitle=>"Banago Elementary School I";
        public string Objective=>"Evacuate to Banago Elementary School I.";
        public string SideMissionTitle=>"Help secure the family's supplies";
        public string EquipmentNote=>HasPrepared("gloves")?"Work gloves packed — safer, faster handling":HasPrepared("whistle")?"Whistle packed — useful for attracting nearby help":"No protective gloves — lift slowly and carefully";
        public int TaskCount=>3;
        public string TaskLabel(int index)
        {
            if(index==0)return Accepted?"Spoke with the parent":"Speak with the parent";
            if(index==1)return "Secure supplies  "+SecuredCount+" / 2";
            return Complete?"Family rejoined the route":"Family rejoins the route";
        }
        public float TaskProgress(int index)
        {
            if(index==0)return Accepted?1:0;
            if(index==1)return Mathf.Clamp01((SecuredCount+(workingSupply>=0?workProgress:0))/2f);
            return Complete?1:SuppliesSecured?Mathf.Clamp01(familyTravel/8f):0;
        }
        public float OverallProgress=>(TaskProgress(0)+TaskProgress(1)+TaskProgress(2))/3f;
        public Vector3 SideMissionTarget
        {
            get
            {
                if(workingSupply>=0&&workingSupply<supplies.Length&&supplies[workingSupply])return supplies[workingSupply].position;
                if(Accepted)for(int i=0;i<supplies.Length;i++)if(!SupplyDone(i)&&supplies[i])return supplies[i].position;
                return parentNpc?parentNpc.position:RoutePoint(missionDistance,1.8f);
            }
        }
        public Vector3 NavigationTarget
        {
            get
            {
                if(S&&S.physicalDecisions&&S.physicalDecisions.evacuationCenter&&S.physicalDecisions.evacuationCenter.arrivalPoint)
                    return S.physicalDecisions.evacuationCenter.arrivalPoint.position;
                return S&&S.route?S.route.Sample(S.route.length):transform.position;
            }
        }
        public Transform ContextTarget {get {var focused=Focused();return focused?.visual;}}
        public string Prompt
        {
            get
            {
                var target=Focused();if(target==null)return null;
                if(target.kind==TargetKind.Parent)return "E  Ask what happened  ·  Optional side mission";
                int seconds=Mathf.CeilToInt((1-workProgress)*WorkDuration);
                return "Hold E  Lift supply "+(target.index+1)+" of 2  ·  "+seconds+"s";
            }
        }
        public string Hint
        {
            get
            {
                if(!Discovered)return "Follow the minimap toward the school. Roadside incidents are optional.";
                if(Skipped)return "Continue following the minimap toward the school.";
                if(!Accepted)return "The route remains open. Look at the parent and press E to help, or continue evacuating.";
                if(!SuppliesSecured)return "Aim at a loose supply and hold E until its task bar is full.";
                if(!Complete)return "The family is moving again. Continue along the evacuation route.";
                return "Side mission complete. Continue toward the school.";
            }
        }
        public string ClearInstructions=>Hint+"\nWASD move · Mouse look · E interact/hold · Shift run";
        int SecuredCount=>(SupplyDone(0)?1:0)+(SupplyDone(1)?1:0);
        float WorkDuration=>HasPrepared("gloves")?1.35f:2.35f;

        void Start(){BuildAndLayout();RestorePresentation();}
        public void ResetRun(){BuildAndLayout();RestorePresentation();}
        public void Layout(){if(Application.isPlaying){BuildAndLayout();RestorePresentation();}}
        public void RestoreRun(){BuildAndLayout();RestorePresentation();}

        void BuildAndLayout()
        {
            if(!S||!S.route||Data==null)return;
            if(runtimeRoot)Destroy(runtimeRoot.gameObject);
            targets.Clear();workingSupply=-1;workProgress=0;familyTravel=0;familyPositioned=false;
            runtimeRoot=new GameObject(RootName).transform;runtimeRoot.SetParent(transform,false);
            missionDistance=Mathf.Min(S.route.length-45,Mathf.Max(8,Data.routeStart+28));
            var incident=WorldGroup("Optional mission - family supply cart",RoutePoint(missionDistance,1.9f),RouteRotation(missionDistance));
            parentNpc=CreateNpc(parentPrefab,"Parent",incident,new Vector3(-.95f,0,.15f),1f);
            childNpc=CreateNpc(childPrefab?childPrefab:parentPrefab,"Child",incident,new Vector3(.2f,0,-.65f),.72f);
            parentAnimator=parentNpc?parentNpc.GetComponentInChildren<Animator>():null;
            childAnimator=childNpc?childNpc.GetComponentInChildren<Animator>():null;
            if(parentNpc)Register(incident,TargetKind.Parent,0,parentNpc,new Vector3(1.2f,2.25f,1.2f),new Vector3(-.95f,1.05f,.15f));
            cart=LocalGroup("Family handcart",incident,new Vector3(.55f,0,.2f),Quaternion.identity);
            Part(cart,"Wet cart bed",PrimitiveType.Cube,new Vector3(0,.48f,0),new Vector3(.92f,.16f,.62f),new Color(.27f,.2f,.14f));
            Part(cart,"Rear rail",PrimitiveType.Cube,new Vector3(0,.8f,-.28f),new Vector3(.92f,.055f,.055f),new Color(.36f,.26f,.16f));
            Part(cart,"Left handle",PrimitiveType.Cylinder,new Vector3(-.38f,.83f,.5f),new Vector3(.035f,.5f,.035f),new Color(.25f,.18f,.12f),Quaternion.Euler(58,0,0));
            Part(cart,"Right handle",PrimitiveType.Cylinder,new Vector3(.38f,.83f,.5f),new Vector3(.035f,.5f,.035f),new Color(.25f,.18f,.12f),Quaternion.Euler(58,0,0));
            Part(cart,"Left wheel",PrimitiveType.Cylinder,new Vector3(-.5f,.28f,0),new Vector3(.22f,.075f,.22f),new Color(.06f,.07f,.07f),Quaternion.Euler(0,0,90));
            Part(cart,"Right wheel",PrimitiveType.Cylinder,new Vector3(.5f,.28f,0),new Vector3(.22f,.075f,.22f),new Color(.06f,.07f,.07f),Quaternion.Euler(0,0,90));
            var colors=new[]{new Color(.65f,.17f,.1f),new Color(.08f,.38f,.55f)};
            for(int i=0;i<supplies.Length;i++)
            {
                supplies[i]=LocalGroup("Loose sealed supply "+(i+1),incident,new Vector3(-.15f+i*.72f,.2f,1.32f+(i%2)*.28f),Quaternion.Euler(0,i==0?-14:17,0));
                var body=Part(supplies[i],"Waterproof family supply bag",PrimitiveType.Cube,Vector3.zero,new Vector3(.48f,.2f,.34f),colors[i]);
                Part(body,"Black securing strap",PrimitiveType.Cube,new Vector3(0,.55f,0),new Vector3(.15f,.06f,1.04f),new Color(.04f,.05f,.05f));
                Part(body,"Carry handle",PrimitiveType.Cylinder,new Vector3(0,.68f,0),new Vector3(.035f,.18f,.035f),new Color(.04f,.05f,.05f),Quaternion.Euler(0,0,90));
                supplyStarts[i]=supplies[i].position;
                Register(incident,TargetKind.Supply,i,body,new Vector3(.9f,.75f,.9f),supplies[i].localPosition+Vector3.up*.12f);
            }
            var saved=S.GetComponent<SibucaoEvacuationExperience>()?.State;
            if(saved!=null&&saved.familyPositionSaved&&SuppliesSecured)
            {cart.position=saved.familyCartPosition;parentNpc.position=saved.familyParentPosition;childNpc.position=saved.familyChildPosition;familyPositioned=true;}
            blockedSeconds=0;ready=true;Physics.SyncTransforms();
        }

        Transform CreateNpc(GameObject prefab,string displayName,Transform parent,Vector3 localPosition,float scale)
        {
            if(prefab)
            {
                var instance=Instantiate(prefab,parent);instance.name=displayName+" - animated resident";instance.transform.localPosition=localPosition;instance.transform.localRotation=Quaternion.identity;instance.transform.localScale=Vector3.one*scale;
                foreach(var resident in instance.GetComponentsInChildren<SibucaoResident>(true))resident.enabled=false;
                foreach(var actor in instance.GetComponentsInChildren<SibucaoActor>(true))actor.enabled=false;
                foreach(var body in instance.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
                var lit=Shader.Find("Universal Render Pipeline/Lit");
                if(lit)foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var converted=new Material[renderer.sharedMaterials.Length];
                    for(int i=0;i<converted.Length;i++)
                    {
                        var source=renderer.sharedMaterials[i];var material=new Material(lit){name=(source?source.name:displayName)+" - runtime URP"};
                        if(source)
                        {
                            if(source.HasProperty("_MainTex")){var texture=source.GetTexture("_MainTex");material.SetTexture("_BaseMap",texture);}
                            if(source.HasProperty("_Color"))material.SetColor("_BaseColor",source.GetColor("_Color"));
                            material.mainTextureScale=source.mainTextureScale;material.mainTextureOffset=source.mainTextureOffset;
                        }
                        converted[i]=material;
                    }
                    renderer.sharedMaterials=converted;
                }
                return instance.transform;
            }
            var fallback=LocalGroup(displayName+" - fallback",parent,localPosition,Quaternion.identity);
            Part(fallback,"Raincoat body",PrimitiveType.Capsule,new Vector3(0,.92f,0),new Vector3(.32f,.55f,.27f),displayName=="Child"?new Color(.12f,.38f,.55f):new Color(.18f,.39f,.34f));
            Part(fallback,"Head",PrimitiveType.Sphere,new Vector3(0,1.72f,0),new Vector3(.23f,.23f,.23f),new Color(.48f,.31f,.23f));fallback.localScale=Vector3.one*scale;return fallback;
        }

        public bool Interact()
        {
            var target=Focused();if(target==null)return false;
            if(target.kind==TargetKind.Parent&&!Accepted)
            {
                Flag("side_family_discovered");Flag("side_family_accepted");Data.activeSideMission=MissionId;Data.sideWorkActive=true;S.state.scores[2]+=10;
                S.physicalDecisions?.RecordRouteDecision("family-supplies","help","selected",HasPrepared("gloves")||HasPrepared("whistle"));
                string gear=HasPrepared("gloves")?"Your gloves will make the wet bags safer to handle.":"The straps are slippery. Please lift them carefully.";
                S.Say("Parent: The runoff pulled our supplies off the cart. "+gear,8);S.Notify("Optional side mission started","Secure two family supplies. You may leave at any time and continue to the school.");
                PlayAnimation(parentAnimator,false,true);S.RequestCheckpoint();return true;
            }
            if(target.kind==TargetKind.Supply&&!SupplyDone(target.index)&&Accepted){workingSupply=target.index;return true;}
            return false;
        }

        void Update()
        {
            if(ready&&S&&(S.Mode==SibucaoMode.ArrivalReview||S.Mode==SibucaoMode.Complete)){UpdateFamily(Time.deltaTime);RestorePresentation();return;}
            if(!ready||!S||S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked)return;
            float progress=S.route.Progress(S.player.transform.position),distance=Flat(S.player.transform.position,RoutePoint(missionDistance,1.9f));
            if(!Discovered&&!Skipped&&(distance<22||Mathf.Abs(progress-missionDistance)<16))
            {Flag("side_family_discovered");S.Notify("Optional help nearby","A parent is struggling with supplies beside the route. Stop to help or continue evacuating.");}
            if(Discovered&&!Accepted&&!Skipped&&progress>missionDistance+20)
            {Flag("side_family_skipped");S.physicalDecisions?.RecordRouteDecision("family-supplies","continue","resolved",false,true,false);S.Notify("Optional mission passed","You continued toward the school. The decision has been recorded.");}
            UpdateHandling();UpdateFamily(Time.deltaTime);RestorePresentation();
        }

        void UpdateHandling()
        {
            if(workingSupply<0||workingSupply>=supplies.Length||SupplyDone(workingSupply)){workingSupply=-1;workProgress=0;return;}
            var focused=Focused();bool aimed=focused!=null&&focused.kind==TargetKind.Supply&&focused.index==workingSupply;
            bool held=Keyboard.current!=null&&SibucaoControls.Button(Key.E).isPressed||SibucaoControls.WorkHeld;
#if UNITY_EDITOR
            if(SibucaoEditorInput.Active)held=SibucaoEditorInput.UseHeld;
#endif
            // Once a lift begins, slight head movement or the animated bag leaving the
            // crosshair must not cancel the hold. Only walking away cancels the work.
            if(!aimed&&Flat(S.player.view.transform.position,supplies[workingSupply].position)>Reach+1.5f){workingSupply=-1;workProgress=0;return;}if(!held)return;
            workProgress=Mathf.Clamp01(workProgress+Time.deltaTime/WorkDuration);
            var hands=S.player.hands?S.player.hands.GetComponent<SibucaoHandPresentation>():null;
            hands?.DriveIncident(supplies[workingSupply],6,workProgress);
            var target=cart.TransformPoint(new Vector3(workingSupply==0?-.25f:.25f,.82f,0));
            supplies[workingSupply].position=Vector3.Lerp(supplyStarts[workingSupply],target,Mathf.SmoothStep(0,.72f,workProgress));
            supplies[workingSupply].rotation=Quaternion.Slerp(supplies[workingSupply].rotation,cart.rotation,Time.deltaTime*3);
            if(workProgress>=1)SecureSupply(workingSupply);
        }

        void SecureSupply(int index)
        {
            if(SupplyDone(index))return;
            Flag("side_family_supply_"+index);supplies[index].SetParent(cart,true);supplies[index].localPosition=new Vector3(index==0?-.25f:.25f,.82f,0);supplies[index].localRotation=Quaternion.identity;
            if(!HasPrepared("gloves")){Data.stamina=Mathf.Max(0,Data.stamina-4);Flag("side_family_bare_hand_exposure");}else S.state.scores[0]+=8;
            S.state.scores[2]+=10;SibucaoSoundFeedback.Play(SibucaoSoundKind.Placement);workingSupply=-1;workProgress=0;
            if(SecuredCount<2)S.Notify("Supply secured","One wet supply remains beside the cart.");
            else{Flag("side_family_supplies_secured");S.state.scores[2]+=20;S.state.scores[3]+=15;S.Say("Parent: Both bags are secure. We can move with the evacuation flow now. Thank you.",7);S.Notify("Supplies secured","The family is rejoining the evacuation route.");}
            S.RequestCheckpoint();
        }

        void UpdateFamily(float dt)
        {
            if(Has("experience_family_arrived")){PlayAnimation(parentAnimator,false,false);PlayAnimation(childAnimator,false,false);return;}
            if(!SuppliesSecured)
            {
                if(parentNpc){Vector3 look=Accepted?cart.position:S.player.transform.position;look.y=parentNpc.position.y;if(Flat(parentNpc.position,look)<9)parentNpc.rotation=Quaternion.Slerp(parentNpc.rotation,Quaternion.LookRotation((look-parentNpc.position).normalized),dt*2.5f);}
                PlayAnimation(parentAnimator,false,false);PlayAnimation(childAnimator,false,false);return;
            }
            var experience=S.GetComponent<SibucaoEvacuationExperience>();
            if(S.GetComponent<FloodSimulationManager>()?.IsCatchingUp==true)return;
            float playerDistance=S.route.Progress(S.player.transform.position);
            float current=S.route.Progress(cart.position);
            bool reception=S.Mode==SibucaoMode.ArrivalReview||S.Mode==SibucaoMode.Complete;
            float destination=Mathf.Clamp(reception?S.route.length:playerDistance-2,missionDistance,S.route.length);
            if(familyPositioned&&Flat(cart.position,S.player.transform.position)>12){PlayAnimation(parentAnimator,false,false);PlayAnimation(childAnimator,false,false);return;}
            float next=Mathf.Min(Mathf.Max(current,destination),current+1.5f);
            bool moving=Mathf.Abs(next-current)>.001f;
            float d=next;
            var forward=RouteForward(d);var right=Vector3.Cross(Vector3.up,forward);
            if(experience.State.familyTrail==null)experience.State.familyTrail=new List<Vector3>();
            var leader=reception?NavigationTarget:S.player.transform.position;
            var cartPos=SibucaoSafeNpcMotion.TrailTarget(experience.State.familyTrail,leader,cart.position,reception);
            var oldCart=cart.position;var oldParent=parentNpc?parentNpc.position:Vector3.zero;var oldChild=childNpc?childNpc.position:Vector3.zero;cart.rotation=Quaternion.Slerp(cart.rotation,Quaternion.LookRotation(forward),1-Mathf.Exp(-dt*4));
            familyPositioned=true;
            bool clear=SibucaoSafeNpcMotion.Step(cart,cartPos,1.65f,S,runtimeRoot,.38f);
            if(parentNpc){var p=cart.position-forward*.9f;clear&=SibucaoSafeNpcMotion.Step(parentNpc,p,1.8f,S,runtimeRoot);parentNpc.rotation=Quaternion.Slerp(parentNpc.rotation,Quaternion.LookRotation(forward),dt*5);}
            if(childNpc){var p=parentNpc.position-forward*.9f;clear&=SibucaoSafeNpcMotion.Step(childNpc,p,1.8f,S,runtimeRoot);childNpc.rotation=Quaternion.Slerp(childNpc.rotation,Quaternion.LookRotation(forward),dt*5);}
            if(!clear){cart.position=oldCart;if(parentNpc)parentNpc.position=oldParent;if(childNpc)childNpc.position=oldChild;moving=false;blockedSeconds+=dt;}
            else{blockedSeconds=0;moving=Flat(oldCart,cart.position)>.001f;familyTravel=Mathf.Max(familyTravel,S.route.Progress(cart.position)-missionDistance);if(experience)experience.State.familyDistance=S.route.Progress(cart.position);}
            if(experience){experience.State.familyCartPosition=cart.position;experience.State.familyParentPosition=parentNpc.position;experience.State.familyChildPosition=childNpc.position;experience.State.familyPositionSaved=true;}
            if(blockedSeconds>3&&!Has("side_family_wait_notice")){Flag("side_family_wait_notice");S.Notify("Family waiting on the route","Their passage is obstructed or wet. Do not lead them through floodwater. Request evacuation assistance from the guide if access is cut off.");}
            PlayAnimation(parentAnimator,moving,false);PlayAnimation(childAnimator,moving,false);
            if(experience&&Complete&&S.physicalDecisions&&S.physicalDecisions.evacuationCenter&&S.physicalDecisions.evacuationCenter.HasArrived(cart.position)&&parentNpc&&childNpc&&S.physicalDecisions.evacuationCenter.HasArrived(parentNpc.position)&&S.physicalDecisions.evacuationCenter.HasArrived(childNpc.position)&&!Has("experience_family_arrived"))
            {Flag("experience_family_arrived");experience.Record("family-arrived","The family reached school","The parent and child you helped have checked in beside you.","Your assistance continued beyond the roadside task.");}
            foreach(var wheel in cart.GetComponentsInChildren<Transform>())if(wheel.name.IndexOf("wheel",StringComparison.OrdinalIgnoreCase)>=0)if(moving)wheel.Rotate(Vector3.right,dt*240,Space.Self);
            if(!Complete&&familyTravel>=8){Flag("side_family_complete");Data.sideWorkActive=false;Data.activeSideMission=null;S.state.scores[3]+=20;S.physicalDecisions?.RecordRouteDecision("family-supplies","help","resolved",HasPrepared("gloves")||HasPrepared("whistle"),true,false);S.Notify("Side mission complete","Family ready to evacuate. Continue toward Banago Elementary School I.");S.RequestCheckpoint();}
        }

        void PlayAnimation(Animator animator,bool walking,bool react)
        {
            if(!animator||!animator.isActiveAndEnabled)return;
            string state=walking?"metarig|Walk":"metarig|Idle";float rate=walking?1.15f:react?1.35f:.45f;
            animator.Play(state,0,Mathf.Repeat(Time.time*rate,1));animator.applyRootMotion=false;
        }
        public bool ArriveByAssistance(Vector3 origin,Vector3 destination)
        {
            if(!SuppliesSecured||!parentNpc||!childNpc||Flat(cart.position,origin)>12||Flat(parentNpc.position,origin)>12||Flat(childNpc.position,origin)>12)return false;
            cart.position=destination+Vector3.right*.8f;parentNpc.position=destination+Vector3.left*.8f;childNpc.position=destination+Vector3.back*.8f;
            Flag("side_family_complete");Flag("experience_family_arrived");Flag("family_assisted_transport");
            Data.sideWorkActive=false;Data.activeSideMission=null;
            var e=S.GetComponent<SibucaoEvacuationExperience>();e.State.familyCartPosition=cart.position;e.State.familyParentPosition=parentNpc.position;e.State.familyChildPosition=childNpc.position;e.State.familyPositionSaved=true;e.State.familyDistance=S.route.length;
            e.Record("family-arrived","The family checked in","The nearby family travelled with you on the scenario's abstracted assisted transport.","Helping with supplies and reaching reception are separate outcomes.");return true;
        }
        void RestorePresentation()
        {
            if(!ready)return;for(int i=0;i<supplies.Length;i++)if(supplies[i]){supplies[i].gameObject.SetActive(!Skipped);if(SupplyDone(i)&&supplies[i].parent!=cart){supplies[i].SetParent(cart,true);supplies[i].localPosition=new Vector3(i==0?-.25f:.25f,.82f,0);supplies[i].localRotation=Quaternion.identity;}}
        }
        Target Focused()
        {
            if(!ready||!S||!S.player||!S.player.view||Skipped||Complete)return null;
            var view=S.player.view.transform;var hits=Physics.RaycastAll(view.position,view.forward,Reach,~0,QueryTriggerInteraction.Collide);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){if(hit.transform.IsChildOf(S.player.transform))continue;if(targets.TryGetValue(hit.collider,out var target)&&Available(target))return target;if(!hit.collider.isTrigger&&!hit.transform.IsChildOf(runtimeRoot))break;}return null;
        }
        bool Available(Target target)=>target.kind==TargetKind.Parent?!Accepted:Accepted&&!SuppliesSecured&&!SupplyDone(target.index);
        void Register(Transform root,TargetKind kind,int index,Transform visual,Vector3 size,Vector3 localCenter)
        {var zone=new GameObject("Interaction - "+kind+" "+index);zone.transform.SetParent(root,false);zone.transform.localPosition=localCenter;var collider=zone.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=size;var target=new Target{kind=kind,index=index,visual=visual};targets[collider]=target;foreach(var visible in visual.GetComponentsInChildren<Collider>(true))targets[visible]=target;}
        Transform WorldGroup(string name,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(runtimeRoot,false);go.transform.SetPositionAndRotation(position,rotation);return go.transform;}
        static Transform LocalGroup(string name,Transform parent,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;return go.transform;}
        Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color,Quaternion? rotation=null)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=scale;var renderer=go.GetComponent<Renderer>();if(renderer){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);}return go.transform;}
        Vector3 RoutePoint(float distance,float lane){var forward=RouteForward(distance);return S.route.Sample(Mathf.Clamp(distance,0,S.route.length))+Vector3.Cross(Vector3.up,forward)*lane;}
        Vector3 RouteForward(float distance){var forward=S.route.Sample(Mathf.Min(S.route.length,distance+2))-S.route.Sample(Mathf.Max(0,distance-2));forward.y=0;return forward.sqrMagnitude>.001f?forward.normalized:Vector3.forward;}
        Quaternion RouteRotation(float distance)=>Quaternion.LookRotation(RouteForward(distance));
        bool SupplyDone(int index)=>Has("side_family_supply_"+index);
        bool Has(string flag)=>Data!=null&&Data.flags!=null&&Data.flags.Contains(flag);
        void Flag(string flag){if(S.physicalDecisions)S.physicalDecisions.SetFlag(flag);else if(Data!=null&&!Data.flags.Contains(flag))Data.flags.Add(flag);}
        PhysicalGrabbable PackedPrepared(string id){if(!S.inventory)return null;for(int i=0;i<SibucaoInventory.Capacity;i++){var item=S.inventory.Slot(i);var prep=item?item.GetComponent<PreparednessItem>():null;if(prep&&prep.id==id)return item;}return null;}
        bool HasPrepared(string id){var experience=S.GetComponent<SibucaoEvacuationExperience>();if(experience)return experience.HasGear(id);var item=PackedPrepared(id);var prep=item?item.GetComponent<PreparednessItem>():null;return prep&&prep.prepared;}
        static float Flat(Vector3 a,Vector3 b){var d=a-b;return new Vector2(d.x,d.z).magnitude;}
#if UNITY_EDITOR
        public bool EditorAdvanceSupply(float seconds){if(!Accepted||SuppliesSecured)return false;for(int i=0;i<supplies.Length;i++)if(!SupplyDone(i)){workingSupply=i;workProgress=Mathf.Clamp01(workProgress+seconds/WorkDuration);if(workProgress>=1)SecureSupply(i);return true;}return false;}
#endif
    }
}
