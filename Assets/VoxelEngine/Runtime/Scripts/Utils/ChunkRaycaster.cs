using System;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class ChunkRaycaster : MonoBehaviour
    {
        public int3 VoxelPosition => voxelPosition;
        public float3 HitPosition => hitPosition;
        public int3 HitNormal => hitNormal;
        public bool HitTerrain => hitTerrain;


        private int3 voxelPosition;
        private float3 hitPosition;
        private int3 hitNormal;
        private bool hitTerrain;

        [SerializeField]
        private WorldGenerator worldGenerator;

        private void Update()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            hitTerrain = RayVoxel(ray, out voxelPosition, out hitPosition, out hitNormal);

            if (Input.GetMouseButtonDown(0) && HitTerrain)
            {
                worldGenerator.AddVoxel(VoxelPosition + HitNormal, 15);
            }
            else if (Input.GetMouseButtonDown(1) & HitTerrain)
            {
                worldGenerator.RemoveVoxel(VoxelPosition);
            }
        }

        public bool RayVoxel(Ray ray, out int3 voxelPosition, out float3 hitPosition, out int3 hitNormal,
            float maxDistance = 10)
        {
            int3 traverse = new int3(0, 0, 0);
            int3 startingVoxel = new int3(Mathf.FloorToInt(ray.origin.x), Mathf.FloorToInt(ray.origin.y),
                Mathf.FloorToInt(ray.origin.z));
            ray.direction.Normalize();
            int stepX = (int)Mathf.Sign(ray.direction.x);
            int stepY = (int)Mathf.Sign(ray.direction.y);
            int stepZ = (int)Mathf.Sign(ray.direction.z);
            float mx = Mathf.Abs(ray.direction.x == 0 ? 9000 : 1 / ray.direction.x);
            float my = Mathf.Abs(ray.direction.y == 0 ? 9000 : 1 / ray.direction.y);
            float mz = Mathf.Abs(ray.direction.z == 0 ? 9000 : 1 / ray.direction.z);

            int index = 0;

            float currentLengthX = stepX < 0
                ? (ray.origin.x - startingVoxel.x) * mx
                : ((startingVoxel.x + 1) - ray.origin.x) * mx;
            float currentLengthY = stepY < 0
                ? (ray.origin.y - startingVoxel.y) * my
                : ((startingVoxel.y + 1) - ray.origin.y) * my;
            float currentLengthZ = stepZ < 0
                ? (ray.origin.z - startingVoxel.z) * mz
                : ((startingVoxel.z + 1) - ray.origin.z) * mz;

            while (Mathf.Abs(traverse.x) + Mathf.Abs(traverse.y) + Mathf.Abs(traverse.z) < maxDistance)
            {
                if (currentLengthX <= currentLengthY && currentLengthX <= currentLengthZ)
                {
                    traverse.x += stepX;
                    hitPosition = ray.origin + currentLengthX * ray.direction;
                    currentLengthX += mx;
                    hitNormal = new int3(-stepX, 0, 0);
                }
                else if (currentLengthY <= currentLengthX && currentLengthY <= currentLengthZ)
                {
                    traverse.y += stepY;
                    hitPosition = ray.origin + currentLengthY * ray.direction;
                    currentLengthY += my;
                    hitNormal = new int3(0, -stepY, 0);
                }
                else //z is shortest
                {
                    traverse.z += stepZ;
                    hitPosition = ray.origin + currentLengthZ * ray.direction;
                    currentLengthZ += mz;
                    hitNormal = new int3(0, 0, -stepZ);
                }

                if (!worldGenerator.IsVoxelSolid(startingVoxel + traverse))
                    continue;

                voxelPosition = startingVoxel + traverse;
                return true;
            }

            voxelPosition = new int3(0, 0, 0);
            hitNormal = new int3(0, 0, 0);
            hitPosition = new float3(0, 0, 0);
            return false;
        }
    }
}