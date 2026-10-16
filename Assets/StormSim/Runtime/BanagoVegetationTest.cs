using UnityEngine;

/// <summary>Source and limits of the deliberately small vegetation review batch.</summary>
public sealed class BanagoVegetationTest : MonoBehaviour
{
    public Vector2 southWest = new Vector2(190, 450);
    public float zoneSizeMeters = 100;
    public int trees, grassClumps;
    public float buildingClearanceMeters = 1.5f;
    public float roadClearanceMeters = 1f;
    public string placementSource = "Satellite canopy mask and live building/road footprints";
    public string sourceData;
    public string sceneBackup;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.7f, 1f, .15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3(southWest.x + zoneSizeMeters / 2, 0, southWest.y + zoneSizeMeters / 2),
            new Vector3(zoneSizeMeters, .2f, zoneSizeMeters));
    }
}
