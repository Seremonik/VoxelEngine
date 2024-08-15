using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine
{
    public interface IVoxelsGenerator
    {
        NativeArray<byte> Voxels { get; }
        int GetVoxel(int3 position);
        int GetVoxel(int x, int y, int z);

        NativeArray<ulong> BitMatrix { get; }
    }
}