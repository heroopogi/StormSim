Shader "StormSim/Aftermath Flood Residue"
{
    Properties
    {
        _BaseColor("Residue tint", Color)=(.28,.22,.14,.65)
        _Smoothness("Wetness", Range(0,1))=.25
        _Mode("Silt 0 Wet film 1 Puddle 2 Wall 3 Litter 4", Float)=0
        _WaterLevel("Historic flood elevation", Float)=-.05
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "FloodResidue"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness,_Mode;
                float _WaterLevel;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; half4 c:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; half4 c:COLOR; half fog:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(v.p.xyz);
                o.p=p.positionCS; o.world=p.positionWS; o.n=TransformObjectToWorldNormal(v.n);
                o.c=v.c; o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float broad=Noise(v.world.xz*1.8),fine=Noise(v.world.xz*26);
                float alpha=v.c.a*_BaseColor.a;
                float variation=.82+.24*broad+.10*fine;
                if(_Mode<.5) alpha*=smoothstep(.12,.65,broad*.65+fine*.35);
                else if(_Mode<1.5) alpha*=.65+.35*broad;
                else if(_Mode<2.5) variation=.94+.06*broad;
                else if(_Mode<3.5)
                {
                    float edge=_WaterLevel-v.world.y+(Noise(v.world.xz*4)-.5)*.09;
                    float band=(1-smoothstep(.025,.12,abs(edge)))*.27;
                    alpha*=saturate(smoothstep(-.025,.08,edge)*(.38+.30*Noise(float2(v.world.x+v.world.z,v.world.y)*9))+band);
                }
                clip(alpha-.008);
                SurfaceData s=(SurfaceData)0;
                s.albedo=_BaseColor.rgb*v.c.rgb*variation; s.metallic=0; s.smoothness=_Smoothness;
                s.normalTS=half3(0,0,1); s.occlusion=1; s.alpha=1;
                InputData d=(InputData)0; d.positionWS=v.world; d.normalWS=normalize(v.n);
                d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.world); d.shadowCoord=TransformWorldToShadowCoord(v.world);
                d.bakedGI=SampleSH(d.normalWS); d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.p); d.shadowMask=half4(1,1,1,1);
                half4 col=UniversalFragmentPBR(d,s); col.rgb=MixFog(col.rgb,v.fog); col.a=alpha; return col;
            }
            ENDHLSL
        }
    }
}
