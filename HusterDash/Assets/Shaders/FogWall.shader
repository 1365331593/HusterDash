Shader "HusterDash/FogWall"
{
    Properties
    {
        // 雾墙颜色
        _MainColor ("雾颜色", Color) = (0.85, 0.85, 0.85, 1.0)

        // 噪声纹理参数
        _NoiseScale ("噪声缩放", Range(0.1, 10.0)) = 3.0
        _Density ("雾密度", Range(0.5, 5.0)) = 1.5
        _ScrollSpeedX ("水平漂移速度", Range(0.0, 0.5)) = 0.05
        _ScrollSpeedY ("垂直漂移速度", Range(0.0, 0.5)) = 0.02

        // 渐变参数
        _TopFade ("顶部虚化高度比", Range(0.1, 1.0)) = 0.6
        _EdgeSoftness ("边缘柔和度", Range(0.0, 0.5)) = 0.15
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
            Name "FogWall_Forward"
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
                float3 normalOS    : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _MainColor;
                float  _NoiseScale;
                half   _Density;
                float  _ScrollSpeedX;
                float  _ScrollSpeedY;
                float  _TopFade;
                float  _EdgeSoftness;
            CBUFFER_END

            // ---------- 噪声函数 ----------

            // 2D 哈希函数，生成伪随机值
            float2 hash2D(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 2D 梯度噪声（类似 Perlin 噪声的简化版）
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

            // 分形布朗运动 ——多层噪声叠加产生"雾团"质感
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

            // ---------- 顶点着色器 ----------

            Varyings vert(Attributes input)
            {
                Varyings output;
                // 转换顶点到裁剪空间
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                // 世界空间法线，用于计算朝向摄像机的角度
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // 世界空间视线方向
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetCameraPositionWS() - positionWS;
                return output;
            }

            // ---------- 片段着色器 ----------

            half4 frag(Varyings input) : SV_Target
            {
                // 带漂移的 UV 坐标
                float2 scrolledUV = input.uv * _NoiseScale;
                scrolledUV.x += _Time.y * _ScrollSpeedX;
                scrolledUV.y += _Time.y * _ScrollSpeedY;

                // 生成多层噪声叠加
                float noise1 = fbm(scrolledUV, 3);
                float noise2 = fbm(scrolledUV * 1.7 + 3.0, 2) * 0.6;
                float noiseTotal = noise1 + noise2;

                // 噪声映射到 [0.3, 1.0]——低谷处仍有基础浓度
                float fogAlpha = saturate(noiseTotal * 1.5 + 0.35);

                // 乘以密度（密度=5.0 时整面接近不透明）
                fogAlpha = saturate(fogAlpha * _Density);

                // 顶部虚化：从底部密集到顶部渐透
                float topFade = 1.0 - saturate((input.uv.y - _TopFade) / max(1.0 - _TopFade, 0.001));
                topFade = smoothstep(0.0, 1.0, topFade);

                // 左右边缘柔和过渡
                float edgeFade = 1.0;
                float soft = _EdgeSoftness;
                if (input.uv.x < soft)
                    edgeFade = input.uv.x / max(soft, 0.001);
                else if (input.uv.x > 1.0 - soft)
                    edgeFade = (1.0 - input.uv.x) / max(soft, 0.001);
                edgeFade = smoothstep(0.0, 1.0, edgeFade);

                // 合成最终 alpha
                fogAlpha *= topFade;
                fogAlpha *= edgeFade;
                fogAlpha = saturate(fogAlpha);

                half4 finalColor = _MainColor;
                finalColor.a *= fogAlpha;

                return finalColor;
            }
            ENDHLSL
        }

        // 深度写入 Pass（用于深度排序和阴影接收，不影响颜色）
        Pass
        {
            Name "FogWall_DepthOnly"
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
