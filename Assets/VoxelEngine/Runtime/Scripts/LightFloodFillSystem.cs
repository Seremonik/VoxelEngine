using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelEngine
{
    [BurstCompile]
    partial struct SunLightFloodFillJob : IJob
    {
        public NativeArray<byte> Lights;
        [ReadOnly]
        public NativeArray<byte> Voxels;
        public NativeQueue<int4> LightsQueue;

        public void Execute()
        {
            PopulateSunLightQueue();
            PropagateLight();
        }

        //Propagate from top do distribute sun light
        private void PopulateSunLightQueue()
        {
            //TODO Properly Check in upper chunks if we can see sun
            for (int x = 1; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE-1; x++)
            {
                for (int z = 1; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE-1; z++)
                {
                    var currentY = VoxelEngineConstants.CHUNK_VOXEL_SIZE-2;
                    int currentVoxelValue = GetVoxel(x,currentY,z);
                    if (currentVoxelValue != 0)
                        continue;

                    while (currentVoxelValue == 0)
                    {
                        LightsQueue.Enqueue(new int4(x,currentY,z, 15));
                        currentY--;
                        currentVoxelValue = GetVoxel(x,currentY,z);
                    }
                }
            }
        }

        private void PropagateLight()
        {
            while (LightsQueue.Count > 0)
            {
                int4 lightSource = LightsQueue.Dequeue();

                if (GetLightValue(lightSource) >= lightSource.w)
                    continue;
                SetLight(lightSource);

                if (GetLightValue(lightSource.x + 1, lightSource.y, lightSource.z) <= lightSource.w-2)
                {
                    LightsQueue.Enqueue(new int4(lightSource.x + 1, lightSource.y, lightSource.z, lightSource.w-1));
                }
                if (GetLightValue(lightSource.x - 1, lightSource.y, lightSource.z) <= lightSource.w-2)
                {
                    LightsQueue.Enqueue(new int4(lightSource.x - 1, lightSource.y, lightSource.z, lightSource.w-1));
                }
                if (GetLightValue(lightSource.x, lightSource.y, lightSource.z+1) <= lightSource.w-2)
                {
                    LightsQueue.Enqueue(new int4(lightSource.x , lightSource.y, lightSource.z+1, lightSource.w-1));
                }
                if (GetLightValue(lightSource.x, lightSource.y, lightSource.z-1) <= lightSource.w-2)
                {
                    LightsQueue.Enqueue(new int4(lightSource.x , lightSource.y, lightSource.z-1, lightSource.w-1));
                }
                if (GetLightValue(lightSource.x, lightSource.y+1, lightSource.z) <= lightSource.w-2)
                {
                    LightsQueue.Enqueue(new int4(lightSource.x , lightSource.y+1, lightSource.z, lightSource.w-1));
                }
                if (GetLightValue(lightSource.x, lightSource.y-1, lightSource.z) <= lightSource.w-2)
                {
                    LightsQueue.Enqueue(new int4(lightSource.x , lightSource.y-1, lightSource.z, lightSource.w-1));
                }
            }
        }

        private byte GetVoxel(int x, int y, int z)
        {
            return Voxels[
                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
        }
        
        private byte GetLightValue(int x, int y,int z)
        {
            if (x <= 0 || x >= 62 || y <= 0 || y >= 62 ||z <= 0 || z >= 62)
                return 15; // stop propagating on the border
            
            return Lights[
                x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
        }
        
        private byte GetLightValue(int4 lightPosition)
        {
            return GetLightValue(lightPosition.x, lightPosition.y, lightPosition.z);
        }

        private void SetLight(int4 lightPosition)
        {
            Lights[
                lightPosition.x + lightPosition.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                lightPosition.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = (byte)lightPosition.w;
        }
    }

    public class LightFloodFillSystem
    {
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
                Lights = chunkData.Light,
                Voxels = chunkData.Voxels,
                LightsQueue = new NativeQueue<int4>(Allocator.TempJob),
            };
            
            return job.Schedule(dependency);
        }
    }
}