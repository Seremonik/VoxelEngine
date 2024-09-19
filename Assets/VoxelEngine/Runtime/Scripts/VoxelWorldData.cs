using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine
{
    /// <summary>
    /// Contains all the information about the World
    /// </summary>
    public class VoxelWorldData
    {
        public string name;
        public int seed;
        
        public int3 PlayerChunk { get; private set; }
        public readonly Dictionary<int3, ChunkGameObject> VisibleChunks = new();
        public readonly LightFloodFillSystem LightingSystem = new();
    }
}