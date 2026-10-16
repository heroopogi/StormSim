using UnityEngine;
using Crest;

namespace StormSim.Sibucao
{
    // Crest displays the existing simulation. Flood depth/current and gameplay queries remain authoritative.
    [DefaultExecutionOrder(100)]
    public sealed class SibucaoCrestBridge : MonoBehaviour
    {
        public FloodSimulationManager flood;
        [Tooltip("Render inland flood water directly on the simulation mesh; Crest continues to render the sea.")]
        public bool directTerrainFlood;
        public OceanRenderer ocean;
        public MeshFilter floodHeight, floodClip, floodFlow, seaHeight, seaClip;
        public Renderer seaHeightRenderer;
        Renderer legacyFlood,legacySea;
        bool floodWasEnabled,seaWasEnabled,captured;
        MaterialPropertyBlock seaProperties;
        public bool IsRendering=>enabled&&ocean&&ocean.isActiveAndEnabled&&ocean.Root&&floodHeight&&floodHeight.sharedMesh;
        void LateUpdate()
        {
            if(!flood||!ocean||!flood.SurfaceMesh)return;
            if(!captured)
            {
                var source=flood.transform.Find("Terrain-connected rainfall and runoff");
                legacyFlood=source?source.GetComponent<Renderer>():null;
                legacySea=flood.sea?flood.sea.GetComponent<Renderer>():null;
                floodWasEnabled=legacyFlood&&legacyFlood.enabled;seaWasEnabled=legacySea&&legacySea.enabled;
                captured=true;seaProperties=new MaterialPropertyBlock();
            }
            if(floodHeight)floodHeight.sharedMesh=flood.SurfaceMesh;
            if(floodClip)floodClip.sharedMesh=flood.SurfaceMesh;
            if(floodFlow)floodFlow.sharedMesh=flood.SurfaceMesh;
            if(flood.sea&&seaHeight&&seaClip)
            {
                var source=flood.sea.GetComponent<MeshFilter>();
                seaHeight.sharedMesh=seaClip.sharedMesh=source.sharedMesh;
                if(legacySea&&seaHeightRenderer){legacySea.GetPropertyBlock(seaProperties);seaHeightRenderer.SetPropertyBlock(seaProperties);}
            }
            if(floodHeight)floodHeight.gameObject.SetActive(!directTerrainFlood);
            if(floodClip)floodClip.gameObject.SetActive(!directTerrainFlood);
            if(floodFlow)floodFlow.gameObject.SetActive(!directTerrainFlood);
            bool ready=IsRendering;
            if(legacyFlood)legacyFlood.enabled=(directTerrainFlood||!ready)&&floodWasEnabled;
            if(legacySea)legacySea.enabled=!ready&&seaWasEnabled;
        }
        void OnDisable()
        {
            if(legacyFlood)legacyFlood.enabled=floodWasEnabled;
            if(legacySea)legacySea.enabled=seaWasEnabled;
            if(ocean)ocean.gameObject.SetActive(false);
        }
        void OnEnable(){if(ocean)ocean.gameObject.SetActive(true);}
    }
}

