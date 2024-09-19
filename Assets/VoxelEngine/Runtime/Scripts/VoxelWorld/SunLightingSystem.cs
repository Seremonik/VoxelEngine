using System.Collections.Generic;
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
        public NativeArray<int3>.ReadOnly  NeighborOffsets;
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
                        SetLight(Lights, new int4(x, currentY, z, 15));
                        currentY--;
                        currentVoxelValue = GetVoxel(Voxels, x, currentY, z);
                    }
                }
            }
        }

        public static void PropagateLight(NativeQueue<int4> lightsQueue, NativeArray<byte> voxels,
            NativeArray<byte> lights, NativeArray<int3>.ReadOnly  neighborOffsets)
        {
            while (lightsQueue.Count > 0)
            {
                int4 lightSource = lightsQueue.Dequeue();

                for (int i = 0; i < neighborOffsets.Length; i++)
                {
                    int3 neighborPosition = new int3(
                        lightSource.x + neighborOffsets[i].x,
                        lightSource.y + neighborOffsets[i].y,
                        lightSource.z + neighborOffsets[i].z);

                    if (GetLightValue(lights, neighborPosition) >= lightSource.w - 1)
                        continue; // Ignore higher light values

                    if (GetVoxel(voxels, neighborPosition.x, neighborPosition.y, neighborPosition.z) != 0)
                        continue; // Ignore solid voxels

                    if (lightSource.w <= 1)
                        continue; //Ignore when light is less than 1

                    SetLight(lights, new int4(neighborPosition, lightSource.w - 1));
                    lightsQueue.Enqueue(new int4(neighborPosition, lightSource.w - 1));
                }
            }
        }

        public static void PropagateDarkness(NativeQueue<int4> darknessQueue, NativeQueue<int4> lightsQueue,
            NativeArray<byte> lights, NativeArray<int3>.ReadOnly  neighborOffsets)
        {
            while (darknessQueue.Count > 0)
            {
                int4 darknessSource = darknessQueue.Dequeue();

                for (int i = 0; i < neighborOffsets.Length; i++)
                {
                    int3 neighborPosition = new int3(
                        darknessSource.x + neighborOffsets[i].x,
                        darknessSource.y + neighborOffsets[i].y,
                        darknessSource.z + neighborOffsets[i].z);

                    int neighborLight = GetLightValue(lights, neighborPosition);
                    
                    if (neighborLight == 15 || neighborLight >
                        darknessSource.w) //Check if there is stronger light source
                    {
                        //Add light for further propagation
                        lightsQueue.Enqueue(new int4(neighborPosition, darknessSource.w));
                        continue;
                    }

                    if (neighborLight == 0)
                        continue; // Ignore 0 light values

                    if (darknessSource.w <= 1)
                        continue; //stop Darkness propagation when we reach 1 light

                    SetLight(lights, new int4(neighborPosition.xyz, 0));
                    darknessQueue.Enqueue(new int4(neighborPosition, darknessSource.w - 1));
                }
            }
        }

        public static byte GetVoxel(NativeArray<byte> voxels, int x, int y, int z)
        {
            return voxels[
                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
        }

        public static byte GetLightValue(NativeArray<byte> lights, int3 position)
        {
            if (position.x <= 0 || position.x >= 63 || position.y <= 0 || position.y >= 63 || position.z <= 0 ||
                position.z >= 63)
                return 15; // stop propagating on the border

            return lights[
                position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
        }

        public static void SetLight(NativeArray<byte> lights, int4 lightPosition)
        {
            lights[
                lightPosition.x + lightPosition.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                lightPosition.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = (byte)lightPosition.w;
        }
    }

    public class SunLightingSystem
    {
        public JobHandle CalculateLocalSunLight(ChunkData chunkData, JobHandle dependency)
        {
            if (!chunkData.Light.IsCreated)
            {
                chunkData.Light =
                    new NativeArray<byte>(
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
            }

            var lightsQueue = new NativeQueue<int4>(Allocator.TempJob);
            var job = new SunLightFloodFillJob()
            {
                NeighborOffsets = LookupTables.VoxelNeighborOffsets,
                Lights = chunkData.Light,
                Voxels = chunkData.Voxels,
                LightsQueue = lightsQueue,
            };

            var handle = job.Schedule(dependency);
            lightsQueue.Dispose(handle);
            return handle;
        }

        public JobHandle CalculateNeighboringLight(ChunkData chunkData, List<ChunkData> neighboringChunks, JobHandle dependency)
        {
            return default;
        }
        

        public void RemoveVoxel(ChunkData chunkData, int3 voxelPosition)
        {
            voxelPosition += 1; //Offset by one as we have padding of 1. Instead of 64 we render 62
            var lightsQueue = new NativeQueue<int4>(Allocator.Temp);

            int3 voxelAbove = voxelPosition + new int3(0, 1, 0);
            //Check if we unblocked the sun
            if (SunLightFloodFillJob.GetLightValue(chunkData.Light, voxelAbove) == 15)
            {
                while (SunLightFloodFillJob.GetVoxel(chunkData.Voxels, voxelPosition.x, voxelPosition.y,
                           voxelPosition.z) == 0)
                {
                    SunLightFloodFillJob.SetLight(chunkData.Light, new int4(voxelPosition, 15));
                    lightsQueue.Enqueue(new int4(voxelPosition, 15));
                    voxelPosition.y -= 1;
                }
            }
            else // Voxel is not lit by the sun
            {
                int newLightValue = 0;

                //Find neighbor with highest light value
                for (int i = 0; i < LookupTables.VoxelNeighborOffsets.Length && newLightValue < 14; i++)
                {
                    int3 neighborPosition = voxelPosition + LookupTables.VoxelNeighborOffsets[i];
                    int neighborValue = SunLightFloodFillJob.GetLightValue(chunkData.Light, neighborPosition);
                    newLightValue = math.max(newLightValue, neighborValue - 1);
                }

                SunLightFloodFillJob.SetLight(chunkData.Light, new int4(voxelPosition, newLightValue));
                lightsQueue.Enqueue(new int4(voxelPosition, newLightValue));
            }

            SunLightFloodFillJob.PropagateLight(lightsQueue, chunkData.Voxels, chunkData.Light, LookupTables.VoxelNeighborOffsets);
            lightsQueue.Dispose();
        }

        public void AddVoxel(ChunkData chunkData, int3 voxelPosition)
        {
            voxelPosition += 1; //Offset by one as we have padding of 1. Instead of 64 we render 62

            var lightsQueue = new NativeQueue<int4>(Allocator.Temp);
            var darknessQueue = new NativeQueue<int4>(Allocator.Temp);

            int currentLightValue = SunLightFloodFillJob.GetLightValue(chunkData.Light, voxelPosition);
            SunLightFloodFillJob.SetLight(chunkData.Light, new int4(voxelPosition, 0));
            darknessQueue.Enqueue(new int4(voxelPosition, currentLightValue));
            voxelPosition.y -= 1;

            while (SunLightFloodFillJob.GetLightValue(chunkData.Light, voxelPosition) == 15)
            {
                SunLightFloodFillJob.SetLight(chunkData.Light, new int4(voxelPosition, 0));
                darknessQueue.Enqueue(new int4(voxelPosition, 15));
                voxelPosition.y -= 1;
            }

            SunLightFloodFillJob.PropagateDarkness(darknessQueue, lightsQueue, chunkData.Light,
                LookupTables.VoxelNeighborOffsets);
            SunLightFloodFillJob.PropagateLight(lightsQueue, chunkData.Voxels, chunkData.Light, LookupTables.VoxelNeighborOffsets);

            lightsQueue.Dispose();
            darknessQueue.Dispose();
        }
    }
}