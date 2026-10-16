Shader "StormSim/Aftermath Overcast Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; };
            struct output { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            output vert(input v){output o;o.position=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
            half4 frag(output i):SV_Target
            {
                float3 d=normalize(i.direction);
                float cloud=sin(d.x*9+d.z*4)*sin(d.z*12-d.y*7)*.025;
                float3 c=lerp(float3(.65,.69,.71),float3(.38,.43,.49),saturate(d.y));
                return half4(c+cloud*saturate(d.y*3),1);
            }
            ENDHLSL
        }
    }
}
