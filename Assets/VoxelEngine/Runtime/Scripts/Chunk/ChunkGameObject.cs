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
        private Mesh collisionMesh;
        private ComputeBuffer voxelBuffer;
        private Material voxelMaterial;

        public void Initialize()
        {
            mesh = new Mesh();
            meshFilter.mesh = mesh;

            voxelBuffer = new ComputeBuffer(
                (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) * (VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2) *
                (VoxelEngineConstants.CHUNK_VOXEL_SIZE / 4), System.Runtime.InteropServices.Marshal.SizeOf(typeof(int)),
                ComputeBufferType.Default);

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

            mesh.SetSubMesh(0, new SubMeshDescriptor(0, trisCount),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.bounds = new Bounds(
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f, VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f),
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE, VoxelEngineConstants.CHUNK_VOXEL_SIZE,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE));

            if (ChunkData.VoxelBuffer.IsCreated)
            {
                voxelBuffer.SetData(ChunkData.VoxelBuffer);
                voxelMaterial.SetBuffer("voxelBuffer", voxelBuffer);
                ChunkData.VoxelBuffer.Dispose();
            }

            ChunkData.Triangles.Clear();
            ChunkData.Vertices.Clear();
            GeneratePhysicMesh();
        }

        private void GeneratePhysicMesh()
        {
            if (collisionMesh == null)
            {
                collisionMesh = new Mesh();
            }

            collisionMesh.Clear();

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
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            };
            collisionMesh.SetVertexBufferParams(vertexCount, layout);
            collisionMesh.SetVertexBufferData(vertices.AsArray(), 0, 0, vertexCount);

            // Tris
            collisionMesh.SetIndexBufferParams(trisCount, IndexFormat.UInt32);
            collisionMesh.SetIndexBufferData(triangles.AsArray(), 0, 0, trisCount);

            collisionMesh.SetSubMesh(0, new SubMeshDescriptor(0, trisCount),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            collisionMesh.bounds = new Bounds(
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f, VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE / 2f),
                new Vector3(VoxelEngineConstants.CHUNK_VOXEL_SIZE, VoxelEngineConstants.CHUNK_VOXEL_SIZE,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE));
            //collisionMesh.RecalculateNormals();
            meshCollider.sharedMesh = collisionMesh;

            ChunkData.BitMatrix.Dispose();
        }

        private void OnDestroy()
        {
            voxelBuffer.Dispose();
            Destroy(mesh);
            Destroy(collisionMesh);
        }
    }
}