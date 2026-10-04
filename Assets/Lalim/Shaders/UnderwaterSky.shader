// Underwater "sky" for a dome around the camera (UnderwaterSky.cs). Its horizon is the current fog
// colour, so fogged-out geometry melts into it at any distance; above it brightens toward the
// surface (a soft window of light and slow light rays), below it sinks into the depths. Every
// colour is derived from the fog, so a polluted (murky) or pitch-black fog takes the sky with it.
Shader "Lalim/Underwater Sky"
{
    Properties
    {
        _ZenithBoost ("Surface brightness", Range(1, 4)) = 2.0
        _SurfaceTint ("Surface tint", Color) = (0.55, 0.95, 1, 1)
        _DeepFactor ("Depths (share of horizon)", Range(0, 1)) = 0.3
        _WindowSize ("Surface light tightness", Range(1, 200)) = 16
        _WindowStrength ("Surface light", Range(0, 3)) = 0.8
        _RayStrength ("Light rays", Range(0, 1)) = 0.22
        _RaySpeed ("Ray drift", Range(0, 1)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Pass
        {
            Name "Sky"
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _ZenithBoost;
                half4 _SurfaceTint;
                half _DeepFactor;
                half _WindowSize;
                half _WindowStrength;
                half _RayStrength;
                half _RaySpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.positionWS - _WorldSpaceCameraPos);
                half3 horizon = unity_FogColor.rgb;
                half lum = max(max(horizon.r, horizon.g), horizon.b);
                float up = saturate(d.y);
                float down = saturate(-d.y);

                // Brighter toward the surface, darker toward the depths.
                half3 zenith = horizon * _ZenithBoost + _SurfaceTint.rgb * lum * 0.35;
                half3 col = lerp(horizon, zenith, pow(up, 0.6));
                col = lerp(col, horizon * _DeepFactor, pow(down, 0.7));

                // The bright window of the surface straight above.
                col += _SurfaceTint.rgb * lum * _WindowStrength * pow(up, _WindowSize);

                // Soft light rays slanting down from the surface, drifting slowly.
                float az = atan2(d.x, d.z);
                float t = _Time.y * _RaySpeed * 6.2831;
                float r = (sin(az * 23.0 + t) * 0.5 + 0.5) * (sin(az * 41.0 - t * 0.7 + 1.3) * 0.5 + 0.5);
                r = r * r * r;
                float band = smoothstep(0.05, 0.35, up) * (1.0 - smoothstep(0.55, 0.95, up));
                col += _SurfaceTint.rgb * lum * _RayStrength * r * band;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
