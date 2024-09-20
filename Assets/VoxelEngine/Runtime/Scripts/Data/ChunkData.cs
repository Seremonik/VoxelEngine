using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelEngine
{
    //must be reusable
    public class ChunkData : IDisposable
    {
        public ChunkState ChunkLoadedState;
        public int3 ChunkPosition;
        public JobHandle GenerationJobHandle;

        //Buffers
        public NativeArray<bool> Flags;
        public NativeArray<byte> Voxels;
        public NativeArray<byte> Light;
        //Temp Buffers - Calculated when needed
        public NativeArray<ulong> BitMatrix;
        public NativeArray<uint> VoxelBuffer;
        public NativeList<int> Triangles;
        public NativeList<uint> Vertices;

        public bool IsEmpty => Flags[0];
        public bool IsSolid => Flags[1];
        
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
            ChunkLoadedState = ChunkState.UnInitialized;
            ChunkPosition = position;

            InitializeArrays();
        }

        private void InitializeArrays()
        {
            Triangles = new NativeList<int>(Allocator.Persistent);
            Vertices = new NativeList<uint>(Allocator.Persistent);
            Flags = new NativeArray<bool>(2, Allocator.Persistent);
            Voxels = new NativeArray<byte>(
                VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
            Light = new NativeArray<byte>(
                VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
        }
        
        public void Dispose()
        {
            Flags.Dispose();
            Voxels.Dispose();
            BitMatrix.Dispose();
            VoxelBuffer.Dispose();
            Triangles.Dispose();
            Vertices.Dispose();
        }
    }
}