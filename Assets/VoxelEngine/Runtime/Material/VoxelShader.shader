Shader "Custom/DebugVoxelTextureAtlas"
{
    Properties
    {
        _TexArray ("Texture Array", 2DArray) = "white" {}
        _ChunkSize ("Chunk Size", int) = 32
        _ChunkSizeSquared ("Chunk Size Squared", int) = 1024
        _Color ("Color", Color) = (1,1,1,1)
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
            #include <AutoLight.cginc>
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_TexArray);
            
            struct appdata
            {
                int encodedData : TEXCOORD0;
            };

            struct v2f
            {
                float3 localPos : TEXCOORD2;
                float4 pos : SV_POSITION;
                int faceIndex : TEXCOORD1;
                float sunLight : TEXCOORD3;
                SHADOW_COORDS(3)
            };

            StructuredBuffer<int> voxelBuffer;
            
            float4 _Color;
            int _ChunkSize;
            int _ChunkSizeSquared;

            // Function to decode packed data
            void unpackVertexData(uint packedData, out float3 pos, out float faceIndex, out float sunLight)
            {
                pos.x = float((packedData & 0x3F)); // x
                pos.y = float((packedData >> 6) & 0x3F); // y
                pos.z = float((packedData >> 12) & 0x3F); // z
                faceIndex = float((packedData >> 18) & 0x7); // faceIndex
                sunLight = ((packedData >> 21) & 0xF);
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

                int voxel = voxelBuffer[voxelPos.x + voxelPos.y * _ChunkSize + ((voxelPos.z / 4) * _ChunkSizeSquared)]
                    >>
                    (3 - (voxelPos.z % 4)) * 8;
                voxel &= 255;

                return voxel;
            }

            v2f vert(appdata v)
            {
                float3 pos;
                float faceIndex;
                uint sunlight;
                unpackVertexData(v.encodedData, pos, faceIndex, sunlight);

                const float4 newPos = float4(pos, 1);

                v2f o;
                o.pos = UnityObjectToClipPos(newPos);
                o.localPos = pos;
                o.sunLight = sunlight;
                o.faceIndex = faceIndex;

                return o;
            }

            float lightValue(float normal)
            {
                switch (normal)
                {
                case 0: //right
                    return 0.8;
                case 1: //left
                    return 0.6;
                case 2: //top
                    return 1;
                case 3: // bottom
                    return 0.3;
                case 4: // back
                    return 0.4;
                }
                return 0.9; //front
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv;

                const int3 voxel_pos = roundUp(i.localPos);

                if (i.faceIndex == 0 || i.faceIndex == 1)
                {
                    uv = i.localPos.zy;
                }
                else if (i.faceIndex == 2 || i.faceIndex == 3)
                {
                    uv = i.localPos.xz;
                }
                else if (i.faceIndex == 3 || i.faceIndex == 4)
                {
                    uv = i.localPos.xy;
                }
                const int voxelId = getVoxelId(voxel_pos, i.faceIndex);
                
                fixed4 tex_color = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(uv,voxelId)) * _Color;

                float artificialLight = (1,1,1,1);
                float sunLight = pow(0.7, (15 - i.sunLight)) * 1;
                const fixed3 diffuse = lightValue(i.faceIndex) * tex_color.rgb * sunLight;

                return fixed4(diffuse, tex_color.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}