using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

namespace VoxelEngine
{
    public abstract class VoxelsGeneratorBase : ScriptableObject, IVoxelsGenerator
    {
        public abstract void Initialize(JobScheduler jobScheduler);

        public abstract Task GenerateVoxels(ChunkData chunkData);

        public abstract JobHandle ScheduleVoxelsGeneration(ChunkData chunkData);

        public abstract JobHandle ScheduleBitMatrixRecalculation(ChunkData chunkData, JobHandle dependency = default);

        public abstract JobHandle ScheduleVoxelBufferRecalculation(ChunkData chunkData, JobHandle dependency = default);
    }
}