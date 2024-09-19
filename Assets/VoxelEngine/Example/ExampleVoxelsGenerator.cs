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
        public ProfilerMarker PerformanceMarker;
        
        public void Execute()
        {
            PerformanceMarker.Begin();
            CalculateBitMatrix();
            PerformanceMarker.End();
        }

        private void CalculateBitMatrix()
        {
            for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
            {
                for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
                {
                    for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
                    {
                        bool isSolid =
                            Voxels[
                                x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) +
                                (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)] != 0;
                        if (!isSolid)
                            continue;

                        BitMatrix[z + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE)] |= 1UL << x; // Left-Right
                        BitMatrix[
                            x + (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE) +
                            VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] |= 1UL << y; // Top-Bottom
                        BitMatrix[
                            x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) +
                            VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] |= 1UL << z; // Front-Back
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct VoxelBufferGenerationJob : IJob
    {
        [ReadOnly]
        public NativeArray<byte> Voxels;
        public NativeArray<uint> VoxelBuffer;
        public ProfilerMarker PerformanceMarker;

        public void Execute()
        {
            PerformanceMarker.Begin();
            GetVoxelBuffer();
            PerformanceMarker.End();
        }

        private void GetVoxelBuffer()
        {
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
                            Voxels[
                                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                                (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)];
                        VoxelBuffer[bufferIndex] |= voxel << (3 - ((z - 1) % 4)) * 8;
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

        public ProfilerMarker VoxelGenerationMarker;

        public void Execute()
        {
            GenerateVoxels(Voxels, ChunkOffset, 0.06f, VoxelGenerationMarker);
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
                        // float perlinValue = PerlinNoise.Perlin3D(
                        //     (x - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.x) * scale,
                        //     (y - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.y) * scale,
                        //     (z - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.z) * scale);
                        float perlinValue = PerlinNoise.Perlin3D(
                            (x - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.x) * scale,
                            1,
                            (z - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.z) * scale);
                        perlinValue = perlinValue * 40 + 10;
                        
                        byte value = (y - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * chunkOffset.y) > perlinValue ? (byte)0 : (byte)1;
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
    }

    [CreateAssetMenu(fileName = "Voxel Generator", menuName = "ScriptableObjects/VoxelGenerator", order = 1)]
    public class ExampleVoxelsGenerator : ScriptableObject, IVoxelsGenerator
    {
        public JobHandle ScheduleBitMatrixRecalculation(ChunkData chunkData, JobHandle dependency)
        {
            chunkData.BitMatrix =
                new NativeArray<ulong>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 3, Allocator.TempJob);

            var bitMatrixGenerationJob = new BitMatrixGenerationJob()
            {
                PerformanceMarker = new ProfilerMarker("Bit Matrix Calculation"),
                BitMatrix = chunkData.BitMatrix,
                Voxels = chunkData.Voxels
            };
            var handle = bitMatrixGenerationJob.Schedule(dependency);

            return handle;
        }

        public JobHandle ScheduleVoxelBufferRecalculation(ChunkData chunkData, JobHandle dependency)
        {
            chunkData.VoxelBuffer = new NativeArray<uint>(
                (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) *
                (VoxelEngineConstants.CHUNK_VOXEL_SIZE / 4),
                Allocator.TempJob);

            var bitMatrixGenerationJob = new VoxelBufferGenerationJob()
            {
                PerformanceMarker = new ProfilerMarker("Voxel Buffer creation"),
                VoxelBuffer = chunkData.VoxelBuffer,
                Voxels = chunkData.Voxels
            };
            var handle = bitMatrixGenerationJob.Schedule(dependency);

            return handle;
        }

        public JobHandle ScheduleChunkGeneration(ChunkData chunkData)
        {
            chunkData.Voxels =
                new NativeArray<byte>(
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
            chunkData.Vertices = new NativeList<uint>(Allocator.Persistent);
            chunkData.Triangles = new NativeList<int>(Allocator.Persistent);

            var voxelGenerationJob = new VoxelGenerationJob()
            {
                VoxelGenerationMarker = new ProfilerMarker("Voxel Generation creation"),
                Voxels = chunkData.Voxels,
                ChunkOffset = chunkData.ChunkPosition,
            };

            var voxelGenerationHandle = voxelGenerationJob.Schedule();
            var voxelBufferGenerationHandle = ScheduleVoxelBufferRecalculation(chunkData, voxelGenerationHandle);
            var bitMatrixGenerationHandle = ScheduleBitMatrixRecalculation(chunkData, voxelGenerationHandle);

            return JobHandle.CombineDependencies(bitMatrixGenerationHandle, voxelBufferGenerationHandle);
        }
    }
}