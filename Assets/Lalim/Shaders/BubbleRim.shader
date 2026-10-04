// Soap-bubble look for the Bubble Barrier: nearly clear in the middle, bright fresnel rim with a
// slight iridescent shift, and a gentle wobble. Back faces are culled so the bubble is only ever
// seen from outside (it never tints the whole screen). _Fade is driven per-renderer by
// BubbleBarrierAbility for the hit flicker and the last-3-seconds warning.
Shader "Lalim/BubbleRim"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.6, 0.92, 1, 0.05)
        _RimColor ("Rim Color", Color) = (0.75, 0.97, 1, 0.9)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _Iridescence ("Iridescence", Range(0, 1)) = 0.35
        _Wobble ("Wobble", Range(0, 0.1)) = 0.025
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BubbleForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _Iridescence;
                half _Wobble;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 p = input.positionOS.xyz;
                float wobble = sin(_Time.y * 2.2 + p.y * 7.0) * cos(_Time.y * 1.7 + p.x * 5.0) * _Wobble;
                p += input.normalOS * wobble;

                float3 positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewWS);
                float fresnel = pow(1.0 - saturate(dot(n, v)), _RimPower);

                half3 iridescent = 0.5 + 0.5 * cos(6.28318 * (fresnel * 1.2 + _Time.y * 0.08 + float3(0.0, 0.33, 0.67)));
                half3 rim = lerp(_RimColor.rgb, iridescent, _Iridescence);
                half3 color = lerp(_BaseColor.rgb, rim, fresnel);
                half alpha = saturate(_BaseColor.a + fresnel * _RimColor.a) * _Fade;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
