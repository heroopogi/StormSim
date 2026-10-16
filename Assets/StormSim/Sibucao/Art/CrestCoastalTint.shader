Shader "Crest/Inputs/Albedo/STORMSIM coastal tint"
{
    Properties { _Color("Deep blue sea tint",Color)=(.018,.065,.12,.72) }
    SubShader { Pass {
        Blend Off ZWrite Off Cull Off
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        float4 _Color;
        float4 vert(float4 p:POSITION):SV_POSITION { return UnityObjectToClipPos(p); }
        float4 frag():SV_Target { return _Color; }
        ENDCG
    }}
}
