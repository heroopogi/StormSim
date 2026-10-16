Shader "StormSim/FloodTest/Tsunami Surge"
{
    Properties
    {
        _SurgeFront("Surge front Z",Float)=-65
        _SurgeWidth("Surge front width",Float)=12
        _SurgeHeight("Surge height",Float)=3.4
        _SurgeStrength("Surge strength",Float)=0
        _FlowTime("Simulation time",Float)=0
        _ShallowColor("Shallow sediment tint", Color) = (0.28,0.34,0.24,1)
        _DeepColor("Deep floodwater tint", Color) = (0.045,0.13,0.12,1)
        _Absorption("Water turbidity", Range(0.1,4)) = 0.85
        _WaveHeight("Visual wave height (metres)", Range(0,0.15)) = 0.045
        _WaveSpeed("Wave speed", Range(0,3)) = 0.8
        _RippleStrength("Ripple strength", Range(0,0.4)) = 0.12
        _Refraction("Refraction distortion", Range(0,0.03)) = 0.008
        _FoamWidth("Contact foam width", Range(0.01,0.8)) = 0.22
        _FoamStrength("Contact foam strength", Range(0,1)) = 0.45
        _Smoothness("Reflection smoothness", Range(0,1)) = 0.88
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "FloodWater"
            Tags { "LightMode"="UniversalForwardOnly" }
            // The opaque scene is sampled and attenuated explicitly in the fragment shader.
            Blend One Zero
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _SurgeFront, _SurgeWidth, _SurgeHeight, _SurgeStrength, _FlowTime;
                half4 _ShallowColor, _DeepColor;
                float _Absorption, _WaveHeight, _WaveSpeed, _RippleStrength;
                float _Refraction, _FoamWidth, _FoamStrength, _Smoothness;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half fog:TEXCOORD1; };
            float Hash(float2 p)
            {
                return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);
            }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                    lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Ripple(float2 p,float t)
            {
                float2 drift=float2(t*0.23,-t*0.16);
                float2 q=float2(p.x*0.82+p.y*0.57,-p.x*0.57+p.y*0.82);
                return Noise(p*2.8+drift)*0.6+Noise(q*5.9-drift*1.3)*0.28+Noise(p*11.7+drift*0.7)*0.12;
            }
            float Surge(float2 p)
            {
                float distance=_SurgeFront+sin(p.x*0.18)*1.4-p.y;
                float fill=smoothstep(0,_SurgeWidth,distance);
                float crestDistance=(distance-_SurgeWidth*0.65)/(_SurgeWidth*0.32);
                float crest=exp(-crestDistance*crestDistance);
                return _SurgeStrength*_SurgeHeight*(fill+0.24*crest);
            }
            void Waves(float2 p, out float height, out float2 slope)
            {
                float t = _Time.y * _WaveSpeed;
                float a = dot(p,float2(1.3,0.65)) - t*1.8;
                float b = dot(p,float2(-0.8,1.7)) - t*1.3;
                float c = dot(p,float2(2.4,1.1)) + t*2.1;
                height = _WaveHeight*(sin(a)*0.55+sin(b)*0.3+sin(c)*0.15);
                slope = _WaveHeight*(cos(a)*0.55*float2(1.3,0.65)+cos(b)*0.3*float2(-0.8,1.7)+cos(c)*0.15*float2(2.4,1.1));
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.positionOS);
                float h; float2 slope;
                Waves(o.world.xz,h,slope);
                // Displacement is cosmetic; RisingWater retains the mean flood/CA level.
                o.world.y+=h+Surge(o.world.xz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float EyeDepth(float raw)
            {
                if(unity_OrthoParams.w<0.5) return LinearEyeDepth(raw,_ZBufferParams);
                #if UNITY_REVERSED_Z
                    raw=1-raw;
                #endif
                return lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
            }
            half4 Frag(Varyings v):SV_Target
            {
                float t=_Time.y*_WaveSpeed;
                float2 p=v.world.xz;
                float2 flowP=p-float2(0,_FlowTime*2.8*_SurgeStrength);
                float h; float2 slope;
                Waves(p,h,slope);
                // Advected noise layers avoid a repeating sine-grid appearance.
                float ripple=Ripple(flowP,t);
                slope+=_RippleStrength*float2(Ripple(flowP+float2(0.06,0),t)-ripple,
                    Ripple(flowP+float2(0,0.06),t)-ripple)/0.06;
                float surge=Surge(p);
                slope+=float2(Surge(p+float2(0.1,0))-surge,Surge(p+float2(0,0.1))-surge)/0.1;
                float3 n=normalize(float3(-slope.x,1,-slope.y));
                float3 view=GetWorldSpaceNormalizeViewDir(v.world);
                float2 uv=GetNormalizedScreenSpaceUV(v.positionCS);
                float waterEye=-TransformWorldToView(v.world).z;
                float depth=max(0,EyeDepth(SampleSceneDepth(uv))-waterEye);
                float2 distortedUV=saturate(uv+n.xz*_Refraction*saturate(depth));
                // Avoid refracting foreground walls and the above-water capsule into the water.
                if(EyeDepth(SampleSceneDepth(distortedUV))<waterEye) distortedUV=uv;
                half3 behind=SampleSceneColor(distortedUV);
                float transmission=exp(-depth*_Absorption);
                Light sun=GetMainLight(TransformWorldToShadowCoord(v.world));
                half3 tint=lerp(_ShallowColor.rgb,_DeepColor.rgb,1-exp(-depth*0.45));
                half3 scatter=tint*(0.6+0.4*saturate(dot(n,sun.direction)))*lerp(0.6,1,sun.shadowAttenuation);
                half3 color=lerp(scatter,behind,transmission);
                float fresnel=0.025+0.975*pow(1-saturate(dot(n,view)),5);
                half3 reflected=GlossyEnvironmentReflection(reflect(-view,n),1-_Smoothness,1);
                reflected=max(reflected,half3(0.16,0.25,0.3));
                color=lerp(color,reflected,saturate(fresnel));
                float glint=pow(saturate(dot(n,normalize(sun.direction+view))),180)*0.45;
                color+=sun.color*glint*sun.shadowAttenuation;
                float breakup=Noise(flowP*12+float2(t*0.5,-t*0.3));
                float foam=(1-smoothstep(0,_FoamWidth,depth))*smoothstep(0.28,0.72,breakup)*_FoamStrength;
                float distance=_SurgeFront+sin(p.x*0.18)*1.4-p.y;
                float boreDistance=(distance-_SurgeWidth*0.65)/(_SurgeWidth*0.6);
                float bore=exp(-boreDistance*boreDistance);
                float wake=smoothstep(0,5,distance)*(1-smoothstep(15,55,distance));
                float churn=Noise(flowP*0.55+Noise(flowP*0.17)*2)*0.6+Noise(flowP*1.8)*0.4;
                float lace=1-smoothstep(0.07,0.2,abs(churn-0.5));
                float whitewater=saturate((bore*0.55+wake*0.22)*lace*_SurgeStrength);
                foam=max(foam,whitewater);
                color=lerp(color,half3(0.72,0.76,0.69),foam);
                return half4(MixFog(color,v.fog),1);
            }
            ENDHLSL
        }
    }
}
