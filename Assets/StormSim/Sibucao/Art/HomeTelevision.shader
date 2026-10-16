Shader "StormSim/Sibucao Home Television"
{
    Properties { _VideoTex("Video",2D)="black"{} _Power("Power",Float)=1 _HasVideo("Has video",Float)=0 _Glitch("Signal interference",Range(0,1))=0 _Clock("Sequence time",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            TEXTURE2D(_VideoTex);SAMPLER(sampler_VideoTex);
            CBUFFER_START(UnityPerMaterial)
            float _HasVideo,_Glitch,_Clock,_Power;
            CBUFFER_END
            V vert(A i){V o;o.pos=TransformObjectToHClip(i.vertex.xyz);o.uv=i.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            half4 frag(V i):SV_Target
            {
                float2 uv=i.uv;float tick=floor(_Clock*24);
                uv.x+=_Glitch*(hash(float2(floor(uv.y*24),tick))-.5)*.09;
                half3 c=lerp(half3(.025,.09,.15),SAMPLE_TEXTURE2D(_VideoTex,sampler_VideoTex,uv).rgb,_HasVideo);
                c*=.93+.07*sin(uv.y*720);
                float n=hash(float2(floor(uv.x*480)+tick,floor(uv.y*270)));
                c=lerp(c,half3(n,n,n),_Glitch*.65);
                c+=step(.975,frac(uv.y+_Clock*.31))*_Glitch*.3;
                return half4(c*_Power,1);
            }
            ENDHLSL
        }
    }
}
