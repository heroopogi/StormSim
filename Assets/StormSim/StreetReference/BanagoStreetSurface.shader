Shader "StormSim/Reference Dirt Mud and Paving"
{
 Properties
 {
  _MainTex("Packed dirt",2D)="white"{}
  _MudTex("Wet mud",2D)="white"{}
  [Normal] _NormalMap("Ruts and pothole relief",2D)="bump"{}
  _TileSize("Texture tile size in metres",Vector)=(4,8,0,0)
  _MudAmount("Wet mud amount",Range(0,1))=.4
  _NormalStrength("Rut relief",Range(0,2))=.85
  _PuddleAmount("Standing water",Range(0,1))=.52
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-105"}
  Pass
  {
   Tags{"LightMode"="UniversalForwardOnly"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   TEXTURE2D(_MudTex);SAMPLER(sampler_MudTex);
   TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _TileSize;half _MudAmount,_NormalStrength,_PuddleAmount;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 c:COLOR;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;half4 c:COLOR;half fog:TEXCOORD3;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.c=a.c;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   half4 Frag(V i):SV_Target
   {
    float2 uv=i.uv/max(_TileSize.xy,.1);float2 p=i.w.xz;
    float paved=saturate(i.c.g);
    float patch=noise(i.uv*float2(.62,.19)+17)*.65+noise(p*.79)*.35;
    float wet=smoothstep(.46,.79,patch*.62+i.c.r*.51+_MudAmount*.20)*(1-paved*.88);
    float puddle=smoothstep(.70,.82,patch*.70+i.c.r*.46)*_PuddleAmount*(1-paved*.92);
    half3 dirt=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
    half3 mud=SAMPLE_TEXTURE2D(_MudTex,sampler_MudTex,uv).rgb;
    half3 col=lerp(dirt,mud,wet*.86);
    float joint=frac(i.uv.y/4.8);
    float crack=(1-smoothstep(.003,.012,min(joint,1-joint)))*.55;
    float grain=noise(p*42);
    half3 pavement=lerp(half3(.34,.32,.27),half3(.41,.39,.34),noise(p*.18))*(.94+.09*grain);
    pavement*=1-crack*.13;
    col=lerp(col,pavement,paved);
    col=lerp(col,half3(.10,.095,.071),puddle*.48);
    half3 n=normalize(i.n);half3 relief=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uv),_NormalStrength*(1-puddle*.95)*(1-paved*.84));
    float3 px=ddx(i.w),py=ddy(i.w);float2 ux=ddx(i.uv),uy=ddy(i.uv);float det=ux.x*uy.y-ux.y*uy.x;
    float3 tangent=normalize((px*uy.y-py*ux.y)*sign(det)+float3(.000001,0,0));
    float3 bitangent=normalize((-px*uy.x+py*ux.x)*sign(det)+float3(0,0,.000001));
    n=normalize(tangent*relief.x+bitangent*relief.y+n*relief.z);
    SurfaceData s=(SurfaceData)0;s.albedo=col;s.metallic=0;s.smoothness=lerp(.065,.91,puddle);s.occlusion=1;s.alpha=1;s.normalTS=half3(0,0,1);
    InputData d=(InputData)0;d.positionWS=i.w;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=half4(1,1,1,1);
    half4 color=UniversalFragmentPBR(d,s);
    // A restrained sky tint keeps small puddles readable without a realtime planar reflection.
    float fresnel=pow(1-saturate(dot(n,d.viewDirectionWS)),4);
    color.rgb=lerp(color.rgb,half3(.25,.29,.30),puddle*(.09+.30*fresnel));
    color.rgb=MixFog(color.rgb,i.fog);color.a=saturate(i.c.a+(noise(p*2.7)-.5)*.12);return color;
   }
   ENDHLSL
  }
 }
}
