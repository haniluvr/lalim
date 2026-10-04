// Teleport arch portal: the looping video, unlit and double-sided, with saturation and brightness
// controls so a locked portal can be shown grey and dim (TeleportArchGate sets them per renderer).
Shader "Lalim/PortalVideo"
{
    Properties
    {
        [MainTexture] _BaseMap ("Video", 2D) = "white" {}
        [HDR][MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Saturation;
                half _Brightness;
                half _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half grey = dot(c, half3(0.299h, 0.587h, 0.114h));
                c = lerp(grey.xxx, c, _Saturation) * _BaseColor.rgb * _Brightness;
                c = MixFog(c, input.fogFactor);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
