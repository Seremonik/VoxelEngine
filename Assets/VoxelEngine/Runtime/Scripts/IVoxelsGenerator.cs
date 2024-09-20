using System.Threading.Tasks;
using Unity.Jobs;

namespace VoxelEngine
{
    public interface IVoxelsGenerator
    {
        void Initialize(JobScheduler jobScheduler);
        Task GenerateVoxels(ChunkData chunkData);
        JobHandle ScheduleVoxelsGeneration(ChunkData chunkData);
        JobHandle ScheduleBitMatrixRecalculation(ChunkData chunkData, JobHandle dependency);
        JobHandle ScheduleVoxelBufferRecalculation(ChunkData chunkData, JobHandle dependency);
    }
}