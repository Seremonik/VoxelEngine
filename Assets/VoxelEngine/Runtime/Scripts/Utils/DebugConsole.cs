using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class DebugConsole : MonoBehaviour
    {
        [SerializeField]
        private Transform playerPosition;
        [SerializeField]
        private GameObject debugConsoleContainer;
        [SerializeField]
        private Transform chunkBorder;
        [SerializeField]
        private ChunkRaycaster chunkRaycaster;
        
        [SerializeField, Header("Crosshair Debug Info")]
        private TMP_Text voxelPosition;
        [SerializeField]
        private TMP_Text lightValue;
        
        private bool isOpened;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                isOpened = !isOpened;
                debugConsoleContainer.SetActive(isOpened);
            }

            if (isOpened)
            {
                UpdateChunkBordersPreview();
                UpdateVoxelPosition();
            }
        }

        private void UpdateVoxelPosition()
        {
            int3 voxelPosition = chunkRaycaster.GetVoxelPosition();
            this.voxelPosition.text = $"Voxel: ({voxelPosition.x}, {voxelPosition.y},{voxelPosition.z})";
        }
        
        private void UpdateChunkBordersPreview()
        {
            Vector3 chunkPosition = new Vector3(
                Mathf.Floor(playerPosition.position.x / 62f),
                Mathf.Floor(playerPosition.position.y / 62f),
                Mathf.Floor(playerPosition.position.z / 62f));
            chunkBorder.position = chunkPosition * 62f;
        }
    }
}