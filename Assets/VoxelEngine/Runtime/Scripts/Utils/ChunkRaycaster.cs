using System;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class ChunkRaycaster : MonoBehaviour
    {
        [SerializeField]
        private WorldGenerator worldGenerator;

        private GameObject ball;
        private void Start()
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            ball.GetComponent<Collider>().enabled = false;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, maxDistance:100))
                {
                    ball.transform.position = hit.point;
                    worldGenerator.AddVoxel(RoundUpRaycast(hit), 15);
                }
            }
            else if (Input.GetMouseButtonDown(1))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, maxDistance:100))
                {
                    ball.transform.position = hit.point;
                    worldGenerator.RemoveVoxel(RoundUpRaycast(hit));
                }
            }
        }

        private RaycastHit RoundUpRaycast(RaycastHit raycastHit)
        {
            if (raycastHit.normal.x != 0)
            {
                raycastHit.point = new Vector3(math.round(raycastHit.point.x),raycastHit.point.y,raycastHit.point.z);
            }
            else if (raycastHit.normal.y != 0)
            {
                raycastHit.point = new Vector3(raycastHit.point.x,math.round(raycastHit.point.y),raycastHit.point.z);
            }
            else if (raycastHit.normal.z != 0)
            {
                raycastHit.point = new Vector3(raycastHit.point.x,raycastHit.point.y,math.round(raycastHit.point.z));
            }
            return raycastHit;
        }
    }
}