Shader "PGS/UI/OverheadHealthBar"
{
    Properties
    {
        [Header(Vitals)]
        _HealthRatio ("Health Ratio", Range(0, 1)) = 1.0
        _GhostRatio ("Ghost Ratio", Range(0, 1)) = 1.0
        _FadeAlpha ("Fade Alpha", Range(0, 1)) = 1.0
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0.0

        [Header(Dimensions and Sizing)]
        _BarDimensions ("Bar Dimensions (Width, Height, BorderX, BorderY)", Vector) = (1.2, 0.14, 0.015, 0.015)
        _DistanceScaleParams ("Distance Scale Params (Enabled, RefDist, MinScale, MaxScale)", Vector) = (1.0, 10.0, 0.75, 2.0)

        [Header(Segmentation)]
        _MaxHealth ("Max Health", Float) = 100.0
        _SegmentSize ("Segment Size (HP per Pip, 0 = Smooth)", Float) = 20.0
        _SegmentGap ("Segment Gap (Normalized)", Range(0, 0.2)) = 0.02
        _Slant ("Slant Angle (Shear)", Range(-1.0, 1.0)) = 0.0
        _NotchWidth ("Notch Width", Float) = 0.01

        [Header(Colors)]
        _HealthColor ("Health Color", Color) = (0.2, 0.85, 0.3, 1.0)
        _GhostColor ("Ghost Color", Color) = (0.95, 0.6, 0.15, 1.0)
        _BackgroundColor ("Background Color", Color) = (0.12, 0.12, 0.12, 0.85)
        _BorderColor ("Border Color", Color) = (0.02, 0.02, 0.02, 1.0)

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+50" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "DisableBatching" = "False"
        }

        Pass
        {
            Name "OverheadHealthBarForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _HealthRatio)
                UNITY_DEFINE_INSTANCED_PROP(float, _GhostRatio)
                UNITY_DEFINE_INSTANCED_PROP(float, _FadeAlpha)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BarDimensions)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DistanceScaleParams)
                UNITY_DEFINE_INSTANCED_PROP(float, _MaxHealth)
                UNITY_DEFINE_INSTANCED_PROP(float, _SegmentSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SegmentGap)
                UNITY_DEFINE_INSTANCED_PROP(float, _Slant)
                UNITY_DEFINE_INSTANCED_PROP(float, _NotchWidth)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HealthColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GhostColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BackgroundColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BorderColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 barDim = UNITY_ACCESS_INSTANCED_PROP(Props, _BarDimensions);
                float4 distParams = UNITY_ACCESS_INSTANCED_PROP(Props, _DistanceScaleParams);

                // 1. Transform object anchor (0,0,0 local space) to world space
                float3 worldPivot = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

                // 2. Camera distance scaling (GPU-evaluated, keeping bar legible across the combat arena)
                float scaleMultiplier = 1.0;
                if (distParams.x > 0.5)
                {
                    float camDist = length(_WorldSpaceCameraPos.xyz - worldPivot);
                    scaleMultiplier = clamp(camDist / max(0.001, distParams.y), distParams.z, distParams.w);
                }

                // 3. Camera basis vectors in world space from the inverse view matrix
                // Transforming view-space Right (1,0,0) and Up (0,1,0) directly to world space
                float3 camRight = mul((float3x3)UNITY_MATRIX_I_V, float3(1.0, 0.0, 0.0));
                float3 camUp    = mul((float3x3)UNITY_MATRIX_I_V, float3(0.0, 1.0, 0.0));

                // 4. Expand quad vertices along camera right and up in world space (GPU Camera Billboarding - 0 CPU overhead)
                float2 quadOffset = input.positionOS.xy * (barDim.xy * scaleMultiplier);
                float3 worldPos = worldPivot + camRight * quadOffset.x + camUp * quadOffset.y;

                // 5. Project world position to clip space using standard URP pipeline (Reverse-Z & stereo safe)
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float fadeAlpha = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _FadeAlpha));
                if (fadeAlpha <= 0.001)
                {
                    discard;
                }

                float healthRatio = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _HealthRatio));
                float ghostRatio = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _GhostRatio));
                float flashIntensity = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _FlashIntensity));
                float4 barDim = UNITY_ACCESS_INSTANCED_PROP(Props, _BarDimensions);
                float maxHealth = UNITY_ACCESS_INSTANCED_PROP(Props, _MaxHealth);
                float segmentSize = UNITY_ACCESS_INSTANCED_PROP(Props, _SegmentSize);
                float segmentGap = UNITY_ACCESS_INSTANCED_PROP(Props, _SegmentGap);
                float slant = UNITY_ACCESS_INSTANCED_PROP(Props, _Slant);
                float notchWidth = UNITY_ACCESS_INSTANCED_PROP(Props, _NotchWidth);
                float4 healthColor = UNITY_ACCESS_INSTANCED_PROP(Props, _HealthColor);
                float4 ghostColor = UNITY_ACCESS_INSTANCED_PROP(Props, _GhostColor);
                float4 bgColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BackgroundColor);
                float4 borderColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BorderColor);

                float2 uv = input.uv;

                // 1. Compute border padding in normalized UV space
                float2 borderUV = barDim.zw / max(float2(0.001, 0.001), barDim.xy);

                // 2. Check outer border frame
                if (uv.x < borderUV.x || uv.x > (1.0 - borderUV.x) ||
                    uv.y < borderUV.y || uv.y > (1.0 - borderUV.y))
                {
                    borderColor.a *= fadeAlpha;
                    return borderColor;
                }

                // 3. Remap UV.x to playable bar interior [0, 1]
                float2 innerUV = saturate((uv - borderUV) / max(float2(0.001, 0.001), 1.0 - 2.0 * borderUV));
                float innerSlantedX = innerUV.x - (innerUV.y - 0.5) * slant;
                float fillU = saturate(innerSlantedX);

                // 4. Procedural segmentation, pips, and divider notches
                float isGap = 0.0;
                float isNotch = 0.0;

                if (segmentSize > 0.0 && maxHealth > 0.0)
                {
                    float totalSegments = max(1.0, round(maxHealth / segmentSize));
                    if (totalSegments > 1.0)
                    {
                        // In normalized bar interior [0, 1]:
                        float nearestDivider = round(fillU * totalSegments);
                        bool isDivider = (nearestDivider >= 1.0) && (nearestDivider <= (totalSegments - 1.0));

                        if (isDivider)
                        {
                            float dividerNormPos = nearestDivider / totalSegments;
                            float distToDivider = abs(fillU - dividerNormPos);

                            // Minimum screen pixel thickness: at least 1.5 screen pixels half-width
                            // to ensure dividers never alias away or vanish at any distance
                            float screenPixelHalfWidth = max(0.002, fwidth(fillU) * 0.85);

                            if (segmentGap > 0.0001)
                            {
                                float halfGap = max(segmentGap * 0.5, screenPixelHalfWidth);
                                if (distToDivider < halfGap)
                                {
                                    isGap = 1.0;
                                }
                            }

                            if (notchWidth > 0.0001)
                            {
                                float halfNotch = max(notchWidth * 0.5, screenPixelHalfWidth);
                                if (distToDivider < halfNotch)
                                {
                                    isNotch = 1.0;
                                }
                            }
                        }
                    }
                }

                if (segmentGap > 0.0001 && isGap > 0.5)
                {
                    discard;
                }

                // 5. Layer selection: Health Fill -> Damaged Section (Ghost / White Flash) -> Background
                float4 finalColor = bgColor;
                if (fillU <= ghostRatio)
                {
                    // Damaged health section flashes white on impact, then smoothly drains as ghost bar
                    finalColor = lerp(ghostColor, float4(1.0, 1.0, 1.0, 1.0), flashIntensity);
                }
                if (fillU <= healthRatio)
                {
                    // Active remaining health with impact sheen
                    finalColor = lerp(healthColor, float4(1.0, 1.0, 1.0, healthColor.a), flashIntensity * 0.7);
                }

                // 6. Subtle vertical specular gradient for rich physical depth across health fill
                float grad = lerp(0.88, 1.12, innerUV.y);
                finalColor.rgb *= grad;

                // 7. StarCraft 2 Style: Apply solid filled divider notches matching the outer border frame
                if (isNotch > 0.5)
                {
                    finalColor = borderColor;
                }

                // 8. Apply master alpha fade
                finalColor.a *= fadeAlpha;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
