using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    public enum SibucaoMode { Menu, Playing, Decision, Paused, Settings, Complete, Confirm, About, Cinematic, RoleSelection, ArrivalReview, Preparation, DifficultySelection, Recovery, ResumeSelection }
    [Serializable] public sealed class SibucaoSettings
    {
        public bool subtitles=true,hints=true,interactPrompts=true,fullscreen=true;
        public float volume=.75f,brightness=.7f,sensitivity=.11f;
        public int graphics=1;
        public float textScale=1,speechVolume=1,environmentVolume=1,effectsVolume=1,fieldOfView=70;
        public bool invertLook,toggleGrip,reducedFlashes,reducedMotion;
        public string[] keyBindings;
    }
    [Serializable] public sealed class RunState
    {
        public int version=1,spawnIndex,firstAid=1;
        public int difficultyLevel=2;
        public float elapsed,speedMultiplier=1,pendingAt;
        public int pendingPenalty;
        public bool companion;
        public bool[] resolved=new bool[9];
        public int[] chosen={-1,-1,-1,-1,-1,-1,-1,-1,-1};
        public int[] scores=new int[5];
        public string[] outcomes=new string[9];
        public Vector3 position,forward;
        public VehicleState[] vehicles;
        public string drivenVehicle;
        public bool homeSequence,homeDoorOpen;
        public float homeSeconds;
        public int homeSupplies;
        public int homeIndex;
        public string preparationSnapshot;
        public System.Collections.Generic.List<StormSim.Preparedness.PreparationActionRecord> preparationActions=new System.Collections.Generic.List<StormSim.Preparedness.PreparationActionRecord>();
        public PhysicalRunData physical=new PhysicalRunData();
    }
    [DefaultExecutionOrder(-100)]
    public sealed partial class SibucaoSession : MonoBehaviour
    {
        public static SibucaoSession Instance {get;private set;}
        public FloodDifficultySettings floodDifficulty;
        [Range(1,5)] public int selectedDifficulty=2;
        [Tooltip("Temporary route-design validation switch. New runs use the shallow Easy flood while the During encounters are tested.")]
        public bool keepFloodLowForRouteValidation=true;
        public SibucaoPlayer player;
        [Tooltip("Optional player location when this scene first opens. New runs and saved runs retain their own spawning logic.")]
        public Transform initialPlayerSpawn;
        public SibucaoWeather weather;
        public SibucaoRoute route;
        public SibucaoHomeOpening home;
        public SibucaoHomeOpening[] homes;
        public DecisionTreeManager physicalDecisions;
        public SibucaoMissionGuide missionGuide;
        public SibucaoInventory inventory;
        public SibucaoStoryPlayback story;
        public SibucaoPassageDirector passages;
        public SibucaoEncounter[] encounters;
        public DecisionNode opening,arrival;
        public LayerMask groundMask=1;
        public RunState state=new RunState();
        public SibucaoSettings settings=new SibucaoSettings();
        public SibucaoMode Mode {get;private set;}=SibucaoMode.Menu;
        public SibucaoMode settingsReturn=SibucaoMode.Menu;
        int settingsTransitionFrame=-1;
        public bool SettingsTransitionThisFrame=>settingsTransitionFrame==Time.frameCount;
        public SibucaoEncounter nearby,activeEncounter;
        public string toastTitle="",toastText="",subtitle="",error="";
        public float toastUntil,subtitleUntil;
        public string confirmation="";
        public SibucaoMode confirmReturn;
        string savePath;
        bool quitting;
        public bool HasSave=>!string.IsNullOrEmpty(savePath)&&(SlotExists(false)||SlotExists(true)||SlotExists(false,true)||SlotExists(true,true));
        public bool CanMove=>Mode==SibucaoMode.Playing;
        public string SavePrefix => GetComponent<CoastalAreaProfile>() ? GetComponent<CoastalAreaProfile>().SavePrefix : "sibucao-rain";
        void Awake()
        {
            Instance=this;savePath=Path.Combine(Application.persistentDataPath,SavePrefix+"-save.json");
            try {if(PlayerPrefs.HasKey("SibucaoSettings"))settings=JsonUtility.FromJson<SibucaoSettings>(PlayerPrefs.GetString("SibucaoSettings"))??new SibucaoSettings();}catch{settings=new SibucaoSettings();}
            state=new RunState();Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(!story)story=GetComponent<SibucaoStoryPlayback>();
            if(!story)story=gameObject.AddComponent<SibucaoStoryPlayback>();
            if(!passages)passages=GetComponent<SibucaoPassageDirector>();
        }
        void Start(){if(!GetComponent<SibucaoSoundFeedback>())gameObject.AddComponent<SibucaoSoundFeedback>();if(!GetComponent<SibucaoInteractionOutline>())gameObject.AddComponent<SibucaoInteractionOutline>();ApplySettings();if(initialPlayerSpawn)player.Teleport(initialPlayerSpawn.position,initialPlayerSpawn.forward);else player.Teleport(route.spawnPoints[0],route.Sample(10)-route.Sample(0));weather.SetStorm(false);SetMode(SibucaoMode.Menu);}
        public void SetMode(SibucaoMode mode)
        {
            if(Mode==SibucaoMode.Settings||mode==SibucaoMode.Settings)settingsTransitionFrame=Time.frameCount;
            if(mode==SibucaoMode.Playing&&state?.physical!=null&&(state.physical.needsRescue||state.physical.health<=0)){state.physical.needsRescue=true;mode=SibucaoMode.Recovery;}
            Mode=mode;Time.timeScale=mode==SibucaoMode.Paused||mode==SibucaoMode.Settings||mode==SibucaoMode.Decision||mode==SibucaoMode.Confirm||mode==SibucaoMode.Cinematic||mode==SibucaoMode.Recovery||mode==SibucaoMode.ResumeSelection?0:1;
            if(player && player.hands)player.hands.gameObject.SetActive((!home||!home.MovementLocked)&&(mode==SibucaoMode.Playing||mode==SibucaoMode.Decision||mode==SibucaoMode.Paused));
            if(mode!=SibucaoMode.Playing)SibucaoControls.ResetGrip();
            bool capture=mode==SibucaoMode.Playing||mode==SibucaoMode.Preparation;
            Cursor.lockState=capture?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!capture;
        }
        public void LockCursor(){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        public void SelectRole(){if(physicalDecisions)physicalDecisions.playerRole="Resident";StartRun();}
        public void StartWithRole(string role)
        {
            // Legacy callers may still pass a role; all players now use the same rules.
            if(physicalDecisions)physicalDecisions.playerRole="Resident";
            StartRun();
        }
        public void StartWithDifficulty(int level)
        {
            if(level<1||level>5)throw new ArgumentOutOfRangeException(nameof(level));
            selectedDifficulty=level;StartRun();
        }
        public void StartRun()
        {
            var preparation=GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();
            if(preparation&&preparation.TryBegin())return;
            if(story)story.ResetPlayback();
            if(homes!=null && homes.Length>0)
            {
                if(home)home.Suspend();
                int last=PlayerPrefs.GetInt("SibucaoLastHome",-1);
                int selected=UnityEngine.Random.Range(0,homes.Length);
                if(selected==last && homes.Length>1)selected=(selected+UnityEngine.Random.Range(1,homes.Length))%homes.Length;
                if(preparation&&preparation.HandingOff)selected=Array.IndexOf(homes,preparation.firstHome);
                home=homes[selected];PlayerPrefs.SetInt("SibucaoLastHome",selected);
            }
            foreach(var v in SibucaoVehicle.All)v.ResetVehicle();
            int previous=PlayerPrefs.GetInt("SibucaoLastSpawn",-1);
            int pick=UnityEngine.Random.Range(0,route.spawnPoints.Length);
            if(pick==previous && route.spawnPoints.Length>1)pick=(pick+UnityEngine.Random.Range(1,route.spawnPoints.Length))%route.spawnPoints.Length;
            PlayerPrefs.SetInt("SibucaoLastSpawn",pick);PlayerPrefs.Save();
            state=new RunState{spawnIndex=pick,difficultyLevel=keepFloodLowForRouteValidation?1:selectedDifficulty};state.resolved[0]=true;state.outcomes[0]=opening.message;
            if(homes!=null)state.homeIndex=Array.IndexOf(homes,home);
            foreach(var e in encounters){e.following=false;if(e.actor)e.actor.localPosition=Vector3.zero;}
            player.Teleport(route.spawnPoints[pick],route.Sample(route.spawnDistances[pick]+8)-route.Sample(route.spawnDistances[pick]));
            activeEncounter=null;nearby=null;error="";SetMode(SibucaoMode.Playing);weather.SetStorm(true);
            Notify(opening.title,"Reach Banago Elementary School I.");Say(opening.message,10);
            if(home){home.Begin();subtitleUntil=0;}
            foreach(var resident in UnityEngine.Object.FindObjectsByType<SibucaoResident>())if(resident.enabled)resident.ResetResident();
            if(physicalDecisions)physicalDecisions.ResetRun();
            runStarted=true;checkpointRequested=true;nextCheckpoint=Time.unscaledTime+5;
        }
        void Update()
        {
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if(SettingsTransitionThisFrame)return;
                if(Mode==SibucaoMode.Settings&&SibucaoUI.BindingCapture){SibucaoUI.CancelBinding();return;}
                if(missionGuide && missionGuide.BlocksInput || inventory && inventory.BlocksInput)return;
                if(Mode==SibucaoMode.Playing)Pause();else if(Mode==SibucaoMode.Paused)SetMode(SibucaoMode.Playing);
                else if(Mode==SibucaoMode.Settings){ApplySettings();SetMode(settingsReturn);}else if(Mode==SibucaoMode.Decision){activeEncounter=null;SetMode(SibucaoMode.Playing);}
                else if(Mode==SibucaoMode.Confirm)SetMode(confirmReturn);
                else if(Mode==SibucaoMode.ResumeSelection)SetMode(resumeReturn);
                else if(Mode==SibucaoMode.Recovery)SetMode(SibucaoMode.Playing);
            }
            if(Mode!=SibucaoMode.Playing){nearby=null;return;}
            if(home && home.MovementLocked){nearby=null;return;}
            var flood=GetComponent<FloodSimulationManager>();if(flood&&flood.IsCatchingUp){nearby=null;return;}
            // Home time is the authoritative storm clock, including all physical branch travel/work.
            if(home&&state.homeSequence)state.elapsed=state.homeSeconds;
            else {state.homeSeconds+=Time.deltaTime;state.elapsed=state.homeSeconds;}
            TickCheckpoint();
            if(physicalDecisions){nearby=null;return;}
            if(state.pendingPenalty>0 && state.elapsed>=state.pendingAt)
            {
                state.scores[(int)ScoreCategory.Evacuation]-=state.pendingPenalty;state.pendingPenalty=0;state.speedMultiplier=Mathf.Min(state.speedMultiplier,.85f);
                Say("The cut is slowing you down. Keep moving toward shelter.",7);
            }
            nearby=null;float best=20;
            foreach(var e in encounters)
            {
                if(e.arrival || state.resolved[e.index])continue;
                Vector3 delta=e.transform.position+Vector3.up-player.view.transform.position;
                float distance=delta.magnitude;
                if(distance<best && distance<11 && Vector3.Dot(player.view.transform.forward,delta.normalized)>.8f && !Physics.Linecast(player.view.transform.position,e.transform.position+Vector3.up,groundMask,QueryTriggerInteraction.Ignore)){nearby=e;best=distance;}
            }
        }
        public void OpenDecision(SibucaoEncounter e)
        {
            if(physicalDecisions)return;
            if(player.vehicle){Say("Stop and get out to speak or inspect.",4);return;}
            if(Mode!=SibucaoMode.Playing||e.arrival||state.resolved[e.index])return;
            activeEncounter=e;SetMode(SibucaoMode.Decision);
        }
        public bool IsAvailable(DecisionChoice choice)=>!choice.consumesFirstAid||state.firstAid>0;
        public void Choose(int choiceIndex)
        {
            if(Mode!=SibucaoMode.Decision || activeEncounter==null)return;
            var e=activeEncounter;if(state.resolved[e.index]||choiceIndex<0||choiceIndex>=e.node.choices.Length)return;
            var c=e.node.choices[choiceIndex];if(!IsAvailable(c)){ShowError("Your first aid kit has already been used.");return;}
            float chance=c.baseSuccessChance;int points=c.basePoints;
            // Role bonuses have been retired; difficulty and physical conditions determine outcomes.
            bool success=UnityEngine.Random.value<Mathf.Clamp01(chance);
            state.resolved[e.index]=true;state.chosen[e.index]=choiceIndex;
            state.elapsed+=c.timeCost;
            state.scores[(int)ScoreCategory.DecisionMaking]+=points-(success?0:c.failurePenalty);
            state.scores[(int)ScoreCategory.ResourceManagement]+=c.resourcePoints;
            state.scores[(int)ScoreCategory.Teamwork]+=c.teamworkPoints;
            state.scores[(int)ScoreCategory.Evacuation]+=c.evacuationPoints;
            state.speedMultiplier=Mathf.Min(state.speedMultiplier,c.movementMultiplier);
            if(c.consumesFirstAid)state.firstAid--;
            if(c.recruitCompanion){state.companion=true;e.following=true;}
            if(!success && c.delayedConsequenceFlag){state.pendingAt=state.elapsed+20;state.pendingPenalty+=c.failurePenalty;}
            else if(!success)state.speedMultiplier=Mathf.Min(state.speedMultiplier,.85f);
            string outcome=success||string.IsNullOrEmpty(c.failureOutcome)?c.outcome:c.failureOutcome;
            state.outcomes[e.index]=outcome;
            activeEncounter=null;SetMode(SibucaoMode.Playing);player.Gesture(e.index==3?1:e.index>3?2:0);Say(outcome,9);Notify(c.consumesFirstAid?"First aid used":"Continue to shelter",NextObjective());
        }
        public void Complete()
        {
            if(physicalDecisions)return;
            if(Mode!=SibucaoMode.Playing||state.resolved[8])return;
            state.resolved[8]=true;state.outcomes[8]=arrival.message;
            state.scores[(int)ScoreCategory.Evacuation]+=20;
            if(state.companion)state.scores[(int)ScoreCategory.Teamwork]+=10;
            if(state.pendingPenalty>0){state.scores[(int)ScoreCategory.Evacuation]-=state.pendingPenalty;state.pendingPenalty=0;}
            SetMode(SibucaoMode.Complete);weather.SetStorm(false);
        }
        public string NextObjective()
        {
            if(physicalDecisions)return "Continue through Sibucao";
            foreach(var e in encounters)if(!e.arrival&&!state.resolved[e.index])return e.node.title;
            return "Enter the school campus";
        }
        public Vector3 NavigationTarget()
        {
            if(physicalDecisions)return route.Sample(Mathf.Min(route.length,route.Progress(player.transform.position)+18));
            float progress=route.Progress(player.transform.position);
            foreach(var e in encounters)if(!e.arrival&&!state.resolved[e.index] && e.routeDistance<progress-12)return e.transform.position;
            return route.Sample(Mathf.Min(route.length,progress+18));
        }
        public void Pause(){SetMode(SibucaoMode.Paused);}
        public void Notify(string title,string text){toastTitle=title;toastText=text;toastUntil=Time.unscaledTime+6;}
        public void Say(string text,float seconds){RouteChoices?.StopVoices();GetComponent<SibucaoNarration>()?.InterruptForDialogue(seconds);SayNarration(text,seconds);}
        internal void SayNarration(string text,float seconds){subtitle=text;subtitleUntil=Time.unscaledTime+seconds;}
        public void ShowError(string text){error=text;Notify("Unable to continue",text);}
        public void OpenSettings(){settingsReturn=Mode;SetMode(SibucaoMode.Settings);}
        public void ApplySettings()
        {
            SibucaoControls.Ensure(settings);settings.textScale=Mathf.Clamp(settings.textScale,1,1.3f);settings.fieldOfView=Mathf.Clamp(settings.fieldOfView,60,100);
            if(player&&player.view&&!UnityEngine.XR.XRSettings.isDeviceActive)player.view.fieldOfView=settings.fieldOfView;
            settings.speechVolume=Mathf.Clamp01(settings.speechVolume);settings.environmentVolume=Mathf.Clamp01(settings.environmentVolume);settings.effectsVolume=Mathf.Clamp01(settings.effectsVolume);
            settings.volume=Mathf.Clamp01(settings.volume);settings.sensitivity=Mathf.Clamp(settings.sensitivity,.03f,.3f);
            AudioListener.volume=settings.volume;
            bool xr=UnityEngine.XR.XRSettings.isDeviceActive;
            QualitySettings.vSyncCount=xr?0:1;Application.targetFrameRate=xr?-1:60;
            if(!Application.isEditor&&!xr)Screen.fullScreen=settings.fullscreen;
            if(weather)weather.ApplyQuality(settings.graphics);
            PlayerPrefs.SetString("SibucaoSettings",JsonUtility.ToJson(settings));PlayerPrefs.Save();
        }
        void ApplyLoadedRun(RunState loaded)
        {
            if(story)story.ResetPlayback();
                if(home)home.Suspend();
                state=loaded;selectedDifficulty=state.difficultyLevel;
                GetComponent<FloodSimulationManager>()?.RestoreClock();
                if(homes!=null && homes.Length>0)home=homes[Mathf.Clamp(state.homeIndex,0,homes.Length-1)];
                foreach(var v in SibucaoVehicle.All){v.ResetVehicle();if(state.vehicles!=null)foreach(var saved in state.vehicles)if(saved.id==v.vehicleId)v.Restore(saved);}
                player.Teleport(state.position,state.forward);
                foreach(var v in SibucaoVehicle.All)if(v.vehicleId==state.drivenVehicle)v.TryEnter(player,true);
                foreach(var e in encounters){e.following=state.companion && e.index==5;if(e.actor)e.actor.localPosition=Vector3.zero;}
                SetMode(state.resolved[8]?SibucaoMode.Complete:SibucaoMode.Playing);weather.SetStorm(!state.resolved[8]);Notify("Evacuation resumed",NextObjective());
                if(home)home.Restore();
                GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>()?.RestoreStormSnapshot(state.preparationSnapshot);
                if(physicalDecisions){if(state.physical==null)state.physical=new PhysicalRunData();physicalDecisions.RestoreRun();SetMode(state.physical.boundaryReached?SibucaoMode.Complete:SibucaoMode.Playing);}
                if(!string.IsNullOrEmpty(state.preparationSnapshot))GetComponent<StormSim.Preparedness.PreparationContinuity>()?.HideLegacyBag();
            weather.SetStorm(Mode!=SibucaoMode.Complete);
            if(physicalDecisions&&physicalDecisions.RestoredLegacyAccess)
            {
                ReturnToUnfinishedJunction();
                Notify("Map access updated", "Extra task ramps have been removed. Completed work and supplies are retained; resume from the unfinished junction.");
            }
        }
        public void Confirm(string action){confirmation=action;confirmReturn=Mode;SetMode(SibucaoMode.Confirm);}
        public void ConfirmYes()
        {
            if(runStarted&&confirmReturn==SibucaoMode.Paused)SaveCheckpoint();
            if(confirmation=="exit"){quitting=true;Application.Quit();if(Application.isEditor){SetMode(SibucaoMode.Menu);Notify("Desktop exit","Stop Play mode to leave the editor preview.");}}
            else{SetMode(SibucaoMode.Menu);weather.SetStorm(false);}
        }
        void OnDestroy(){if(Instance==this)Instance=null;if(!quitting){Time.timeScale=1;AudioListener.volume=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
    }
}
