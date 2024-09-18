using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelEngine
{
    [BurstCompile]
    partial struct SunLightFloodFillJob : IJob
    {
        [ReadOnly]
        public NativeArray<byte> Voxels;
        [ReadOnly]
        public NativeArray<int3> NeighborOffsets;
        public NativeQueue<int4> LightsQueue;
        public NativeArray<byte> Lights;

        public void Execute()
        {
            PopulateSunLightQueue();
            PropagateLight(LightsQueue, Voxels, Lights, NeighborOffsets);
        }

        //Propagate from top do distribute sun light
        private void PopulateSunLightQueue()
        {
            //TODO Properly Check in upper chunks if we can see sun
            for (int x = 1; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; x++)
            {
                for (int z = 1; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; z++)
                {
                    var currentY = VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2;
                    int currentVoxelValue = GetVoxel(Voxels, x, currentY, z);
                    if (currentVoxelValue != 0)
                        continue;

                    while (currentVoxelValue == 0)
                    {
                        LightsQueue.Enqueue(new int4(x, currentY, z, 15));
                        currentY--;
                        currentVoxelValue = GetVoxel(Voxels, x, currentY, z);
                    }
                }
            }
        }

        public static void PropagateLight(NativeQueue<int4> LightsQueue, NativeArray<byte> Voxels,
            NativeArray<byte> Lights, NativeArray<int3> neighborOffsets)
        {
            while (LightsQueue.Count > 0)
            {
                int4 lightSource = LightsQueue.Dequeue();

                if (GetLightValue(Voxels, Lights, lightSource) >= lightSource.w)
                    continue;
                SetLight(Lights, lightSource);

                for (int i = 0; i < neighborOffsets.Length; i++)
                {
                    int3 neighborPosition = new int3(
                        lightSource.x + neighborOffsets[i].x,
                        lightSource.y + neighborOffsets[i].y,
                        lightSource.z + neighborOffsets[i].z);

                    if (GetLightValue(Lights, neighborPosition.x, neighborPosition.y, neighborPosition.z) >
                        lightSource.w - 2) continue;
                    
                    if (GetVoxel(Voxels, neighborPosition.x, neighborPosition.y, neighborPosition.z) == 0)
                    {
                        LightsQueue.Enqueue(new int4(neighborPosition, lightSource.w - 1));
                    }
                }
            }
        }

        public static byte GetVoxel(NativeArray<byte> Voxels, int x, int y, int z)
        {
            return Voxels[
                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
        }

        public static byte GetLightValue(NativeArray<byte> Lights, int x, int y, int z)
        {
            if (x <= 0 || x >= 63 || y <= 0 || y >= 63 || z <= 0 || z >= 63)
                return 15; // stop propagating on the border

            return Lights[
                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
        }

        private static byte GetLightValue(NativeArray<byte> Voxels, NativeArray<byte> Lights, int4 lightPosition)
        {
            return GetLightValue(Lights, lightPosition.x, lightPosition.y, lightPosition.z);
        }

        public static void SetLight(NativeArray<byte> Lights, int4 lightPosition)
        {
            Lights[
                lightPosition.x + lightPosition.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                lightPosition.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = (byte)lightPosition.w;
        }
    }

    public class LightFloodFillSystem
    {
        private NativeArray<int3> neighborOffsets;

        public LightFloodFillSystem()
        {
            // Allocate the NativeArray and set the 6 neighbor offsets
            neighborOffsets = new NativeArray<int3>(6, Allocator.Persistent);
            neighborOffsets[0] = new int3(1, 0, 0); // Right
            neighborOffsets[1] = new int3(-1, 0, 0); // Left
            neighborOffsets[2] = new int3(0, 1, 0); // Up
            neighborOffsets[3] = new int3(0, -1, 0); // Down
            neighborOffsets[4] = new int3(0, 0, 1); // Forward
            neighborOffsets[5] = new int3(0, 0, -1); // Backward
        }

        ~LightFloodFillSystem()
        {
            neighborOffsets.Dispose();
        }

        public JobHandle CalculateLight(ChunkData chunkData, JobHandle dependency)
        {
            if (!chunkData.Light.IsCreated)
            {
                chunkData.Light =
                    new NativeArray<byte>(
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
            }

            var job = new SunLightFloodFillJob()
            {
                NeighborOffsets = neighborOffsets,
                Lights = chunkData.Light,
                Voxels = chunkData.Voxels,
                LightsQueue = new NativeQueue<int4>(Allocator.TempJob),
            };

            return job.Schedule(dependency);
        }

        public void RemoveVoxel(ChunkData chunkData, int3 voxelPosition)
        {
            var lightsQueue = new NativeQueue<int4>(Allocator.Temp);

            voxelPosition += 1; //Offset by one as we have padding of 1. Instead of 64 we render 62
            
            //Check if we unblocked the sun
            if (SunLightFloodFillJob.GetLightValue(chunkData.Light,
                    voxelPosition.x, voxelPosition.y + 1, voxelPosition.z) == 15)
            {
                while (SunLightFloodFillJob.GetVoxel(chunkData.Voxels, voxelPosition.x, voxelPosition.y,
                           voxelPosition.z) == 0)
                {
                    lightsQueue.Enqueue(new int4(voxelPosition, 15));
                    voxelPosition.y -= 1;
                }
            }
            else // Voxel is not lit by the sun
            {
                int newLightValue = 0;

                //Find neighbor with highest light value
                for (int i = 0; i < neighborOffsets.Length && newLightValue < 14; i++)
                {
                    int3 neighborPosition = voxelPosition + neighborOffsets[i];
                    int neighborValue = SunLightFloodFillJob.GetLightValue(chunkData.Light,
                        neighborPosition.x, neighborPosition.y, neighborPosition.z);
                    newLightValue = math.max(newLightValue, neighborValue - 1);
                }
                
                lightsQueue.Enqueue(new int4(voxelPosition, newLightValue));
            }

            SunLightFloodFillJob.PropagateLight(lightsQueue, chunkData.Voxels, chunkData.Light, neighborOffsets);
        }

        public void AddVoxel(int3 voxelPosition)
        {
        }
    }
}