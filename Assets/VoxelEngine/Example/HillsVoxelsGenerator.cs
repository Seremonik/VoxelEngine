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
        public NativeArray<bool> Result; //[0] - IsChunkEmpty //[1] IsChunkSolid
        public ProfilerMarker VoxelGenerationMarker;

        public void Execute()
        {
            VoxelGenerationMarker.Begin();
            bool isChunkEmpty = true;
            bool isChunkSolid = true;
            float scale = 0.06f;

            for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
            {
                for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
                {
                    for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
                    {
                        float perlinValue = PerlinNoise.Perlin3D(
                            (x - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * ChunkOffset.x) * scale,
                            (y - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * ChunkOffset.y) * scale,
                            (z - 1 + (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * ChunkOffset.z) * scale);

                        float t = y / (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1f);
                        byte value = perlinValue > math.pow(t, 0.7f) ? (byte)1 : (byte)0;

                        if (x == 0 || y == 0 || z == 0 ||
                            x == VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1 ||
                            y == VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1 ||
                            z == VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1)
                            value = 0;

                        isChunkSolid &= value != 0;
                        isChunkEmpty &= value == 0;

                        Voxels[x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                               z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = value;
                    }
                }
            }

            // Mark the topmost solid voxel in each column as value 2 (surface)
            for (int x = 1; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; x++)
            {
                for (int z = 1; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; z++)
                {
                    for (int y = VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2; y >= 1; y--)
                    {
                        int idx = x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                                  z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED;
                        if (Voxels[idx] != 0)
                        {
                            Voxels[idx] = 2;
                            break;
                        }
                    }
                }
            }

            Result[0] = isChunkEmpty;
            Result[1] = isChunkSolid;
            VoxelGenerationMarker.End();
        }
    }

    [CreateAssetMenu(fileName = "Hills Voxel Generator", menuName = "ScriptableObjects/Hills Voxel Generator")]
    public class HillsVoxelsGenerator : VoxelsGeneratorBase
    {
        public override JobHandle ScheduleVoxelsGeneration(ChunkData chunkData)
        {
            return new VoxelGenerationJob()
            {
                VoxelGenerationMarker = new ProfilerMarker("Voxel Generation"),
                Voxels = chunkData.Voxels,
                ChunkOffset = chunkData.ChunkPosition,
                Result = chunkData.Flags
            }.Schedule();
        }
    }
}
