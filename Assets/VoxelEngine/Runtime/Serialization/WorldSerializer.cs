using Unity.Mathematics;

namespace VoxelEngine
{
    public class WorldSerializer : IWorldSerializer
    {
        public bool HasSerializedChunk(int3 chunkPosition)
        {
            return false;
        }
    }
}