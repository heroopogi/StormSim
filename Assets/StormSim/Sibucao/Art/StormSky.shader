Shader "StormSim/Sibucao Overcast Sky"
{
    Properties { _Tint("Cloud tint",Color)=(0.25,0.29,0.34,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; float3 direction:TEXCOORD0; };
            float4 _Tint;
            v2f vert(float4 vertex:POSITION){v2f o;o.pos=UnityObjectToClipPos(vertex);o.direction=vertex.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);float2 p=d.xz/(max(d.y,0)+.3)*2+float2(_Time.y*.009,_Time.y*.004);
                float cloud=noise(p)*.5+noise(p*2.03)*.25+noise(p*4.1)*.125+noise(p*8.3)*.0625;
                float horizon=pow(1-saturate(d.y),3);
                return float4(lerp(_Tint.rgb*(.42+cloud*.8),float3(.31,.35,.39),horizon*.8),1);
            }
            ENDCG
        }
    }
}
