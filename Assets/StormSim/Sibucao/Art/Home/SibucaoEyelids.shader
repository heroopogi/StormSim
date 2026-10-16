Shader "StormSim/Sibucao Eyelids"
{
 Properties { _Opening("Eye opening",Range(0,1))=1 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay+100" "RenderType"="Transparent"}
  Pass
  {
   ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   CBUFFER_START(UnityPerMaterial)
   float _Opening;
   CBUFFER_END
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
   half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float aperture=_Opening*1.7*(1-.3*p.x*p.x);float alpha=smoothstep(aperture-.04,aperture+.035,abs(p.y));alpha=lerp(alpha,1,1-smoothstep(0,.025,_Opening));return half4(0,0,0,alpha);}
   ENDHLSL
  }
 }
}
