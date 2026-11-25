Shader "Custom/URPQuadWireframe"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (0, 1, 0, 1) // Default Green
        _LineWidth ("Line Width", Range(0.0, 0.5)) = 0.02
        _FillColor ("Fill Color", Color) = (0, 0, 0, 0) // Transparent center
    }
    SubShader
    {
        // URP Transparent setup
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual // Use 'Always' if you want X-Ray vision

        Pass
        {
            Name "QuadWireframe"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // This ensures the line draws on top of the object slightly
            Offset -1, -1

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _LineColor;
                float4 _FillColor;
                float _LineWidth;
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
                float2 uv = input.uv;
                
                // MATH: Check if we are near the edge of the UV square (0 to 1)
                // If UV is close to 0 OR close to 1, we are on an edge.
                
                // step(a, b) returns 1 if b >= a.
                // We create a "box" in the middle that is 0, and edges are 1.
                
                float left = step(_LineWidth, uv.x);
                float bottom = step(_LineWidth, uv.y);
                float right = step(uv.x, 1.0 - _LineWidth);
                float top = step(uv.y, 1.0 - _LineWidth);
                
                // 'center' will be 1.0 only if we are inside the borders
                float center = left * bottom * right * top;

                // Invert it: 1.0 for edges, 0.0 for center
                float isBorder = 1.0 - center;

                // Mix colors: If border, use LineColor. If center, use FillColor.
                float4 finalColor = lerp(_FillColor, _LineColor, isBorder);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}