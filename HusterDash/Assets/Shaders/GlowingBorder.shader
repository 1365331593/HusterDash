Shader "HusterDash/GlowingBorder"
{
    Properties
    {
        _Color ("发光颜色", Color) = (0.314, 0.604, 0.906, 1.0)  // #509AE7
        _BorderWidth ("边框宽度 (UV)", Range(0.005, 0.2)) = 0.02
        _PulseSpeed ("脉冲速度", Range(0.0, 5.0)) = 2.0
        _PulseAmount ("脉冲幅度", Range(0.0, 0.5)) = 0.3
        _EmissionStrength ("自发光强度", Range(0.0, 5.0)) = 1.5
        _EdgeSoftness ("边框边缘柔和度", Range(0.0, 0.5)) = 0.08
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
            Name "GlowingBorder_Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                half4  _Color;
                float  _BorderWidth;
                float  _PulseSpeed;
                half   _PulseAmount;
                half   _EmissionStrength;
                float  _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;
                float bw = _BorderWidth;
                float soft = _EdgeSoftness;

                // 计算到四条边框最近的距离
                float distLeft = u;
                float distRight = 1.0 - u;
                float distBottom = v;
                float distTop = 1.0 - v;
                float distToBorder = min(min(distLeft, distRight), min(distBottom, distTop));

                // 边框区域判定：距离小于边框宽度时，计算边框强度
                float borderAlpha = 1.0 - smoothstep(max(bw - soft, 0.0), bw + soft, distToBorder);

                // 内边缘（边框内侧过渡）：使用第二层 smoothstep 让内侧也柔和
                float innerFade = smoothstep(max(bw - soft * 2.0, 0.0), max(bw - soft, 0.001), distToBorder);

                // 边框 alpha
                borderAlpha *= innerFade;

                // 脉冲呼吸动画
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                // 最终颜色
                half4 finalColor = _Color;
                finalColor.a = saturate(borderAlpha * pulse);

                // 自发光效果：边框区域颜色增强
                finalColor.rgb *= _EmissionStrength;

                // 完全透明则丢弃片段
                if (finalColor.a < 0.001)
                    discard;

                return finalColor;
            }
            ENDHLSL
        }

        // 深度写入 Pass（用于深度排序和阴影接收）
        Pass
        {
            Name "GlowingBorder_DepthOnly"
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
