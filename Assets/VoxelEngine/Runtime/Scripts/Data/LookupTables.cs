using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public static class LookupTables
    {
        public static NativeArray<int3>.ReadOnly VoxelNeighborOffsets => voxelNeighborOffsets.AsReadOnly();
        
        private static NativeArray<int3> voxelNeighborOffsets;

        [RuntimeInitializeOnLoadMethod]
        private static void CreateLookupTables()
        {
            CreateVoxelNeighborOffsets();
        }
        
        private static void CreateVoxelNeighborOffsets()
        {
            // Allocate the NativeArray and set the 6 neighbor offsets
            voxelNeighborOffsets = new NativeArray<int3>(6, Allocator.Persistent);
            voxelNeighborOffsets[0] = new int3(1, 0, 0); // Right
            voxelNeighborOffsets[1] = new int3(-1, 0, 0); // Left
            voxelNeighborOffsets[2] = new int3(0, 1, 0); // Up
            voxelNeighborOffsets[3] = new int3(0, -1, 0); // Down
            voxelNeighborOffsets[4] = new int3(0, 0, 1); // Forward
            voxelNeighborOffsets[5] = new int3(0, 0, -1); // Backward
        }
    }
}