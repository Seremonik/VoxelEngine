using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine
{
    public class PlayerCollision : MonoBehaviour
    {
        [SerializeField]
        private WorldGenerator worldGenerator;
        
        private Transform myTransform;
        
        private Queue<BoxCollider> boxCollidersPool = new ();
        private Dictionary<int3, BoxCollider> activeColliders = new ();
        private int3 lastPosition = int3.zero;
        private int3 currentPosition = int3.zero;

        private void Awake()
        {
            myTransform = transform;
        }

        private void Start()
        {
            for (int i = 0; i < 120; i++)
            {
                InstantiateBoxCollider();
            }
        }

        private void Update()
        {
            int3 newPosition = new int3((int)math.floor(transform.position.x), (int)math.floor(transform.position.y), (int)math.floor(transform.position.z));
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
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    for (int z = -3; z <= 3; z++)
                    {
                        var boxPosition = currentPosition + new int3(x, y, z);
                        if (x == 3 || x == -3 || y == 3 || y == -3 || z == 3 || z == -3)
                        {
                            if (activeColliders.TryGetValue(boxPosition, out var activeCollider))
                            {
                                activeCollider.gameObject.SetActive(false);
                                activeColliders.Remove(boxPosition);
                            }

                            continue;
                        }
                        if (worldGenerator.IsVoxelSolid(boxPosition))
                        {
                            if (!activeColliders.ContainsKey(boxPosition))
                            {
                                var collider = boxCollidersPool.Dequeue();
                                collider.gameObject.SetActive(true);
                                collider.transform.position = new Vector3(boxPosition.x+0.5f, boxPosition.y+0.5f, boxPosition.z+0.5f);
                                activeColliders.TryAdd(boxPosition, collider);
                                boxCollidersPool.Enqueue(collider);
                            }
                        }
                        else if (activeColliders.TryGetValue(boxPosition, out var activeCollider))
                        {
                            activeCollider.gameObject.SetActive(false);
                            activeColliders.Remove(boxPosition);
                        }
                    }
                }
            }
        }
    }
}