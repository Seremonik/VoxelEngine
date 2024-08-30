using Unity.Jobs;

namespace VoxelEngine
{
    public interface IVoxelsGenerator
    {
        JobHandle ScheduleChunkGeneration(ChunkData chunkData);
    }
}