using System;
using UnityEngine;

namespace VoxelEngine
{
    [Serializable]
    [CreateAssetMenu(fileName = "Engine Settings", menuName = "ScriptableObjects/Voxel Engine Settings", order = 1)]
    public class EngineSettings : ScriptableObject
    {
        private void OnValidate()
        {
            Shader.SetGlobalFloat("_AOStrength", AmbientOcclusionStrength);
        }
        
        [Header("Rendering")]
        [Range(0,1)]public float AmbientOcclusionStrength; // TODO set the AO strength in Mesh Generator
        [Header("World")]
        [Tooltip("Maximum Jobs that can be scheduled during one frame.")] public int MaxJobsPerFrame;
        [Tooltip("Radius of the generated world")] public int WorldRadius;
    }
}