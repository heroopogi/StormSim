using UnityEngine;
[ExecuteAlways]
public sealed class BaybayinExpansionInfo : MonoBehaviour
{
    [TextArea(5,12)] public string referenceNotes;
    public string sourceFolder;
    public int buildingCount;
    public int coastalBuildingCount;
    public CesiumForUnity.CesiumPolygonRasterOverlay terrainOverlay;
    public CesiumForUnity.CesiumCartographicPolygon[] originalTerrainAreas;
    public CesiumForUnity.CesiumCartographicPolygon[] expandedTerrainAreas;
    void OnEnable()
    {
        if(terrainOverlay && expandedTerrainAreas!=null && expandedTerrainAreas.Length>0)
            terrainOverlay.polygons=new System.Collections.Generic.List<CesiumForUnity.CesiumCartographicPolygon>(expandedTerrainAreas);
    }
    void OnDisable()
    {
        if(terrainOverlay && originalTerrainAreas!=null && originalTerrainAreas.Length>0)
            terrainOverlay.polygons=new System.Collections.Generic.List<CesiumForUnity.CesiumCartographicPolygon>(originalTerrainAreas);
    }
}
