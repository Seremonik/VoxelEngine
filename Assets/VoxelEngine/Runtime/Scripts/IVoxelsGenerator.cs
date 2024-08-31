using Unity.Jobs;

namespace VoxelEngine
{
    public interface IVoxelsGenerator
    {
        JobHandle ScheduleChunkGeneration(ChunkData chunkData);
        JobHandle ScheduleBitMatrixRecalculation(ChunkData chunkData, JobHandle dependency);
        JobHandle ScheduleVoxelBufferRecalculation(ChunkData chunkData, JobHandle dependency);
    }
}