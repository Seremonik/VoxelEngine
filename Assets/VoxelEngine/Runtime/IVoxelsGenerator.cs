using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine
{
    public interface IVoxelsGenerator
    {
        NativeArray<ushort> Voxels { get; }
        int GetVoxel(int3 position);
        int GetVoxel(int x, int y, int z);
    }
}