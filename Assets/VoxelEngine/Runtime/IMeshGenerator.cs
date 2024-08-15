using UnityEngine;

namespace VoxelEngine
{
    public interface IMeshGenerator
    {
        public Mesh BuildChunkMesh(ChunkData chunkData, Mesh mesh = null);
    }
}