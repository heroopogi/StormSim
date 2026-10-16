using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace StormSim.Sibucao
{
    [Serializable] public sealed class PhysicalBodyState
    {
        public string id,parentId;public Vector3 position,velocity;public Quaternion rotation;
        public bool secured,delivered,everHeld,active,preparedForUse,kinematic;
    }
    [Serializable] public sealed class PhysicalRunData
    {
        public int schemaVersion=4;
        public List<RouteMissionProgress> sideMissions=new List<RouteMissionProgress>();
        public CausalRun causal;
        public FloodVariantState[] floodVariants=new FloodVariantState[9];
        public float health=100,stamina=100;
        public float hunger;
        public float submergedSeconds;
        public bool needsRescue;
        public int assistedRecoveries;
        public PhysicalBranchAttempt[] branches=new PhysicalBranchAttempt[9];
        public List<PhysicalChoiceResult> history=new List<PhysicalChoiceResult>();
        public List<PendingPhysicalConsequence> pending=new List<PendingPhysicalConsequence>();
        public List<ConsequenceNotice> consequenceNotices=new List<ConsequenceNotice>();
        public bool arrivalReported;
        public bool arrivalReceived;
        public PhysicalAttempt[] attempts=new PhysicalAttempt[9];
        public int seed;public string role="Student";public float routeStart;
        public int[] results={-1,-1,-1,-1,-1,-1,-1,-1,-1};
        public float[] started={-1,-1,-1,-1,-1,-1,-1,-1,-1};
        public List<string> flags=new List<string>();
        public List<PhysicalBodyState> bodies=new List<PhysicalBodyState>();
        public bool boundaryReached;
        public string backpackId,equippedInventoryId;
        public string activeSideMission;
        public bool sideWorkActive;
        public float[] sectionLifts;
        public string[] inventorySlots=new string[27];
        public int inventorySelection;
        public float[] observedSeconds=new float[9];
        public List<StormPassageState> passages=new List<StormPassageState>();
        public float[] detourDistance=new float[9],detourSeconds=new float[9];
        public float waterExposureMetres;
        public int waterLossChecks;
    }
    public sealed partial class DecisionTreeManager : MonoBehaviour
    {
        public PhysicalDecisionStation[] stations;
        public SibucaoDryRefuge evacuationCenter;
        public PhysicalGrabber Grabber;
        public UnityEvent onRiversideBoundary=new UnityEvent();
        [Tooltip("Assign the real Riverside scene when available. Empty uses the Sibucao completion checkpoint.")]
        public string riversideScene;
        public string playerRole="Resident";
        public float studentDirectionsMultiplier=1,parentAssistMultiplier=1,volunteerHelpMultiplier=1;
        public PhysicalRunData Data=>SibucaoSession.Instance.state.physical;
        public void EnsureNodeCapacity()
        {
            int count=9;foreach(var st in stations)if(st)count=Mathf.Max(count,st.index+1);
            int old=Data.results?.Length??0;Array.Resize(ref Data.results,count);for(int i=old;i<count;i++)Data.results[i]=-1;
            old=Data.started?.Length??0;Array.Resize(ref Data.started,count);for(int i=old;i<count;i++)Data.started[i]=-1;
            if(Data.floodVariants==null||Data.floodVariants.Length!=9)Array.Resize(ref Data.floodVariants,9);
            Array.Resize(ref Data.branches,count);Array.Resize(ref Data.attempts,count);Array.Resize(ref Data.observedSeconds,count);
            Array.Resize(ref Data.detourDistance,count);Array.Resize(ref Data.detourSeconds,count);
        }
        public float Beat {get;private set;}
        public float ShockFlash {get;private set;}
        Vector3 lastTravelPoint;
        readonly Dictionary<PhysicalGrabbable,PhysicalBodyState> initial=new Dictionary<PhysicalGrabbable,PhysicalBodyState>();
        readonly Dictionary<PhysicalGrabbable,Vector3> initialLocal=new Dictionary<PhysicalGrabbable,Vector3>();
        readonly Dictionary<PhysicalGrabbable,Quaternion> initialRotation=new Dictionary<PhysicalGrabbable,Quaternion>();
        readonly Dictionary<PhysicalGrabbable,Transform> initialParent=new Dictionary<PhysicalGrabbable,Transform>();
        readonly Dictionary<PhysicalGrabbable,PhysicalDecisionStation> initialStation=new Dictionary<PhysicalGrabbable,PhysicalDecisionStation>();
        static readonly int[] caps={1500,1200,1000,1000,300};
        public static readonly string[] Titles={"Water rising unexpectedly","Stalled cars","Overflowing canal","Downed power line","Broken go-bag strap","Wheelchair at curb","Rescue boat diversion","Collapsed evacuee","Creek boundary"};
        void Awake()
        {
            foreach(var item in FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None)){initial[item]=Capture(item);initialLocal[item]=item.transform.localPosition;initialRotation[item]=item.transform.localRotation;initialParent[item]=item.transform.parent;initialStation[item]=item.station;}
        }
        public void RegisterPreparationItem(PhysicalGrabbable item)
        {
            if(!item||!item.Body||initial.ContainsKey(item))return;
            initial[item]=Capture(item);initialLocal[item]=item.transform.localPosition;initialRotation[item]=item.transform.localRotation;initialParent[item]=item.transform.parent;initialStation[item]=item.station;
        }
        PhysicalBodyState Capture(PhysicalGrabbable item){var parent=item.transform.parent?item.transform.parent.GetComponentInParent<PhysicalGrabbable>():null;return new PhysicalBodyState{id=item.persistentId,parentId=parent?parent.persistentId:null,position=item.transform.position,rotation=item.transform.rotation,velocity=item.Body.linearVelocity,secured=item.secured,delivered=item.delivered,everHeld=item.everHeld,active=item.gameObject.activeSelf,preparedForUse=item.preparedForUse,kinematic=item.Body.isKinematic};}
        public void ResetRun()
        {
            arrivalBlockedShown=false;
            foreach(var st in stations){var adapter=st.GetComponent<FloodEncounterAdapter>();if(adapter)adapter.ResetPresentation();}
            Grabber.Release();var s=SibucaoSession.Instance;var feedback=s.player.GetComponent<SibucaoPhysicalFeedback>();if(feedback)feedback.Clear();
            s.state.physical=new PhysicalRunData{seed=UnityEngine.Random.Range(1,int.MaxValue),role="Resident",routeStart=s.route.Progress(s.home?s.home.standingPoint.position:s.player.transform.position)};
            EnsureNodeCapacity();if(causalModel){causalModel.ValidateRun(Data);Data.causal=new CausalRun();}Layout();
            foreach(var pair in initial)if(pair.Key){var item=pair.Key;item.station=initialStation[item];item.transform.SetParent(initialParent[item],true);RestoreBody(item,pair.Value);item.transform.localPosition=initialLocal[item];item.transform.localRotation=initialRotation[item];item.secured=item.delivered=item.everHeld=false;item.Body.isKinematic=item.station&&item.station.index==7;}
            foreach(var station in stations){station.ResetBranches();if(station.resolutionBeat)station.resolutionBeat.ResetBeat();station.brokenBag=null;if(station.spill)station.spill.transform.localPosition=Vector3.zero;station.ResetStation();}
            foreach(var station in stations)station.GetComponent<FloodEncounterAdapter>()?.PrepareStaticAccess();
            s.state.firstAid=0;s.toastUntil=0;s.subtitleUntil=0;
            if(s.inventory)s.inventory.RestoreInventory();
            if(s.passages)s.passages.Restore();
            lastTravelPoint=s.player.transform.position;
        }
        public void Layout()
        {
            var route=SibucaoSession.Instance.route;
            bool coastal=GetComponent<CoastalAreaProfile>() || SibucaoSession.Instance.GetComponent<CoastalAreaProfile>();
            float routeStart=coastal ? 0 : Data.routeStart;
            float available=route.length-routeStart-12;
            for(int i=0;i<stations.Length;i++)
            {
                var station=stations[i];
                // Coastal encounters are fitted to real streets; moving them by route percentage breaks their baked access paths.
                if(station.supplementary||coastal){station.routeDistance=route.Progress(station.transform.position);continue;}
                station.routeDistance=routeStart+Mathf.Max(5,available*(station.index+1)/10f);
                var p=route.Sample(station.routeDistance);var forward=route.Sample(Mathf.Min(route.length,station.routeDistance+2))-route.Sample(Mathf.Max(0,station.routeDistance-2));forward.y=0;
                station.transform.SetPositionAndRotation(p,Quaternion.LookRotation(forward.sqrMagnitude>.01f?forward:Vector3.forward));
            }
            foreach(var st in stations)if(st.supplementary)
            {
                bool included=(st.departureEncounter||st.routeDistance>Data.routeStart+8)&&(st.homeMask&(1<<SibucaoSession.Instance.state.homeIndex))!=0;
                st.gameObject.SetActive(included);if(!included)Data.results[st.index]=98;
            }
            if(Application.isPlaying)foreach(var station in stations)if(station.branches!=null)foreach(var branch in station.branches)if(branch)branch.ApplyLayout(SibucaoSession.Instance.state.homeIndex);
            var routeChoices=GetComponent<SibucaoRouteChoices>();if(routeChoices)routeChoices.ApplyLayout();
            Physics.SyncTransforms();
        }
        public void SavePhysical()
        {
            GetComponent<SibucaoRouteChoices>()?.CaptureProgress();
            foreach(var station in stations)if(station.branches!=null)foreach(var branch in station.branches)if(branch)branch.SaveBinding();
            Data.bodies.Clear();foreach(var item in initial.Keys)if(item)Data.bodies.Add(Capture(item));
        }
        void RestoreBody(PhysicalGrabbable item,PhysicalBodyState body)
        {
            item.gameObject.SetActive(body.active);item.transform.SetPositionAndRotation(body.position,body.rotation);item.secured=body.secured;item.delivered=body.delivered;item.everHeld=body.everHeld;item.preparedForUse=body.preparedForUse;
            item.Body.isKinematic=body.kinematic||body.secured||body.delivered;if(!item.Body.isKinematic){item.Body.linearVelocity=body.velocity;item.Body.angularVelocity=Vector3.zero;}
        }
        public bool RestoredLegacyAccess {get;private set;}
        public void RestoreRun()
        {
            RestoredLegacyAccess=Data.schemaVersion<4;
            var oldLifts=new float[9];for(int i=0;i<9;i++)oldLifts[i]=Data.floodVariants!=null&&i<Data.floodVariants.Length&&Data.floodVariants[i]!=null?Data.floodVariants[i].lift:0;
            arrivalBlockedShown=false;
            foreach(var st in stations){var adapter=st.GetComponent<FloodEncounterAdapter>();if(adapter)adapter.ResetPresentation();}
            EnsureNodeCapacity();
            if(causalModel){causalModel.ValidateRun(Data);if(Data.causal==null)Data.causal=new CausalRun{path=SibucaoCausalModel.Path(Data)};if(NextCoreStage<9&&Data.started[NextCoreStage]>=0)EnterCausalStage(NextCoreStage);}
            playerRole=Data.role="Resident";
            if(Data.consequenceNotices==null)Data.consequenceNotices=new List<ConsequenceNotice>();
            Grabber.Release();if(Data.schemaVersion<2){Data.health=100;Data.stamina=100;Data.schemaVersion=2;}foreach(var item in initial.Keys)if(item)item.transform.SetParent(initialParent[item],true);Layout();foreach(var station in stations){station.ResetStation();if(station.spill)station.spill.SetActive(Data.started[station.index]>=0&&!Data.flags.Contains("no_bag_to_break"));station.probed=Data.flags.Contains("depth_probed_"+station.index);}
            foreach(var st in stations){var adapter=st.GetComponent<FloodEncounterAdapter>();if(adapter)adapter.RestorePresentation();}
            foreach(var item in initial.Keys)if(item)
            {
                var body=Data.bodies.Find(x=>x.id==item.persistentId);if(body==null)continue;
                var st=item.station;
                if(RestoredLegacyAccess&&st&&!st.supplementary&&st.FloodAdapter&&SibucaoPhysicalOption.FlatDistance(body.position,st.transform.position)<12&&Mathf.Abs(body.position.y-st.transform.position.y-oldLifts[st.index])<2.5f)
                    body.position+=Vector3.up*(st.FloodAdapter.Lift-oldLifts[st.index]);
                RestoreBody(item,body);
            }
            foreach(var item in initial.Keys)if(item){var body=Data.bodies.Find(x=>x.id==item.persistentId);if(body!=null&&!string.IsNullOrEmpty(body.parentId))foreach(var parent in initial.Keys)if(parent&&parent.persistentId==body.parentId)item.transform.SetParent(parent.transform,true);}
            foreach(var item in initial.Keys)if(item)
            {
                if(Data.flags.Contains("spilled:"+item.persistentId)){item.station=stations[4];if(!item.secured)item.transform.SetParent(stations[4].spill.transform,true);}
                if(Data.flags.Contains("broken_bag:"+item.persistentId)){stations[4].brokenBag=item;item.Body.constraints=RigidbodyConstraints.FreezeRotation;}
            }
            if(Data.flags.Contains("strap_broken")){var proxy=stations[4].spill.transform.Find("Go-bag with broken strap");if(proxy)proxy.gameObject.SetActive(false);}
            foreach(var station in stations)if(station.branches!=null)foreach(var branch in station.branches)if(branch)branch.RestoreBinding();
            if(SibucaoSession.Instance.inventory)SibucaoSession.Instance.inventory.RestoreInventory();
            if(SibucaoSession.Instance.passages)SibucaoSession.Instance.passages.Restore();
            GetComponent<SibucaoRouteChoices>()?.ResumeSavedWork();
            Data.schemaVersion=4;
            lastTravelPoint=SibucaoSession.Instance.player.transform.position;
        }
        void Update()
        {
            var s=SibucaoSession.Instance;if(!s||s.Mode!=SibucaoMode.Playing||s.home&&s.home.MovementLocked)return;
            PresentConsequenceNotice();
            Beat=Mathf.MoveTowards(Beat,0,Time.deltaTime);ShockFlash=Mathf.MoveTowards(ShockFlash,0,Time.deltaTime*2);
            float progress=s.route.Progress(s.player.transform.position);
            var travel=s.player.transform.position-lastTravelPoint;travel.y=0;lastTravelPoint=s.player.transform.position;
            bool travelling=travel.magnitude<1.2f&&travel.sqrMagnitude>.000004f&&s.player.MoveInput.sqrMagnitude>.01f;
            foreach(var station in stations)
            {
                if(station.Resolved||!CanEnter(station))continue;
                // A route projection alone must never silently complete an unseen encounter.
                Vector3 offset=s.player.transform.position-station.InteractionPosition;
                float horizontal=new Vector2(offset.x,offset.z).magnitude;
                if(!station.Active && horizontal<12 && Mathf.Abs(offset.y)<4 && Mathf.Abs(progress-station.routeDistance)<14)station.Activate();
                station.CheckArrival();
                var engaged=station.EngagedBranch;
                bool followingChoice=false;
                if(engaged){engaged.CrossingPosition(s.player.transform.position,out _,out _,out float lateral);followingChoice=lateral<6;}
                if(!followingChoice&&travelling&&horizontal>12&&Mathf.Abs(progress-station.routeDistance)<35)
                {Data.detourDistance[station.index]+=travel.magnitude;Data.detourSeconds[station.index]+=Time.deltaTime;}
                bool longDetour=Data.detourDistance[station.index]>30&&Data.detourSeconds[station.index]>12;
                if((!causalModel||station.supplementary)&&!followingChoice&&(longDetour||station.supplementary&&station.Active)&&progress>station.routeDistance+24&&horizontal>24)
                {if(longDetour)SetFlag("long_detour_"+station.index);Missed(station);s.Notify("Taking another passage","Continue toward the next situation.");continue;}
                // Only leaving the encounter records abandonment; waiting never awards a route choice.
                if(!station.node && station.Active && progress>station.routeDistance+20 && horizontal>20 && s.state.elapsed-Data.started[station.index]>12)
                    Missed(station);
                if(station.index==8&&station.ambient)
                {
                    float distance=Vector3.Distance(s.player.transform.position,station.transform.position);
                    station.ambient.volume=Mathf.Lerp(.12f,.85f,1-Mathf.Clamp01(distance/25))*SibucaoAudioMix.Environment;
                    if(distance<25&&!station.ambient.isPlaying)station.ambient.Play();
                }
            }
            CheckEndDestination();
        }
        public bool TryInteract()
        {
            foreach(var station in stations)if(station.TryInteract())return true;
            return false;
        }
        bool arrivalBlockedShown;
        public void CheckEndDestination()
        {
            var s=SibucaoSession.Instance;if(Data.boundaryReached||s.Mode!=SibucaoMode.Playing||s.home&&s.home.MovementLocked)return;
            if(evacuationCenter)
            {
                if(!evacuationCenter.HasArrived(s.player.transform.position)){arrivalBlockedShown=false;return;}
                var water=s.GetComponent<FloodSimulationManager>();
                if(water&&water.GetWaterDepth(s.player.transform.position)>.018f)return;
            }
            else
            {
                var delta=s.player.transform.position-s.route.Sample(s.route.length);
                if(new Vector2(delta.x,delta.z).magnitude>5||Mathf.Abs(delta.y)>4){arrivalBlockedShown=false;return;}
            }
            if(causalModel&&NextCoreStage<9){if(!arrivalBlockedShown){arrivalBlockedShown=true;s.OpenRecovery(true);}return;}
            // Record bypassed situations as missed; do not leave the player at a dead waypoint.
            foreach(var station in stations)if(!station.Resolved){if(station.index==4&&Data.started[4]<0&&(!s.inventory||!s.inventory.HasBag))SetFlag("no_bag_to_break");Missed(station);}
            if(!Data.boundaryReached)ReachBoundary();
        }
        public void SetFlag(string flag){if(!Data.flags.Contains(flag))Data.flags.Add(flag);}
        public bool SpillBag(PhysicalDecisionStation station)
        {
            var inventory=SibucaoSession.Instance.inventory;
            if(inventory&&inventory.HasBag)
            {
                bool spilled=inventory.Spill(station);if(spilled&&station.fallbackBag)station.fallbackBag.gameObject.SetActive(false);
                var placeholder=station.spill.transform.Find("Go-bag with broken strap");if(placeholder)placeholder.gameObject.SetActive(false);
                foreach(var item in station.spill.GetComponentsInChildren<PhysicalGrabbable>(true))if(initialStation.ContainsKey(item)&&initialStation[item]==station&&item.kind==PhysicalItemKind.Supply)item.gameObject.SetActive(false);
                return spilled;
            }
            PhysicalGrabbable bag=null;foreach(var item in initial.Keys)if(item&&item.kind==PhysicalItemKind.Bag&&item.everHeld&&Vector3.Distance(item.transform.position,SibucaoSession.Instance.player.transform.position)<3){bag=item;break;}
            if(!bag)return false;
            station.brokenBag=bag;SetFlag("broken_bag:"+bag.persistentId);SetFlag("strap_broken");
            if(bag.holder)bag.holder.Release();station.spill.transform.position=bag.transform.position;
            var proxy=station.spill.transform.Find("Go-bag with broken strap");if(proxy)proxy.gameObject.SetActive(false);
            foreach(var proxyItem in station.spill.GetComponentsInChildren<PhysicalGrabbable>())if(proxyItem.kind==PhysicalItemKind.Supply&&initialStation.ContainsKey(proxyItem)&&initialStation[proxyItem]==station)proxyItem.gameObject.SetActive(false);
            var cargo=bag.GetComponentsInChildren<PhysicalGrabbable>();int n=0;
            foreach(var item in cargo)if(item!=bag)
            {
                item.transform.SetParent(station.spill.transform,true);item.station=station;item.secured=false;item.Body.isKinematic=false;item.Body.AddForce(new Vector3((n++%2==0?1:-1)*.7f,.4f,.3f),ForceMode.VelocityChange);SetFlag("spilled:"+item.persistentId);
            }
            return true;
        }
        public void EnvironmentBeat(){Beat=1;if(SibucaoSession.Instance.weather)SibucaoSession.Instance.weather.SetStorm(true);}
        public void Shock(){ShockFlash=1;SibucaoSession.Instance.player.Gesture(2);SibucaoSession.Instance.state.speedMultiplier=.7f;}
        public void Resolve(PhysicalDecisionStation station,int result)
        {
            if(station.node){if(result==99)Missed(station);else FinishPhysicalChoice(station,result);return;}
            Debug.LogError("Physical station has no DecisionNode binding: "+station.name,station);
        }
        void ReachBoundary()
        {
            if(Data.boundaryReached||causalModel&&NextCoreStage<9)return;
            EnterCausalStage(9);
            ApplyPending(9);
            if(!Data.arrivalReported){SibucaoSession.Instance.SetMode(SibucaoMode.ArrivalReview);return;}
            Data.boundaryReached=true;onRiversideBoundary.Invoke();
            if(!string.IsNullOrEmpty(riversideScene)&&Application.CanStreamedLevelBeLoaded(riversideScene))UnityEngine.SceneManagement.SceneManager.LoadScene(riversideScene);
            else SibucaoSession.Instance.SetMode(SibucaoMode.Complete);
        }
        public void ConfirmArrivalReport()
        {
            if(SibucaoSession.Instance.Mode!=SibucaoMode.ArrivalReview)return;
            if(!Data.arrivalReceived){Data.arrivalReceived=true;SibucaoSession.Instance.Say("School volunteer: Welcome. We have recorded your arrival. Tell us who still needs help and what supplies you have.",7);return;}
            Data.arrivalReported=true;SetFlag("arrival_details_reported");ReachBoundary();
        }
        public void AddConsequenceNotice(string id,int destination,string message)
        {
            if(Data.consequenceNotices==null)Data.consequenceNotices=new List<ConsequenceNotice>();
            if(Data.consequenceNotices.Exists(n=>n.id==id))return;
            Data.consequenceNotices.Add(new ConsequenceNotice{id=id,destination=destination,message=message});
        }
        void PresentConsequenceNotice()
        {
            if(Data.consequenceNotices==null)return;var s=SibucaoSession.Instance;
            if(Time.unscaledTime<s.subtitleUntil||s.story&&s.Mode==SibucaoMode.Cinematic)return;
            foreach(var n in Data.consequenceNotices)if(!n.shown){n.shown=true;s.Say(n.message,7);break;}
        }
    }
}
