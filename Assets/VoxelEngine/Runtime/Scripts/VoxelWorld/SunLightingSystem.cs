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

        //Propagate from top to distribute sun light
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

                    while (currentVoxelValue == 0 && currentY >= 1)
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
            NativeArray<byte> lights, NativeArray<int3>.ReadOnly neighborOffsets)
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
            NativeArray<byte> lights, NativeArray<int3>.ReadOnly neighborOffsets)
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

    // Propagates light inward from pre-seeded border cells written by SunLightingSystem.SeedBordersFromNeighbors.
    // Unlike SunLightFloodFillJob, this uses real bounds checking instead of returning 15 at borders,
    // allowing light to flow freely between border cells and inner voxels.
    [BurstCompile]
    struct SunNeighborFloodFillJob : IJob
    {
        [ReadOnly]
        public NativeArray<byte> Voxels;
        [ReadOnly]
        public NativeArray<int3>.ReadOnly NeighborOffsets;
        public NativeQueue<int4> LightsQueue;
        public NativeArray<byte> Lights;

        public void Execute()
        {
            PopulateBorderQueue();
            PropagateLightBounded(LightsQueue, Voxels, Lights, NeighborOffsets);
        }

        // Seeds the queue from all 6 border faces that were pre-filled by SeedBordersFromNeighbors.
        private void PopulateBorderQueue()
        {
            int last = VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1;

            for (int a = 0; a < VoxelEngineConstants.CHUNK_VOXEL_SIZE; a++)
            {
                for (int b = 0; b < VoxelEngineConstants.CHUNK_VOXEL_SIZE; b++)
                {
                    TryEnqueue(0,    a,    b);
                    TryEnqueue(last, a,    b);
                    TryEnqueue(a,    0,    b);
                    TryEnqueue(a,    last, b);
                    TryEnqueue(a,    b,    0);
                    TryEnqueue(a,    b,    last);
                }
            }
        }

        private void TryEnqueue(int x, int y, int z)
        {
            // Solid border voxels don't emit light
            if (Voxels[x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                       z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] != 0)
            {
                return;
            }

            byte light = Lights[x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                                 z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
            if (light > 1)
                LightsQueue.Enqueue(new int4(x, y, z, light));
        }

        // Same as PropagateLight but uses a real bounds check instead of returning 15 at borders.
        // This allows border cells to propagate light inward and to adjacent border cells.
        public static void PropagateLightBounded(NativeQueue<int4> lightsQueue, NativeArray<byte> voxels,
            NativeArray<byte> lights, NativeArray<int3>.ReadOnly neighborOffsets)
        {
            int size = VoxelEngineConstants.CHUNK_VOXEL_SIZE;

            while (lightsQueue.Count > 0)
            {
                int4 lightSource = lightsQueue.Dequeue();
                if (lightSource.w <= 1) continue;

                for (int i = 0; i < neighborOffsets.Length; i++)
                {
                    int3 n = new int3(
                        lightSource.x + neighborOffsets[i].x,
                        lightSource.y + neighborOffsets[i].y,
                        lightSource.z + neighborOffsets[i].z);

                    if (n.x < 0 || n.x >= size || n.y < 0 || n.y >= size || n.z < 0 || n.z >= size)
                        continue;

                    int idx = n.x + n.y * size + n.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED;

                    if (lights[idx] >= lightSource.w - 1) continue;
                    if (voxels[idx] != 0) continue;

                    lights[idx] = (byte)(lightSource.w - 1);
                    lightsQueue.Enqueue(new int4(n, lightSource.w - 1));
                }
            }
        }
    }

    public class SunLightingSystem
    {
        public JobHandle CalculateLocalSunLight(ChunkData chunkData, JobHandle dependency = default)
        {
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

        // Copies each loaded neighbor's inner border row into this chunk's outer border cells,
        // then runs a bounded flood fill inward from those seeded values.
        public JobHandle CalculateNeighboringLight(ChunkData chunkData, Dictionary<int3, ChunkData> loadedChunks,
            JobHandle dependency = default)
        {
            SeedBordersFromNeighbors(chunkData, loadedChunks);

            var lightsQueue = new NativeQueue<int4>(Allocator.TempJob);
            var job = new SunNeighborFloodFillJob()
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

        // Reads each available neighbor's inner border row and writes it into this chunk's
        // corresponding outer border cells (index 0 or 63). Only reads from neighbors whose
        // local light has fully completed (LocalLightCalculated or beyond).
        private static void SeedBordersFromNeighbors(ChunkData chunk, Dictionary<int3, ChunkData> loadedChunks)
        {
            int size  = VoxelEngineConstants.CHUNK_VOXEL_SIZE;
            int size2 = VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED;
            int inner = size - 2; // 62 — last inner voxel row
            int outer = size - 1; // 63 — border index

            // Initialize all 6 border faces to 15 (assume full daylight at chunk boundaries).
            // Neighbors will overwrite these with their actual values where available.
            for (int a = 0; a < size; a++)
                for (int b = 0; b < size; b++)
                {
                    chunk.Light[0     + a * size + b * size2] = 15;
                    chunk.Light[outer + a * size + b * size2] = 15;
                    chunk.Light[a + 0     * size + b * size2] = 15;
                    chunk.Light[a + outer * size + b * size2] = 15;
                    chunk.Light[a + b * size + 0     * size2] = 15;
                    chunk.Light[a + b * size + outer * size2] = 15;
                }

            // Y+ (above): neighbor's y=1 → our y=63
            if (TryGetLitNeighbor(chunk.ChunkPosition + new int3(0, 1, 0), loadedChunks, out var above))
                for (int x = 0; x < size; x++)
                    for (int z = 0; z < size; z++)
                        chunk.Light[x + outer * size + z * size2] = above.Light[x + 1 * size + z * size2];

            // Y- (below): neighbor's y=62 → our y=0
            if (TryGetLitNeighbor(chunk.ChunkPosition + new int3(0, -1, 0), loadedChunks, out var below))
                for (int x = 0; x < size; x++)
                    for (int z = 0; z < size; z++)
                        chunk.Light[x + 0 * size + z * size2] = below.Light[x + inner * size + z * size2];

            // X+ (right): neighbor's x=1 → our x=63
            if (TryGetLitNeighbor(chunk.ChunkPosition + new int3(1, 0, 0), loadedChunks, out var right))
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                        chunk.Light[outer + y * size + z * size2] = right.Light[1 + y * size + z * size2];

            // X- (left): neighbor's x=62 → our x=0
            if (TryGetLitNeighbor(chunk.ChunkPosition + new int3(-1, 0, 0), loadedChunks, out var left))
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                        chunk.Light[0 + y * size + z * size2] = left.Light[inner + y * size + z * size2];

            // Z+ (front): neighbor's z=1 → our z=63
            if (TryGetLitNeighbor(chunk.ChunkPosition + new int3(0, 0, 1), loadedChunks, out var front))
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < size; y++)
                        chunk.Light[x + y * size + outer * size2] = front.Light[x + y * size + 1 * size2];

            // Z- (back): neighbor's z=62 → our z=0
            if (TryGetLitNeighbor(chunk.ChunkPosition + new int3(0, 0, -1), loadedChunks, out var back))
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < size; y++)
                        chunk.Light[x + y * size + 0 * size2] = back.Light[x + y * size + inner * size2];
        }

        private static bool TryGetLitNeighbor(int3 pos, Dictionary<int3, ChunkData> loadedChunks,
            out ChunkData neighbor)
        {
            // LightFullyCalculated is set only after SunNeighborFloodFillJob completes,
            // guaranteeing the Light array is no longer being written to.
            return loadedChunks.TryGetValue(pos, out neighbor) &&
                   neighbor.ChunkLoadedState >= ChunkState.LightFullyCalculated;
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
                           voxelPosition.z) == 0 && voxelPosition.y>=1)
                {
                    SunLightFloodFillJob.SetLight(chunkData.Light, new int4(voxelPosition, 15));
                    lightsQueue.Enqueue(new int4(voxelPosition, 15));
                    voxelPosition.y -= 1;
                }
            }
            else
            {
                int newLightValue = 0;
                for (int i = 0; i < LookupTables.VoxelNeighborOffsets.Length && newLightValue < 14; i++)
                {
                    int3 neighborPosition = voxelPosition + LookupTables.VoxelNeighborOffsets[i];
                    int neighborValue = SunLightFloodFillJob.GetLightValue(chunkData.Light, neighborPosition);
                    newLightValue = math.max(newLightValue, neighborValue - 1);
                }

                SunLightFloodFillJob.SetLight(chunkData.Light, new int4(voxelPosition, newLightValue));
                lightsQueue.Enqueue(new int4(voxelPosition, newLightValue));
            }

            SunLightFloodFillJob.PropagateLight(lightsQueue, chunkData.Voxels, chunkData.Light,
                LookupTables.VoxelNeighborOffsets);
            lightsQueue.Dispose();
        }

        public void AddVoxel(ChunkData chunkData, int3 voxelPosition)
        {
            voxelPosition += 1;

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
            SunLightFloodFillJob.PropagateLight(lightsQueue, chunkData.Voxels, chunkData.Light,
                LookupTables.VoxelNeighborOffsets);

            lightsQueue.Dispose();
            darknessQueue.Dispose();
        }
    }
}
