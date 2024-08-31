using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public static class Extensions
    {
        public static int CountTrailingZeros(this ulong number)
        {
            if (number == 0)
                return 64; // Special case: All bits are zero, so 64 trailing zeros

            int count = 0;

            // Count trailing zeros using bitwise operations
            while ((number & 1) == 0)
            {
                count++;
                number >>= 1;
            }

            return count;
        }
    }
}