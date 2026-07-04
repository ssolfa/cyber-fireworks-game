Shader "Custom/SynthwaveSea"
{
    Properties
    {
        _SeaColorNear ("Sea Color Near", Color) = (0.227, 0.067, 0.439, 1.0)  // #3a1170
        _SeaColorMid ("Sea Color Mid", Color) = (0.078, 0.102, 0.361, 1.0)   // #141a5c
        _SeaColorFar ("Sea Color Far", Color) = (0.020, 0.039, 0.180, 1.0)   // #050a2e
        
        _GridColor ("Grid Color", Color) = (0.1, 0.0, 0.3, 1.0)
        _GridDensityX ("Grid Density X", Float) = 15.0
        _GridDensityY ("Grid Density Y", Float) = 20.0
        _ScrollSpeed ("Grid Scroll Speed", Float) = 5.0
        _GridLineWidth ("Grid Line Width", Float) = 0.05
        
        _WaveColor ("Wave Color", Color) = (0.0, 0.941, 1.0, 1.0) // Light blue emissive #00f0ff
        _WaveThickness ("Wave Thickness", Float) = 40.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            float4 _SeaColorNear;
            float4 _SeaColorMid;
            float4 _SeaColorFar;
            float4 _GridColor;
            float _GridDensityX;
            float _GridDensityY;
            float _ScrollSpeed;
            float _GridLineWidth;
            float4 _WaveColor;
            float _WaveThickness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                
                // 1. Sea Base Gradient (Vertical)
                float4 seaBase;
                if (uv.y < 0.5)
                {
                    seaBase = lerp(_SeaColorNear, _SeaColorMid, uv.y * 2.0);
                }
                else
                {
                    seaBase = lerp(_SeaColorMid, _SeaColorFar, (uv.y - 0.5) * 2.0);
                }
                
                // 2. Scrolling Perspective Grid
                // Prevent divide-by-zero, horizon is at top (y = 1.0)
                float yPersp = 1.0 / (1.02 - uv.y);
                
                // Horizontal lines scrolling down (towards viewer)
                float horizVal = sin(yPersp * _GridDensityY - _Time.y * _ScrollSpeed);
                float gridH = smoothstep(1.0 - _GridLineWidth, 1.0, 1.0 - abs(horizVal));
                
                // Vertical lines converging to horizon (center at x = 0.5)
                float xPersp = (uv.x - 0.5) * yPersp;
                float vertVal = sin(xPersp * _GridDensityX);
                float gridV = smoothstep(1.0 - _GridLineWidth * 1.5, 1.0, 1.0 - abs(vertVal));
                
                // Combine and fade out grid near the horizon to avoid aliasing and moire
                float gridIntensity = max(gridH, gridV) * saturate(1.2 - uv.y);
                float4 finalColor = lerp(seaBase, _GridColor * 2.5, gridIntensity);
                
                // 3. Layered animated sine waves
                float waveSum = 0.0;
                
                // Wave 1 (Far, slower, smaller)
                float h1 = 0.7;
                float w1 = h1 + sin(uv.x * 12.0 + _Time.y * 1.2) * 0.015;
                float line1 = exp(-pow((uv.y - w1) * _WaveThickness * 1.5, 2.0));
                waveSum = max(waveSum, line1 * 0.5); // dimmer because far
                
                // Wave 2 (Mid-far)
                float h2 = 0.5;
                float w2 = h2 + sin(uv.x * 8.0 - _Time.y * 1.8) * 0.025;
                float line2 = exp(-pow((uv.y - w2) * _WaveThickness, 2.0));
                waveSum = max(waveSum, line2 * 0.8);
                
                // Wave 3 (Mid-near)
                float h3 = 0.35;
                float w3 = h3 + sin(uv.x * 5.0 + _Time.y * 2.5) * 0.035;
                float line3 = exp(-pow((uv.y - w3) * _WaveThickness * 0.8, 2.0));
                waveSum = max(waveSum, line3 * 1.0);
                
                // Wave 4 (Near, fast, larger)
                float h4 = 0.15;
                float w4 = h4 + sin(uv.x * 3.5 - _Time.y * 3.2) * 0.05;
                float line4 = exp(-pow((uv.y - w4) * _WaveThickness * 0.6, 2.0));
                waveSum = max(waveSum, line4 * 1.2); // brighter because near
                
                // Add wave lines with HDR glow
                finalColor.rgb += _WaveColor.rgb * waveSum * 2.5;
                finalColor.a = 1.0;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
