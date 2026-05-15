using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

namespace VoxelEngine
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
            PerformanceMarker.End();
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
            PerformanceMarker.End();
        }
    }

    public abstract class VoxelsGeneratorBase : ScriptableObject, IVoxelsGenerator
    {
        private JobScheduler jobScheduler;

        public void Initialize(JobScheduler jobScheduler)
        {
            this.jobScheduler = jobScheduler;
        }

        public Task GenerateVoxels(ChunkData chunkData)
        {
            chunkData.Vertices.Clear();
            chunkData.Triangles.Clear();
            var jobHandle = ScheduleVoxelsGeneration(chunkData);
            var tcs = new TaskCompletionSource<bool>();
            jobScheduler.ScheduleJob(jobHandle, tcs);
            return tcs.Task;
        }

        public abstract JobHandle ScheduleVoxelsGeneration(ChunkData chunkData);

        public JobHandle ScheduleBitMatrixRecalculation(ChunkData chunkData, JobHandle dependency = default)
        {
            chunkData.BitMatrix =
                new NativeArray<ulong>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 3, Allocator.TempJob);

            return new BitMatrixGenerationJob()
            {
                PerformanceMarker = new ProfilerMarker("Bit Matrix Calculation"),
                BitMatrix = chunkData.BitMatrix,
                Voxels = chunkData.Voxels
            }.Schedule(dependency);
        }

        public JobHandle ScheduleVoxelBufferRecalculation(ChunkData chunkData, JobHandle dependency = default)
        {
            chunkData.VoxelBuffer = new NativeArray<uint>(
                (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) *
                (VoxelEngineConstants.CHUNK_VOXEL_SIZE / 4),
                Allocator.TempJob);

            return new VoxelBufferGenerationJob()
            {
                PerformanceMarker = new ProfilerMarker("Voxel Buffer creation"),
                VoxelBuffer = chunkData.VoxelBuffer,
                Voxels = chunkData.Voxels
            }.Schedule(dependency);
        }
    }
}
