using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class VoxelWorldGenerator : MonoBehaviour
    {
        [SerializeField]
        private ChunkGameObject chunkPrefab;

        [SerializeField]
        private EngineSettings engineSettings;
        [SerializeField]
        private InterfaceReference<IVoxelsGenerator> voxelsGenerator;
        [SerializeField]
        private InterfaceReference<IMeshGenerator> meshGenerator;

        private List<ChunkData> scheduledChunks = new();
        private Queue<ChunkGameObject> pooledChunks = new();
        private Queue<int3> scheduledChunksCreation = new();
        public readonly Dictionary<int3, ChunkGameObject> VisibleChunks = new();
        
        private bool isInitialized;

        private VoxelWorldData voxelWorldData;
        private Transform chunkParent;
        
        public void Initialize(VoxelWorldData voxelWorldData)
        {
            if (isInitialized)
                return;

            chunkParent = new GameObject("Chunks").transform;
            this.voxelWorldData = voxelWorldData;
            voxelWorldData.PlayerChunkUpdated += OnPlayerChunkUpdated;
            CreateNewChunkGameObjects();
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized)
                return;

            for (int i = 0; scheduledChunksCreation.Count > 0 && i < engineSettings.MaxJobsPerFrame; i++)
            {
                int3 chunkPosition = scheduledChunksCreation.Dequeue();
                GenerateChunk(chunkPosition);
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

                if (!VisibleChunks.TryGetValue(chunk.ChunkPosition, out var chunkGameObject))
                {
                    chunkGameObject = pooledChunks.Dequeue();
                    chunkGameObject.transform.position = new Vector3(
                        chunk.ChunkPosition.x * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2),
                        chunk.ChunkPosition.y * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2),
                        chunk.ChunkPosition.z * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2));
                    chunkGameObject.gameObject.name = $"Chunk({chunk.ChunkPosition.x}, {chunk.ChunkPosition.y},{chunk.ChunkPosition.z})";
                    chunkGameObject.SetChunkData(chunk);
                }
                
                chunkGameObject.UpdateMesh();
                
                scheduledChunks.RemoveAt(i);
                VisibleChunks.TryAdd(chunk.ChunkPosition, chunkGameObject);
            }
        }

        public void GenerateInitialWorld()
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        scheduledChunksCreation.Enqueue( voxelWorldData.PlayerChunk + new int3(x, y, z));
                    }
                }
            }
        }

        private void LoadChunk(int3 chunkPosition)
        {
            
        }

        private void GenerateChunk(int3 chunkPosition)
        {
            if (pooledChunks.Count <= 0)
            {
                CreateNewChunkGameObjects();
            }

            ChunkData newChunkData = new ChunkData(chunkPosition);
            voxelWorldData.LoadedChunks.TryAdd(chunkPosition, newChunkData);

            var voxelGenerationHandle = voxelsGenerator.Value.ScheduleChunkGeneration(newChunkData);
            var lightFloodHandle = voxelWorldData.LightingSystem.CalculateLocalSunLight(newChunkData, voxelGenerationHandle);
            
            var meshGenerationHandle =
                meshGenerator.Value.ScheduleMeshGeneration(newChunkData, lightFloodHandle);

            newChunkData.GenerationJobHandle = meshGenerationHandle;
            scheduledChunks.Add(newChunkData);
        }

        public void RefreshChunk(int3 chunkPosition, bool isAddition)
        {
            if (!voxelWorldData.LoadedChunks.TryGetValue(chunkPosition, out var chunk))
                return;

            var voxelBufferRecalculationHandle = new JobHandle();
            if (isAddition)
            {
                voxelBufferRecalculationHandle =
                    voxelsGenerator.Value.ScheduleVoxelBufferRecalculation(chunk, new JobHandle());
            }

            var bitMatrixRecalculationHandle =
                voxelsGenerator.Value.ScheduleBitMatrixRecalculation(chunk, new JobHandle());
            var meshGenerationHandle =
                meshGenerator.Value.ScheduleMeshGeneration(chunk,
                    JobHandle.CombineDependencies(voxelBufferRecalculationHandle, bitMatrixRecalculationHandle));
            chunk.GenerationJobHandle = meshGenerationHandle;
            scheduledChunks.Add(chunk);
        }

        private void CreateNewChunkGameObjects()
        {
            int chunkAmount = (int)math.ceil(engineSettings.WorldRadius * engineSettings.WorldRadius * 3.14f);
            chunkAmount = 27;
            for (int i = 0; i < chunkAmount; i++)
            {
                var chunk = Instantiate(chunkPrefab);
                chunk.Initialize();
                if (Application.isEditor)
                {
                    chunk.transform.SetParent(chunkParent);
                }
                pooledChunks.Enqueue(chunk);
            }
        }
        
        private void OnPlayerChunkUpdated(int3 newChunkPosition)
        {
            if (voxelWorldData.LoadedChunks.Count == 0)
            {
                GenerateInitialWorld();
            }
        }
    }
}