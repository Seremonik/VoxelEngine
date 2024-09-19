using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelEngine
{
    public class ChunkData : IDisposable
    {
        public ChunkState ChunkLoadedState;
        public int3 ChunkPosition;
        public JobHandle GenerationJobHandle;
        
        //Buffers
        public NativeArray<byte> Voxels;
        public NativeArray<byte> Light;
        //Temp Buffers - Calculated when needed
        public NativeArray<ulong> BitMatrix;
        public NativeArray<uint> VoxelBuffer;
        public NativeList<int> Triangles;
        public NativeList<uint> Vertices;
        

        // public bool IsDirty;
        // public bool RequiresSaving;
        // public bool HasLeftWall;
        // public bool HasRightWall;
        // public bool HasTopWall;
        // public bool HasBottomWall;
        // public bool HasFrontWall;
        // public bool HasBackWall;

        public ChunkData(int3 position)
        {
            ChunkPosition = position;
        }

        public void Dispose()
        {
            Voxels.Dispose();
            BitMatrix.Dispose();
            VoxelBuffer.Dispose();
            Triangles.Dispose();
            Vertices.Dispose();
        }
    }
}