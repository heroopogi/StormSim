using UnityEngine;
using System.Collections.Generic;
namespace StormSim.Sibucao
{
    /// <summary>Scene-baked north-up street map; does not render a second world camera.</summary>
    public sealed class SibucaoMinimap : MonoBehaviour
    {
        public Texture2D streetMap;
        public Rect worldBounds;
        public float visibleWidth=110;
        SibucaoSession S=>SibucaoSession.Instance;
        public PhysicalDecisionStation TargetStation
        {
            get
            {
                if(!S||!S.physicalDecisions)return null;
                if(S.missionGuide && S.missionGuide.CurrentStation)return S.missionGuide.CurrentStation;
                if(S.missionGuide)return S.missionGuide.NextStation;
                foreach(var station in S.physicalDecisions.stations)
                    if(!station.Resolved)return station;
                return null;
            }
        }
        public bool HomeTarget=>S.home&&(S.home.MovementLocked||S.missionGuide&&S.missionGuide.InsideHome);
        public SibucaoOptionalSideMission OptionalMission=>S?S.GetComponent<SibucaoOptionalSideMission>():null;
        public SibucaoFloodedStreetIncident FloodedStreet=>S?S.GetComponent<SibucaoFloodedStreetIncident>():null;
        public SibucaoRouteIncidentSequence RouteIncidents=>S?S.GetComponent<SibucaoRouteIncidentSequence>():null;
        public bool HasSideMissionMarker=>OptionalMission&&OptionalMission.SideMissionVisible;
        public Vector3 SideMissionMarker=>OptionalMission?OptionalMission.SideMissionTarget:Target;
        public Vector3 Target=>HomeTarget?S.home.doorPanel.position:S.missionGuide?S.missionGuide.NavigationTarget:TargetStation?TargetStation.NavigationTarget:S.route.Sample(S.route.length);
        public string TargetLabel=>HomeTarget?(S.home.MovementLocked?"Weather bulletin":"Leave the house"):RouteIncidents&&RouteIncidents.WantsLocalGuidance?(RouteIncidents.ActiveChapter==2?"blocked drainage":"injured evacuee"):FloodedStreet&&FloodedStreet.WantsLocalGuidance?"flood assessment":OptionalMission&&OptionalMission.SideMissionActive?OptionalMission.ShortTitle:S.GetComponent<SibucaoContinuousChapterOne>() is SibucaoContinuousChapterOne chapter?chapter.ShortTitle:S.missionGuide&&S.missionGuide.RouteChoices&&S.missionGuide.RouteChoices.Working?S.missionGuide.RouteChoices.Working.title:TargetStation?(TargetStation.index==0&&!TargetStation.HasVisibleWater?"Rain-soaked street":TargetStation.title):"School evacuation center";
        public float Distance=>Vector3.Distance(S.player.transform.position,Target);
        readonly List<Vector3> guidance=new List<Vector3>();int pathFrame=-1;
        public IReadOnlyList<Vector3> GuidancePath
        {
            get
            {
                if(pathFrame==Time.frameCount)return guidance;pathFrame=Time.frameCount;guidance.Clear();
                var player=S.player.transform.position;guidance.Add(player);
                var choices=S.missionGuide?S.missionGuide.RouteChoices:null;var side=choices?choices.Working:null;
                var branch=TargetStation?TargetStation.EngagedBranch:null;
                if(!side&&choices&&choices.TryDepartureTarget(out var departure))
                {
                    if(choices.TryStreetConnection(out var connection,out int first))for(int i=first;i<connection.Length;i++)guidance.Add(connection[i]);
                    else guidance.Add(departure);
                    return guidance;
                }
                else if(side&&(side.Step==null||side.Step.work==RouteWork.Escort))
                {
                    int nearest=0;float best=float.MaxValue;
                    for(int i=0;i<side.route.Length;i++){float d=(side.route[i]-player).sqrMagnitude;if(d<best){best=d;nearest=i;}}
                    int start=side.Step!=null?side.State.escortPoint:nearest;
                    for(int i=start;i<side.route.Length;i++)guidance.Add(side.route[i]);
                }
                else if(branch&&branch.path!=null)
                    for(int i=Mathf.Clamp(branch.Attempt.step,0,branch.path.Length);i<branch.path.Length;i++)guidance.Add(branch.path[i].position);
                else if(!HomeTarget&&!side&&Vector3.Distance(player,Target)>6)
                {
                    float end=S.route.Progress(Target),start=S.route.TravelJoinProgress(player,end);float sign=Mathf.Sign(end-start);
                    for(float d=start;Mathf.Abs(end-d)>2&&guidance.Count<600;d+=sign*2)guidance.Add(S.route.Sample(d));
                }
                // Work targets may be behind the final route point; an engaged route already ends at its destination.
                if(!branch&&!side||side&&side.Step!=null&&side.Step.work!=RouteWork.Escort||guidance.Count==1)guidance.Add(Target);
                return guidance;
            }
        }
        public Vector3 NextDirectionPoint
        {
            get{if(S.RouteChoices&&S.RouteChoices.TryDepartureTarget(out var returnPoint))return returnPoint;var path=GuidancePath;foreach(var p in path)if(Vector3.Distance(p,S.player.transform.position)>2.5f)return p;return Target;}
        }
        public string DirectionText
        {
            get
            {
                if(HomeTarget)return "GO TO THE FRONT DOOR";
                if(S.RouteChoices&&S.RouteChoices.TryDepartureTarget(out _))return "RETURN ROUTE";
                if(Distance<3)return "YOU ARE AT THE TASK";
                var delta=NextDirectionPoint-S.player.transform.position;delta.y=0;
                float angle=Vector3.SignedAngle(S.player.view.transform.forward,delta,Vector3.up);
                return Mathf.Abs(angle)>135?"TURN AROUND":angle>30?"TURN RIGHT":angle< -30?"TURN LEFT":"CONTINUE AHEAD";
            }
        }
        public Vector2 Project(Vector3 point,Rect area)
        {
            var delta=point-S.player.transform.position;float scale=area.width/visibleWidth;
            return area.center+new Vector2(delta.x,-delta.z)*scale;
        }
        public Rect MapUV(Rect area)
        {
            float height=visibleWidth*area.height/area.width;var p=S.player.transform.position;
            return new Rect((p.x-visibleWidth/2-worldBounds.x)/worldBounds.width,(p.z-height/2-worldBounds.y)/worldBounds.height,visibleWidth/worldBounds.width,height/worldBounds.height);
        }
    }
}
