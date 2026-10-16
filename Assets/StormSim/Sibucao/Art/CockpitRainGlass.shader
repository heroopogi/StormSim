Shader "StormSim/CockpitRainGlass"
{
 Properties { _Wipers("Wipers",Float)=1 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _Wipers;
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target
 {
 float2 uv=i.uv;float2 cell=floor(uv*float2(75,45));float rand=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
 float2 jitter=float2(frac(rand*13.7),frac(rand*31.9))*.7-.35;
 float2 p=frac(uv*float2(75,45)+float2(0,_Time.y*(.12+rand*.3)))-.5-jitter;
 float drop=smoothstep(.13,.04,length(p*float2(1,1.9)))*step(.5,rand);
 float cleared=_Wipers*step(.12,uv.y)*step(uv.y,.85)*step(.08,uv.x)*step(uv.x,.92);
 return half4(.65,.77,.82,.018+drop*.38*(1-cleared*.95));
 }
 ENDHLSL
 }}
}

