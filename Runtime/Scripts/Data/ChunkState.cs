namespace VoxelEngine
{
    public enum ChunkState
    {
        UnInitialized = 0,
        Skipped = 1, //No need to load a Chunk
        LocalLightCalculated = 2,
        LightFullyCalculated = 3,
        FullyRendered = 4
    }
    
}