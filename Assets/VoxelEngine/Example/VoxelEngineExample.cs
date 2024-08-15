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

        private void Start()
        {
            GenerateMeshNaive();
            GenerateMeshBinary();
        }

        private void GenerateMeshNaive()
        {
            IVoxelsGenerator voxelsesGenerator = new ExampleVoxelsGenerator(32,32,32);
            IMeshGenerator meshGenerator = new ChunkMeshGenerator();
            var chunk = new ChunkData()
            {
                Voxels = voxelsesGenerator.Voxels,
                BitMatrix = voxelsesGenerator.BitMatrix
            };
            var mesh = meshGenerator.BuildChunkMesh(chunk);
            meshFilter.mesh = mesh;
        }
        
        private void GenerateMeshBinary()
        {
            IVoxelsGenerator voxelsesGenerator = new ExampleVoxelsGenerator(32,32,32);
            IMeshGenerator meshGenerator = new BinaryMeshGenerator();
            var chunk = new ChunkData()
            {
                Voxels = voxelsesGenerator.Voxels,
                BitMatrix = voxelsesGenerator.BitMatrix
            };
            var mesh = meshGenerator.BuildChunkMesh(chunk);
            meshFilter2.mesh = mesh;
        }
    }
}