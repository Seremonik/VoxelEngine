using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace VoxelEngine.Example
{
    public class CharacterControllerExample : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float gravity = -9.81f;
        public float jumpHeight = 1.5f;

        public float mouseSensitivity = 100f;
        public Transform cameraTransform;

        private CharacterController controller;
        private Vector3 velocity;
        private bool isGrounded;
        private float xRotation = 0f;
        private bool isLocked;

        void Start()
        {
            controller = GetComponent<CharacterController>();

            // Lock the cursor to the center of the screen
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                isLocked = !isLocked;
            }

            if (isLocked)
            {
                return;
            }
            // Mouse look
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Limit vertical look

            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);

            // Check if the player is on the ground
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Ensure the player sticks to the ground
            }

            // Get input for movement
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            // Calculate movement direction
            Vector3 move = transform.right * moveX + transform.forward * moveZ;

            // Move the character
            controller.Move(move * moveSpeed * Time.deltaTime);

            // Check for jump input
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;

            // Apply vertical movement (gravity and jumping)
            controller.Move(velocity * Time.deltaTime);
        }
    }
}