using UnityEngine;

namespace VoxelEngine
{
    public interface IChunkSerializer
    {
        bool Serialize(ChunkData chunkData);
        ChunkData DeSerialize(Vector3Int chunkPosition);
    }
}