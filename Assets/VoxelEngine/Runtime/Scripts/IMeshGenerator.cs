using Unity.Jobs;
using UnityEngine;

namespace VoxelEngine
{
    public interface IMeshGenerator
    {
        public JobHandle ScheduleMeshGeneration(ChunkData chunkData, JobHandle dependency);
    }
}