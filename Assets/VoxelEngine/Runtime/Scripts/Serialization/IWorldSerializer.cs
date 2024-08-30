using Unity.Mathematics;

namespace VoxelEngine
{
    public interface IWorldSerializer
    {
        bool HasSerializedChunk(int3 chunkPosition);
    }
}