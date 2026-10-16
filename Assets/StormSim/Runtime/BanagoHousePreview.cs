using UnityEngine;

// Provenance for the review batch; facade details and undocumented heights are estimates.
public sealed class BanagoHousePreview : MonoBehaviour
{
    public string footprintId;
    public double longitude;
    public double latitude;
    public float wallHeightMeters;
    public float roofRiseMeters;
    public float footprintAreaSquareMeters;
    public Color sampledRoofColor;
    public bool heightEstimated = true;
    public string source = "Overture Maps building footprint; roof color sampled from Unity satellite imagery";
}
