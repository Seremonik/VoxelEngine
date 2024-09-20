using System;
using Unity.Mathematics;

namespace VoxelEngine
{
    public class VoxelWorldSerializer
    {
        public bool IsChunkSerialized(int3 chunkPosition)
        {
            return false;
        }

        public ChunkData LoadChunkMetaData(int3 chunkPosition)
        {
            throw new NotImplementedException();
        }
        
        public ChunkData LoadChunkData(ChunkData chunk)
        {
            throw new NotImplementedException();
        }
        
        public ChunkData SerializeChunkData(ChunkData chunk)
        {
            throw new NotImplementedException();
        }
    }
}