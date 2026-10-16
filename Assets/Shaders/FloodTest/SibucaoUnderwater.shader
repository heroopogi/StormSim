Shader "StormSim/Sibucao Underwater Sediment"
{
    Properties { _Color("Sediment",Color)=(.075,.16,.14,1) _Density("Visibility loss per metre",Float)=.38 _Immersion("Head immersion",Range(0,1))=0 _HeadDepth("Depth",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay-10" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;float _Density,_Immersion,_HeadDepth;
            CBUFFER_END
            struct A {float3 p:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A a){V o;o.p=TransformObjectToHClip(a.p);o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float2 screen=GetNormalizedScreenSpaceUV(i.p);
                float distance=LinearEyeDepth(SampleSceneDepth(screen),_ZBufferParams);
                float extinction=1-exp(-max(0,distance-.15)*_Density);
                float darkness=exp(-_HeadDepth*.16);
                float vignette=smoothstep(.28,.75,length(i.uv-.5))*.12;
                half3 tint=_Color.rgb*lerp(.45,1,darkness)*(1-vignette);
                return half4(tint,saturate(max(.1,extinction)+vignette)*_Immersion);
            }
            ENDHLSL
        }
    }
}
