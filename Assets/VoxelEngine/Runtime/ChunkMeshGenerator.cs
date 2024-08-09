using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct Vertex
    {
        public float3 pos;
        public int voxelId;
        public int faceIndex;
        public int data;
    }

    [BurstCompile]
    struct MarchingCubesJob : IJob
    {
        [ReadOnly]
        public NativeArray<ushort> Voxels;
        public NativeList<int> Triangles;
        public NativeList<Vertex> Verts;
        private int vertexCount;

        public void Execute()
        {
            vertexCount = 0;

            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    for (int z = 0; z < 32; z++)
                    {
                        int arrayIndex = x + y * 32 + z * 32 * 32;
                        ushort voxel = Voxels[arrayIndex];
                        
                        if (isAir(arrayIndex, Voxels))
                            continue;

                        if (isAir(x + 1, y, z, Voxels))
                        {
                            GenerateFace(x + 1, y, z, 1, Triangles, Verts, voxel, vertexCount);
                            vertexCount += 4;
                        }

                        if (isAir(x - 1, y, z, Voxels))
                        {
                            GenerateFace(x, y, z, 0, Triangles, Verts, voxel,vertexCount);
                            vertexCount += 4;
                        }

                        if (isAir(x, y + 1, z, Voxels))
                        {
                            GenerateFace(x, y + 1, z, 2, Triangles, Verts, voxel,vertexCount);
                            vertexCount += 4;
                        }

                        if (isAir(x, y - 1, z, Voxels))
                        {
                            GenerateFace(x, y, z, 3, Triangles, Verts, voxel,vertexCount);
                            vertexCount += 4;
                        }

                        if (isAir(x, y, z + 1, Voxels))
                        {
                            GenerateFace(x, y, z + 1, 4, Triangles, Verts, voxel,vertexCount);
                            vertexCount += 4;
                        }

                        if (isAir(x, y, z - 1, Voxels))
                        {
                            GenerateFace(x, y, z, 5, Triangles, Verts, voxel,vertexCount);
                            vertexCount += 4;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool isAir(int x, int y, int z, NativeArray<ushort> voxels)
        {
            if (x is < 0 or >= 32)
                return true;
            if (y is < 0 or >= 32)
                return true;
            if (z is < 0 or >= 32)
                return true;

            return voxels[x + y * 32 + z * 32 * 32] == 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool isAir(int arrayIndex, NativeArray<ushort> voxels)
        {
            return voxels[arrayIndex] == 0;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FrontFace(NativeList<int> Triangles, int vertexCount)
        {
            //Check the AddRange performance
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 1);
            Triangles.Add(vertexCount + 2);
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 2);
            Triangles.Add(vertexCount + 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BackFace(NativeList<int> Triangles, int vertexCount)
        {
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 2);
            Triangles.Add(vertexCount + 1);
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 3);
            Triangles.Add(vertexCount + 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EncodeValue(int faceId, ushort voxelId, int x, int y, int z)
        {
            //change to byte
            int result = faceId;
            result <<= 8;
            result |= voxelId;
            result <<= 6;
            result |= x;
            result <<= 6;
            result |= y;
            result <<= 6;
            result |= z;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GenerateFace(int x, int y, int z, int face, NativeList<int> Triangles,
            NativeList<Vertex> Verts, ushort voxelId, int vertexCount)
        {
            switch (face)
            {
                case 0: //Left
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z),
                        faceIndex = 1,
                        data = EncodeValue(1, voxelId, x, y, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z + 1),
                        faceIndex = 1,
                        data = EncodeValue(1, voxelId, x, y, z + 1),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y + 1, z + 1),
                        faceIndex = 1,
                        data = EncodeValue(1, voxelId, x, y + 1, z + 1),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y + 1, z),
                        faceIndex = 1,
                        data = EncodeValue(1, voxelId, x, y + 1, z),
                    });

                    FrontFace(Triangles, vertexCount);
                    break;

                case 1: //Right
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z),
                        data = EncodeValue(0, voxelId, x, y, z),
                        faceIndex = 0,
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z + 1),
                        data = EncodeValue(0, voxelId, x, y, z + 1),
                        faceIndex = 0,
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y + 1, z + 1),
                        data = EncodeValue(0, voxelId, x, y + 1, +1),
                        faceIndex = 0,
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y + 1, z),
                        data = EncodeValue(0, voxelId, x, y + 1, z),
                        faceIndex = 0,
                    });

                    BackFace(Triangles, vertexCount);

                    break;
                case 2: //Top
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z),
                        faceIndex = 2,
                        data = EncodeValue(2, voxelId, x, y, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z + 1),
                        faceIndex = 2,
                        data = EncodeValue(2, voxelId, x, y, z + 1),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y, z + 1),
                        faceIndex = 2,
                        data = EncodeValue(2, voxelId, x + 1, y, z + 1),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y, z),
                        faceIndex = 2,
                        data = EncodeValue(2, voxelId, x + 1, y, z),
                    });

                    FrontFace(Triangles, vertexCount);
                    break;
                case 3: //Bottom
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z),
                        faceIndex = 3,
                        data = EncodeValue(3, voxelId, x, y, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z + 1),
                        faceIndex = 3,
                        data = EncodeValue(3, voxelId, x, y, z + 1),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y, z + 1),
                        faceIndex = 3,
                        data = EncodeValue(3, voxelId, x + 1, y, z + 1),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y, z),
                        faceIndex = 3,
                        data = EncodeValue(3, voxelId, x + 1, y, z),
                    });

                    BackFace(Triangles, vertexCount);

                    break;
                case 4: //Bottom
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z),
                        faceIndex = 4,
                        data = EncodeValue(4, voxelId, x, y, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y + 1, z),
                        faceIndex = 4,
                        data = EncodeValue(4, voxelId, x, y + 1, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y + 1, z),
                        faceIndex = 4,
                        data = EncodeValue(4, voxelId, x + 1, y + 1, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y, z),
                        faceIndex = 4,
                        data = EncodeValue(4, voxelId, x + 1, y, z),
                    });

                    BackFace(Triangles, vertexCount);
                    break;
                case 5: //Bottom
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y, z),
                        faceIndex = 5,
                        data = EncodeValue(5, voxelId, x, y, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x, y + 1, z),
                        faceIndex = 5,
                        data = EncodeValue(5, voxelId, x, y + 1, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y + 1, z),
                        faceIndex = 5,
                        data = EncodeValue(5, voxelId, x + 1, y + 1, z),
                    });
                    Verts.Add(new Vertex
                    {
                        pos = new float3(x + 1, y, z),
                        faceIndex = 5,
                        data = EncodeValue(5, voxelId, x + 1, y, z),
                    });

                    FrontFace(Triangles, vertexCount);
                    break;
            }
        }
    }

    public class ChunkMeshGenerator
    {
        private int trianglesCount = 0;

        private NativeList<int> triangles;
        private NativeList<Vertex> verts = new(Allocator.Persistent);

        public Mesh BuildChunkMesh(ChunkData chunkData, Mesh mesh = null)
        {
            triangles = new NativeList<int>(Allocator.Persistent);
            verts = new NativeList<Vertex>(Allocator.Persistent);

            if (!mesh)
                mesh = new Mesh();
            mesh.Clear();

            var job = new MarchingCubesJob()
            {
                Triangles = triangles,
                Verts = verts,
                Voxels = chunkData.Voxels
            };
            var handle = job.Schedule();
            handle.Complete();

            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32, 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.UInt32, 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.UInt32, 1),
            };
            var vertexCount = verts.Length;
            var trisCount = triangles.Length;
            //Verts
            mesh.SetVertexBufferParams(vertexCount, layout);
            mesh.SetVertexBufferData(verts.AsArray(), 0, 0, vertexCount);

            // Tris
            mesh.SetIndexBufferParams(trisCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(triangles.AsArray(), 0, 0, trisCount);

            mesh.SetSubMesh(0, new SubMeshDescriptor(0, trisCount));
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}