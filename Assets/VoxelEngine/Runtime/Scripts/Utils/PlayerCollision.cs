using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class PlayerCollision : MonoBehaviour
    {
        [SerializeField]
        private VoxelWorld voxelWorld;

        private Queue<BoxCollider> boxCollidersPool = new();
        private Dictionary<int3, BoxCollider> activeColliders = new();
        private int3 lastPosition = int3.zero;
        private int3 currentPosition = int3.zero;
        
        private void Start()
        {
            voxelWorld.ChunkUpdated += RecalculateBoxPositions;
            for (int i = 0; i < 120; i++)
            {
                InstantiateBoxCollider();
            }
        }

        private void OnDestroy()
        {
            voxelWorld.ChunkUpdated -= RecalculateBoxPositions;
        }

        private void FixedUpdate()
        {
            int3 newPosition = new int3((int)math.floor(transform.position.x), (int)math.floor(transform.position.y),
                (int)math.floor(transform.position.z));
            if (newPosition.Equals(currentPosition))
                return;
            if (newPosition.Equals(lastPosition))
            {
                lastPosition = currentPosition;
                currentPosition = newPosition;
                return;
            }

            lastPosition = currentPosition;
            currentPosition = newPosition;
            RecalculateBoxPositions();
        }

        private void InstantiateBoxCollider()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PlayerChunkCollider";
            boxCollidersPool.Enqueue(cube.GetComponent<BoxCollider>());
            Destroy(cube.GetComponent<MeshFilter>());
            Destroy(cube.GetComponent<MeshRenderer>());
            cube.SetActive(false);
        }

        private void RecalculateBoxPositions()
        {
            foreach (var boxCollider in boxCollidersPool)
            {
                boxCollider.gameObject.SetActive(false);
            }
            
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        var boxPosition = currentPosition + new int3(x, y, z);

                        if (!voxelWorld.IsVoxelSolid(boxPosition)) continue;
                        
                        var collider = boxCollidersPool.Dequeue();
                        collider.gameObject.SetActive(true);
                        collider.transform.position = new Vector3(boxPosition.x + 0.5f, boxPosition.y + 0.5f,
                            boxPosition.z + 0.5f);
                        activeColliders.TryAdd(boxPosition, collider);
                        boxCollidersPool.Enqueue(collider);
                    }
                }
            }
        }
    }
}