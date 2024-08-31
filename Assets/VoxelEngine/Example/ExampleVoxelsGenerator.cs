using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VoxelEngine.Example
{
    [BurstCompile]
    public struct BitMatrixGenerationJob : IJob
    {
        [ReadOnly]
        public NativeArray<byte> Voxels;
        public NativeArray<ulong> BitMatrix;
        
        public void Execute()
        {
            CalculateBitMatrix(BitMatrix, Voxels);
        }
        
        private static void CalculateBitMatrix(NativeArray<ulong> bitMatrix, NativeArray<byte> voxels)
        {
            for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
            {
                for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
                {
                    for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
                    {
                        bool isSolid =
                            voxels[
                                x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) +
                                (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)] != 0;
                        if (!isSolid)
                            continue;

                        bitMatrix[z + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE)] |= 1UL << x; // Left-Right
                        bitMatrix[x + (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] |= 1UL << y; // Top-Bottom
                        bitMatrix[x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] |= 1UL << z; // Front-Back
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct VoxelGenerationJob : IJob
    {
        public int3 ChunkOffset;
        public NativeArray<byte> Voxels;
        public NativeArray<uint> VoxelBuffer;
        public NativeArray<ulong> BitMatrix;

        public ProfilerMarker VoxelGenerationMarker;
        public ProfilerMarker VoxelBufferMarker;
        public ProfilerMarker BitMatrixMarker;

        public void Execute()
        {
            GenerateVoxels(Voxels, ChunkOffset, 0.06f, VoxelGenerationMarker);
            GetVoxelBuffer(VoxelBuffer, Voxels, VoxelBufferMarker);
            //CalculateBitMatrix(BitMatrix, Voxels, BitMatrixMarker);
        }

        public static void GenerateVoxels(NativeArray<byte> voxels, int3 chunkOffset, float scale,
            ProfilerMarker voxelGenerationMarker)
        {
            voxelGenerationMarker.Begin();

            for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
            {
                for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
                {
                    for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
                    {
                        float perlinValue = PerlinNoise.Perlin3D(
                            (x - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.x) * scale,
                            (y - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.y) * scale,
                            (z - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.z) * scale);
                        byte value = perlinValue >= 0.5 ? (byte)1: (byte)0;
                        // value = 1;
                        //
                        // if (x == 0 || y==0 || z == 0)
                        // {
                        //     value = 0;
                        // }
                        // if (x == VoxelEngineConstants.CHUNK_VOXEL_SIZE-1 || y==VoxelEngineConstants.CHUNK_VOXEL_SIZE-1 || z == VoxelEngineConstants.CHUNK_VOXEL_SIZE-1)
                        // {
                        //     value = 0;
                        // }
                        //
                        // if (value > 0 && z == 1)
                        // {
                        //     value = (byte)(15);
                        // }

                        voxels[
                            x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) +
                            (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)] = value;
                    }
                }
            }

            voxelGenerationMarker.End();
        }

        public static void GetVoxelBuffer(NativeArray<uint> voxelBuffer, NativeArray<byte> voxels,
            ProfilerMarker voxelBufferMarker)
        {
            voxelBufferMarker.Begin();
            for (int x = 1; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; x++)
            {
                for (int y = 1; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; y++)
                {
                    for (int z = 1; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; z++)
                    {
                        int bufferIndex = (x - 1) + (y - 1) * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) +
                                          (z - 1) / 4 * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) *
                                          (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2);
                        uint voxel =
                            voxels[
                                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                                (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)];
                        voxelBuffer[bufferIndex] |= voxel << (3 - ((z - 1) % 4)) * 8;
                    }
                }
            }

            voxelBufferMarker.End();
        }

        // private static void CalculateBitMatrix(NativeArray<ulong> bitMatrix, NativeArray<byte> voxels,
        //     ProfilerMarker bitMatrixMarker)
        // {
        //     bitMatrixMarker.Begin();
        //     
        //     for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
        //     {
        //         for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
        //         {
        //             for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
        //             {
        //                 bool isSolid =
        //                     voxels[
        //                         x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) +
        //                         (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)] != 0;
        //                 if (!isSolid)
        //                     continue;
        //
        //                 bitMatrix[z + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE)] |= 1UL << x; // Left-Right
        //                 bitMatrix[x + (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] |= 1UL << y; // Top-Bottom
        //                 bitMatrix[x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] |= 1UL << z; // Front-Back
        //             }
        //         }
        //     }
        //
        //     bitMatrixMarker.End();
        // }
    }

    [CreateAssetMenu(fileName = "Voxel Generator", menuName = "ScriptableObjects/VoxelGenerator", order = 1)]
    public class ExampleVoxelsGenerator : ScriptableObject, IVoxelsGenerator
    {
        //private float scale = 0.06f;

        public JobHandle ScheduleBitMatrixRecalculation(ChunkData chunkData)
        {
            chunkData.BitMatrix =
                new NativeArray<ulong>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 3, Allocator.TempJob);
            
            var bitMatrixGenerationJob = new BitMatrixGenerationJob()
            {
                BitMatrix = chunkData.BitMatrix,
                Voxels = chunkData.Voxels
            };
            var handle = bitMatrixGenerationJob.Schedule();

            return handle;
        }
        
        public JobHandle ScheduleChunkGeneration(ChunkData chunkData)
        {
            chunkData.Voxels =
                new NativeArray<byte>(
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
            chunkData.VoxelBuffer =
                new NativeArray<uint>(
                    (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) *
                    (VoxelEngineConstants.CHUNK_VOXEL_SIZE / 4),
                    Allocator.TempJob);
            chunkData.BitMatrix =
                new NativeArray<ulong>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 3, Allocator.TempJob);
            chunkData.Vertices = new NativeList<uint>(Allocator.Persistent);
            chunkData.Triangles = new NativeList<int>(Allocator.Persistent);

            var voxelGenerationJob = new VoxelGenerationJob()
            {
                BitMatrixMarker = new ProfilerMarker("Bit Matrix creation"),
                VoxelBufferMarker = new ProfilerMarker("Voxel Buffer creation"),
                VoxelGenerationMarker = new ProfilerMarker("Voxel Generation creation"),
                Voxels = chunkData.Voxels,
                VoxelBuffer = chunkData.VoxelBuffer,
                BitMatrix = chunkData.BitMatrix,
                ChunkOffset = chunkData.ChunkPosition,
            };
            var bitMatrixGenerationJob = new BitMatrixGenerationJob()
            {
                BitMatrix = chunkData.BitMatrix,
                Voxels = chunkData.Voxels
            };
            
            var handle = voxelGenerationJob.Schedule();
            var handle2 = bitMatrixGenerationJob.Schedule(handle);
            
            return handle2;
        }
    }
}