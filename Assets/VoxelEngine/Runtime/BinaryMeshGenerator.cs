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

            GenerateCulledBitMatrix();
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

        private void GenerateCulledBitMatrix()
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
            int axisIndex = (int)sideOrientation / 2;
            int faceNormal = (int)sideOrientation % 2 == 0 ? 1 : -1;

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
                        var currentAmbientOcclusion = CalculateAO(i, j, bitIndex, axisIndex, faceNormal);

                        cullingBitMatrixSlice[i + j * VoxelEngineConstants.CHUNK_VOXEL_SIZE] &= ~(1UL << bitIndex);

                        for (;; bitIndex++)
                        {
                            if (bitIndex < VoxelEngineConstants.CHUNK_VOXEL_SIZE - 2 &&
                                ((currentRow >> (bitIndex + 1)) & 1UL) == 1 &&
                                CalculateAO(i, j, bitIndex + 1, axisIndex, faceNormal).Equals(currentAmbientOcclusion))
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
                                            axisIndex, faceNormal))
                                    {
                                        height++;
                                        cullingBitMatrixSlice[i + k * VoxelEngineConstants.CHUNK_VOXEL_SIZE] &=
                                            ~currentMask;
                                    }
                                    else
                                    {
                                        DrawFace(i - 1, j - 1, startIndex - 1, width, height, sideOrientation, currentAmbientOcclusion
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

        private bool HasSameAo(int width, int4 currentAo, int i, int j, int bitIndex, int axisIndex, int faceNormal)
        {
            var isTheSameAo = true;
            for (int rowIndex = 0; rowIndex < width && isTheSameAo; rowIndex++)
            {
                isTheSameAo &= CalculateAO(i, j, bitIndex + rowIndex, axisIndex, faceNormal).Equals(currentAo);
            }

            return isTheSameAo;
        }

        private int4 CalculateAO(int i, int j, int bitIndex, int axisIndex, int faceNormal)
        {
            i += faceNormal; //move to slice above (+1) or below (-1)
            var result = new int4(0, 0, 0, 0);

            result[0] = GetVoxelForAxis(bitIndex - 1, j, i, axisIndex);
            result[3] = result[0];
            result[1] = GetVoxelForAxis(bitIndex + 1, j, i, axisIndex);
            result[2] = result[1];

            int currentValue = GetVoxelForAxis(bitIndex, j + 1, i, axisIndex);
            result[3] += currentValue;
            result[2] += currentValue;

            currentValue = GetVoxelForAxis(bitIndex, j - 1, i, axisIndex);
            result[0] += currentValue;
            result[1] += currentValue;
            
            if (result[0] != 0) //Check if corner voxel is actually visible
                result[0] += GetVoxelForAxis(bitIndex - 1, j - 1, i, axisIndex);


            if (result[1] != 0) //Check if corner voxel is actually visible
                result[1] += GetVoxelForAxis(bitIndex + 1, j - 1, i, axisIndex);


            if (result[2] != 0) //Check if corner voxel is actually visible
                result[2] += GetVoxelForAxis(bitIndex + 1, j + 1, i, axisIndex);


            if (result[3] != 0) //Check if corner voxel is actually visible
                result[3] += GetVoxelForAxis(bitIndex - 1, j + 1, i, axisIndex);


            result += 15; //Add our voxel as being fully lit
            result /= 4; //Average each corner 
            return result;
        }
        
        private int GetVoxelForAxis(int x, int y, int z, int axisIndex)
        {
            return ((BitMatrix[
                x +
                y * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED * axisIndex] >> z) & 1UL) == 0
                ? 15
                : 0;
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
                    EncodeValue(sideOrientation, z, x + 1, y + height, sunLight[3], 0);
                    EncodeValue(sideOrientation, z + width, x + 1, y + height, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, x + 1, y, sunLight[1], 0);

                    FrontFace();
                    break;
                case SideOrientation.Bottom: //Bottom
                    EncodeValue(sideOrientation, z, x, y, sunLight[0], 0);
                    EncodeValue(sideOrientation, z, x, y + height, sunLight[3], 0);
                    EncodeValue(sideOrientation, z + width, x, y + height, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, x, y, sunLight[1], 0);

                    BackFace();

                    break;
                case SideOrientation.Front: //Front
                    EncodeValue(sideOrientation, z, y, x, sunLight[0], 0);
                    EncodeValue(sideOrientation, z, y + height, x, sunLight[3], 0);
                    EncodeValue(sideOrientation, z + width, y + height, x, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, y, x, sunLight[1], 0);

                    FrontFace();
                    break;
                case SideOrientation.Back: //Back
                    EncodeValue(sideOrientation, z, y, x + 1, sunLight[0], 0);
                    EncodeValue(sideOrientation, z, y + height, x + 1, sunLight[3], 0);
                    EncodeValue(sideOrientation, z + width, y + height, x + 1, sunLight[2], 0);
                    EncodeValue(sideOrientation, z + width, y, x + 1, sunLight[1], 0);

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

        //We need to Transpose the Matrices to apply greedy meshing
        private void TransposeCullingMatrices()
        {
            TransposingMatrixMarker.Begin();

            for (int sides = 0; sides < 6; sides++)
            {
                for (int i = 0; i < VoxelEngineConstants.CHUNK_VOXEL_SIZE; i++)
                {
                    var tempSlice = new NativeSlice<ulong>(CullingBitMatrix,
                        i * VoxelEngineConstants.CHUNK_VOXEL_SIZE +
                        sides * VoxelEngineConstants.CHUNK_VOXEL_SIZE_SQUARED,
                        VoxelEngineConstants.CHUNK_VOXEL_SIZE);
                    Transpose64x64Matrix(tempSlice, TransposeMatrixLookupTable);
                }
            }

            TransposingMatrixMarker.End();
        }

        //source: https://lukas-prokop.at/articles/2021-07-23-transpose
        private void Transpose64x64Matrix(NativeSlice<ulong> bitMatrix,
            NativeArray<ulong> transposeMatrixLookupTable)
        {
            for (int j = 5; j >= 0; j--)
            {
                var s = 1 << j;
                int p;
                for (p = 0; p < 32 / s; p++)
                {
                    int i;
                    for (i = 0; i < s; i++)
                    {
                        var idx0 = (p * 2 * s + i);
                        var idx1 = (p * 2 * s + i + s);
                        var x = (bitMatrix[idx0] & transposeMatrixLookupTable[j]) |
                                ((bitMatrix[idx1] & transposeMatrixLookupTable[j]) << s);
                        var y = ((bitMatrix[idx0] & transposeMatrixLookupTable[j + 6]) >> s) |
                                (bitMatrix[idx1] & transposeMatrixLookupTable[j + 6]);
                        bitMatrix[idx0] = x;
                        bitMatrix[idx1] = y;
                    }
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