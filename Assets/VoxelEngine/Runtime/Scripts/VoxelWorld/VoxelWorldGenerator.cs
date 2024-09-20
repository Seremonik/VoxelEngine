using System.Collections.Generic;
using System.Threading.Tasks;
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

        private List<ChunkData> scheduledChunks = new();
        private Queue<ChunkGameObject> pooledChunks = new();
        private Queue<int3> scheduledChunksCreation = new();
        public readonly Dictionary<int3, ChunkGameObject> VisibleChunks = new();

        private bool isInitialized;

        private IMeshGenerator meshGenerator;
        private IVoxelsGenerator voxelsGenerator;
        private VoxelWorldSerializer voxelWorldSerializer;
        private VoxelWorldData voxelWorldData;
        private Transform chunkParent;
        private JobScheduler jobScheduler;

        public void Initialize(VoxelWorldData voxelWorldData, VoxelWorldSerializer voxelWorldSerializer,
            IMeshGenerator meshGenerator, IVoxelsGenerator voxelsGenerator)
        {
            if (isInitialized)
                return;

            chunkParent = new GameObject("Chunks").transform;
            this.meshGenerator = meshGenerator;
            this.voxelsGenerator = voxelsGenerator;
            this.voxelWorldSerializer = voxelWorldSerializer;
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

                //Move to some function
                if (!VisibleChunks.TryGetValue(chunk.ChunkPosition, out var chunkGameObject))
                {
                    if (pooledChunks.Count <= 0)
                    {
                        CreateNewChunkGameObjects();
                    }

                    chunkGameObject = pooledChunks.Dequeue();
                    chunkGameObject.transform.position = new Vector3(
                        chunk.ChunkPosition.x * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2),
                        chunk.ChunkPosition.y * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2),
                        chunk.ChunkPosition.z * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2));
                    chunkGameObject.gameObject.name =
                        $"Chunk({chunk.ChunkPosition.x}, {chunk.ChunkPosition.y},{chunk.ChunkPosition.z})";
                    chunkGameObject.SetChunkData(chunk);
                }

                chunkGameObject.UpdateMesh();
                chunk.ChunkLoadedState = ChunkState.FullyRendered;

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
                        scheduledChunksCreation.Enqueue(voxelWorldData.PlayerChunk + new int3(x, y, z));
                    }
                }
            }
        }

        private async Task GenerateChunkV2(int3 chunkPosition)
        {
            ChunkData newChunkData = default;
            if (voxelWorldSerializer.IsChunkSerialized(chunkPosition))
            {
                //TBD
            }
            else
            {
                newChunkData = new ChunkData(chunkPosition);
            }

            await voxelsGenerator.GenerateVoxels(newChunkData);
            //continue here
        }

        private void GenerateChunk(int3 chunkPosition)
        {
            ChunkData chunkData = default;
            if (voxelWorldSerializer.IsChunkSerialized(chunkPosition))
            {
                //TBD
            }
            else
            {
                chunkData = new ChunkData(chunkPosition);
            }

            voxelWorldData.LoadedChunks.TryAdd(chunkPosition, chunkData);

            var voxelGenerationHandle = voxelsGenerator.ScheduleVoxelsGeneration(chunkData);
            var voxelBufferGenerationHandle =
                voxelsGenerator.ScheduleVoxelBufferRecalculation(chunkData, voxelGenerationHandle);
            var bitMatrixGenerationHandle =
                voxelsGenerator.ScheduleBitMatrixRecalculation(chunkData, voxelGenerationHandle);
            var lightFloodHandle =
                voxelWorldData.LightingSystem.CalculateLocalSunLight(chunkData, voxelGenerationHandle);

            var combinedJobHandle = JobHandle.CombineDependencies(bitMatrixGenerationHandle,
                voxelBufferGenerationHandle, lightFloodHandle);

            var meshGenerationHandle = meshGenerator.ScheduleMeshGeneration(chunkData, combinedJobHandle);

            chunkData.GenerationJobHandle = meshGenerationHandle;
            scheduledChunks.Add(chunkData);
        }

        public void RefreshChunk(int3 chunkPosition, bool isAddition)
        {
            if (!voxelWorldData.LoadedChunks.TryGetValue(chunkPosition, out var chunk))
                return;

            var voxelBufferRecalculationHandle = new JobHandle();
            if (isAddition)
            {
                voxelBufferRecalculationHandle =
                    voxelsGenerator.ScheduleVoxelBufferRecalculation(chunk, new JobHandle());
            }

            var bitMatrixRecalculationHandle =
                voxelsGenerator.ScheduleBitMatrixRecalculation(chunk, new JobHandle());
            var meshGenerationHandle =
                meshGenerator.ScheduleMeshGeneration(chunk,
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