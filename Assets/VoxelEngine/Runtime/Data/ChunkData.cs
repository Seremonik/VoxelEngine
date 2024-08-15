using Unity.Collections;
using UnityEngine;

namespace VoxelEngine
{
    public struct ChunkData
    {
        public NativeArray<byte> Voxels;
        public NativeArray<ulong> BitMatrix;
        public Vector3Int ChunkPosition;
        public Vector3 PivotPosition;
        public Mesh Mesh;

        public bool IsDirty;
        public bool RequiresSaving;
        public bool HasLeftWall;
        public bool HasRightWall;
        public bool HasTopWall;
        public bool HasBottomWall;
        public bool HasFrontWall;
        public bool HasBackWall;
    }
}