// Full-screen underwater depth fog (URP Full Screen Pass, before post-processing). Uses the camera
// depth texture to find how far each pixel is, and lets the water absorb light per colour channel:
// red fades first, then green, blue last — so distant reef loses its warm colours like real water —
// while the lost light is replaced by the water's own colour (the current fog colour, so polluted
// murk and the dark well carry through). Surfaces deeper below the surface sit in darker water.
// Driven by UnderwaterDepthFog.cs through global values; off (strength 0) in scenes without it.
Shader "Lalim/Underwater Depth Fog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "DepthFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _LalimDepthFog;        // x strength (0 = off), y start distance (m), z density, w darkening per metre of depth
            float4 _LalimDepthFogAbsorb;  // rgb absorption per metre, a water surface height (world y)
            float4 _LalimDepthFogColor;   // the water's colour (the current fog colour)

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_LalimDepthFog.x <= 0.0) return scene;

                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                if (raw <= 0.000001) return scene;          // the sky dome: already the water's colour
            #else
                if (raw >= 0.999999) return scene;
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
            #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float distanceIn = max(0.0, distance(world, _WorldSpaceCameraPos) - _LalimDepthFog.y);

                // How much of each colour survives the swim to the eye.
                float3 survives = exp(-_LalimDepthFogAbsorb.rgb * distanceIn * _LalimDepthFog.z);
                float below = max(0.0, _LalimDepthFogAbsorb.a - world.y);
                float3 water = _LalimDepthFogColor.rgb * exp(-below * _LalimDepthFog.w);
                float3 fogged = scene.rgb * survives + water * (1.0 - survives);
                return half4(lerp(scene.rgb, fogged, saturate(_LalimDepthFog.x)), scene.a);
            }
            ENDHLSL
        }
    }
}
