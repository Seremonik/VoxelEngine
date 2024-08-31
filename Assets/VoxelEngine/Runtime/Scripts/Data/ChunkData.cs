using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class ChunkData : IDisposable
    {
        //Buffers
        public NativeArray<byte> Voxels;
        public NativeArray<ulong> BitMatrix;
        public NativeArray<uint> VoxelBuffer;
        public NativeList<int> Triangles;
        public NativeList<uint> Vertices;
        
        public int3 ChunkPosition;

        public bool IsDirty;
        public bool RequiresSaving;
        public bool HasLeftWall;
        public bool HasRightWall;
        public bool HasTopWall;
        public bool HasBottomWall;
        public bool HasFrontWall;
        public bool HasBackWall;

        public ChunkData(int x, int y, int z)
        {
            ChunkPosition = new int3(x, y, z);
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