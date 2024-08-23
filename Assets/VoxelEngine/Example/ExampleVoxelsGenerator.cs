using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VoxelEngine.Example
{
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
            CalculateBitMatrix(BitMatrix, Voxels, BitMatrixMarker);
        }

        public static void GenerateVoxels(NativeArray<byte> voxels, int3 chunkOffset, float scale, ProfilerMarker voxelGenerationMarker)
        {
            voxelGenerationMarker.Begin();

            for (int i = 0; i < VoxelEngineConstants.CHUNK_VOXEL_SIZE; i++)
            {
                for (int j = 0; j < VoxelEngineConstants.CHUNK_VOXEL_SIZE; j++)
                {
                    for (int k = 0; k < VoxelEngineConstants.CHUNK_VOXEL_SIZE; k++)
                    {
                        float perlinValue = PerlinNoise.Perlin3D(
                            (i + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1) * chunkOffset.x) * scale,
                            (j + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1) * chunkOffset.y) * scale,
                            (k + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1) * chunkOffset.z) * scale);
                        byte value = perlinValue >= 0.5 ? (byte)1 : (byte)0;
                        // if (i == 0 || j==0 || k == 0)
                        // {
                        //     value = 0;
                        // }
                        // if (i == VoxelEngineConstants.CHUNK_VOXEL_SIZE-1 || j==VoxelEngineConstants.CHUNK_VOXEL_SIZE-1 || k == VoxelEngineConstants.CHUNK_VOXEL_SIZE-1)
                        // {
                        //     value = 0;
                        // }
                        // if (value > 0)
                        // {
                        //     value = (byte)(j);
                        // }

                        voxels[
                            i + (j * VoxelEngineConstants.CHUNK_VOXEL_SIZE) +
                            (k * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)] = value;
                    }
                }
            }
            voxelGenerationMarker.End();
        }

        public static void GetVoxelBuffer(NativeArray<uint> voxelBuffer, NativeArray<byte> voxels,
            ProfilerMarker voxelBufferMarker)
        {
            voxelBufferMarker.Begin();
            for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
            {
                for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
                {
                    for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
                    {
                        int bufferIndex = x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                                          (z / 4 * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED);
                        uint voxel =
                            voxels[
                                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                                (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)];
                        voxelBuffer[bufferIndex] |= voxel << z * (VoxelEngineConstants.CHUNK_VOXEL_SIZE / 4) %
                            VoxelEngineConstants.CHUNK_VOXEL_SIZE;
                    }
                }
            }
            voxelBufferMarker.End();
        }

        private static void CalculateBitMatrix(NativeArray<ulong> bitMatrix, NativeArray<byte> voxels,
            ProfilerMarker bitMatrixMarker)
        {
            bitMatrixMarker.Begin();
            int chunkSize = VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED;
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

                        bitMatrix[z + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE)] |= 1UL << x;
                        bitMatrix[x + (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + chunkSize] |= 1UL << y;
                        bitMatrix[x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + chunkSize * 2] |= 1UL << z;
                    }
                }
            }
            bitMatrixMarker.End();
        }
    }

    [CreateAssetMenu(fileName = "Voxel Generator", menuName = "ScriptableObjects/VoxelGenerator", order = 1)]
    public class ExampleVoxelsGenerator : ScriptableObject, IVoxelsGenerator
    {
        //private float scale = 0.06f;

        public JobHandle ScheduleChunkGeneration(ChunkData chunkData)
        {
            chunkData.Voxels =
                new NativeArray<byte>(
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
            chunkData.VoxelBuffer =
                new NativeArray<uint>(
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * (VoxelEngineConstants.CHUNK_VOXEL_SIZE / 4),
                    Allocator.TempJob);
            chunkData.BitMatrix =
                new NativeArray<ulong>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 3, Allocator.TempJob);
            chunkData.Vertices = new NativeList<uint>(Allocator.Persistent);
            chunkData.Triangles = new NativeList<int>(Allocator.Persistent);

            var job = new VoxelGenerationJob()
            {
                BitMatrixMarker = new ProfilerMarker("Bit Matrix creation"),
                VoxelBufferMarker = new ProfilerMarker("Voxel Buffer creation"),
                VoxelGenerationMarker = new ProfilerMarker("Voxel Generation creation"),
                Voxels = chunkData.Voxels,
                VoxelBuffer = chunkData.VoxelBuffer,
                BitMatrix = chunkData.BitMatrix,
                ChunkOffset = chunkData.ChunkPosition,
            };
            var handle = job.Schedule();

            return handle;
        }
    }
}