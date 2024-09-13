using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class WorldGenerator : MonoBehaviour
    {
        [SerializeField]
        private ChunkGameObject chunkPrefab;

        [SerializeField]
        private EngineSettings engineSettings;
        [SerializeField]
        private InterfaceReference<IVoxelsGenerator> voxelsGenerator;
        [SerializeField]
        private InterfaceReference<IMeshGenerator> meshGenerator;
        [SerializeField]
        private bool initializeOnStart;

        private List<ChunkGameObject> scheduledChunks = new();
        private Queue<ChunkGameObject> pooledChunks = new();
        private Queue<int3> scheduledChunksCreation = new();

        private bool isInitialized;
        private int jobsScheduledCount = 0;

        private Dictionary<int3, ChunkGameObject> visibleChunks = new();

        private void Start()
        {
            if (initializeOnStart)
            {
                Initialize();
            }

            //scheduledChunksCreation.Enqueue(new int3(0, 0, 0));
            GenerateWorld();
        }

        private void Update()
        {
            for (int i = 0; scheduledChunksCreation.Count > 0 && i < engineSettings.MaxJobsPerFrame; i++)
            {
                int3 pos = scheduledChunksCreation.Dequeue();
                GenerateChunk(pos.x, pos.z);
            }
        }

        private void LateUpdate()
        {
            for (int i = scheduledChunks.Count - 1; i >= 0; i--)
            {
                if (!scheduledChunks[i].GenerationJobHandle.IsCompleted)
                    continue;
                var chunk = scheduledChunks[i];
                chunk.GenerationJobHandle.Complete();
                chunk.UpdateMesh();
                scheduledChunks.RemoveAt(i);
                visibleChunks.TryAdd(chunk.ChunkData.ChunkPosition, chunk);
            }
        }

        public void GenerateWorld()
        {
            //scheduledChunksCreation.Enqueue(new int3(0, 0, 0));
            SpiralOutward(engineSettings.WorldRadius, 0, 0,
                (x, z) => scheduledChunksCreation.Enqueue(new int3(x, 0, z)));
        }

        public bool IsVoxelSolid(int3 voxelPosition)
        {
            var chunkPosition = new int3(
                (int)Mathf.Floor(voxelPosition.x / 62f),
                (int)Mathf.Floor(voxelPosition.y / 62f),
                (int)Mathf.Floor(voxelPosition.z / 62f));

            int modX = (voxelPosition.x % 62 + 62) % 62;
            int modY = (voxelPosition.y % 62 + 62) % 62;
            int modZ = (voxelPosition.z % 62 + 62) % 62;
            
            if (visibleChunks.TryGetValue(chunkPosition, out var chunk))
            {
                return chunk.ChunkData.Voxels[
                    (modX + 1) + (modY + 1) * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                    (modZ + 1) * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] != 0;
            }

            return false;
        }

        public void RemoveVoxel(int3 voxelPosition)
        {
            if (ChangeVoxel(voxelPosition, 0, out int3 chunk))
            {
                RefreshChunk(chunk, false);
            }
            
            int modX = (voxelPosition.x % 62 + 62) % 62;
            int modY = (voxelPosition.y % 62 + 62) % 62;
            int modZ = (voxelPosition.z % 62 + 62) % 62;

            if (modX == 0)
                RefreshChunk(chunk + new int3(-1, 0, 0), false);
            if (modX == 61)
                RefreshChunk(chunk + new int3(1, 0, 0), false);
            if (modY == 0)
                RefreshChunk(chunk + new int3(0, -1, 0), false);
            if (modY == 61)
                RefreshChunk(chunk + new int3(0, 1, 0), false);
            if (modZ == 0)
                RefreshChunk(chunk + new int3(0, 0, -1), false);
            if (modZ == 61)
                RefreshChunk(chunk + new int3(0, 0, 1), false);
        }

        public void AddVoxel(int3 voxelPosition, byte voxelId)
        {
            if (ChangeVoxel(voxelPosition, voxelId, out int3 chunk))
            {
                RefreshChunk(chunk, true);
            }
        }

        private bool ChangeVoxel(int3 position, byte newValue, out int3 chunkPosition)
        {
            chunkPosition = new int3(
                (int)Mathf.Floor(position.x / 62f),
                (int)Mathf.Floor(position.y / 62f),
                (int)Mathf.Floor(position.z / 62f));
            position = (position % 62 + 62) % 62;

            position += 1; //Offset by one as we have padding of 1. Instead of 64 we render 62

            if (visibleChunks.TryGetValue(chunkPosition, out var chunk))
            {
                chunk.ChunkData.Voxels[
                    position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                    position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                //Make sure we update neighbor chunks if voxel lays on border
                if (position.x == 1 && visibleChunks.TryGetValue(chunkPosition + new int3(-1, 0, 0), out chunk))
                    chunk.ChunkData.Voxels[
                        63 + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.x == 62 && visibleChunks.TryGetValue(chunkPosition + new int3(1, 0, 0), out chunk))
                    chunk.ChunkData.Voxels[
                        0 + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.y == 1 && visibleChunks.TryGetValue(chunkPosition + new int3(0, -1, 0), out chunk))
                    chunk.ChunkData.Voxels[
                        position.x + 63 * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.y == 62 && visibleChunks.TryGetValue(chunkPosition + new int3(0, 1, 0), out chunk))
                    chunk.ChunkData.Voxels[
                        position.x +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.z == 1 && visibleChunks.TryGetValue(chunkPosition + new int3(0, 0, -1), out chunk))
                    chunk.ChunkData.Voxels[
                        position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        63 * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.z == 62 && visibleChunks.TryGetValue(chunkPosition + new int3(0, 0, 1), out chunk))
                    chunk.ChunkData.Voxels[
                        position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE] = newValue;

                return true;
            }

            return false;
        }

        private void RefreshChunk(int3 chunkPosition, bool isAddition)
        {
            if (!visibleChunks.TryGetValue(chunkPosition, out var chunk))
                return;

            var voxelBufferRecalculationHandle = new JobHandle();
            if (isAddition)
            {
                voxelBufferRecalculationHandle =
                    voxelsGenerator.Value.ScheduleVoxelBufferRecalculation(chunk.ChunkData, new JobHandle());
            }

            var bitMatrixRecalculationHandle =
                voxelsGenerator.Value.ScheduleBitMatrixRecalculation(chunk.ChunkData, new JobHandle());
            var meshGenerationHandle =
                meshGenerator.Value.ScheduleMeshGeneration(chunk.ChunkData,
                    JobHandle.CombineDependencies(voxelBufferRecalculationHandle, bitMatrixRecalculationHandle));
            chunk.GenerationJobHandle = meshGenerationHandle;
            scheduledChunks.Add(chunk);
        }

        private void GenerateChunk(int x, int z)
        {
            if (pooledChunks.Count <= 0)
            {
                CreateNewChunkGameObject();
            }

            var freeChunk = pooledChunks.Dequeue();
            freeChunk.transform.position = new Vector3(x * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2), 0,
                z * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2));
            freeChunk.gameObject.name = $"Chunk({x}, {0},{z})";
            freeChunk.SetChunkData(new ChunkData(x, 0, z));
            var voxelGenerationHandle = voxelsGenerator.Value.ScheduleChunkGeneration(freeChunk.ChunkData);
            var meshGenerationHandle =
                meshGenerator.Value.ScheduleMeshGeneration(freeChunk.ChunkData, voxelGenerationHandle);

            freeChunk.GenerationJobHandle = meshGenerationHandle;
            scheduledChunks.Add(freeChunk);
        }

        public void Initialize()
        {
            if (isInitialized)
                return;

            int chunkAmount = (int)math.ceil(engineSettings.WorldRadius * engineSettings.WorldRadius * 3.14f);

            for (int i = 0; i < chunkAmount; i++)
            {
                CreateNewChunkGameObject();
            }
        }

        private void CreateNewChunkGameObject()
        {
            var chunk = Instantiate(chunkPrefab);
            chunk.Initialize();
            pooledChunks.Enqueue(chunk);
        }

        void SpiralOutward(int radius, int centerX, int centerZ, Action<int, int> processPoint)
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