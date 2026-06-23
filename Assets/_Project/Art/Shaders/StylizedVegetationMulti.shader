// URP replacement for the Holotna pack's "Shader Graphs/VegetationMultiColor" / "PropMultiColor"
// (HDRP-authored -> magenta under URP). The mesh's vertex colours mask between up to three
// material colours: trees = green leaves (primary) + brown trunk (secondary); flowers = blue
// petals (primary) + green stem (secondary). Vertex.g selects primary vs secondary; vertex.b
// blends toward tertiary. Property names match the original so values carry over on swap.
Shader "RPGArena/StylizedVegetationMulti"
{
    Properties
    {
        _Primary_Color   ("Primary Color",   Color) = (0.141, 0.443, 0.239, 1)
        _Secondary_Color ("Secondary Color", Color) = (0.235, 0.220, 0.192, 1)
        _Tertiary_Color  ("Tertiary Color",  Color) = (0.0, 0.0, 0.0, 1)
        _BaseShade ("Base Shade", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off // leaf/petal cards are single-sided geometry
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float4 color : COLOR; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Primary_Color;
                float4 _Secondary_Color;
                float4 _Tertiary_Color;
                float _BaseShade;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                o.positionHCS = p.positionCS;
                o.normalWS = GetVertexNormalInputs(IN.normalOS).normalWS;
                o.color = IN.color;
                return o;
            }

            half4 frag (Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // vertex.g: 1 -> primary (leaves/petals), 0 -> secondary (trunk/stem).
                // vertex.b: blends toward tertiary accent. vertex.r: tip/AO shade.
                half3 baseCol = lerp(_Secondary_Color.rgb, _Primary_Color.rgb, IN.color.g);
                baseCol = lerp(baseCol, _Tertiary_Color.rgb, IN.color.b);
                half shade = lerp(_BaseShade, 1.0h, saturate(IN.color.r + 0.25h));
                half3 albedo = baseCol * shade;
                float3 n = normalize(IN.normalWS) * (IS_FRONT_VFACE(facing, 1.0, -1.0));
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(n, mainLight.direction)) * 0.7h + 0.3h;
                half3 ambient = SampleSH(n);
                half3 col = albedo * (mainLight.color * ndl + ambient);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
