Shader "Custom/SynthwaveSky"
{
    Properties
    {
        _SkyColorBottom ("Sky Color Bottom", Color) = (0.478, 0.122, 0.420, 1.0) // #7a1f6b
        _SkyColorMid ("Sky Color Mid", Color) = (0.141, 0.071, 0.349, 1.0)    // #241259
        _SkyColorTop ("Sky Color Top", Color) = (0.039, 0.024, 0.094, 1.0)    // #0a0618
        
        _SunColorTop ("Sun Color Top", Color) = (1.0, 0.824, 0.247, 1.0)     // #ffd23f
        _SunColorBottom ("Sun Color Bottom", Color) = (1.0, 0.184, 0.816, 1.0)  // #ff2fd0
        
        _SunRadius ("Sun Radius", Float) = 0.25
        _SunCenterY ("Sun Center Y", Float) = 0.2
        _SunEmission ("Sun Emission", Float) = 2.0
        
        _ScanlineFreq ("Scanline Frequency", Float) = 60.0
        _ScanlineSpeed ("Scanline Speed", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

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

            float4 _SkyColorBottom;
            float4 _SkyColorMid;
            float4 _SkyColorTop;
            float4 _SunColorTop;
            float4 _SunColorBottom;
            float _SunRadius;
            float _SunCenterY;
            float _SunEmission;
            float _ScanlineFreq;
            float _ScanlineSpeed;

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
                
                // 1. Sky Gradient (Vertical)
                float4 skyColor;
                if (uv.y < 0.5)
                {
                    skyColor = lerp(_SkyColorBottom, _SkyColorMid, uv.y * 2.0);
                }
                else
                {
                    skyColor = lerp(_SkyColorMid, _SkyColorTop, (uv.y - 0.5) * 2.0);
                }
                
                // 2. Draw Sun (Half-Circle above horizon at Y = 0.2)
                float2 sunCenter = float2(0.5, _SunCenterY);
                float distToSun = distance(uv, sunCenter);
                
                float4 finalColor = skyColor;
                
                if (distToSun < _SunRadius && uv.y >= _SunCenterY)
                {
                    // Sun Gradient (Yellow to Pink)
                    float sunT = (uv.y - _SunCenterY) / _SunRadius;
                    float4 sunColor = lerp(_SunColorBottom, _SunColorTop, sunT);
                    
                    // Sun cuts (Synthwave style horizontal bars getting smaller towards bottom)
                    // We can use a sine wave or stepped function for the cut bars
                    float barPattern = sin(uv.y * 80.0 - _Time.y * 1.5);
                    // Generate gap threshold that increases near the bottom of the sun
                    float gapThreshold = lerp(-0.7, 0.3, (uv.y - _SunCenterY) / _SunRadius);
                    
                    if (barPattern > gapThreshold)
                    {
                        finalColor = sunColor * _SunEmission;
                    }
                }
                
                // 3. Horizontal scanlines across everything
                float scanline = sin(uv.y * _ScanlineFreq - _Time.y * _ScanlineSpeed);
                float scanlineFactor = smoothstep(-0.6, 0.6, scanline) * 0.15 + 0.85;
                finalColor.rgb *= scanlineFactor;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
