using UnityEngine;
[ExecuteAlways]
public sealed class BanagoWorldSigns : MonoBehaviour
{
    public Material lettering;
    Font font;
    void OnEnable(){font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");Font.textureRebuilt+=Rebuild;Rebuild(font);}
    void OnDisable(){Font.textureRebuilt-=Rebuild;}
    void Rebuild(Font changed)
    {
        if(!lettering||changed!=font)return;
        lettering.mainTexture=font.material.mainTexture;
        foreach(var text in GetComponentsInChildren<TextMesh>())text.GetComponent<MeshRenderer>().sharedMaterial=lettering;
    }
    void Update(){if(lettering&&font&&lettering.mainTexture!=font.material.mainTexture)Rebuild(font);}
}
