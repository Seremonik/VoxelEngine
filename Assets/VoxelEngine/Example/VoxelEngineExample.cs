using System;
using UnityEngine;

namespace VoxelEngine.Example
{
    public class VoxelEngineExample : MonoBehaviour
    {
        [SerializeField]
        private MeshFilter meshFilter;
        [SerializeField]
        private MeshFilter meshFilter2;
        [SerializeField]
        private MeshRenderer meshRenderer;
        [SerializeField]
        private MeshRenderer meshRenderer1;
        private void Start()
        {
            GenerateMeshBinary();
        }
        
        
        public ComputeBuffer voxelBuffer;
        private Material voxelMaterial;

        private void GenerateMeshBinary()
        {
            IVoxelsGenerator voxelsesGenerator = new ExampleVoxelsGenerator(VoxelEngineConstants.CHUNK_VOXEL_SIZE);
            IMeshGenerator meshGenerator = new BinaryMeshGenerator();
            voxelBuffer = new ComputeBuffer(VoxelEngineConstants.CHUNK_VOXEL_SIZE* VoxelEngineConstants.CHUNK_VOXEL_SIZE * (VoxelEngineConstants.CHUNK_VOXEL_SIZE/4), System.Runtime.InteropServices.Marshal.SizeOf(typeof(int)), ComputeBufferType.Default);
            voxelBuffer.SetData(voxelsesGenerator.GetVoxelBuffer());
            voxelMaterial = Instantiate(meshRenderer1.sharedMaterial);
            voxelMaterial.SetBuffer("voxelBuffer", voxelBuffer);
            var chunk = new ChunkData()
            {
                Voxels = voxelsesGenerator.Voxels,
                BitMatrix = voxelsesGenerator.BitMatrix
            };
            var mesh = meshGenerator.BuildChunkMesh(chunk);
            meshRenderer.material = voxelMaterial;
            meshFilter2.mesh = mesh;
        }
        
        private void OnDestroy()
        {
            voxelBuffer.Release();
        }
    }
}