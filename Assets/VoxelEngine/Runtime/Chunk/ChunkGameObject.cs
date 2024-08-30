using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine
{
    public class ChunkGameObject : MonoBehaviour
    {
        [SerializeField]
        private MeshFilter meshFilter;
        [SerializeField]
        private MeshRenderer meshRenderer;
        [SerializeField]
        private MeshCollider meshCollider;
        
        public JobHandle GenerationJobHandle;
        public ChunkData ChunkData { private set; get; }
        private Mesh mesh;
        private ComputeBuffer voxelBuffer;
        private Material voxelMaterial;

        public void Initialize()
        {
            mesh = new Mesh();
            
            voxelBuffer = new ComputeBuffer((VoxelEngineConstants.CHUNK_VOXEL_SIZE-2)* (VoxelEngineConstants.CHUNK_VOXEL_SIZE-2) * (VoxelEngineConstants.CHUNK_VOXEL_SIZE/4), System.Runtime.InteropServices.Marshal.SizeOf(typeof(int)), ComputeBufferType.Default);
            
            voxelMaterial = Instantiate(meshRenderer.sharedMaterial);
            meshRenderer.material = voxelMaterial;
        }

        public void SetChunkData(ChunkData chunkData)
        {
            ChunkData = chunkData;
        }

        public void UpdateMesh()
        {
            mesh.Clear();
            
            var vertexCount = ChunkData.Vertices.Length;
            var trisCount = ChunkData.Triangles.Length;
            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UInt32, 1),
            };
            mesh.SetVertexBufferParams(vertexCount, layout);
            mesh.SetVertexBufferData(ChunkData.Vertices.AsArray(), 0, 0, vertexCount);
            
            // Tris
            mesh.SetIndexBufferParams(trisCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(ChunkData.Triangles.AsArray(), 0, 0, trisCount);
            
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, trisCount), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.bounds = new Bounds(
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f, VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f),
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE, VoxelEngineConstants.CHUNK_VOXEL_SIZE,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE));
            
            voxelBuffer.SetData(ChunkData.VoxelBuffer);
            voxelMaterial.SetBuffer("voxelBuffer", voxelBuffer);
            ChunkData.VoxelBuffer.Dispose();
            //ChunkData.BitMatrix.Dispose();
            meshFilter.mesh = mesh;
            GeneratePhysicMesh();
        }

        private void GeneratePhysicMesh()
        {
            if (!meshCollider.sharedMesh)
            {
                meshCollider.sharedMesh = new Mesh();
            }
            meshCollider.sharedMesh.Clear();
            var mesh = new Mesh();
            
            NativeList<Vertex> vertices = new NativeList<Vertex>(Allocator.Persistent);
            NativeList<int> triangles = new NativeList<int>(Allocator.Persistent);

            CollisionMeshGenerator meshGenerator = new CollisionMeshGenerator();
            var jobHandle = meshGenerator.ScheduleMeshGeneration(ChunkData.BitMatrix, triangles, vertices);
            jobHandle.Complete();
            
            var vertexCount = vertices.Length;
            var trisCount = triangles.Length;
            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            };
            mesh.SetVertexBufferParams(vertexCount, layout);
            mesh.SetVertexBufferData(vertices.AsArray(), 0, 0, vertexCount);
            
            // Tris
            mesh.SetIndexBufferParams(trisCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(triangles.AsArray(), 0, 0, trisCount);
            
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, trisCount), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.bounds = new Bounds(
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f, VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f),
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE, VoxelEngineConstants.CHUNK_VOXEL_SIZE,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE));
            mesh.RecalculateNormals();
            
            ChunkData.BitMatrix.Dispose();
            meshCollider.sharedMesh = mesh;
            //meshFilter.mesh = mesh;
        }
        
        private void OnDestroy()
        {
            voxelBuffer.Dispose();
            Destroy(mesh);
        }
    }
}