Shader "StormSim/Banago Ground Surface"
{
    Properties
    {
        [NoScaleOffset] _SurfaceMap("Surface weights (grass, soil, sand, concrete)", 2D) = "white" {}
        [Toggle] _UseSurfaceMap("Use mapped surface areas", Float) = 1
        _SurfaceWeights("Uniform surface weights", Vector) = (0,1,0,0)
        _GrassColor("Grass", Color) = (.10,.23,.045,1)
        _SoilColor("Soil", Color) = (.25,.15,.075,1)
        _SandColor("Sand", Color) = (.58,.46,.28,1)
        _ConcreteColor("Concrete", Color) = (.30,.31,.28,1)
        _DetailStrength("Surface variation", Range(0,1)) = .32
        _Smoothness("Smoothness", Range(0,1)) = .12
        [HideInInspector] _BaseMap("Base map", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "GroundForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _GrassColor, _SoilColor, _SandColor, _ConcreteColor;
                half4 _SurfaceWeights;
                half _UseSurfaceMap, _DetailStrength, _Smoothness, _Cutoff, _Surface, _Cull;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float2 uv:TEXCOORD2;
                float2 surfacePosition:TEXCOORD3;
                half fog:TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.uv=v.uv;
                // Object space keeps the pattern attached when the georeference moves.
                o.surfacePosition=v.positionOS.xz;
                o.fog=ComputeFogFactor(p.positionCS.z);
                return o;
            }
            float Hash(float2 p)
            {
                float3 q=frac(float3(p.xyx)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                            lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings v):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                half4 weights=lerp(_SurfaceWeights,SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,v.uv),_UseSurfaceMap);
                weights=max(weights,0); weights/=max(dot(weights,half4(1,1,1,1)),.0001);
                float2 p=v.surfacePosition;
                float patch=Noise(p*.12);
                float grain=Noise(p*3.2+17);
                float footprint=max(length(ddx(p)),length(ddy(p)));
                float closeDetail=1-smoothstep(.15,.9,footprint);
                float grassFibers=(Noise(p*float2(18,3.5))-.5)*closeDetail;
                float variation=(patch-.5)*.32+(grain-.5)*.18*closeDetail;
                half3 grass=_GrassColor.rgb*(1+_DetailStrength*(variation+grassFibers*.30));
                half3 soil=_SoilColor.rgb*(1+_DetailStrength*variation*1.4);
                half3 sand=_SandColor.rgb*(1+_DetailStrength*variation*.65);
                half3 concrete=_ConcreteColor.rgb*(1+_DetailStrength*variation*.35);
                SurfaceData s=(SurfaceData)0;
                s.albedo=grass*weights.r+soil*weights.g+sand*weights.b+concrete*weights.a;
                s.smoothness=_Smoothness; s.normalTS=half3(0,0,1); s.occlusion=1; s.alpha=1;
                InputData d=(InputData)0;
                d.positionWS=v.positionWS; d.normalWS=normalize(v.normalWS);
                d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);
                d.shadowCoord=TransformWorldToShadowCoord(v.positionWS);
                d.bakedGI=SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
                d.shadowMask=half4(1,1,1,1);
                d.vertexLighting=VertexLighting(v.positionWS,d.normalWS);
                half4 color=UniversalFragmentPBR(d,s);
                color.rgb=MixFog(color.rgb,v.fog);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
