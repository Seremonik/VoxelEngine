Shader "Custom/DebugVoxelTextureAtlas"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AtlasSize ("Atlas Size", int) = 16
        _SpriteSize ("Sprite Size", float) = 0.0625
        _ChunkSize ("Chunk Size", int) = 32
        _ChunkSizeSquared ("Chunk Size Squared", int) = 1024
        _Color ("Color", Color) = (1,1,1,1)
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma shader_feature _ SHADOWS_SCREEN
            #include <AutoLight.cginc>

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                int encodedData : TEXCOORD0;
            };

            struct v2f
            {
                float3 localPos : TEXCOORD2;
                float3 worldNormal : TEXCOORD1;
                float4 pos : SV_POSITION;
                SHADOW_COORDS(3)
            };

            StructuredBuffer<int> voxelBuffer;

            sampler2D _MainTex;
            float4 _Color;
            int _AtlasSize;
            float _SpriteSize;
            int _ChunkSize;
            int _ChunkSizeSquared;
            float _AmbientBoost;

            // Function to decode packed data
            void unpackVertexData(uint packedData, out float3 pos, out float faceIndex)
            {
                pos.x = float((packedData & 0x3F)); // x
                pos.y = float((packedData >> 6) & 0x3F); // y
                pos.z = float((packedData >> 12) & 0x3F); // z
                faceIndex = float((packedData >> 18) & 0x7); // faceIndex
            }

            float3 getNormal(float normalIndex)
            {
                if (normalIndex == 0) return float3(1, 0, 0);
                if (normalIndex == 1) return float3(-1, 0, 0);
                if (normalIndex == 2) return float3(0, 1, 0);
                if (normalIndex == 3) return float3(0, -1, 0);
                if (normalIndex == 4) return float3(0, 0, 1);
                return float3(0, 0, -1); // normalIndex == 5
            }

            int3 roundUp(float3 inputValue)
            {
                const float epsilon = 1 - 1e-3; // A small value to adjust precision

                const int3 result = frac(inputValue) >= epsilon ? ceil(inputValue) : floor(inputValue);
                return result;
            }

            int getVoxelId(int3 voxelPos, int face)
            {
                if (face == 0)
                {
                    voxelPos.x -= 1;
                }
                else if (face == 2)
                {
                    voxelPos.y -= 1;
                }
                else if (face == 4)
                {
                    voxelPos.z -= 1;
                }

                int voxel = voxelBuffer[voxelPos.x + voxelPos.y * _ChunkSize + (voxelPos.z / 4 * _ChunkSizeSquared)] >>
                    (voxelPos.z * (_ChunkSize/4)) % _ChunkSize;
                voxel &= 255;
                
                return voxel;
            }

            v2f vert(appdata v)
            {
                float3 pos;
                float faceIndex;
                unpackVertexData(v.encodedData, pos, faceIndex);

                const float4 newPos = float4(pos, 1);

                v2f o;
                o.pos = UnityObjectToClipPos(newPos);
                o.worldNormal = getNormal(faceIndex);
                o.localPos = pos;

                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv;
                const float epsilon = 1 - 1e-6;
                int voxelId = 0;
                const int3 voxelPos = roundUp(i.localPos) + 1;
                
                if (abs(i.worldNormal.x) > epsilon)
                {
                    uv = i.localPos.zy;
                    voxelId = getVoxelId(voxelPos, i.worldNormal.x > 0 ? 0 : 1);
                }
                else if (abs(i.worldNormal.y) > epsilon)
                {
                    uv = i.localPos.xz;
                    voxelId = getVoxelId(voxelPos, i.worldNormal.y > 0 ? 2 : 3);
                }
                else if (abs(i.worldNormal.z) > epsilon)
                {
                    uv = i.localPos.xy;
                    voxelId = getVoxelId(voxelPos, i.worldNormal.z > 0 ? 4 : 5);
                }

                const float2 spriteOffset = float2((voxelId % _AtlasSize) * _SpriteSize, (voxelId / _AtlasSize) * _SpriteSize);
                uv = spriteOffset + frac(uv) / _AtlasSize;

                fixed4 texColor = tex2D(_MainTex, uv) * _Color;
                //return fixed4(uv, 0, 1);
                
                // Apply lighting
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * texColor.rgb;
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float diff = max(0, dot(i.worldNormal, lightDir));
                fixed3 diffuse = diff * _LightColor0.rgb * texColor.rgb;

                // Calculate shadow attenuation
                float shadow = SHADOW_ATTENUATION(i);
                fixed3 finalColor = (ambient + diffuse) * shadow + _AmbientBoost * texColor.rgb;

                return fixed4(finalColor, texColor.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}