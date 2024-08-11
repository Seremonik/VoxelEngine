Shader "Custom/QuadAtlasMultiSprite"
{
    Properties
    {
        _MainTex ("Texture Atlas", 2D) = "white" {}
        _AtlasSize ("Atlas Size", Vector) = (2, 2, 0, 0) // Number of sprites across and down in the atlas (e.g., 2x2)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _AtlasSize; // X = horizontal count, Y = vertical count

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // Pass through UV for now
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                int width = 5;
                int height = 2;
                int atlasSize = 16;
                float atlasFragSize = 1.0/16.0;
                int index1 = 17;
                int index2 = 54;
                int index3 = 90;
                int index4 = 242;
                float2 uv = i.uv;

                
                // Determine which part of the quad the fragment belongs to (0-1 range for each axis)
                float2 localUV = frac(uv * _AtlasSize.xy);

                // Determine which sprite to sample from based on original UV
                float2 spriteOffset = float2(0.0, 0.0);
                
                // Bottom-left sprite
                if (uv.x < 0.5 && uv.y < 0.5)
                {
                    spriteOffset = float2((atlasFragSize * index1)%atlasSize, (index1/atlasSize)*atlasFragSize);
                }
                // Bottom-right sprite
                else if (uv.x >= 0.5 && uv.y < 0.5)
                {
                    spriteOffset = float2((atlasFragSize * index2)%atlasSize, (index2/atlasSize)*atlasFragSize);
                }
                // Top-left sprite
                else if (uv.x < 0.5 && uv.y >= 0.5)
                {
                    spriteOffset = float2((atlasFragSize * index3)%atlasSize, (index3/atlasSize)*atlasFragSize);
                }
                // Top-right sprite
                else if (uv.x >= 0.5 && uv.y >= 0.5)
                {
                    spriteOffset = float2((atlasFragSize * index4)%atlasSize, (index4/atlasSize)*atlasFragSize);
                }

                // Calculate the final UV by adding the sprite offset
                float2 finalUV = spriteOffset + frac(uv * 2.0) * atlasFragSize;
                
                // Sample the atlas texture
                fixed4 col = tex2D(_MainTex, finalUV);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}