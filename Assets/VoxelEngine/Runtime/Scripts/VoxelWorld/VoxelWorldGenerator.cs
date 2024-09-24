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
            IMeshGenerator meshGenerator, IVoxelsGenerator voxelsGenerator, JobScheduler jobScheduler)
        {
            if (isInitialized)
                return;

            chunkParent = new GameObject("Chunks").transform;
            this.meshGenerator = meshGenerator;
            this.jobScheduler = jobScheduler;
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

        private void GenerateChunkMesh(ChunkData chunkData)
        {
            //Move to some function
            if (!VisibleChunks.TryGetValue(chunkData.ChunkPosition, out var chunkGameObject))
            {
                if (pooledChunks.Count <= 0)
                {
                    CreateNewChunkGameObjects();
                }

                chunkGameObject = pooledChunks.Dequeue();
                chunkGameObject.transform.position = new Vector3(
                    chunkData.ChunkPosition.x * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2),
                    chunkData.ChunkPosition.y * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2),
                    chunkData.ChunkPosition.z * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2));
                chunkGameObject.gameObject.name =
                    $"Chunk({chunkData.ChunkPosition.x}, {chunkData.ChunkPosition.y},{chunkData.ChunkPosition.z})";
                chunkGameObject.SetChunkData(chunkData);
            }

            chunkGameObject.UpdateMesh();
            chunkData.ChunkLoadedState = ChunkState.FullyRendered;
            
            VisibleChunks.TryAdd(chunkData.ChunkPosition, chunkGameObject);
        }
        
        private async Task GenerateChunk(int3 chunkPosition)
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
            voxelWorldData.LoadedChunks.Add(chunkPosition, chunkData);
            
            await voxelsGenerator.GenerateVoxels(chunkData);

            if (chunkData.IsEmpty) // Check also if is surrounded
            {
                chunkData.ChunkLoadedState = ChunkState.Skipped;
                return;
            }
            
            var voxelBufferGenerationHandle =
                voxelsGenerator.ScheduleVoxelBufferRecalculation(chunkData);
            var bitMatrixGenerationHandle =
                voxelsGenerator.ScheduleBitMatrixRecalculation(chunkData);
            var lightFloodHandle =
                voxelWorldData.LightingSystem.CalculateLocalSunLight(chunkData);

            var combinedJobHandle = JobHandle.CombineDependencies(bitMatrixGenerationHandle,
                voxelBufferGenerationHandle, lightFloodHandle);

            var meshGenerationHandle = meshGenerator.ScheduleMeshGeneration(chunkData, combinedJobHandle);

            jobScheduler.ScheduleJob(meshGenerationHandle, ()=>GenerateChunkMesh(chunkData));
        }

        public void RefreshChunk(int3 chunkPosition, bool isAddition)
        {
            if (!voxelWorldData.LoadedChunks.TryGetValue(chunkPosition, out var chunkData))
                return;

            var voxelBufferRecalculationHandle = new JobHandle();
            if (isAddition)
            {
                voxelBufferRecalculationHandle =
                    voxelsGenerator.ScheduleVoxelBufferRecalculation(chunkData, new JobHandle());
            }

            var bitMatrixRecalculationHandle =
                voxelsGenerator.ScheduleBitMatrixRecalculation(chunkData, new JobHandle());
            var meshGenerationHandle =
                meshGenerator.ScheduleMeshGeneration(chunkData,
                    JobHandle.CombineDependencies(voxelBufferRecalculationHandle, bitMatrixRecalculationHandle));

            jobScheduler.ScheduleJob(meshGenerationHandle, ()=>GenerateChunkMesh(chunkData));
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