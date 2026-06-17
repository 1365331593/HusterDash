Shader "HusterDash/PortalVortex"
{
    Properties
    {
        // 颜色渐变：内圈蓝 → 中圈淡紫 → 外圈白
        _InnerColor ("内圈颜色", Color) = (0.314, 0.604, 0.906, 1.0)   // #509AE7
        _MidColor ("中间颜色", Color) = (0.553, 0.553, 0.941, 1.0)     // #8D8DF0
        _OuterColor ("外圈颜色", Color) = (1.0, 1.0, 1.0, 1.0)         // #FFFFFF

        // 漩涡参数
        _VortexSpeed ("漩涡旋转速度", Range(0.0, 5.0)) = 1.5
        _DistortStrength ("扭曲强度", Range(0.0, 0.5)) = 0.25

        // 光环形状
        _RingRadius ("光环主半径", Range(0.0, 1.0)) = 0.38
        _RingThickness ("光环厚度", Range(0.01, 0.5)) = 0.12
        _EdgeSoftness ("边缘柔和度", Range(0.01, 0.5)) = 0.15

        // 中心微光
        _CenterGlow ("中心微光强度", Range(0.0, 0.3)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PortalVortex_Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv          : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _InnerColor;
                half4  _MidColor;
                half4  _OuterColor;
                float  _VortexSpeed;
                float  _DistortStrength;
                float  _RingRadius;
                float  _RingThickness;
                float  _EdgeSoftness;
                float  _CenterGlow;
            CBUFFER_END

            // ---------- 噪声函数 ----------

            // 2D 哈希函数，生成伪随机值
            float2 hash2D(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 2D 梯度噪声（简化版 Perlin 噪声）
            float gradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                // 平滑插值权重
                float2 u = f * f * (3.0 - 2.0 * f);

                float n00 = dot(hash2D(i + float2(0.0, 0.0)), f - float2(0.0, 0.0));
                float n10 = dot(hash2D(i + float2(1.0, 0.0)), f - float2(1.0, 0.0));
                float n01 = dot(hash2D(i + float2(0.0, 1.0)), f - float2(0.0, 1.0));
                float n11 = dot(hash2D(i + float2(1.0, 1.0)), f - float2(1.0, 1.0));

                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
            }

            // 分形布朗运动——多层噪声叠加产生漩涡质感
            float fbm(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float persistence = 0.55;

                for (int i = 0; i < 5; i++)
                {
                    if (i >= octaves) break;
                    value += amplitude * gradientNoise(p * frequency);
                    frequency *= 2.1;
                    amplitude *= persistence;
                }
                return value;
            }

            // ---------- 顶点着色器（Billboard 面朝相机）----------

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 获取物体世界空间中心位置
                float3 worldCenter = float3(
                    unity_ObjectToWorld._m03,
                    unity_ObjectToWorld._m13,
                    unity_ObjectToWorld._m23
                );

                // 将世界中心转到观察空间
                float3 viewCenter = mul(UNITY_MATRIX_V, float4(worldCenter, 1.0)).xyz;

                // 在观察空间的 XY 平面上偏移顶点，实现 Billboard 效果
                // input.positionOS.xy 是 Quad 在物体空间的偏移（±0.5 范围）
                float3 viewPos = viewCenter;
                viewPos.x += input.positionOS.x;
                viewPos.y += input.positionOS.y;

                // 转到裁剪空间
                output.positionCS = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));

                // UV 保持不变
                output.uv = input.uv;
                return output;
            }

            // ---------- 片段着色器（程序化漩涡 + 光环遮罩）----------

            half4 frag(Varyings input) : SV_Target
            {
                // 将 UV 居中到 [-0.5, 0.5]
                float2 centeredUV = input.uv - 0.5;

                // 转换为极坐标
                float radius = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x);

                // 归一化半径：中心为 0，圆边缘为 1（Quad 内切圆半径 = 0.5）
                float radiusNorm = radius * 2.0;

                // 超出圆范围的片段直接丢弃（传送门是圆形）
                if (radiusNorm > 1.05)
                    discard;

                // —— 漩涡旋转 ——
                float rotatedAngle = angle + _Time.y * _VortexSpeed;

                // —— 多层噪声扭曲 ——
                // 低频扭曲：沿环面采样，产生大漩涡
                float2 noiseUV1 = float2(
                    rotatedAngle / (2.0 * 3.14159),
                    radiusNorm * 2.5
                );
                float distort1 = fbm(noiseUV1, 4) * _DistortStrength;

                // 高频细节扭曲
                float2 noiseUV2 = float2(
                    rotatedAngle / (2.0 * 3.14159) + _Time.y * 0.15,
                    radiusNorm * 4.0 + 1.7
                );
                float distort2 = fbm(noiseUV2, 3) * _DistortStrength * 0.4;

                // 角度方向的漩涡扰动
                float swirlNoise = fbm(
                    float2(radiusNorm * 3.5 + _Time.y * 0.2, rotatedAngle / 3.14159 + 0.8),
                    3
                ) * _DistortStrength * 0.35;

                // 叠加所有扭曲
                float distortedRadius = radiusNorm + distort1 + distort2 + swirlNoise;

                // —— 光环遮罩（模糊环，非硬边）——
                float ringCenter = _RingRadius;
                float ringHalf = _RingThickness * 0.5;
                float innerEdge = ringCenter - ringHalf;
                float outerEdge = ringCenter + ringHalf;

                // 内边缘柔和过渡（内圈 → 环带）
                float innerFade = smoothstep(
                    innerEdge - _EdgeSoftness,
                    innerEdge + _EdgeSoftness * 0.3,
                    distortedRadius
                );

                // 外边缘柔和过渡（环带 → 透明）
                float outerFade = 1.0 - smoothstep(
                    outerEdge - _EdgeSoftness * 0.3,
                    outerEdge + _EdgeSoftness,
                    distortedRadius
                );

                float ringAlpha = innerFade * outerFade;

                // —— 中心微光（让传送门中心不完全透明）——
                float centerGlow = exp(-radiusNorm * 3.5) * _CenterGlow;

                // 合成最终 alpha
                float finalAlpha = saturate(ringAlpha + centerGlow);

                // —— 颜色渐变：内蓝 → 中淡紫 → 外白 ——
                // 在光环带内插值，映射扭曲半径到 [0,1] 范围
                float colorT = saturate((distortedRadius - innerEdge) / max(_RingThickness, 0.001));

                half3 vortexColor;
                if (colorT < 0.5)
                {
                    // 内半段：蓝 → 淡紫
                    vortexColor = lerp(_InnerColor.rgb, _MidColor.rgb, colorT * 2.0);
                }
                else
                {
                    // 外半段：淡紫 → 白
                    vortexColor = lerp(_MidColor.rgb, _OuterColor.rgb, (colorT - 0.5) * 2.0);
                }

                // 中心微光使用内圈颜色
                half3 finalColor = lerp(_InnerColor.rgb, vortexColor, saturate(ringAlpha * 10.0));

                // 外边缘额外提亮（白色辉光）
                float edgeGlow = smoothstep(outerEdge - _EdgeSoftness * 0.5, outerEdge, distortedRadius);
                finalColor = lerp(finalColor, _OuterColor.rgb, edgeGlow * 0.3);

                return half4(finalColor, finalAlpha * _InnerColor.a);
            }
            ENDHLSL
        }

        // 深度写入 Pass（用于深度排序，不影响颜色）
        Pass
        {
            Name "PortalVortex_DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
