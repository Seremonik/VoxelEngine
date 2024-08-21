using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class ChunkData : IDisposable
    {
        public NativeArray<byte> Voxels;
        public NativeArray<ulong> BitMatrix;
        public NativeArray<uint> VoxelBuffer;
        public int3 ChunkPosition;
        public Vector3 PivotPosition;
        public Mesh Mesh;
        public NativeList<int> Triangles;
        public NativeList<uint> Vertices;

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