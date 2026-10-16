Shader "Crest/Inputs/Clip Surface/STORMSIM wet cells"
{
    SubShader { Tags {"Queue"="Geometry"} Pass {
        Blend Off ZWrite Off Cull Off ColorMask R
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct V {float4 p:SV_POSITION;float depth:TEXCOORD0;};
        V vert(float4 p:POSITION,float2 uv:TEXCOORD0){V o;o.p=UnityObjectToClipPos(p);o.depth=uv.x;return o;}
        float frag(V i):SV_Target{clip(i.depth-.018);return 0;}
        ENDCG
    }}
}
