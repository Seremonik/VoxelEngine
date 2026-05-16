using UnityEngine;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;

namespace VoxelEngine.Example
{
    public class CharacterControllerExample : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float gravity = -9.81f;
        public float jumpHeight = 1.5f;

        public float mouseSensitivity = 0.1f;
        public Transform cameraTransform;

        private CharacterController controller;
        private Vector3 velocity;
        private bool isGrounded;
        private float xRotation = 0f;
        private bool isLocked;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (keyboard.bKey.wasPressedThisFrame)
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;

            if (keyboard.cKey.wasPressedThisFrame)
                isLocked = !isLocked;

            if (isLocked)
                return;

            // Mouse look
            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);

            // Grounded check
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
                velocity.y = -2f;

            // Movement
            float moveX = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float moveZ = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            Vector3 move = transform.right * moveX + transform.forward * moveZ;
            controller.Move(move * moveSpeed * Time.deltaTime);

            // Jump
            if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Gravity
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
