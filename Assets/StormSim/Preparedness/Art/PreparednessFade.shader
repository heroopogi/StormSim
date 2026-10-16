Shader "TORMSIM/Preparation Fade"
{
    Properties { _Color("Fade",Color)=(0,0,0,0) }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO};
            half4 _Color;
            Varyings vert(Attributes input){Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output;}
            half4 frag(Varyings input):SV_Target {return _Color;}
            ENDHLSL
        }
    }
}
