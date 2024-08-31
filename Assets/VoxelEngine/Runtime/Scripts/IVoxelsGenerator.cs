using Unity.Jobs;

namespace VoxelEngine
{
    public interface IVoxelsGenerator
    {
        JobHandle ScheduleChunkGeneration(ChunkData chunkData);
        JobHandle ScheduleBitMatrixRecalculation(ChunkData chunkData);
    }
}