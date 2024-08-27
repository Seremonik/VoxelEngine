using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VoxelEngine
{
    public enum SideOrientation
    {
        Right = 0,
        Left = 1,
        Top = 2,
        Bottom = 3,
        Back = 4,
        Front = 5,
    }

    [BurstCompile]
    partial struct BinaryMeshingJob : IJob
    {
        [ReadOnly]
        public NativeArray<ulong> BitMatrix;
        [ReadOnly]
        public NativeArray<ulong> TransposeMatrixLookupTable;

        public NativeList<int> Triangles;
        public NativeList<uint> Verts;
        [DeallocateOnJobCompletion]
        public NativeArray<ulong> CullingBitMatrix;
        private int vertexCount;

        public ProfilerMarker SideMatrixMarker;
        public ProfilerMarker TransposingMatrixMarker;
        public ProfilerMarker GreedyMeshMarker;

        public void Execute()
        {
            vertexCount = 0;

            GenerateSidesBitMatrix();
            TransposeCullingMatrices();
            GreedyMeshMarker.Begin();
            for (int i = 0; i < 6; i++)
            {
                var sideSlice = new NativeSlice<ulong>(CullingBitMatrix,
                    i * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED,
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED);
                vertexCount = GreedyMesh(sideSlice, (SideOrientation)i);
            }

            GreedyMeshMarker.End();
        }

        private void GenerateSidesBitMatrix()
        {
            SideMatrixMarker.Begin();
            int sizeSquare = VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED;

            for (int i = 0; i < sizeSquare; i++)
            {
                for (int faces = 0; faces < 3; faces++)
                {
                    CullingBitMatrix[i + sizeSquare * faces * 2] = BitMatrix[i + sizeSquare * faces] &
                                                                   ~(BitMatrix[i + sizeSquare * faces] >> 1);
                    CullingBitMatrix[i + sizeSquare * (faces * 2 + 1)] = BitMatrix[i + sizeSquare * faces] &
                                                                         ~(BitMatrix[i + sizeSquare * faces] << 1);
                }
            }

            SideMatrixMarker.End();
        }

        private int GreedyMesh(NativeSlice<ulong> cullingBitMatrixSlice, SideOrientation sideOrientation)
        {
            int faceNormal = sideOrientation == SideOrientation.Top || sideOrientation == SideOrientation.Right ||
                             sideOrientation == SideOrientation.Back
                ? +1
                : -1;
            
            for (int i = 1; i < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; i++)
            {
                for (int j = 1; j < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; j++)
                {
                    var currentRow = cullingBitMatrixSlice[i + j * VoxelEngineConstants.CHUNK_VOXEL_SIZE];
                    if (currentRow == 0) //if row is empty (no faces), then skip
                        continue;

                    //traverse over the ulong (face row)
                    for (int bitIndex = 1; bitIndex < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1; bitIndex++)
                    {
                        if ((currentRow & (1UL << bitIndex)) == 0) //if its empty (no face) then skip
                            continue;

                        var width = 1;
                        var height = 1;
                        var startIndex = bitIndex;

                        var currentMask = currentRow & (1UL << startIndex);
                        var currentAmbientOcclusion = CalculateAO(i, j, bitIndex, sideOrientation, faceNormal);

                        cullingBitMatrixSlice[i + j * VoxelEngineConstants.CHUNK_VOXEL_SIZE] &= ~(1UL << bitIndex);

                        for (;; bitIndex++)
                        {
                            if (bitIndex < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2 &&
                                ((currentRow >> (bitIndex + 1)) & 1UL) == 1 &&
                                CalculateAO(i, j, bitIndex + 1, sideOrientation, faceNormal).Equals(currentAmbientOcclusion))
                            {
                                width++;
                                currentMask |= 1UL << (bitIndex + 1);
                                cullingBitMatrixSlice[i + j * VoxelEngineConstants.CHUNK_VOXEL_SIZE] &=
                                    ~(1UL << bitIndex);
                            }
                            else
                            {
                                for (int k = j + 1;; k++)
                                {
                                    currentRow = cullingBitMatrixSlice[i + k * VoxelEngineConstants.CHUNK_VOXEL_SIZE];
                                    if (k < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 1 &&
                                        (currentRow & currentMask) == currentMask &&
                                        HasSameAo(width, currentAmbientOcclusion, i, k, startIndex,
                                            sideOrientation, faceNormal))
                                    {
                                        height++;
                                        cullingBitMatrixSlice[i + k * VoxelEngineConstants.CHUNK_VOXEL_SIZE] &=
                                            ~currentMask;
                                    }
                                    else
                                    {
                                        DrawFace(i - 1, j - 1, startIndex - 1, width, height, sideOrientation, 1
                                        );
                                        break;
                                    }
                                }

                                break;
                            }
                        }

                        currentRow = cullingBitMatrixSlice[i + j * VoxelEngineConstants.CHUNK_VOXEL_SIZE];
                        if (currentRow == 0)
                            break;
                    }
                }
            }

            return vertexCount;
        }

        private (int x, int y, int z) ConvertCullPositionToVoxelPosition(int i, int j, int bitIndex,
            SideOrientation sideOrientation)
        {
            switch (sideOrientation)
            {
                case SideOrientation.Right:
                    break;
                case SideOrientation.Left:
                    break;
                case SideOrientation.Top:
                    return new(bitIndex, i, j);
                    break;
                case SideOrientation.Bottom:
                    break;
                case SideOrientation.Back:
                    return new(bitIndex, i, j);
                    break;
                case SideOrientation.Front:
                    break;
            }

            return new(i, j, bitIndex);
        }

        private bool HasSameAo(int width, int4 currentAo, int i, int j, int bitIndex, SideOrientation sideOrientation, int faceNormal)
        {
            bool isTheSameAo = true;
            for (int rowIndex = 0; rowIndex < width && isTheSameAo; rowIndex++)
            {
                isTheSameAo &= CalculateAO(i, j, bitIndex + rowIndex, sideOrientation, faceNormal).Equals(currentAo);
            }

            return isTheSameAo;
        }

        private int4 CalculateAO(int i, int j, int bitIndex, SideOrientation sideOrientation, int faceNormal)
        {
            (int x, int y, int z) = ConvertCullPositionToVoxelPosition(i, j, bitIndex, sideOrientation);
            y += faceNormal;
            
            int4 result = new int4(0, 0, 0, 0);

            result[0] = ((BitMatrix[
                x - 1 + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> z) & 1UL) == 0
                ? 15
                : 0;
            result[3] = result[0];
            result[1] = ((BitMatrix[
                x + 1 + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> z) & 1UL) == 0
                ? 15
                : 0;
            result[2] = result[1];

            int currentValue =
                ((BitMatrix[
                    x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> (z + 1)) & 1UL) == 0
                    ? 15
                    : 0;
            result[3] += currentValue;
            result[2] += currentValue;

            currentValue =
                ((BitMatrix[
                    x + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                    VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> (z - 1)) & 1UL) == 0
                    ? 15
                    : 0;

            result[0] += currentValue;
            result[1] += currentValue;


            if (result[0] != 0)
            {
                result[0] +=
                    ((BitMatrix[
                        x - 1 + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> (z - 1)) & 1UL) ==
                    0
                        ? 15
                        : 0;
            }

            if (result[1] != 0)
            {
                result[1] +=
                    ((BitMatrix[
                        x + 1 + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> (z - 1)) & 1UL) ==
                    0
                        ? 15
                        : 0;
            }

            if (result[2] != 0)
            {
                result[2] +=
                    ((BitMatrix[
                        x + 1 + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> (z + 1)) & 1UL) ==
                    0
                        ? 15
                        : 0;
            }

            if (result[3] != 0)
            {
                result[3] +=
                    ((BitMatrix[
                        x - 1 + y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * 2] >> (z + 1)) & 1UL) ==
                    0
                        ? 15
                        : 0;
            }

            result += 15;
            result /= 4;
            return result;
        }

        private void DrawFace(int x, int y, int z, int width, int height, SideOrientation sideOrientation,
            int4 sunLight)
        {
            switch (sideOrientation)
            {
                case SideOrientation.Left: //Left
                    EncodeValue(sideOrientation, x, y, z, sunLight[0], 0);
                    EncodeValue(sideOrientation, x, y, z + width, sunLight[1], 0);
                    EncodeValue(sideOrientation, x, y + height, z + width, sunLight[2], 0);
                    EncodeValue(sideOrientation, x, y + height, z, sunLight[3], 0);
                    FrontFace();
                    break;

                case SideOrientation.Right: //Right
                    EncodeValue(sideOrientation, x + 1, y, z, sunLight[0], 0);
                    EncodeValue(sideOrientation, x + 1, y, z + width, sunLight[1], 0);
                    EncodeValue(sideOrientation, x + 1, y + height, z + width, sunLight[2], 0);
                    EncodeValue(sideOrientation, x + 1, y + height, z, sunLight[3], 0);
                    BackFace();

                    break;
                case SideOrientation.Top: //Top
                    EncodeValue(sideOrientation, z, x + 1, y, sunLight[0], 0);
                    EncodeValue(sideOrientation, z, x + 1, y + height, sunLight[1], 0);
                    EncodeValue(sideOrientation, z + width, x + 1, y + height, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, x + 1, y, sunLight[3], 0);

                    FrontFace();
                    break;
                case SideOrientation.Bottom: //Bottom
                    EncodeValue(sideOrientation, z, x, y, sunLight[0], 0);
                    EncodeValue(sideOrientation, z, x, y + height, sunLight[1], 0);
                    EncodeValue(sideOrientation, z + width, x, y + height, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, x, y, sunLight[3], 0);

                    BackFace();

                    break;
                case SideOrientation.Front: //Front
                    EncodeValue(sideOrientation, z, y, x, sunLight[0], 0);
                    EncodeValue(sideOrientation, z, y + height, x, sunLight[1], 0);
                    EncodeValue(sideOrientation, z + width, y + height, x, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, y, x, sunLight[3], 0);

                    FrontFace();
                    break;
                case SideOrientation.Back: //Back
                    EncodeValue(sideOrientation, z, y, x + 1, sunLight[0], 0);
                    EncodeValue(sideOrientation, z, y + height, x + 1, sunLight[1], 0);
                    EncodeValue(sideOrientation, z + width, y + height, x + 1, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, y, x + 1, sunLight[3], 0);

                    BackFace();
                    break;
            }

            vertexCount += 4;
        }

        private void FrontFace()
        {
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 1);
            Triangles.Add(vertexCount + 2);
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 2);
            Triangles.Add(vertexCount + 3);
        }

        private void BackFace()
        {
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 2);
            Triangles.Add(vertexCount + 1);
            Triangles.Add(vertexCount);
            Triangles.Add(vertexCount + 3);
            Triangles.Add(vertexCount + 2);
        }

        private void TransposeCullingMatrices()
        {
            TransposingMatrixMarker.Begin();
            NativeSlice<ulong> tempSlice;

            for (int sides = 0; sides < 6; sides++)
            {
                for (int i = 0; i < VoxelEngineConstants.CHUNK_VOXEL_SIZE; i++)
                {
                    tempSlice = new NativeSlice<ulong>(CullingBitMatrix,
                        i * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        sides * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED,
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE);
                    Transpose64x64Matrix(tempSlice, TransposeMatrixLookupTable);
                }
            }

            TransposingMatrixMarker.End();
        }

        private void Transpose64x64Matrix(NativeSlice<ulong> bitMatrix,
            NativeArray<ulong> transposeMatrixLookupTable)
        {
            int i, p, s, idx0, idx1;
            ulong x, y;
            for (int j = 5; j >= 0; j--)
            {
                s = 1 << j;
                for (p = 0; p < 32 / s; p++)
                for (i = 0; i < s; i++)
                {
                    idx0 = (p * 2 * s + i);
                    idx1 = (p * 2 * s + i + s);
                    x = (bitMatrix[idx0] & transposeMatrixLookupTable[j]) |
                        ((bitMatrix[idx1] & transposeMatrixLookupTable[j]) << s);
                    y = ((bitMatrix[idx0] & transposeMatrixLookupTable[j + 6]) >> s) |
                        (bitMatrix[idx1] & transposeMatrixLookupTable[j + 6]);
                    bitMatrix[idx0] = x;
                    bitMatrix[idx1] = y;
                }
            }
        }

        private void EncodeValue(SideOrientation side, int x, int y, int z, int sunLight,
            int lampsLight)
        {
            int faceId = (int)side;

            x &= 0x3F; // 6 bits (0-63)
            y &= 0x3F; // 6 bits (0-63)
            z &= 0x3F; // 6 bits (0-63)
            faceId &= 0x7; // 3 bits (0-7)
            sunLight &= 0xF;
            //Bits taken: 29/32

            uint packedData = (uint)(x) | ((uint)y << 6) | ((uint)z << 12) |
                              ((uint)faceId << 18 | ((uint)sunLight << 21));
            Verts.Add(packedData);
        }
    }

    [CreateAssetMenu(fileName = "Binary Mesh Generator", menuName = "ScriptableObjects/Binary Mesh Generator",
        order = 1)]
    public class BinaryMeshGenerator : ScriptableObject, IMeshGenerator
    {
        private NativeArray<ulong> transposeMatrixLookupTable;

        public BinaryMeshGenerator()
        {
            transposeMatrixLookupTable = new NativeArray<ulong>(12, Allocator.Persistent)
            {
                [0] = 0x5555555555555555UL,
                [1] = 0x3333333333333333UL,
                [2] = 0x0F0F0F0F0F0F0F0FUL,
                [3] = 0x00FF00FF00FF00FFUL,
                [4] = 0x0000FFFF0000FFFFUL,
                [5] = 0x00000000FFFFFFFFUL,
                [6] = 0xAAAAAAAAAAAAAAAAUL,
                [7] = 0xCCCCCCCCCCCCCCCCUL,
                [8] = 0xF0F0F0F0F0F0F0F0UL,
                [9] = 0xFF00FF00FF00FF00UL,
                [10] = 0xFFFF0000FFFF0000UL,
                [11] = 0xFFFFFFFF00000000UL,
            };
        }

        public JobHandle ScheduleMeshGeneration(ChunkData chunkData, JobHandle dependency)
        {
            var job = new BinaryMeshingJob()
            {
                TransposeMatrixLookupTable = transposeMatrixLookupTable,
                GreedyMeshMarker = new ProfilerMarker("Greedy meshing"),
                SideMatrixMarker = new ProfilerMarker("SideMatrix"),
                TransposingMatrixMarker = new ProfilerMarker("Transposing"),
                Triangles = chunkData.Triangles,
                Verts = chunkData.Vertices,
                BitMatrix = chunkData.BitMatrix,
                CullingBitMatrix =
                    new NativeArray<ulong>(
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE * VoxelEngineConstants.CHUNK_VOXEL_SIZE * 6,
                        Allocator.TempJob)
            };
            return job.Schedule(dependency);
        }
    }
}