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
        public event Action<int3> PlayerChunkUpdated = delegate {  }; 
        public string name;
        public int seed;

        public int3 PlayerChunk
        {
            get => playerChunk;
            set
            {
                if (playerChunk.Equals(value))
                    return;
                playerChunk = value;
                PlayerChunkUpdated?.Invoke(playerChunk);
            }
        }
        public readonly Dictionary<int3, ChunkData> LoadedChunks = new();
        public readonly SunLightingSystem LightingSystem = new();

        private int3 playerChunk;
    }
}