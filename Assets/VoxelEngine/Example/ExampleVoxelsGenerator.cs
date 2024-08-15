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
        public NativeArray<byte> Voxels => voxels;
        public NativeArray<ulong> BitMatrix => bitMatrix;
        private NativeArray<byte> voxels;
        private NativeArray<ulong> bitMatrix;

        public ExampleVoxelsGenerator(ushort xSize, ushort ySize, ushort zSize)
        {
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
            size = xSize * ySize * zSize;
            GenerateVoxels();
            CalculateBitMatrix();
        }

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
            voxels = new NativeArray<byte>(size, Allocator.Persistent);

            for (int i = 0; i < xSize; i++)
            {
                for (int j = 0; j < ySize; j++)
                {
                    for (int k = 0; k < zSize; k++)
                    {
                        voxels[i + (j * xSize) + (k * ySize * xSize)] = PerlinNoise3D(i * scale, j * scale, k * scale);
                    }
                }
            }
        }

        private void CalculateBitMatrix()
        {
            bitMatrix = new NativeArray<ulong>(xSize * ySize * 3, Allocator.Persistent);
            int chunkSize = xSize * xSize;
            for (int x = 0; x < xSize; x++)
            {
                for (int y = 0; y < ySize; y++)
                {
                    for (int z = 0; z < zSize; z++)
                    {
                        bool isSolid = voxels[x + (y * xSize) + (z * ySize * xSize)] != 0;
                        if (!isSolid)
                            continue;

                        bitMatrix[z + (y * xSize)] |= 1UL << x;
                        bitMatrix[x + (z * xSize) + chunkSize ] |= 1UL << y;
                        bitMatrix[x + (y * xSize) + chunkSize * 2] |= 1UL << z;
                    }
                }
            }
        }

        byte PerlinNoise3D(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float yz = Mathf.PerlinNoise(y, z);
            float xz = Mathf.PerlinNoise(x, z);

            float yx = Mathf.PerlinNoise(y*2, x);
            float zy = Mathf.PerlinNoise(z, y);
            float zx = Mathf.PerlinNoise(z, x);

            return ((xy + yz + xz + yx + zy + zx) / 6f) >= 0.5f ? (byte)1 : (byte)0;
        }
    }
}