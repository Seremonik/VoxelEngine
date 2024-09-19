using System;

namespace VoxelEngine
{
    public class VoxelEngineUtils
    {
        public static void SpiralOutward(int radius, int centerX, int centerZ, Action<int, int> processPoint)
        {
            // Start at the center
            int x = 0;
            int z = 0;

            // Initial step size
            int dx = 1;
            int dz = 0;

            int segmentLength = 1;

            // Process the center point
            processPoint(centerX + x, centerZ + z);

            while (segmentLength <= 2 * radius)
            {
                for (int i = 0; i < segmentLength; i++)
                {
                    x += dx;
                    z += dz;

                    // If the point is within the circle's radius, process it
                    if (x * x + z * z <= radius * radius)
                    {
                        processPoint(centerX + x, centerZ + z);
                    }
                }

                // Change direction
                int temp = dx;
                dx = -dz;
                dz = temp;

                // Every two segments, increase the segment length
                if (dz == 0)
                {
                    segmentLength++;
                }
            }
        }
    }
}