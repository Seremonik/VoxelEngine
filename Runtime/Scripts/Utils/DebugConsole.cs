using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private VoxelWorld voxelWorld;
        [SerializeField]
        private ChunkRaycaster chunkRaycaster;
        [SerializeField]
        private Transform hitSphere;
        [SerializeField] private Camera thirdPersonCamera;
        
        [SerializeField, Header("Crosshair Debug Info")]
        private TMP_Text voxelPosition;
        [SerializeField]
        private TMP_Text lightValue;
        
        private bool isOpened;
        private bool isFpsCamera;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                isOpened = !isOpened;
                debugConsoleContainer.SetActive(isOpened);
            }
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                isFpsCamera = !isFpsCamera;
                thirdPersonCamera.gameObject.SetActive(!isFpsCamera);
                playerPosition.gameObject.SetActive(isFpsCamera);
            }

            if (!isOpened) 
                return;
            
            UpdateChunkBordersPreview();
            UpdateCenterText();
            UpdateHitSphere();
        }

        private void UpdateHitSphere()
        {
            hitSphere.transform.position = chunkRaycaster.HitPosition;
            hitSphere.transform.forward = new Vector3(chunkRaycaster.HitNormal.x,chunkRaycaster.HitNormal.y,chunkRaycaster.HitNormal.z);
        }
        
        private void UpdateCenterText()
        {
            int3 voxelPosition = chunkRaycaster.VoxelPosition;
            var lightValue = voxelWorld.GetLightValue(voxelPosition + chunkRaycaster.HitNormal);
            this.voxelPosition.text = $"Voxel: ({voxelPosition.x}, {voxelPosition.y},{voxelPosition.z})";
            this.voxelPosition.text += $"\nLight: {lightValue}";
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