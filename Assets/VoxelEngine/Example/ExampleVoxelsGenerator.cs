using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Example
{
    [BurstCompile]
    public struct VoxelGenerationJob : IJob
    {
        public NativeArray<byte> Voxels;
        public NativeArray<uint> VoxelBuffer;
        public NativeArray<ulong> BitMatrix;
        public void Execute()
        {
            GenerateVoxels(Voxels, 0.06f);
            GetVoxelBuffer(VoxelBuffer, Voxels);
            CalculateBitMatrix(BitMatrix, Voxels);
        }
        
        public static void GenerateVoxels(NativeArray<byte> voxels, float scale)
        {
            int x = 0;
            for (int i = 0; i < 1000000000; i++)
            {
                x++;
            }
            for (int i = 0; i < VoxelEngineConstants.CHUNK_VOXEL_SIZE; i++)
            {
                for (int j = 0; j < VoxelEngineConstants.CHUNK_VOXEL_SIZE; j++)
                {
                    for (int k = 0; k < VoxelEngineConstants.CHUNK_VOXEL_SIZE; k++)
                    {
                        byte value = PerlinNoise3D(i * scale, j * scale, k * scale);
                        // if (i == 0 || j==0 || k == 0)
                        // {
                        //     value = 0;
                        // }
                        // if (i == VoxelEngineConstants.CHUNK_VOXEL_SIZE-1 || j==VoxelEngineConstants.CHUNK_VOXEL_SIZE-1 || k == VoxelEngineConstants.CHUNK_VOXEL_SIZE-1)
                        // {
                        //     value = 0;
                        // }
                        if (value > 0)
                        {
                            value = (byte)(j);
                        }
                        voxels[i + (j * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + (k * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)] = value;
                    }
                }
            }
        }
        
         public static void GetVoxelBuffer(NativeArray<uint> voxelBuffer, NativeArray<byte> voxels)
        {
            //NativeArray<uint> voxelBuffer = new NativeArray<uint>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * (VoxelEngineConstants.CHUNK_VOXEL_SIZE/4), Allocator.Persistent);
            for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
            {
                for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
                {
                    for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
                    {
                        int bufferIndex = x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE + (z / 4 * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED);
                        uint voxel = voxels[x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE + (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)];
                        voxelBuffer[bufferIndex] |= voxel << z*(VoxelEngineConstants.CHUNK_VOXEL_SIZE/4)%VoxelEngineConstants.CHUNK_VOXEL_SIZE;
                    }
                }
            }
        }

         private static void CalculateBitMatrix(NativeArray<ulong> bitMatrix, NativeArray<byte> voxels)
        {
            //bitMatrix = new NativeArray<ulong>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 3, Allocator.Persistent);
            int chunkSize = VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED;
            for (int x = 0; x < VoxelEngineConstants.CHUNK_VOXEL_SIZE; x++)
            {
                for (int y = 0; y < VoxelEngineConstants.CHUNK_VOXEL_SIZE; y++)
                {
                    for (int z = 0; z < VoxelEngineConstants.CHUNK_VOXEL_SIZE; z++)
                    {
                        bool isSolid = voxels[x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED)] != 0;
                        if (!isSolid)
                            continue;

                        bitMatrix[z + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE)] |= 1UL << x;
                        bitMatrix[x + (z * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + chunkSize] |= 1UL << y;
                        bitMatrix[x + (y * VoxelEngineConstants.CHUNK_VOXEL_SIZE) + chunkSize * 2] |= 1UL << z;
                    }
                }
            }
        }
         
         static byte PerlinNoise3D(float x, float y, float z)
         {
             float xy = Mathf.PerlinNoise(x, y);
             float yz = Mathf.PerlinNoise(y, z);
             float xz = Mathf.PerlinNoise(x, z);
         
             float yx = Mathf.PerlinNoise(y * 2, x);
             float zy = Mathf.PerlinNoise(z, y);
             float zx = Mathf.PerlinNoise(z, x);
             float value = ((xy + yz + xz + yx + zy + zx) / 6f);
             if (value < 0.5f )
             {
                 return 0;
             }
            
             return 1;
         }
    }
    
    [CreateAssetMenu(fileName = "Voxel Generator", menuName = "ScriptableObjects/VoxelGenerator", order = 1)]
    public class ExampleVoxelsGenerator : ScriptableObject, IVoxelsGenerator
    {
        //private int volumeSize;
        private float scale = 0.06f;

        //public NativeArray<byte> Voxels => voxels;
        //public NativeArray<ulong> BitMatrix => bitMatrix;

        //private NativeArray<byte> voxels;
        //private NativeArray<ulong> bitMatrix;

        public ChunkData GetChunkData(int3 chunkPosition)
        {
            //var chunkData = new ChunkData();

            

            return null;
        }

        public JobHandle ScheduleChunkGeneration(ChunkData chunkData)
        {
            chunkData.Voxels =
                new NativeArray<byte>(
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE *
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE, Allocator.Persistent);
            chunkData.VoxelBuffer = new NativeArray<uint>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * (VoxelEngineConstants.CHUNK_VOXEL_SIZE/4), Allocator.Persistent);
            chunkData.BitMatrix = new NativeArray<ulong>(VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 3, Allocator.Persistent);
            chunkData.Vertices = new NativeList<uint>(Allocator.Persistent);
            chunkData.Triangles = new NativeList<int>(Allocator.Persistent);
            
            var job = new VoxelGenerationJob()
            {
                Voxels = chunkData.Voxels,
                VoxelBuffer = chunkData.VoxelBuffer,
                BitMatrix = chunkData.BitMatrix,
            };
            var handle = job.Schedule();

            return handle;
        }
        
        
    }
}