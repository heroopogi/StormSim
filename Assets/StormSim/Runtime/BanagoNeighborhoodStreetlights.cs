using UnityEngine;

/// <summary>Only controls lights belonging to the new neighborhood-detail group.</summary>
[ExecuteAlways]
public sealed class BanagoNeighborhoodStreetlights : MonoBehaviour
{
    public Light sun;
    public Light[] lamps;
    public bool forceOn;
    float nextCheck;
    void OnEnable(){Apply();}
    void Update(){if(Time.realtimeSinceStartup>=nextCheck){nextCheck=Time.realtimeSinceStartup+1;Apply();}}
    public void Apply()
    {
        bool on=forceOn||!sun||!sun.enabled||sun.intensity<.25f||Vector3.Dot(-sun.transform.forward,Vector3.up)<.035f;
        if(lamps==null)return;
        foreach(var lamp in lamps)if(lamp)lamp.enabled=on;
    }
    void OnDisable(){if(lamps!=null)foreach(var lamp in lamps)if(lamp)lamp.enabled=false;}
}
