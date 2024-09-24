using System;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class VoxelWorld : MonoBehaviour
    {
        public event Action ChunkUpdated = delegate { };

        [SerializeField]
        private InterfaceReference<IVoxelsGenerator> voxelsGenerator;
        [SerializeField]
        private InterfaceReference<IMeshGenerator> meshGenerator;
        [SerializeField]
        private VoxelWorldGenerator voxelWorldGenerator;

        private VoxelWorldSerializer voxelWorldGSerializer;
        private JobScheduler jobScheduler;
        private VoxelWorldData voxelWorldData;

        private void Start()
        {
            jobScheduler = new JobScheduler();
            voxelWorldData = new VoxelWorldData();
            voxelWorldGSerializer = new VoxelWorldSerializer();
            voxelsGenerator.Value.Initialize(jobScheduler);
            voxelWorldGenerator.Initialize(
                voxelWorldData,
                voxelWorldGSerializer,
                meshGenerator.Value,
                voxelsGenerator.Value,
                jobScheduler);
        }

        private void LateUpdate()
        {
            jobScheduler.LateUpdate();
        }

        public void SetPlayerChunk(int3 playerChunk)
        {
            voxelWorldData.PlayerChunk = playerChunk;
        }

        public byte GetLightValue(int3 voxelPosition)
        {
            var chunkPosition = new int3(
                (int)Mathf.Floor(voxelPosition.x / 62f),
                (int)Mathf.Floor(voxelPosition.y / 62f),
                (int)Mathf.Floor(voxelPosition.z / 62f));

            int modX = (voxelPosition.x % 62 + 62) % 62;
            int modY = (voxelPosition.y % 62 + 62) % 62;
            int modZ = (voxelPosition.z % 62 + 62) % 62;

            if (voxelWorldData.LoadedChunks.TryGetValue(chunkPosition, out var chunk))
            {
                return chunk.Light[
                    (modX + 1) + (modY + 1) * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                    (modZ + 1) * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED];
            }

            return 0;
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

            if (voxelWorldData.LoadedChunks.TryGetValue(chunkPosition, out var chunk))
            {
                if (chunk.ChunkLoadedState is ChunkState.UnInitialized or ChunkState.Skipped)
                    return false;

                return chunk.Voxels[
                    (modX + 1) + (modY + 1) * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                    (modZ + 1) * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] != 0;
            }

            return false;
        }

        public void RemoveVoxel(int3 voxelPosition)
        {
            int modX = (voxelPosition.x % 62 + 62) % 62;
            int modY = (voxelPosition.y % 62 + 62) % 62;
            int modZ = (voxelPosition.z % 62 + 62) % 62;

            if (ChangeVoxel(voxelPosition, 0, out ChunkData chunk))
            {
                voxelWorldData.LightingSystem.RemoveVoxel(chunk, new int3(modX, modY, modZ));
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition, false);
            }

            //Refresh neighboring chunks as well
            if (modX == 0)
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition + new int3(-1, 0, 0), false);
            if (modX == 61)
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition + new int3(1, 0, 0), false);
            if (modY == 0)
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition + new int3(0, -1, 0), false);
            if (modY == 61)
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition + new int3(0, 1, 0), false);
            if (modZ == 0)
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition + new int3(0, 0, -1), false);
            if (modZ == 61)
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition + new int3(0, 0, 1), false);

            ChunkUpdated?.Invoke();
        }

        public void AddVoxel(int3 voxelPosition, byte voxelId)
        {
            if (ChangeVoxel(voxelPosition, voxelId, out ChunkData chunk))
            {
                voxelPosition = (voxelPosition % 62 + 62) % 62;
                voxelWorldData.LightingSystem.AddVoxel(chunk, voxelPosition);
                voxelWorldGenerator.RefreshChunk(chunk.ChunkPosition, true);
            }

            ChunkUpdated?.Invoke();
        }

        private bool ChangeVoxel(int3 position, byte newValue, out ChunkData chunkData)
        {
            var chunkPosition = new int3(
                (int)Mathf.Floor(position.x / 62f),
                (int)Mathf.Floor(position.y / 62f),
                (int)Mathf.Floor(position.z / 62f));
            position = (position % 62 + 62) % 62;

            position += 1; //Offset by one as we have padding of 1. Instead of 64 we render 62

            if (voxelWorldData.LoadedChunks.TryGetValue(chunkPosition, out chunkData))
            {
                chunkData.Voxels[
                    position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                    position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                //Make sure we update neighbor chunks if voxel lays on border
                if (position.x == 1 &&
                    voxelWorldData.LoadedChunks.TryGetValue(chunkPosition + new int3(-1, 0, 0), out var tempChunkData))
                    tempChunkData.Voxels[
                        63 + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.x == 62 &&
                    voxelWorldData.LoadedChunks.TryGetValue(chunkPosition + new int3(1, 0, 0), out tempChunkData))
                    tempChunkData.Voxels[
                        0 + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.y == 1 &&
                    voxelWorldData.LoadedChunks.TryGetValue(chunkPosition + new int3(0, -1, 0), out tempChunkData))
                    tempChunkData.Voxels[
                        position.x + 63 * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.y == 62 &&
                    voxelWorldData.LoadedChunks.TryGetValue(chunkPosition + new int3(0, 1, 0), out tempChunkData))
                    tempChunkData.Voxels[
                        position.x +
                        position.z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.z == 1 &&
                    voxelWorldData.LoadedChunks.TryGetValue(chunkPosition + new int3(0, 0, -1), out tempChunkData))
                    tempChunkData.Voxels[
                        position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        63 * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED] = newValue;

                if (position.z == 62 &&
                    voxelWorldData.LoadedChunks.TryGetValue(chunkPosition + new int3(0, 0, 1), out tempChunkData))
                    tempChunkData.Voxels[
                        position.x + position.y * VoxelEngineConstants.CHUNK_VOXEL_SIZE] = newValue;

                return true;
            }

            chunkData = default;
            return false;
        }
    }
}