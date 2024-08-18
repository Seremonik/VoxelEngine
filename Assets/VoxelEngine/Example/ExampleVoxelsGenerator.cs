using System;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Example
{
    public class ExampleVoxelsGenerator : IVoxelsGenerator
    {
        private ushort sideSize;
        private int volumeSize;
        private int squaredSize;
        private float scale = 0.06f;
        public NativeArray<byte> Voxels => voxels;
        public NativeArray<ulong> BitMatrix => bitMatrix;

        private NativeArray<byte> voxels;
        private NativeArray<ulong> bitMatrix;

        public ExampleVoxelsGenerator(ushort sideSize)
        {
            this.sideSize = sideSize;
            volumeSize = sideSize * sideSize * sideSize;
            squaredSize = sideSize * sideSize;
            GenerateVoxels();
            CalculateBitMatrix();
        }

        public int GetVoxel(int3 position)
        {
            return voxels[position.x + position.y * sideSize + position.z * squaredSize];
        }

        public int GetVoxel(int x, int y, int z)
        {
            return voxels[x + y * sideSize + z * squaredSize];
        }

        public NativeArray<uint> GetVoxelBuffer()
        {
            NativeArray<uint> voxelBuffer = new NativeArray<uint>(squaredSize * (sideSize/4), Allocator.Persistent);
            for (int x = 0; x < sideSize; x++)
            {
                for (int y = 0; y < sideSize; y++)
                {
                    for (int z = 0; z < sideSize; z++)
                    {
                        int bufferIndex = x + y * sideSize + (z / 4 * squaredSize);
                        uint voxel = voxels[x + y * sideSize + (z * squaredSize)];
                        voxelBuffer[bufferIndex] |= voxel << z*(sideSize/4)%sideSize;
                    }
                }
            }
            
            return voxelBuffer;
        }

        private void GenerateVoxels()
        {
            voxels = new NativeArray<byte>(volumeSize, Allocator.Persistent);

            for (int i = 0; i < sideSize; i++)
            {
                for (int j = 0; j < sideSize; j++)
                {
                    for (int k = 0; k < sideSize; k++)
                    {
                        byte value = PerlinNoise3D(i * scale, j * scale, k * scale);
                        if (i == 0 || j==0 || k == 0)
                        {
                            value = 0;
                        }
                        if (i == sideSize-1 || j==sideSize-1 || k == sideSize-1)
                        {
                            value = 0;
                        }
                        if (value > 0)
                        {
                            value = (byte)(j);
                        }
                        voxels[i + (j * sideSize) + (k * squaredSize)] = value;
                    }
                }
            }
        }

        private void CalculateBitMatrix()
        {
            bitMatrix = new NativeArray<ulong>(squaredSize * 3, Allocator.Persistent);
            int chunkSize = squaredSize;
            for (int x = 0; x < sideSize; x++)
            {
                for (int y = 0; y < sideSize; y++)
                {
                    for (int z = 0; z < sideSize; z++)
                    {
                        bool isSolid = voxels[x + (y * sideSize) + (z * squaredSize)] != 0;
                        if (!isSolid)
                            continue;

                        bitMatrix[z + (y * sideSize)] |= 1UL << x;
                        bitMatrix[x + (z * sideSize) + chunkSize] |= 1UL << y;
                        bitMatrix[x + (y * sideSize) + chunkSize * 2] |= 1UL << z;
                    }
                }
            }
        }

        byte PerlinNoise3D(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float yz = Mathf.PerlinNoise(y, z);
            float xz = Mathf.PerlinNoise(x, z);

            float yx = Mathf.PerlinNoise(y * 2, x);
            float zy = Mathf.PerlinNoise(z, y);
            float zx = Mathf.PerlinNoise(z, x);
            float value = ((xy + yz + xz + yx + zy + zx) / 6f);
            // if (value < 0.5f )
            // {
            //     return 0;
            // }
            
            return 1;
        }
    }
}