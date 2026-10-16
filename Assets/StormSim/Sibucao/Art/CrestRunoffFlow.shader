Shader "Crest/Inputs/Flow/STORMSIM runoff current"
{
    SubShader { Pass {
        Blend Off ZWrite Off Cull Off
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct V {float4 p:SV_POSITION;float2 flow:TEXCOORD0;float depth:TEXCOORD1;};
        V vert(float4 p:POSITION,float2 uv:TEXCOORD0,float2 flow:TEXCOORD1){V o;o.p=UnityObjectToClipPos(p);o.flow=flow;o.depth=uv.x;return o;}
        float4 frag(V i):SV_Target{clip(i.depth-.018);return float4(i.flow,0,0);}
        ENDCG
    }}
}
