using System;
using UnityEngine;

namespace VoxelEngine.Example
{
    public class VoxelEngineExample : MonoBehaviour
    {
        [SerializeField]
        private MeshFilter meshFilter;
        private void Start()
        {
            ExampleVoxelsGenerator voxelsesGenerator = new ExampleVoxelsGenerator(32,32,32);
            ChunkMeshGenerator meshGenerator = new ChunkMeshGenerator();
            var chunk = new ChunkData()
            {
                Voxels = voxelsesGenerator.Voxels
            };
            var mesh = meshGenerator.BuildChunkMesh(chunk);
            meshFilter.mesh = mesh;
        }
    }
}