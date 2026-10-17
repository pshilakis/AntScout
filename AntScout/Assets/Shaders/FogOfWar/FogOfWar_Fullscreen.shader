Shader "Hidden/AntScout/FogOfWar_Fullscreen"
{
    Properties
    {
        [HideInInspector] _FogOfWarTexture ("Fog Texture", 2D) = "black" {}
        [HideInInspector] _FogOfWarEnabled ("Fog Enabled", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "FogOfWarPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Parameters bound globally by FogOfWarManager or on pass material
            TEXTURE2D(_FogOfWarTexture);
            SAMPLER(sampler_FogOfWarTexture);

            // _FogOfWarBounds: (minX, minZ, sizeX, sizeZ)
            float4 _FogOfWarBounds;
            float4 _FogOfWarUnexploredColor;
            float4 _FogOfWarExploredColor;
            float _FogOfWarEnabled;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample the scene color from BlitTexture
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                // Bypass Fog of War completely when disabled (Edit Mode, Scene View, or uninitialized)
                if (_FogOfWarEnabled <= 0.5f)
                {
                    return sceneColor;
                }

                // Safeguard against uninitialized bounds in Edit mode / before manager ticks
                if (_FogOfWarBounds.z <= 0.001 || _FogOfWarBounds.w <= 0.001)
                {
                    return sceneColor;
                }

                // Sample depth directly at the pixel coordinate
                #if UNITY_REVERSED_Z
                    float depth = LoadSceneDepth((uint2)input.positionCS.xy);
                    bool isSky = (depth <= 0.00001);
                #else
                    float depth = LoadSceneDepth((uint2)input.positionCS.xy) * 2.0 - 1.0;
                    bool isSky = (depth >= 0.99999);
                #endif

                // If pixel is skybox / far clip, cloak with unexplored shroud
                if (isSky)
                {
                    return half4(_FogOfWarUnexploredColor.rgb, sceneColor.a);
                }

                // Reconstruct world space position (X, Y, Z) from screen UV and depth
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);

                // Map world XZ to FoW UV space [0, 1]
                float2 fowUV = (worldPos.xz - _FogOfWarBounds.xy) / _FogOfWarBounds.zw;

                // If outside map bounds, cloak with unexplored shroud
                if (fowUV.x < 0.0 || fowUV.x > 1.0 || fowUV.y < 0.0 || fowUV.y > 1.0)
                {
                    return half4(_FogOfWarUnexploredColor.rgb, sceneColor.a);
                }

                // Sample FoW texture (R = Explored [0..1], G = Visible [0..1])
                float2 fow = SAMPLE_TEXTURE2D(_FogOfWarTexture, sampler_LinearClamp, fowUV).rg;
                float explored = fow.r;
                float visible = fow.g;

                // Explored terrain tint (dimmed/desaturated shroud)
                half3 exploredShrouded = lerp(sceneColor.rgb, _FogOfWarExploredColor.rgb, _FogOfWarExploredColor.a);

                // Transition from explored shroud to crisp visible scene based on active sight (visible)
                half3 exploredOrVisible = lerp(exploredShrouded, sceneColor.rgb, visible);

                // Transition from pitch black unexplored shroud to explored/visible based on explored
                half3 finalRgb = lerp(_FogOfWarUnexploredColor.rgb, exploredOrVisible, explored);

                return half4(finalRgb, sceneColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
