Shader "Crest/Inputs/Sea Floor Depth/STORMSIM simulation height"
{
    Properties
    {
        _SeaMode("Coastal waves",Float)=0
        _FloodWaveHeight("Runoff wave height",Float)=0
        _FloodWaveSpeed("Runoff wave speed",Float)=1
        _SeaWave0("Wave 0",Vector)=(0,0,0,0)
        _SeaWave1("Wave 1",Vector)=(0,0,0,0)
        _SeaWave2("Wave 2",Vector)=(0,0,0,0)
        _SeaFrequencies("Frequencies",Vector)=(0,0,0,0)
        _SeaTime("Wave clock",Float)=0
        _ShorelineFade("Shoreline fade",Float)=10
    }
    SubShader { Pass {
        Blend Off ZWrite Off Cull Off ColorMask G
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        #include "Assets/Crest/Crest/Shaders/OceanGlobals.hlsl"
        float _SeaMode,_FloodWaveHeight,_FloodWaveSpeed,_SeaTime,_ShorelineFade;
        float4 _SeaWave0,_SeaWave1,_SeaWave2,_SeaFrequencies;
        struct V {float4 p:SV_POSITION;float h:TEXCOORD0;};
        float wave(float4 w,float f,float phase,float2 p){return w.w*sin(dot(w.xy,p)*w.z-f*_SeaTime+phase);}
        V vert(float4 p:POSITION,float2 uv:TEXCOORD0)
        {
            if(_SeaMode>.5){float fade=saturate(uv.x/max(.5,_ShorelineFade));fade=fade*fade*(3-2*fade);p.y+=fade*(wave(_SeaWave0,_SeaFrequencies.x,0,p.xz)+wave(_SeaWave1,_SeaFrequencies.y,1.7,p.xz)+wave(_SeaWave2,_SeaFrequencies.z,4.1,p.xz));}
            float3 w=mul(unity_ObjectToWorld,p).xyz;
            if(_SeaMode<.5){float t=_Time.y*_FloodWaveSpeed;w.y+=_FloodWaveHeight*(sin(w.x*1.3+w.z*.65-t*1.8)*.55+sin(w.x*-.8+w.z*1.7-t*1.3)*.3+sin(w.x*2.4+w.z*1.1+t*2.1)*.15)*saturate(uv.x/.3);}
            V o;o.p=mul(UNITY_MATRIX_VP,float4(w,1));o.h=w.y-_OceanCenterPosWorld.y;return o;
        }
        float4 frag(V i):SV_Target{return float4(0,i.h,0,1);}
        ENDCG
    }}
}
