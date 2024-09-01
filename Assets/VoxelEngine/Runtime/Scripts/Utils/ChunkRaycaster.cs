using System;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class ChunkRaycaster : MonoBehaviour
    {
        [SerializeField]
        private WorldGenerator worldGenerator;

        public int3 GetVoxelPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (RayVoxel(ray, out int3 voxelPosition, out int3 _))
            {
                return voxelPosition;
            }

            return new int3(0, 0, 0);
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (RayVoxel(ray, out int3 voxelPosition, out int3 normal))
                {
                    worldGenerator.AddVoxel(voxelPosition + normal, 15);
                }
            }
            else if (Input.GetMouseButtonDown(1))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (RayVoxel(ray, out int3 voxelPosition, out var _))
                {
                    worldGenerator.RemoveVoxel(voxelPosition);
                }
            }
        }

        public bool RayVoxel(Ray ray, out int3 voxelPosition, out int3 hitNormal,
            float maxDistance = 10)
        {
            int3 traverse = new int3(0, 0, 0);
            int3 startingVoxel = new int3(Mathf.FloorToInt(ray.origin.x), Mathf.FloorToInt(ray.origin.y),
                Mathf.FloorToInt(ray.origin.z));
            ray.direction.Normalize();
            int dx = (int)Mathf.Sign(ray.direction.x);
            int dy = (int)Mathf.Sign(ray.direction.y);
            int dz = (int)Mathf.Sign(ray.direction.z);
            float mx = Mathf.Abs(ray.direction.x == 0 ? 9000 : 1 / ray.direction.x);
            float my = Mathf.Abs(ray.direction.y == 0 ? 9000 : 1 / ray.direction.y);
            float mz = Mathf.Abs(ray.direction.z == 0 ? 9000 : 1 / ray.direction.z);

            int index = 0;

            float3 currentLength;
            if (dx < 0)
            {
                currentLength.x = (ray.origin.x - startingVoxel.x) * mx;
            }
            else
            {
                currentLength.x = ((startingVoxel.x + 1) - ray.origin.x) * mx;
            }

            if (dy < 0)
            {
                currentLength.y = (ray.origin.y - startingVoxel.y) * my;
            }
            else
            {
                currentLength.y = ((startingVoxel.y + 1) - ray.origin.y) * my;
            }

            if (dz < 0)
            {
                currentLength.z = (ray.origin.z - startingVoxel.z) * mz;
            }
            else
            {
                currentLength.z = ((startingVoxel.z + 1) - ray.origin.z) * mz;
            }

            while (Mathf.Abs(traverse.x) + Mathf.Abs(traverse.y) + Mathf.Abs(traverse.z) < maxDistance)
            {
                if (currentLength.x <= currentLength.y && currentLength.x <= currentLength.z)
                {
                    traverse.x += dx;
                    currentLength.x += mx;
                    hitNormal = new int3(-dx, 0, 0);
                }
                else if (currentLength.y <= currentLength.x && currentLength.y <= currentLength.z)
                {
                    traverse.y += dy;
                    currentLength.y += my;
                    hitNormal = new int3(0, -dy, 0);
                }
                else //z is shortest
                {
                    traverse.z += dz;
                    currentLength.z += mz;
                    hitNormal = new int3(0, 0, -dz);
                }

                if (!worldGenerator.IsVoxelSolid(startingVoxel + traverse))
                    continue;

                voxelPosition = startingVoxel + traverse;
                return true;
            }

            voxelPosition = new int3(0, 0, 0);
            hitNormal = new int3(0, 0, 0);
            return false;
        }
    }
}