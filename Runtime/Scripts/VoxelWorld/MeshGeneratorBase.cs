using Unity.Jobs;
using UnityEngine;

namespace VoxelEngine
{
    public abstract class MeshGeneratorBase : ScriptableObject, IMeshGenerator
    {
        public abstract JobHandle ScheduleMeshGeneration(ChunkData chunkData, JobHandle dependency);
    }
}