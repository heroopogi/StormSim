using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>One readable cue from the shared storm. No forced screen-cover minigame.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class SibucaoIncidentDirector : MonoBehaviour
    {
        SibucaoSession S=>SibucaoSession.Instance;
        PhysicalRunData tracked;
        public bool VisionObscured=>false;
        public float MovementFactor=>1;
        public float RemovalProgress=>1;
        public string Prompt=>null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToSession()
        {
            var s=FindAnyObjectByType<SibucaoSession>();
            if(s&&!s.GetComponent<SibucaoIncidentDirector>())s.gameObject.AddComponent<SibucaoIncidentDirector>();
        }
        void Update()
        {
            if(!S||S.state==null||!S.player)return;
            if(tracked!=S.state.physical)tracked=S.state.physical;
            if(tracked.flags.Contains("director_gust_cue")||S.Mode!=SibucaoMode.Playing)return;
            if(S.home&&S.home.MovementLocked||S.inventory&&S.inventory.BlocksInput||S.missionGuide&&S.missionGuide.BlocksInput)return;
            var storm=GetComponent<SibucaoTyphoonExposure>();
            if(!storm||storm.Gust01<.7f||storm.UnderCover||S.player.MoveInput.sqrMagnitude<.05f)return;
            if(S.route.Progress(S.player.transform.position)<tracked.routeStart+100)return;
            var local=GetComponent<SibucaoRouteIncidentSequence>();
            var junction=GetComponent<SibucaoFloodedStreetIncident>();
            var dynamic=GetComponent<SibucaoDynamicIncidentDirector>();
            if(local&&local.ProgressVisible||junction&&junction.Active||dynamic&&dynamic.HasActiveIncident)return;
            tracked.flags.Add("director_gust_cue");
            S.Notify("Strong gust","Slow down before exposed passages. Keep clear of loose materials; a lull is not an all-clear.");
            S.RequestCheckpoint();
        }
        public bool Interact()
        {
            var dynamic=GetComponent<SibucaoDynamicIncidentDirector>();
            return dynamic&&dynamic.Interact();
        }
    }
}
