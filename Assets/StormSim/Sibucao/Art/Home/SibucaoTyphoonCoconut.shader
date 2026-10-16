Shader "StormSim/Sibucao Typhoon Coconut"
{
    Properties
    {
        [MainTexture] _BaseMap("Base map",2D)="white"{}
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        _Cull("Cull",Float)=0
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _Surface("Surface",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;float4 _BaseMap_ST;half _Cull,_Cutoff,_Surface;
            CBUFFER_END
            float _SibucaoHomeGust; struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;half4 c:COLOR;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i)
            {V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz); float leaf=step(.5,i.c.a); float sway=sin(_Time.y*2.1+p.positionWS.x*.3+p.positionWS.z*.17)*.18+sin(_Time.y*.71+p.positionWS.z*.1)*.32; p.positionWS.xz+=float2(1,.35)*sway*leaf*_SibucaoHomeGust; o.p=TransformWorldToHClip(p.positionWS);o.w=p.positionWS;o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.c=i.c;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                bool leaf=i.c.a>.5;half3 paint=i.c.rgb;float h=0;
                if(!leaf)
                {
                    float grain=noise(i.uv*float2(75,9));float y=i.uv.y/.195+noise(float2(i.uv.x*5,0))*.14;
                    float seam=1-smoothstep(.012,.03+fwidth(y),min(frac(y),1-frac(y)));
                    float fibers=noise(i.uv*float2(110,3));
                    paint*=.85+.19*grain+.10*fibers;paint*=1-seam*.25;
                    paint=lerp(paint,half3(.26,.29,.20),(.12*(1-smoothstep(0,.8,i.uv.y))));
                    h=.004*(grain-.5)+fibers*.0015-seam*.004;
                }
                else {float veins=noise(i.uv*float2(48,25));paint*=.95+.08*veins;h=.0007*(veins-.5);}
                #ifndef UNITY_COLORSPACE_GAMMA
                    paint=SRGBToLinear(saturate(paint));
                #endif
                SurfaceData s=(SurfaceData)0;s.albedo=paint*_BaseColor.rgb;s.smoothness=leaf?.20:.06;s.occlusion=1;s.alpha=1;s.normalTS=half3(0,0,1);
                InputData d=(InputData)0;d.positionWS=i.w;float3 n=normalize(i.n)*IS_FRONT_VFACE(face,1,-1);
                float3 px=ddx(i.w),py=ddy(i.w),r1=cross(py,n),r2=cross(n,px);float det=dot(px,r1);float3 grad=(ddx(h)*r1+ddy(h)*r2)*sign(det);
                n=normalize(abs(det)*n-clamp(grad,-.10,.10)+n*.000001);
                d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(n);
                d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=half4(1,1,1,1);d.vertexLighting=VertexLighting(i.w,n);
                half4 color=UniversalFragmentPBR(d,s);color.rgb=MixFog(color.rgb,i.fog);return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}

