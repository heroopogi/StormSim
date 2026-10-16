Shader "StormSim/FloodTest/Depth Water"
{
    Properties
    {
        _RippleNormal("Ocean ripple normal map", 2D) = "bump" {}
        _NormalStrength("Ocean ripple strength", Range(0,.5)) = .14
        _RippleSpeed("Ocean ripple drift", Range(0,.04)) = .006
        _GlintStrength("Ocean highlights", Range(0,1)) = .18
        _FresnelStrength("Ocean grazing reflection", Range(0,1)) = .53
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
        _TerrainAware("Use flood depth and current vertex data", Float) = 0
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
            TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor;
                float _Absorption, _WaveHeight, _WaveSpeed, _RippleStrength;
                float _Refraction, _FoamWidth, _FoamStrength, _Smoothness;
                float _TerrainAware;
                float _NormalStrength, _RippleSpeed, _GlintStrength, _FresnelStrength;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 wet:TEXCOORD0; float2 flow:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half fog:TEXCOORD1; float depth:TEXCOORD2; float2 flow:TEXCOORD3; };
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
                o.depth=v.wet.x;o.flow=v.flow;
                o.world.y+=h*lerp(1,saturate(v.wet.x/.3),_TerrainAware);
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
            half4 Frag(Varyings v, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                if(_TerrainAware>.5)clip(v.depth-.018);
                float t=_Time.y*_WaveSpeed;
                float2 p=v.world.xz-v.flow*_Time.y*.6;
                float h; float2 slope;
                Waves(p,h,slope);
                // Advected noise layers avoid a repeating sine-grid appearance.
                float ripple=Ripple(p,t);
                float2 oceanUV=p/8;
                float2 oceanA=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,oceanUV+_Time.y*_RippleSpeed*float2(.8,.31)).rg*2-1;
                float2 oceanB=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,oceanUV*1.73+_Time.y*_RippleSpeed*float2(-.35,.55)).rg*2-1;
                slope-=(oceanA+oceanB*.55)*_NormalStrength;
                slope+=_RippleStrength*.35*float2(Ripple(p+float2(0.06,0),t)-ripple,
                    Ripple(p+float2(0,0.06),t)-ripple)/0.06;
                float3 n=normalize(float3(-slope.x,1,-slope.y));
                bool above=IS_FRONT_VFACE(face,true,false);
                if(!above)n=-n;
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
                float localDepth=lerp(depth,max(0,v.depth),_TerrainAware);
                half3 tint=lerp(_ShallowColor.rgb,_DeepColor.rgb,smoothstep(.08,1.5,localDepth));
                half3 scatter=tint*(0.6+0.4*saturate(dot(n,sun.direction)))*lerp(0.6,1,sun.shadowAttenuation);
                half3 color=lerp(scatter,behind,transmission);
                float fresnel=0.025+_FresnelStrength*pow(1-saturate(dot(n,view)),5);
                half3 reflected=half3(.40,.51,.57);
                color=lerp(color,reflected,saturate(fresnel));
                float glint=pow(saturate(dot(reflect(-sun.direction,n),view)),180)*_GlintStrength;
                color+=sun.color*glint*sun.shadowAttenuation;
                float breakup=Noise(p*12+float2(t*0.5,-t*0.3));
                float foam=(1-smoothstep(0,_FoamWidth,depth))*smoothstep(0.28,0.72,breakup)*_FoamStrength;
                // Sparse drifting streaks make simulated current direction visible on deeper water.
                float speed=length(v.flow);
                float2 direction=v.flow/max(.001,speed);
                float streak=Noise(float2(dot(p,float2(-direction.y,direction.x))*14,dot(p,direction)*1.8));
                foam+=smoothstep(.72,.9,streak)*saturate(speed/.7)*saturate(localDepth/.5)*.10;
                color=lerp(color,half3(0.72,0.76,0.69),foam);
                return half4(MixFog(color,v.fog),1);
            }
            ENDHLSL
        }
    }
}
