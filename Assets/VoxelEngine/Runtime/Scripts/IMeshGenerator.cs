using Unity.Jobs;

namespace VoxelEngine
{
    public interface IMeshGenerator
    {
        public JobHandle ScheduleMeshGeneration(ChunkData chunkData, JobHandle dependency);
    }
}