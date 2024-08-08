using System.Security.Cryptography;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Example
{
    public class ExampleVoxelsGenerator : IVoxelsGenerator
    {
        private ushort xSize, ySize, zSize;
        private int size;
        private float scale = 0.06f;

        public ExampleVoxelsGenerator(ushort xSize, ushort ySize, ushort zSize)
        {
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
            size = xSize * ySize * zSize;
            GenerateVoxels();
        }

        public NativeArray<ushort> Voxels => voxels;

        private NativeArray<ushort> voxels;
        public int GetVoxel(int3 position)
        {
            return voxels[position.x + position.y * xSize + position.z * xSize * ySize];
        }

        public int GetVoxel(int x, int y, int z)
        {
            return voxels[x + y * xSize + z * xSize * ySize];
        }

        private void GenerateVoxels()
        {
            voxels = new NativeArray<ushort>(size ,Allocator.Persistent);
            
            for (int i = 0; i < xSize; i++)
            {
                for (int j = 0; j < ySize; j++)
                {
                    for (int k = 0; k < zSize; k++)
                    {
                        voxels[i + (j * xSize) + (k * ySize * xSize)] = PerlinNoise3D(i * scale, j* scale, k* scale);
                    }
                }
            }
        }
        
        ushort PerlinNoise3D(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float yz = Mathf.PerlinNoise(y, z);
            float xz = Mathf.PerlinNoise(x, z);

            float yx = Mathf.PerlinNoise(y, x);
            float zy = Mathf.PerlinNoise(z, y);
            float zx = Mathf.PerlinNoise(z, x);

            return ((xy + yz + xz + yx + zy + zx) / 6f)>= 0.5f ? (ushort)1 : (ushort)0;
        }
    }
}