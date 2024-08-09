Shader "Custom/DebugVoxelTextureAtlas"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TileSize ("Tile Size", Vector) = (0.25, 0.25, 0, 0)
        _Color ("Color", Color) = (1,1,1,1)
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.2 
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
            #pragma multi_compile_fwdbase
            #pragma shader_feature _ SHADOWS_SCREEN
            #include <AutoLight.cginc>

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                int voxelId : TEXCOORD1;
                int faceIndex : TEXCOORD2;
            };

            struct v2f
            {
                float3 worldPos : TEXCOORD2;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                SHADOW_COORDS(3)
            };


            sampler2D _MainTex;
            float4 _Color;
            float4 _TileSize;
            float _AmbientBoost; 

            //Calculate UVs based on faceIndex and position
            float2 CalculateUV(float4 vertex, float faceIndex)
            {
                // Get local position in the voxel
                float3 localPos = vertex.xyz;
            
                // Determine UV based on faceIndex
                if (faceIndex == 0 || faceIndex == 1) // +X or -X
                {
                    return float2(localPos.z % 2, localPos.y % 2);
                }
                if (faceIndex == 2 || faceIndex == 3) // +Y or -Y
                {
                    return float2(localPos.x % 2, localPos.z % 2);
                }
                // +Z or -Z
                return float2(localPos.x % 2, localPos.y % 2);
            }

            float2 CalculateAtlasOffset(float voxelId)
            {
                float2 tileOffset = float2(fmod(voxelId, 1.0 / _TileSize.x) * _TileSize.x, 
                                           floor(voxelId * _TileSize.x) * _TileSize.y);
                return tileOffset;
            }
            
            float3 GetNormal(float normalIndex)
            {
                if (normalIndex == 0) return float3(1, 0, 0);
                if (normalIndex == 1) return float3(-1, 0, 0);
                if (normalIndex == 2) return float3(0, 1, 0);
                if (normalIndex == 3) return float3(0, -1, 0);
                if (normalIndex == 4) return float3(0, 0, 1);
                return float3(0, 0, -1); // normalIndex == 5
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = GetNormal(v.faceIndex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                TRANSFER_SHADOW(o);
                // Calculate UV based on the tile index
                float tileIndex = 30;

                float2 uv = CalculateUV(v.vertex, v.faceIndex);
                float2 tileOffset = float2(fmod(tileIndex, 1.0 / _TileSize.x) * _TileSize.x, 
                                           floor(tileIndex * _TileSize.x) * _TileSize.y);
                o.uv = uv * _TileSize.xy + tileOffset;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv) * _Color;

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