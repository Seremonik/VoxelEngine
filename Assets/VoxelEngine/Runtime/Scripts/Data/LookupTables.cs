using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public static class LookupTables
    {
        public static NativeArray<int3>.ReadOnly VoxelNeighborOffsets => voxelNeighborOffsets.AsReadOnly();
        public static NativeArray<ulong>.ReadOnly TransposeMatrixLookupTable => transposeMatrixLookupTable.AsReadOnly();

        private static NativeArray<int3> voxelNeighborOffsets;
        private static NativeArray<ulong> transposeMatrixLookupTable;
        
        [RuntimeInitializeOnLoadMethod]
        private static void CreateLookupTables()
        {
            CreateVoxelNeighborOffsets();
            CreateTransposeMatrixLookupTable();
        }
        
        private static void CreateVoxelNeighborOffsets()
        {
            voxelNeighborOffsets = new NativeArray<int3>(6, Allocator.Persistent);
            voxelNeighborOffsets[0] = new int3(1, 0, 0); // Right
            voxelNeighborOffsets[1] = new int3(-1, 0, 0); // Left
            voxelNeighborOffsets[2] = new int3(0, 1, 0); // Up
            voxelNeighborOffsets[3] = new int3(0, -1, 0); // Down
            voxelNeighborOffsets[4] = new int3(0, 0, 1); // Forward
            voxelNeighborOffsets[5] = new int3(0, 0, -1); // Backward
        }

        private static void CreateTransposeMatrixLookupTable()
        {
            transposeMatrixLookupTable = new NativeArray<ulong>(12, Allocator.Persistent)
            {
                [0] = 0x5555555555555555UL,
                [1] = 0x3333333333333333UL,
                [2] = 0x0F0F0F0F0F0F0F0FUL,
                [3] = 0x00FF00FF00FF00FFUL,
                [4] = 0x0000FFFF0000FFFFUL,
                [5] = 0x00000000FFFFFFFFUL,
                [6] = 0xAAAAAAAAAAAAAAAAUL,
                [7] = 0xCCCCCCCCCCCCCCCCUL,
                [8] = 0xF0F0F0F0F0F0F0F0UL,
                [9] = 0xFF00FF00FF00FF00UL,
                [10] = 0xFFFF0000FFFF0000UL,
                [11] = 0xFFFFFFFF00000000UL,
            };
        }
    }
}