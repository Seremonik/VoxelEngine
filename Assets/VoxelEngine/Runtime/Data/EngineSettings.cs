using System;
using UnityEngine;

namespace VoxelEngine
{
    [Serializable]
    [CreateAssetMenu(fileName = "Engine Settings", menuName = "ScriptableObjects/Voxel Engine Settings", order = 1)]
    public class EngineSettings : ScriptableObject
    {
        [Header("Chunk")]
        public int XChunkSize;
        public int YChunkSize;
        public int ZChunkSize;
        public float VoxelScale;
        [Header("World")]
        public int WorldRadius;
        public int XWorldSize;
        public int YWorldSize;
        public int ZWorldSize;
    }
}