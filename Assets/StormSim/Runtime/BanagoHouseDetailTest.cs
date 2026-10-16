using UnityEngine;

[ExecuteAlways]
public sealed class BanagoHouseDetailTest : MonoBehaviour
{
    public GameObject[] originalHouses = new GameObject[0];
    public bool[] originalActive = new bool[0];
    [TextArea(3,8)] public string notes;
    void OnEnable()=>Apply();
    public void Apply()
    {
        foreach(var house in originalHouses)if(house)house.SetActive(false);
    }
    void OnDisable()
    {
        for(int i=0;i<originalHouses.Length;i++)if(originalHouses[i])originalHouses[i].SetActive(i<originalActive.Length && originalActive[i]);
    }
}
