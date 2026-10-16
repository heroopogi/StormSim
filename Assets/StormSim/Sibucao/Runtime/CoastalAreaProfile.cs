using UnityEngine;
namespace StormSim.Sibucao
{
    // Per-scene identity; all encounter, weather and scoring mechanics remain shared.
    public sealed class CoastalAreaProfile : MonoBehaviour
    {
        public string areaName,saveId;
        public TextAsset streetConnections;
        public bool preserveFurnishingSize;
        public float[] ladderWallOffsets;
        public Vector3[] yardOffsets;
        [SerializeField,HideInInspector] public Vector3 fittedYardOffset;
        [SerializeField,HideInInspector] public float fittedLadderScale=1;
        public void SelectHome(StormSim.Preparedness.SibucaoPreparationBridge bridge, int index)
        {
            var game=GetComponent<SibucaoSession>();
            index=Mathf.Clamp(index,0,game.homes.Length-1);
            var target=game.homes[index];
            if(bridge.firstHome==target){CoastalHomePlacement.Fit(bridge.preparation);bridge.preparation.RefreshInitialPlacement();return;}
            bridge.preparation.ResetForHomeMove();
            var anchor=bridge.preparation.transform;
            var delta=target.transform.localToWorldMatrix*anchor.worldToLocalMatrix;
            var rotation=target.transform.rotation*Quaternion.Inverse(anchor.rotation);
            var oldScale=anchor.lossyScale;
            anchor.SetPositionAndRotation(target.transform.position,target.transform.rotation);
            anchor.localScale=target.transform.lossyScale;
            if(preserveFurnishingSize)
            {
                var ratio=new Vector3(oldScale.x/anchor.lossyScale.x,oldScale.y/anchor.lossyScale.y,oldScale.z/anchor.lossyScale.z);
                foreach(Transform child in anchor)child.localScale=Vector3.Scale(child.localScale,ratio);
            }
            bridge.preparation.RebaseHome(delta,rotation);
            CoastalHomePlacement.Fit(bridge.preparation);
            bridge.preparation.RefreshInitialPlacement();
            bridge.firstHome=target;
            bridge.preparation.homeStart=target.standingPoint;
        }
        public void SelectNewHome(StormSim.Preparedness.SibucaoPreparationBridge bridge)
        {
            var homes=GetComponent<SibucaoSession>().homes;
            int last=PlayerPrefs.GetInt(SavePrefix+"-last-home",-1);
            int index=Random.Range(0,homes.Length);
            if(index==last&&homes.Length>1)index=(index+Random.Range(1,homes.Length))%homes.Length;
            SelectHome(bridge,index);PlayerPrefs.SetInt(SavePrefix+"-last-home",index);
        }
        public string SavePrefix=>string.IsNullOrEmpty(saveId)?"sibucao-rain":saveId;
    }
}
