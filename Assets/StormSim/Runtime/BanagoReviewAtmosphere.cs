using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public sealed class BanagoReviewAtmosphere : MonoBehaviour
{
    public Light sun;
    public Material hazySky;
    public Material originalSky;
    public float originalIntensity,originalTemperature,originalShadowStrength;
    public bool originalFog,configured;
    public Color originalFogColor;
    public float originalFogDensity;
    public FogMode originalFogMode;
    public bool ambientCaptured;
    public AmbientMode originalAmbientMode;
    public Color originalAmbientSky,originalAmbientEquator,originalAmbientGround;
    public void CaptureAndApply()
    {
        if(!sun||configured)return;
        originalSky=RenderSettings.skybox;originalIntensity=sun.intensity;originalTemperature=sun.colorTemperature;originalShadowStrength=sun.shadowStrength;
        originalFog=RenderSettings.fog;originalFogColor=RenderSettings.fogColor;originalFogDensity=RenderSettings.fogDensity;originalFogMode=RenderSettings.fogMode;configured=true;Apply();
    }
    void OnEnable(){Apply();}
    public void Apply()
    {
        if(!configured||!sun)return;
        if(!ambientCaptured){originalAmbientMode=RenderSettings.ambientMode;originalAmbientSky=RenderSettings.ambientSkyColor;originalAmbientEquator=RenderSettings.ambientEquatorColor;originalAmbientGround=RenderSettings.ambientGroundColor;ambientCaptured=true;}
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.69f,.73f,.76f);RenderSettings.ambientEquatorColor=new Color(.52f,.56f,.56f);RenderSettings.ambientGroundColor=new Color(.31f,.32f,.28f);
        sun.intensity=1.05f;sun.colorTemperature=6450;sun.shadowStrength=.52f;sun.shadows=LightShadows.Soft;
        if(hazySky)RenderSettings.skybox=hazySky;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.65f,.68f,.69f);RenderSettings.fogDensity=.00034f;
    }
    void OnDisable()
    {
        if(!configured)return;
        if(sun){sun.intensity=originalIntensity;sun.colorTemperature=originalTemperature;sun.shadowStrength=originalShadowStrength;}
        RenderSettings.skybox=originalSky;RenderSettings.fog=originalFog;RenderSettings.fogColor=originalFogColor;RenderSettings.fogMode=originalFogMode;RenderSettings.fogDensity=originalFogDensity;
        if(ambientCaptured){RenderSettings.ambientMode=originalAmbientMode;RenderSettings.ambientSkyColor=originalAmbientSky;RenderSettings.ambientEquatorColor=originalAmbientEquator;RenderSettings.ambientGroundColor=originalAmbientGround;}
    }
}
