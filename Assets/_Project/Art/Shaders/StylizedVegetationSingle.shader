// URP replacement for the Holotna pack's "Shader Graphs/VegetationSingleColor" (which is
// HDRP-authored and renders magenta under URP). Reproduces the look: a single flat _Color
// shaded by the mesh's vertex-colour gradient (dark at the base of each grass blade, bright
// at the tip), lit by the main directional light + ambient. Double-sided so grass/leaf cards
// show from both sides. Property name (_Color) matches the original so material values carry
// over automatically when the shader is swapped.
Shader "RPGArena/StylizedVegetationSingle"
{
    Properties
    {
        _Color ("Color", Color) = (0.141, 0.427, 0.086, 1)
        _BaseShade ("Base Shade", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off // grass/leaf cards are single-sided geometry — show both faces
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float4 color : COLOR; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
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
                // Vertex colour acts as an ambient-occlusion / tip gradient (dark base -> light tip).
                half shade = lerp(_BaseShade, 1.0h, IN.color.r);
                half3 albedo = _Color.rgb * shade;
                // Flip normal for back faces so both sides of the card light sensibly.
                float3 n = normalize(IN.normalWS) * (IS_FRONT_VFACE(facing, 1.0, -1.0));
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(n, mainLight.direction)) * 0.7h + 0.3h; // soft wrap
                half3 ambient = SampleSH(n);
                half3 col = albedo * (mainLight.color * ndl + ambient);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
