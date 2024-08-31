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

            scheduledChunksCreation.Enqueue(new int3(0, 0, 0));
            //GenerateWorld();
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
            RefreshChunk(new int3(0, 0, 0));
             //scheduledChunksCreation.Enqueue(new int3(0, 0, 0));
            // SpiralOutward(engineSettings.WorldRadius, 0, 0,
            //     (x, z) => scheduledChunksCreation.Enqueue(new int3(x, 0, z)));
        }

        public void RemoveVoxel(RaycastHit raycastHit)
        {
            if (raycastHit.normal.x > 0 || raycastHit.normal.y > 0 || raycastHit.normal.z > 0)
            {
                raycastHit.point -= raycastHit.normal;
            }
            int3 voxelToRemove =new int3((int)math.floor(raycastHit.point.x), (int)math.floor(raycastHit.point.y),(int)math.floor(raycastHit.point.z));
            ChangeVoxel(voxelToRemove, 0);
        }

        public void AddVoxel(RaycastHit raycastHit, byte voxelId)
        {
            if (raycastHit.normal.x < 0 || raycastHit.normal.y < 0 || raycastHit.normal.z < 0)
            {
                raycastHit.point += raycastHit.normal;
            }
            
            int3 voxelToAdd = new int3((int)math.floor(raycastHit.point.x), (int)math.floor(raycastHit.point.y),(int)math.floor(raycastHit.point.z));
            ChangeVoxel(voxelToAdd, 15);
        }

        private void ChangeVoxel(int3 position, byte newValue)
        {
            position += 1; //Offset by one as we have padding of 1. Instead of 64 we render 62
            int3 chunkPosition = position / 62;
            position %= 62;
            Debug.Log(position);
            visibleChunks.TryGetValue(chunkPosition, out var chunk);
            chunk.ChunkData.Voxels[
                position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;
            RefreshChunk(chunkPosition);
        }

        private void RefreshChunk(int3 chunkPosition)
        {
            visibleChunks.TryGetValue(chunkPosition, out var chunk);
            var bitMatrixRecalculationHandle = voxelsGenerator.Value.ScheduleBitMatrixRecalculation(chunk.ChunkData);
            var meshGenerationHandle =
                meshGenerator.Value.ScheduleMeshGeneration(chunk.ChunkData, bitMatrixRecalculationHandle);
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
            freeChunk.transform.position = new Vector3(x * (VoxelEngineConstants.CHUNK_VOXEL_SIZE-2), 0,
                z * (VoxelEngineConstants.CHUNK_VOXEL_SIZE-2));
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