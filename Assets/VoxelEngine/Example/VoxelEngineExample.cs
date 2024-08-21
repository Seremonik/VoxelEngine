using System;
using UnityEngine;

namespace VoxelEngine.Example
{
    public class VoxelEngineExample : MonoBehaviour
    {
        [SerializeField]
        private MeshFilter meshFilter;
        [SerializeField]
        private MeshRenderer meshRenderer;
        private IVoxelsGenerator voxelsesGenerator;
        private IMeshGenerator meshGenerator;
        [SerializeField]
        private WorldGenerator worldGenerator;
        private ChunkData chunkData;
        
        public void Start()
        {
            //voxelsesGenerator = new ExampleVoxelsGenerator(VoxelEngineConstants.CHUNK_VOXEL_SIZE);
            //meshGenerator = new BinaryMeshGenerator();
            
            // chunkData = new ChunkData()
            // {
            //     Voxels = voxelsesGenerator.Voxels,
            //     BitMatrix = voxelsesGenerator.BitMatrix
            // };
            // voxelBuffer = new ComputeBuffer(VoxelEngineConstants.CHUNK_VOXEL_SIZE* VoxelEngineConstants.CHUNK_VOXEL_SIZE * (VoxelEngineConstants.CHUNK_VOXEL_SIZE/4), System.Runtime.InteropServices.Marshal.SizeOf(typeof(int)), ComputeBufferType.Default);
            //
            // //voxelBuffer.SetData(voxelsesGenerator.GetVoxelBuffer());
            // voxelMaterial = Instantiate(meshRenderer.sharedMaterial);
            // voxelMaterial.SetBuffer("voxelBuffer", voxelBuffer);
            // meshRenderer.material = voxelMaterial;
        }

        public void Generate()
        {
            //GenerateMeshBinary();
            worldGenerator.GenerateWorld();
        }

        public ComputeBuffer voxelBuffer;
        private Material voxelMaterial;

        private void GenerateMeshBinary()
        {
            //var mesh = meshGenerator.BuildChunkMesh(chunkData, meshFilter.mesh);
            //meshFilter.mesh = mesh;
        }
        
        private void OnDestroy()
        {
            //voxelBuffer.Release();
        }
    }
}