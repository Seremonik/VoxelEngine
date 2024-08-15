Shader "Custom/BasicLitShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Base Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Include URP's Lighting functions
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            // Declare the texture and color
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseColor;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;

                // Transform normals to world space
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));

                // Calculate view direction in world space
                float3 viewPosWS = GetCameraPositionWS();
                OUT.viewDirWS = normalize(viewPosWS - TransformObjectToWorld(IN.positionOS));

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Sample the base texture using the texture and sampler
                float4 baseColor = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Calculate lighting
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);
                float3 diffuseColor = baseColor.rgb;

                // Get main light data
                Light mainLight = GetMainLight();
                float3 lightDirWS = normalize(mainLight.direction);
                float NdotL = max(dot(normalWS, lightDirWS), 0.0);

                // Calculate final color with simple Lambertian reflection
                float3 diffuse = NdotL * mainLight.color * diffuseColor;
                float3 ambient = 0.1 * diffuseColor; // simple ambient term

                float3 color = diffuse + ambient;
                return float4(color, baseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}